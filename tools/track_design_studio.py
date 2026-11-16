#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""AURORA / Costa Brava 赛道设计工作台（纯标准库，无第三方依赖）。

这是赛道设计的「单一数据源」工具：编辑 assets/track_layout.json 的控制点，
本工具就能在不打开 Unity 的情况下预览俯视图、长度、曲率、坡度与检查点，
并把控制点导出成 Unity 可直接编译的 C# 数组。

算法与 Assets/Scripts/TrackPath.cs 的 BuildSamples() 逐行对应（Catmull-Rom
+ 按 4 m 细分 + 弧长累积 + 切线/侧向向量），所以预览结果 = 游戏里的赛道。

用法（在工程根目录）：
  python tools/track_design_studio.py --sync-from-csharp   # 首次：从 C# 抽出当前赛道
  python tools/track_design_studio.py --preview            # 生成 HTML 预览 + 控制台分析
  python tools/track_design_studio.py --emit-csharp        # 写 TrackLayout.generated.cs
  python tools/track_design_studio.py --all                # 以上三步（sync 除外）
"""

from __future__ import annotations

import argparse
import json
import math
import re
import sys
from datetime import datetime
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
LAYOUT = ROOT / "assets" / "track_layout.json"
TRACK_PATH_CS = ROOT / "Assets" / "Scripts" / "TrackPath.cs"
GENERATED_CS = ROOT / "Assets" / "Scripts" / "TrackLayout.generated.cs"
OUT_DIR = ROOT / "docs" / "track_design"
PREVIEW_HTML = OUT_DIR / "track_preview.html"

# 与 TrackPath.cs 一致的默认布局参数
DEFAULT = {
    "name": "Costa Brava",
    "seed": 41,
    "road_width": 13.0,
    "checkpoints": 10,
    "village_spans": [[0.06, 0.18], [0.30, 0.40], [0.56, 0.68], [0.80, 0.92]],
    "tunnel_spans": [[0.22, 0.27]],
    "jump_spans": [[0.45, 0.47]],
    "notes": "控制点为最终世界坐标（米），已应用 C# 中的 centroid/scale 变换。",
}

# 设计约束：超出会给出警告，不是硬性拒绝
MAX_GRADE = 0.22          # 22% 坡度
MAX_CURVATURE = 0.30      # rad/m（约 17 度/米，极急弯）
MIN_RADIUS = 16.0         # m（低于此值提示复核 AI 通过性，不是错误）


def vsub(a, b):
    return [a[i] - b[i] for i in range(3)]


def vadd(a, b):
    return [a[i] + b[i] for i in range(3)]


def vmul(a, s):
    return [x * s for x in a]


def vlen(a):
    return math.sqrt(sum(x * x for x in a))


def vnorm(a):
    n = vlen(a)
    return [x / n for x in a] if n > 1e-9 else [0.0, 0.0, 0.0]


def vcross(a, b):
    return [a[1] * b[2] - a[2] * b[1],
            a[2] * b[0] - a[0] * b[2],
            a[0] * b[1] - a[1] * b[0]]


def catmull_rom(p0, p1, p2, p3, t):
    """与 TrackPath.cs 第 72 行完全一致。"""
    out = []
    for k in range(3):
        out.append(0.5 * ((2.0 * p1[k]) + (-p0[k] + p2[k]) * t
                          + (2.0 * p0[k] - 5.0 * p1[k] + 4.0 * p2[k] - p3[k]) * t * t
                          + (-p0[k] + 3.0 * p1[k] - 3.0 * p2[k] + p3[k]) * t * t * t))
    return out


def build_samples(controls):
    """返回 [{position, distance, tangent, side}]，与 TrackPath.BuildSamples() 一致。"""
    n = len(controls)
    result = []
    distance = 0.0
    previous = None
    for i in range(n):
        p0 = controls[(i - 1 + n) % n]
        p1 = controls[i]
        p2 = controls[(i + 1) % n]
        p3 = controls[(i + 2) % n]
        steps = max(12, math.ceil(vlen(vsub(p2, p1)) / 4.0))
        for j in range(steps):
            t = j / float(steps)
            p = catmull_rom(p0, p1, p2, p3, t)
            if previous is not None:
                distance += vlen(vsub(p, previous))
            result.append({"position": p, "distance": distance})
            previous = p
    m = len(result)
    for i in range(m):
        nxt = result[(i + 1) % m]["position"]
        prv = result[(i - 1 + m) % m]["position"]
        tangent = vnorm(vsub(nxt, prv))
        side = vnorm(vcross([0.0, 1.0, 0.0], tangent))
        result[i]["tangent"] = tangent
        result[i]["side"] = side
    # 闭环长度：C# 把最后一段也计入 TotalLength
    total = distance + vlen(vsub(result[-1]["position"], result[0]["position"]))
    return result, total


def index_at_distance(samples, total, d):
    d = d % total
    for i, s in enumerate(samples):
        if s["distance"] >= d:
            return i
    return 0


def analyze(samples, total, checkpoint_count=10):
    """曲率、坡度、高度、检查点、急弯列表。"""
    m = len(samples)
    curv = []
    grade = []
    for i, s in enumerate(samples):
        ahead = index_at_distance(samples, total, s["distance"] + 34.0)
        t0, t1 = s["tangent"], samples[ahead]["tangent"]
        dot = max(-1.0, min(1.0, sum(t0[k] * t1[k] for k in range(3))))
        angle = math.acos(dot)                      # rad
        curv.append(angle / 34.0)                   # rad/m
        p0 = s["position"]
        p1 = samples[(i + 8) % m]["position"]
        run = math.hypot(p1[0] - p0[0], p1[2] - p0[2])
        grade.append((p1[1] - p0[1]) / run if run > 1e-6 else 0.0)

    heights = [s["position"][1] for s in samples]
    checkpoints = []
    for c in range(checkpoint_count):
        d = total * c / checkpoint_count
        idx = index_at_distance(samples, total, d)
        checkpoints.append({"index": c, "distance": round(d, 1),
                            "position": [round(x, 2) for x in samples[idx]["position"]]})

    tight = []
    for i, c in enumerate(curv):
        if c > 0.035:  # 半径 < ~29 m，值得标注的弯
            tight.append({"d": round(samples[i]["distance"], 1),
                          "curv": round(c, 4),
                          "radius": round(1.0 / c, 1) if c > 1e-6 else 9999.0})
    # 合并相邻近的急弯，只报前 12 个弯心
    merged = []
    for x in tight:
        if merged and x["d"] - merged[-1]["d"] < 60:
            if x["curv"] > merged[-1]["curv"]:
                merged[-1] = x
        else:
            merged.append(x)

    return {
        "total_length": round(total, 1),
        "sample_count": m,
        "height_min": round(min(heights), 2),
        "height_max": round(max(heights), 2),
        "max_curvature": round(max(curv), 4),
        "min_radius": round(1.0 / max(curv), 1) if max(curv) > 1e-6 else 9999.0,
        "max_grade": round(max(abs(g) for g in grade), 4),
        "checkpoints": checkpoints,
        "tight_corners": merged[:12],
        "curv": [round(c, 5) for c in curv],
        "grade": [round(g, 5) for g in grade],
    }


def check_design(report):
    warns = []
    if report["max_grade"] > MAX_GRADE:
        warns.append(f"最大坡度 {report['max_grade'] * 100:.1f}% 超过 {MAX_GRADE * 100:.0f}% 建议上限，高速下容易飞车")
    if report["min_radius"] < MIN_RADIUS:
        warns.append(f"存在发夹弯（最小半径 {report['min_radius']:.1f} m），需实跑确认 AI 通过性")
    if report["total_length"] < 1500:
        warns.append("赛道偏短，两圈可能不足 3 分钟")
    if report["total_length"] > 4000:
        warns.append("赛道偏长，两圈可能超过 5 分钟验收窗口")
    return warns


# 原始手写的 24 个控制点（4.15 km 版本），保留在这里作为 tracks 的历史基线。
# 正式赛道 = centroid + (raw - centroid) * 0.55，缩放已烘焙进 track_layout.json。
LEGACY_RAW = [
    (0, 4, 0), (0, 4, 190), (0, 5, 430), (30, 10, 650),
    (110, 24, 780), (260, 55, 850), (420, 82, 840),
    (560, 70, 760), (640, 45, 620), (620, 30, 480),
    (555, 20, 350), (460, 12, 260), (350, 9, 160),
    (480, 10, 40), (350, 8, -95), (100, 6, -150),
    (-200, 8, -100), (-380, 20, 20), (-420, 35, 180),
    (-350, 50, 300), (-220, 55, 350), (-150, 45, 250),
    (-180, 30, 140), (-120, 15, 60),
]
LEGACY_CENTROID = [121.5, 27.2, 306.9]
LEGACY_SCALE = 0.55


def sync_from_csharp():
    """重建初始 track_layout.json：优先从 TrackPath.cs 解析，否则用内置历史基线。"""
    scale, centroid, raw, source = LEGACY_SCALE, LEGACY_CENTROID, list(LEGACY_RAW), "built-in legacy baseline"
    src = TRACK_PATH_CS.read_text(encoding="utf-8")
    block = re.search(r"public static Vector3\[\] ControlPoints\(\)(.*?)\n    \}", src, re.S)
    if block:
        parsed = [[float(x) for x in m] for m in
                  re.findall(r"new Vector3\(\s*(-?[\d.]+)\s*,\s*(-?[\d.]+)\s*,\s*(-?[\d.]+)\s*\)", block.group(1))]
        sm = re.search(r"const float Scale = ([\d.]+)f", block.group(1))
        cm = re.search(r"Vector3 centroid = new Vector3\(([\d.]+)f,\s*([\d.]+)f,\s*([\d.]+)f\)", block.group(1))
        if parsed and sm and cm:
            raw, scale, centroid = parsed, float(sm.group(1)), [float(x) for x in cm.groups()]
            source = "parsed from TrackPath.cs ControlPoints()"
    points = [[round(centroid[k] + (p[k] - centroid[k]) * scale, 4) for k in range(3)] for p in raw]
    data = dict(DEFAULT)
    data["control_points"] = points
    data["source"] = source
    data["synced_at"] = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    LAYOUT.parent.mkdir(parents=True, exist_ok=True)
    LAYOUT.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"[sync] {len(points)} 个控制点 -> {LAYOUT.relative_to(ROOT)}  (scale={scale}, centroid={centroid})")
    return data


def load_layout():
    if not LAYOUT.exists():
        print("[info] track_layout.json 不存在，自动从 C# 同步")
        return sync_from_csharp()
    return json.loads(LAYOUT.read_text(encoding="utf-8"))


def emit_csharp(data):
    pts = data["control_points"]
    lines = [
        "// 本文件由 tools/track_design_studio.py 从 assets/track_layout.json 生成，请勿手改。",
        f"// generated: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}  control points: {len(pts)}",
        "using UnityEngine;",
        "",
        "public static class TrackLayout",
        "{",
        "    public static readonly Vector3[] ControlPoints = new[]",
        "    {",
    ]
    for i in range(0, len(pts), 3):
        chunk = ", ".join(
            f"new Vector3({p[0]:.4f}f, {p[1]:.4f}f, {p[2]:.4f}f)" for p in pts[i:i + 3])
        lines.append("        " + chunk + ",")
    lines += [
        "    };",
        "",
        f"    public const float RoadWidth = {float(data.get('road_width', 13.0)):.2f}f;",
        f"    public const int CheckpointCount = {int(data.get('checkpoints', 10))};",
        "}",
        "",
    ]
    GENERATED_CS.parent.mkdir(parents=True, exist_ok=True)
    GENERATED_CS.write_text("\n".join(lines), encoding="utf-8")
    print(f"[emit] -> {GENERATED_CS.relative_to(ROOT)}  ({len(pts)} 点)")


def render_html(data, samples, total, report):
    """自包含 HTML：可拖拽控制点的俯视图 + 曲率/坡度剖面 + 实时统计 + 导出 JSON。"""
    pts = data["control_points"]
    xs = [p[0] for p in samples] if False else [s["position"][0] for s in samples]
    zs = [s["position"][2] for s in samples]
    payload = {
        "layout": data,
        "report": report,
        "xs": [round(x, 3) for x in xs],
        "zs": [round(z, 3) for z in zs],
        "ys": [round(s["position"][1], 3) for s in samples],
    }
    blob = json.dumps(payload, ensure_ascii=False, separators=(",", ":"))
    html = """<!DOCTYPE html>
<html lang="zh-CN"><head><meta charset="utf-8">
<title>AURORA 赛道设计工作台</title>
<style>
:root{--bg:#f6f7f9;--card:#fff;--ink:#1b2330;--muted:#6b7684;--line:#dfe4ea;--accent:#e2622b;--blue:#2c6fd6;--green:#2e9e6b}
*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--ink);font:14px/1.6 -apple-system,Segoe UI,Microsoft YaHei,sans-serif}
header{padding:20px 28px;background:var(--card);border-bottom:1px solid var(--line)}
h1{margin:0;font-size:20px}header p{margin:4px 0 0;color:var(--muted);font-size:13px}
.wrap{display:grid;grid-template-columns:minmax(420px,1.15fr) minmax(320px,.85fr);gap:16px;padding:16px 28px 32px}
.card{background:var(--card);border:1px solid var(--line);border-radius:10px;padding:16px}
.card h2{margin:0 0 10px;font-size:15px}
.stats{display:grid;grid-template-columns:repeat(3,1fr);gap:10px;margin-bottom:12px}
.stat{background:#f2f5f8;border-radius:8px;padding:9px 11px}.stat b{display:block;font-size:19px;line-height:1.25}
.stat span{color:var(--muted);font-size:11px;letter-spacing:.03em}
svg{width:100%;height:auto;background:#fbfcfd;border:1px solid var(--line);border-radius:8px;touch-action:none}
.hint{color:var(--muted);font-size:12px;margin-top:8px}
table{width:100%;border-collapse:collapse;font-size:12px}td,th{padding:5px 7px;border-bottom:1px solid var(--line);text-align:left}
th{color:var(--muted);font-weight:600}
button{background:var(--blue);color:#fff;border:0;border-radius:6px;padding:8px 14px;font-size:13px;cursor:pointer;margin:6px 6px 0 0}
button.alt{background:#eef2f7;color:var(--ink)}
.warn{background:#fff4e8;border-left:3px solid var(--accent);padding:8px 11px;margin:8px 0;font-size:12px}
canvas{width:100%;height:96px;background:#fbfcfd;border:1px solid var(--line);border-radius:8px}
</style></head><body>
<header><h1>AURORA / __NAME__ — 赛道设计工作台</h1>
<p>拖动俯视图里的橙色控制点即可改线；长度、曲率、坡度实时重算。算法与 Unity 的 TrackPath.BuildSamples() 一致。</p></header>
<div class="wrap">
<div class="card"><h2>俯视图（X–Z 平面，橙点可拖动）</h2>
<svg id="map" viewBox="0 0 640 560" preserveAspectRatio="xMidYMid meet"></svg>
<div class="hint">绿点 = 起点／终点；蓝点 = 10 个检查点；灰线 = 道路左右边界。拖动后点「导出 JSON」覆盖 assets/track_layout.json，再跑 --emit-csharp 与无头重建。</div>
<button onclick="exportJSON()">导出 JSON</button><button class="alt" onclick="resetPts()">还原</button>
<button class="alt" onclick="smoothPts()">平滑一次</button>
<div class="stats" id="stats"></div>
<div id="warns"></div>
<h2 style="margin-top:14px">高度剖面 / 曲率剖面</h2>
<canvas id="profile" width="620" height="96"></canvas>
<div class="hint">上：沿赛道高度（m）；下：曲率热色（红=急弯）。</div>
</div>
<div class="card"><h2>设计统计</h2><div id="summary"></div>
<h2 style="margin-top:18px">主要弯道</h2><table id="corners"><thead><tr><th>里程 m</th><th>半径 m</th><th>曲率 rad/m</th></tr></thead><tbody></tbody></table>
<h2 style="margin-top:18px">工具链</h2>
<table><tbody>
<tr><td>1 编辑</td><td>assets/track_layout.json</td></tr>
<tr><td>2 预览</td><td>python tools/track_design_studio.py --preview</td></tr>
<tr><td>3 导出</td><td>--emit-csharp → TrackLayout.generated.cs</td></tr>
<tr><td>4 重建</td><td>python tools/run_headless_build.py --tag &lt;tag&gt;</td></tr>
<tr><td>5 验收</td><td>AuroraCoast.exe -racetest</td></tr>
</tbody></table></div></div>
<script>
const DATA = __BLOB__;
let pts = DATA.layout.control_points.map(p=>p.slice());
const orig = DATA.layout.control_points.map(p=>p.slice());
const W=640,H=560,PAD=34;
const svg=document.getElementById('map');
const NS='http://www.w3.org/2000/svg';
function cr(p0,p1,p2,p3,t){return [0,1,2].map(k=>0.5*((2*p1[k])+(-p0[k]+p2[k])*t+(2*p0[k]-5*p1[k]+4*p2[k]-p3[k])*t*t+(-p0[k]+3*p1[k]-3*p2[k]+p3[k])*t*t*t));}
function dist(a,b){return Math.hypot(a[0]-b[0],a[1]-b[1],a[2]-b[2]);}
function samples(controls){const n=controls.length,out=[];let d=0,prev=null;
 for(let i=0;i<n;i++){const p0=controls[(i-1+n)%n],p1=controls[i],p2=controls[(i+1)%n],p3=controls[(i+2)%n];
  const steps=Math.max(12,Math.ceil(dist(p1,p2)/4));for(let j=0;j<steps;j++){const p=cr(p0,p1,p2,p3,j/steps);if(prev)d+=dist(p,prev);out.push({p,d});prev=p;}}
 const total=d+dist(out[out.length-1].p,out[0].p);
 const m=out.length;for(let i=0;i<m;i++){const a=out[(i+1)%m].p,b=out[(i-1+m)%m].p;
  const t=[a[0]-b[0],a[1]-b[1],a[2]-b[2]],L=Math.hypot(...t)||1;out[i].t=t.map(x=>x/L);
  const s=[-out[i].t[2],0,out[i].t[0]],sl=Math.hypot(...s)||1;out[i].s=s.map(x=>x/sl);}
 return {s:out,total};}
function idxAt(S,d){d=((d%S.total)+S.total)%S.total;for(let i=0;i<S.s.length;i++)if(S.s[i].d>=d)return i;return 0;}
function bounds(){const xs=pts.map(p=>p[0]),zs=pts.map(p=>p[2]);
 const minx=Math.min(...xs)-90,maxx=Math.max(...xs)+90,minz=Math.min(...zs)-90,maxz=Math.max(...zs)+90;
 const sc=Math.min((W-2*PAD)/(maxx-minx),(H-2*PAD)/(maxz-minz));
 return {minx,maxx,minz,maxz,sc,ox:PAD+(W-2*PAD-(maxx-minx)*sc)/2,oz:PAD+(H-2*PAD-(maxz-minz)*sc)/2};}
function proj(p,B){return [B.ox+(p[0]-B.minx)*B.sc, H-B.oz-(p[2]-B.minz)*B.sc];}
function el(tag,attrs){const e=document.createElementNS(NS,tag);for(const k in attrs)e.setAttribute(k,attrs[k]);return e;}
function draw(){
 const B=bounds(),S=samples(pts);svg.innerHTML='';
 const half=DATA.layout.road_width/2;
 for(const side of [-1,1]){const poly=S.s.map(o=>{const q=[o.p[0]+o.s[0]*side*half,o.p[1],o.p[2]+o.s[2]*side*half];return proj(q,B).join(',')}).join(' ');
  svg.appendChild(el('polyline',{points:poly,fill:'none',stroke:'#cdd6e0','stroke-width':1.2}));}
 let curv=[];for(let i=0;i<S.s.length;i++){const a=S.s[i],b=S.s[idxAt(S,a.d+34)];
  const dot=Math.max(-1,Math.min(1,a.t[0]*b.t[0]+a.t[1]*b.t[1]+a.t[2]*b.t[2]));curv.push(Math.acos(dot)/34);}
 for(let i=0;i<S.s.length-1;i++){const a=proj(S.s[i].p,B),b=proj(S.s[i+1].p,B);
  const k=Math.min(1,curv[i]/0.06);const col='rgb('+Math.round(44+200*k)+','+Math.round(111-55*k)+','+Math.round(214-170*k)+')';
  svg.appendChild(el('line',{x1:a[0],y1:a[1],x2:b[0],y2:b[1],stroke:col,'stroke-width':3.2,'stroke-linecap':'round'}));}
 const a=proj(S.s[0].p,B),b=proj(S.s[S.s.length-1].p,B);
 svg.appendChild(el('line',{x1:a[0],y1:a[1],x2:b[0],y2:b[1],stroke:'#2e9e6b','stroke-width':1.5,'stroke-dasharray':'4 3'}));
 for(let c=0;c<DATA.layout.checkpoints;c++){const q=proj(S.s[idxAt(S,S.total*c/DATA.layout.checkpoints)].p,B);
  svg.appendChild(el('circle',{cx:q[0],cy:q[1],r:3.4,fill:'#2c6fd6'}));}
 svg.appendChild(el('circle',{cx:a[0],cy:a[1],r:6,fill:'#2e9e6b',stroke:'#fff','stroke-width':2}));
 pts.forEach((p,i)=>{const q=proj(p,B);const c=el('circle',{cx:q[0],cy:q[1],r:6,fill:'#e2622b',stroke:'#fff','stroke-width':2,style:'cursor:grab'});
  c.dataset.i=i;svg.appendChild(c);const tx=el('text',{x:q[0]+9,y:q[1]-8,fill:'#6b7684','font-size':10});tx.textContent=i;svg.appendChild(tx);});
 const maxC=Math.max(...curv),maxG=Math.max(...S.s.map((o,i)=>{const n=S.s[(i+8)%S.s.length].p,run=Math.hypot(n[0]-o.p[0],n[2]-o.p[2]);return run>1e-6?Math.abs((n[1]-o.p[1])/run):0;}));
 document.getElementById('stats').innerHTML=
  '<div class="stat"><b>'+S.total.toFixed(0)+'</b><span>赛道长度 m</span></div>'+
  '<div class="stat"><b>'+(1/maxC).toFixed(0)+'</b><span>最小半径 m</span></div>'+
  '<div class="stat"><b>'+(maxG*100).toFixed(1)+'%</b><span>最大坡度</span></div>';
 const warns=[];if(maxG>0.22)warns.push('坡度超过 22%，高速容易飞车');if(1/maxC<28)warns.push('最小半径 <28 m，AI 会频繁撞护栏');
 document.getElementById('warns').innerHTML=warns.map(w=>'<div class="warn">'+w+'</div>').join('');
 drawProfile(S.s,curv);
}
function drawProfile(S,curv){const cv=document.getElementById('profile'),g=cv.getContext('2d');g.clearRect(0,0,cv.width,cv.height);
 const ys=S.map(o=>o.p[1]),lo=Math.min(...ys),hi=Math.max(...ys);
 g.strokeStyle='#2c6fd6';g.lineWidth=1.6;g.beginPath();S.forEach((o,i)=>{const x=i/(S.length-1)*cv.width,y=44-(o.p[1]-lo)/Math.max(1,hi-lo)*34;i?g.lineTo(x,y):g.moveTo(x,y);});g.stroke();
 S.forEach((o,i)=>{const k=Math.min(1,curv[i]/0.09);g.fillStyle='rgb('+Math.round(44+200*k)+','+Math.round(111-55*k)+','+Math.round(214-170*k)+')';g.fillRect(i/(S.length-1)*cv.width,60,2,26);});
 g.fillStyle='#6b7684';g.font='10px sans-serif';g.fillText('高度 '+lo.toFixed(1)+'–'+hi.toFixed(1)+' m',6,12);g.fillText('曲率（红=急弯）',6,96);}
let drag=-1;
svg.addEventListener('pointerdown',e=>{if(e.target.dataset.i!==undefined){drag=+e.target.dataset.i;svg.setPointerCapture(e.pointerId);}});
svg.addEventListener('pointermove',e=>{if(drag<0)return;const B=bounds(),r=svg.getBoundingClientRect();
 const x=(e.clientX-r.left)/r.width*W,z=(e.clientY-r.top)/r.height*H;
 pts[drag][0]=B.minx+(x-B.ox)/B.sc;pts[drag][2]=B.minz+(H-z-B.oz)/B.sc;draw();});
svg.addEventListener('pointerup',()=>{drag=-1;});
function exportJSON(){const d=JSON.parse(JSON.stringify(DATA.layout));d.control_points=pts.map(p=>p.map(x=>+x.toFixed(2)));
 const a=document.createElement('a');a.href=URL.createObjectURL(new Blob([JSON.stringify(d,null,2)],{type:'application/json'}));a.download='track_layout.json';a.click();}
function resetPts(){pts=orig.map(p=>p.slice());draw();}
function smoothPts(){const q=pts.map(p=>p.slice());for(let i=0;i<pts.length;i++){const a=pts[(i-1+pts.length)%pts.length],b=pts[i],c=pts[(i+1)%pts.length];
 for(let k=0;k<3;k++)q[i][k]=+(0.25*a[k]+0.5*b[k]+0.25*c[k]).toFixed(2);}pts=q;draw();}
document.getElementById('summary').innerHTML=
 '<table><tbody>'+DATA.report.checkpoints.map(c=>'<tr><td>CP'+c.index+'</td><td>'+c.distance+' m</td><td>'+c.position.join(', ')+'</td></tr>').join('')+'</tbody></table>'+
 '<p class="hint">采样点 '+DATA.report.sample_count+' 个，高度 '+DATA.report.height_min+'–'+DATA.report.height_max+' m。控制点 '+pts.length+' 个。</p>';
document.querySelector('#corners tbody').innerHTML=DATA.report.tight_corners.map(c=>'<tr><td>'+c.d+'</td><td>'+c.radius+'</td><td>'+c.curv+'</td></tr>').join('');
draw();
</script></body></html>"""
    html = html.replace("__BLOB__", blob)
    html = html.replace("__NAME__", str(data.get("name", "track")))
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    PREVIEW_HTML.write_text(html, encoding="utf-8")
    print(f"[preview] -> {PREVIEW_HTML.relative_to(ROOT)}")


def main():
    ap = argparse.ArgumentParser(description="AURORA 赛道设计工作台")
    ap.add_argument("--sync-from-csharp", action="store_true", help="从 TrackPath.cs 抽出当前赛道写 JSON")
    ap.add_argument("--preview", action="store_true", help="生成可拖拽 HTML 预览")
    ap.add_argument("--emit-csharp", action="store_true", help="生成 TrackLayout.generated.cs")
    ap.add_argument("--all", action="store_true", help="preview + emit-csharp + 控制台报告")
    args = ap.parse_args()
    if not any(vars(args).values()):
        args.all = True

    if args.sync_from_csharp:
        sync_from_csharp()
        if not args.all:
            return
    data = load_layout()
    pts = data.get("control_points") or []
    if len(pts) < 4:
        print("[error] track_layout.json 控制点不足 4 个", file=sys.stderr)
        return 2
    samples, total = build_samples(pts)
    report = analyze(samples, total, int(data.get("checkpoints", 10)))

    print(f"[track] {data.get('name','?')}: {len(pts)} 控制点 / {report['sample_count']} 采样点")
    print(f"[track] 长度 {report['total_length']} m, 高度 {report['height_min']}~{report['height_max']} m, "
          f"最小半径 {report['min_radius']} m, 最大坡度 {report['max_grade'] * 100:.1f}%")
    print(f"[track] 主要弯道 {len(report['tight_corners'])} 处: " +
          ", ".join(f"{c['d']}m/R{c['radius']}" for c in report["tight_corners"][:6]))
    warns = check_design(report)
    for w in warns:
        print(f"[warn] {w}")
    if not warns:
        print("[check] 设计约束全部通过")

    if args.preview or args.all:
        render_html(data, samples, total, report)
    if args.emit_csharp or args.all:
        emit_csharp(data)
    return 0


if __name__ == "__main__":
    sys.exit(main())
