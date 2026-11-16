"""Run a headless Unity batch build without PowerShell's duplicate-env failures.

Why this exists: Start-Process throws "An item with the same key has already been
added: path / Path" when the agent shell injects both casings of an environment
variable. Python normalises the environment block (Windows env vars are
case-insensitive), drops lowercase proxy duplicates and streams `-logFile -`
straight to disk so nothing is lost on an abnormal exit.

Usage:
    python tools/run_headless_build.py --tag 20261115_1700
    python tools/run_headless_build.py --method AuroraProjectBuilder.BuildMinimalAndWindows
"""
from __future__ import annotations

import argparse
import os
import subprocess
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
UNITY = Path(r"D:\桌面\Unity Hub\6000.0.29f1\Editor\Unity.exe")


def normalized_env() -> dict[str, str]:
    # Upper-case every key so both `path` and `Path` collapse into one PATH entry.
    env: dict[str, str] = {}
    for key, value in os.environ.items():
        upper = key.upper()
        if upper in {"HTTP_PROXY", "HTTPS_PROXY", "ALL_PROXY", "NO_PROXY"} and key != upper:
            continue  # lowercase duplicate: keep only the canonical spelling
        env[upper] = value
    env["UNITY_MCP_ALLOW_BATCH"] = "1"
    return env


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--method", default="AuroraProjectBuilder.BuildCompleteRace")
    parser.add_argument("--tag", default=time.strftime("%Y%m%d_%H%M"))
    args = parser.parse_args()

    if not UNITY.exists():
        print(f"UNITY_NOT_FOUND {UNITY}", flush=True)
        return 127

    log_dir = ROOT / "tests" / "mcp"
    log_dir.mkdir(parents=True, exist_ok=True)
    out = log_dir / f"build_{args.tag}.out"
    err = log_dir / f"build_{args.tag}.err"
    cmd = [
        str(UNITY), "-batchmode", "-nographics", "-disable-assembly-updater",
        "-projectPath", str(ROOT), "-executeMethod", args.method, "-logFile", "-",
    ]
    print("CMD=" + " ".join(cmd), flush=True)
    print(f"LOG_OUT={out}", flush=True)
    with out.open("w", encoding="utf-8", errors="replace") as fo, \
            err.open("w", encoding="utf-8", errors="replace") as fe:
        process = subprocess.Popen(cmd, cwd=str(ROOT), env=normalized_env(), stdout=fo, stderr=fe)
        print(f"BUILD_PID={process.pid}", flush=True)
        code = process.wait()
    print(f"BUILD_EXIT={code}", flush=True)
    result = ROOT / "tests" / "race" / "build_result.txt"
    if result.exists():
        print("BUILD_RESULT_BEGIN", flush=True)
        print(result.read_text(encoding="utf-8", errors="replace").strip(), flush=True)
        print("BUILD_RESULT_END", flush=True)
    return code


if __name__ == "__main__":
    sys.exit(main())
