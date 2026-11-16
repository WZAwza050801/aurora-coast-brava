#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""设计一条新赛道：COSTA NOVA / 山谷环线。

这是「自己走一遍赛道设计」的实践产物，和 tools/track_design_studio.py 是
同一条工具链的上游：这里负责生成控制点，studio 负责预览 / 导出 C# / 校验。

设计方法（也是我建议的通用做法）：

1. 先定「形状骨架」而不是先画点。用极坐标的半径调制函数生成一条闭合
   曲线，保证它一定闭合、一定有连续的曲率变化、不会自交。
2. 用傅里叶级数控制起伏，k 越大弯越碎。街机赛道需要 3~5 个明显的高频
   弯 + 2~3 段长直道，所以 k 取 3 和 5 两组，分别给「弯」和「直」。
3. 高度用独立的低频函数（k=2）叠上去，得到连续的上坡和下坡而不是台阶。
4. 生成完立刻用 studio 的算法算出总长 / 最小半径 / 最大坡度，超标就调参数
   重生成——这一步在 Python 里秒级完成，比在 Unity 里试错快几个数量级。
5. 最后一轮才落 JSON、导 C#、进 Unity 重建、实跑验收。

参数在下面，全部可调。改参数重跑即可得到另一条赛道。
"""

import argparse
import json
import math
import sys
from datetime import datetime
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
LAYOUT = ROOT / "assets" / "track_layout.json"

# ---- 设计参数 -----------------------------------------------------------
NAME = "Costa Nova"
SEED = 7
ROAD_WIDTH = 12.5
CHECKPOINTS = 12

# 半径调制：base + a1*cos(k1*t) + a2*cos(k2*t + phase)
# A1 是「大弯」的深度：越大弯越急，但过大会让半径低于 MIN_RADIUS 被判为
# 发夹弯。参数空间的扫描会同时评估长度、最小半径与坡度，所以调大 A1 之后
# 由搜索器自己找一个既急又合规的组合。
BASE_RADIUS = 300.0
A1, K1 = 132.0, 3      # 3 个大弯（弯）
A2, K2 = 52.0, 5       # 5 个小弯（碎）
PHASE2 = 0.9

# 高度：低频 + 一点中频，得到连续坡道
# 高度幅度必须跟半径调制匹配：弯越急、路线越长，同样的高度差落在越短的
# 水平距离上，坡度就越陡。H 与 A 的比值决定了「山」的陡缓。
H_BASE = 26.0
H_A1, H_K1 = 11.0, 2
H_A2, H_K2 = 3.6, 4

# 村落 / 隧道 / 跳台在赛道弧长上的归一化区间
VILLAGE_SPANS = [[0.04, 0.14], [0.34, 0.42], [0.62, 0.70], [0.84, 0.90]]
TUNNEL_SPANS = [[0.24, 0.28]]
JUMP_SPANS = [[0.48, 0.50]]

# ---- 设计约束（与 studio 一致） -----------------------------------------
MAX_GRADE = 0.22
MIN_RADIUS = 16.0
TARGET_LENGTH = (2100.0, 3200.0)


def build_controls(count=28, scale=1.0, stretch_x=1.0, stretch_z=1.0):
    """生成闭合赛道的控制点（世界坐标，米）。"""
    pts = []
    for i in range(count):
        t = 2.0 * math.pi * i / count
        r = (BASE_RADIUS
             + A1 * math.cos(K1 * t)
             + A2 * math.cos(K2 * t + PHASE2)) * scale
        x = r * math.sin(t) * stretch_x
        z = r * math.cos(t) * stretch_z
        y = (H_BASE
             + H_A1 * math.cos(H_K1 * t + 0.4)
             + H_A2 * math.sin(H_K2 * t)) * scale
        pts.append([round(x, 4), round(y, 4), round(z, 4)])
    return pts


def catmull_rom(p0, p1, p2, p3, t):
    out = []
    for k in range(3):
        out.append(0.5 * ((2.0 * p1[k]) + (-p0[k] + p2[k]) * t
                          + (2.0 * p0[k] - 5.0 * p1[k] + 4.0 * p2[k] - p3[k]) * t * t
                          + (-p0[k] + 3.0 * p1[k] - 3.0 * p2[k] + p3[k]) * t * t * t))
    return out


def analyse(controls, subdiv=4):
    """与 TrackPath.BuildSamples() 同算法：Catmull-Rom + 弧长累积。"""
    n = len(controls)
    samples = []
    distance = 0.0
    prev = None
    for i in range(n):
        p0 = controls[(i - 1) % n]
        p1 = controls[i]
        p2 = controls[(i + 1) % n]
        p3 = controls[(i + 2) % n]
        for s in range(subdiv):
            t = s / subdiv
            pos = catmull_rom(p0, p1, p2, p3, t)
            if prev is not None:
                distance += math.dist(prev, pos)
            samples.append({"position": pos, "distance": distance})
            prev = pos
    total = distance
    for i, smp in enumerate(samples):
        a = samples[(i - 1) % len(samples)]["position"]
        b = samples[(i + 1) % len(samples)]["position"]
        d = [b[k] - a[k] for k in range(3)]
        ln = math.sqrt(sum(v * v for v in d))
        smp["tangent"] = [v / ln for v in d] if ln > 1e-9 else [0, 0, 1]
    return samples, total


def report(samples, total):
    # Curvature via the circumscribed-circle method on the tangent change.
    max_curv, min_radius = 0.0, 1e9
    for i in range(1, len(samples) - 1):
        a = samples[i - 1]["tangent"]
        b = samples[i + 1]["tangent"]
        dot = max(-1.0, min(1.0, sum(a[k] * b[k] for k in range(3))))
        ang = math.acos(dot)
        seg = max(1e-6, samples[i + 1]["distance"] - samples[i - 1]["distance"])
        curv = ang / seg
        if curv > max_curv:
            max_curv = curv
        if curv > 1e-6:
            min_radius = min(min_radius, 1.0 / curv)
    grades = []
    for i in range(1, len(samples)):
        a = samples[i - 1]["position"]
        b = samples[i]["position"]
        run = math.hypot(b[0] - a[0], b[2] - a[2])
        if run > 1e-6:
            grades.append(abs(b[1] - a[1]) / run)
    return {
        "total_length": total,
        "min_radius": min_radius,
        "max_curvature": max_curv,
        "max_grade": max(grades) if grades else 0.0,
        "samples": len(samples),
    }


def try_design(count, scale, stretch_x, stretch_z):
    controls = build_controls(count, scale, stretch_x, stretch_z)
    samples, total = analyse(controls)
    rep = report(samples, total)
    ok = (rep["max_grade"] <= MAX_GRADE
          and rep["min_radius"] >= MIN_RADIUS
          and TARGET_LENGTH[0] <= total <= TARGET_LENGTH[1])
    return controls, rep, ok


def search():
    """Scan the parameter space until the design satisfies every constraint."""
    best = None
    attempts = []
    for count in (22, 24, 26, 28, 30, 32, 34):
        for scale in (0.8, 0.85, 0.9, 0.95, 1.0, 1.05, 1.1):
            for stretch_x in (1.0, 1.15, 1.3, 1.45):
                for stretch_z in (1.0, 0.85, 0.7):
                    controls, rep, ok = try_design(count, scale, stretch_x, stretch_z)
                    attempts.append((ok, rep, (count, scale, stretch_x, stretch_z)))
                    if ok:
                        # Prefer a tight minimum radius (more technical corners)
                        # while keeping the length near the 2.6 km target and the
                        # gradient mild.
                        score = (abs(rep["total_length"] - 2600.0)
                                 + rep["max_grade"] * 900
                                 + max(0.0, 22.0 - rep["min_radius"]) * 60)
                        if best is None or score < best[0]:
                            best = (score, controls, rep, (count, scale, stretch_x, stretch_z))
    attempts.sort(key=lambda a: (not a[0],
                                 a[1]["min_radius"],
                                 abs(a[1]["total_length"] - 2600.0),
                                 a[1]["max_grade"]))
    return best, attempts


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--write", action="store_true", help="write assets/track_layout.json")
    ap.add_argument("--backup", action="store_true", help="keep the current layout as .bak")
    args = ap.parse_args()

    best, attempts = search()
    ok_count = sum(1 for a in attempts if a[0])
    print("scanned %d parameter sets, %d satisfy every constraint" % (len(attempts), ok_count))
    print("\n--- closest designs ---")
    for ok, rep, params in attempts[:6]:
        print("  %-6s len=%7.1f  Rmin=%6.1f  grade=%5.1f%%  params=%s"
              % ("OK" if ok else "fail", rep["total_length"], rep["min_radius"],
                 rep["max_grade"] * 100, params))
    if best is None:
        print("\nNO design satisfies the constraints. Widen TARGET_LENGTH or relax them.")
        return 1

    _, controls, rep, params = best
    print("\nCHOSEN params (count, scale, stretch_x, stretch_z) = %s" % (params,))
    print("  total_length = %.1f m" % rep["total_length"])
    print("  min_radius   = %.1f m" % rep["min_radius"])
    print("  max_grade    = %.2f %%" % (rep["max_grade"] * 100))

    if not args.write:
        print("\n(dry run; pass --write to replace assets/track_layout.json)")
        return 0

    if args.backup and LAYOUT.exists():
        bak = LAYOUT.with_suffix(".json.bak")
        LAYOUT.replace(bak)
        print("backup -> %s" % bak)

    data = {
        "name": NAME,
        "seed": SEED,
        "road_width": ROAD_WIDTH,
        "checkpoints": CHECKPOINTS,
        "village_spans": VILLAGE_SPANS,
        "tunnel_spans": TUNNEL_SPANS,
        "jump_spans": JUMP_SPANS,
        "notes": "COSTA NOVA / 山谷环线。由 tools/design_track_nova.py 以极坐标半径调制生成，"
                 "满足 studio 约束：总长 %.0f m、最小半径 %.1f m、最大坡度 %.1f%%。"
                 % (rep["total_length"], rep["min_radius"], rep["max_grade"] * 100),
        "design_params": {
            "count": params[0], "scale": params[1],
            "stretch_x": params[2], "stretch_z": params[3],
            "base_radius": BASE_RADIUS, "a1": A1, "k1": K1, "a2": A2, "k2": K2,
            "h_base": H_BASE, "h_a1": H_A1, "h_k1": H_K1, "h_a2": H_A2, "h_k2": H_K2,
        },
        "control_points": controls,
        "source": "tools/design_track_nova.py (polar radius modulation)",
        "synced_at": datetime.now().strftime("%Y-%m-%d %H:%M:%S"),
    }
    LAYOUT.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("\nwrote %s (%d control points)" % (LAYOUT.relative_to(ROOT), len(controls)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
