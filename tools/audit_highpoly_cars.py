#!/usr/bin/env python3
"""Query the 3DAssets.dev catalogue for high-poly CC0 car models.

The catalogue is public and needs no auth. The list endpoint does not expose
triangle counts, so each candidate's Markdown view is fetched, which does.

Writes a ranked shortlist to tests/mcp/highpoly_cars.json.
"""

import json
import re
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "tests" / "mcp" / "highpoly_cars.json"

QUERIES = ["car", "coupe", "supercar", "sports car", "race car", "muscle car",
           "convertible", "roadster", "hot hatch", "grand tourer", "sedan",
           "endurance", "formula", "gt car", "vehicle", "truck", "bus", "motorcycle"]

CAR_WORDS = ("car", "coupe", "supercar", "roadster", "sedan", "race", "muscle",
             "convertible", "hatch", "tourer", "automobile", "vehicle", "gt ",
             "buggy", "truck", "motorcycle", "bike", "van", "bus", "tractor")

TRI_RE = re.compile(r"\*\*Geometry:\*\*\s*([\d,]+)\s+triangles")


def fetch(url, timeout=25):
    req = urllib.request.Request(url, headers={"User-Agent": "aurora-asset-audit"})
    with urllib.request.urlopen(req, timeout=timeout) as resp:
        return resp.read()


def main():
    seen = {}
    for q in QUERIES:
        try:
            raw = fetch("https://3dassets.dev/api/v1/assets?q=%s&limit=100" % q.replace(" ", "%20"))
            data = json.loads(raw.decode("utf-8"))
        except Exception as exc:
            print("query %-14s failed: %s" % (q, exc))
            continue
        for a in data.get("data", []):
            seen[a["id"]] = a
        print("query %-14s cumulative unique = %d" % (q, len(seen)))

    rows = []
    for a in seen.values():
        title = (a.get("title") or "").lower()
        if not any(w in title for w in CAR_WORDS):
            continue
        slug = a.get("slug")
        try:
            md = fetch("https://3dassets.dev/assets/%s.md" % slug).decode("utf-8", "replace")
        except Exception as exc:
            print("  detail failed %-40s %s" % (slug[:40], exc))
            continue
        tri = TRI_RE.search(md)
        tris = int(tri.group(1).replace(",", "")) if tri else 0
        size = re.search(r"\*\*Size \(m\):\*\*\s*([\d.]+) × ([\d.]+) × ([\d.]+)", md)
        meshes = re.search(r"(\d+)\s+meshes", md)
        mats = re.search(r"(\d+)\s+materials", md)
        lic = re.search(r"\*\*Licence:\*\*\s*([^\n:]+)", md)
        contributor = re.search(r"\*\*Contributor:\*\*\s*([^\n]+)", md)
        anims = re.search(r"\*\*Animations:\*\*\s*([^\n]+)", md)
        rows.append({
            "id": a["id"],
            "slug": slug,
            "title": a.get("title"),
            "triangles": tris,
            "meshes": int(meshes.group(1)) if meshes else 0,
            "materials": int(mats.group(1)) if mats else 0,
            "bounds": [float(x) for x in size.groups()] if size else None,
            "license": lic.group(1).strip() if lic else "?",
            "contributor": contributor.group(1).strip() if contributor else "?",
            "animations": anims.group(1).strip() if anims else "",
            "cdn": a.get("cdnUrl"),
        })
        print("  %-7s %8s tris  %-46s %s" % (a["id"], tris, (a.get("title") or "")[:46], slug[:40]))

    rows.sort(key=lambda r: r["triangles"], reverse=True)
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(json.dumps(rows, ensure_ascii=False, indent=1), encoding="utf-8")
    print("\n%d car candidates -> %s" % (len(rows), OUT))
    print("\n--- TOP 30 by triangles ---")
    for r in rows[:30]:
        print("%9s tris  %-50s  %s" % (r["triangles"], (r["title"] or "")[:50], r["slug"][:46]))


if __name__ == "__main__":
    main()
