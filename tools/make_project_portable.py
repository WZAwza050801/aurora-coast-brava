"""Replace the Library junction with a real, portable directory.

The 2026-11-12 speed workaround pointed `Library` at
`D:\\UnityProjectCaches\\Alsphalt8-20261112\\Library` via an NTFS junction. That
absolute link makes the project non-portable. This script removes only the
reparse point (`cmd /c rmdir`, never `/S`, so the cache content stays intact),
creates a real directory and copies the cache back so Unity can open the project
immediately. `Temp` is checked too. The old cache directory is left untouched.

Usage: python tools/make_project_portable.py
"""
from __future__ import annotations

import os
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CACHE = Path(r"D:\UnityProjectCaches\Alsphalt8-20261112\Library")
LIBRARY = ROOT / "Library"
TEMP = ROOT / "Temp"


def is_junction(path: Path) -> bool:
    if not path.exists():
        return False
    try:
        return bool(os.readlink(path))
    except OSError:
        return False


def main() -> int:
    if not LIBRARY.exists():
        print("LIBRARY_MISSING", flush=True)
        return 1

    if is_junction(LIBRARY):
        print(f"JUNCTION target={os.readlink(LIBRARY)}", flush=True)
        result = subprocess.run(
            ["cmd", "/c", "rmdir", str(LIBRARY)],
            capture_output=True, text=True, errors="replace",
        )
        print(f"RMDIR rc={result.returncode} out={result.stdout.strip()} err={result.stderr.strip()}", flush=True)
        if result.returncode != 0 or LIBRARY.exists():
            print("RMDIR_FAILED - aborting, nothing else was touched", flush=True)
            return 2
    else:
        print("LIBRARY_ALREADY_REAL_DIR", flush=True)

    LIBRARY.mkdir(parents=True, exist_ok=True)
    if CACHE.exists():
        command = [
            "robocopy", str(CACHE), str(LIBRARY), "/E", "/COPY:DAT", "/DCOPY:DAT",
            "/R:1", "/W:1", "/NFL", "/NDL", "/NJH", "/NJS", "/NP",
        ]
        result = subprocess.run(command, capture_output=True, text=True, errors="replace")
        print(f"ROBOCOPY rc={result.returncode} (0-7 = success)", flush=True)
        if result.returncode >= 8:
            print(result.stdout[-1500:], flush=True)
            print(result.stderr[-500:], flush=True)
            return 3
    else:
        print("CACHE_MISSING - Unity will rebuild Library from Assets on next open", flush=True)

    files = sum(1 for p in LIBRARY.rglob("*") if p.is_file())
    print(f"PORTABLE_LIBRARY files={files} reparse={is_junction(LIBRARY)}", flush=True)
    print(f"TEMP exists={TEMP.exists()} reparse={is_junction(TEMP)}", flush=True)
    return 0


if __name__ == "__main__":
    sys.exit(main())
