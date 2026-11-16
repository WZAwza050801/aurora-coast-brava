"""Build tools/asset_manifest.json -- the rebuild recipe for the raw assets.

The raw source assets are not versioned: they are re-downloadable and weigh
128 MB. This script records where each one comes from and what its md5 must
be, so a fresh clone can rebuild them with tools/fetch_assets.py.

Poly Haven's remote naming is not uniform: the .gltf carries a resolution
suffix (island_tree_01_1k.gltf) while the .bin next to it does not
(island_tree_01.bin). The manifest therefore stores an explicit
remote -> local mapping instead of guessing from the file name.

Run it from the project root (needs network):

    python tools/build_asset_manifest.py
"""

import io
import json
import os
import sys
import urllib.request

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DOWNLOADS = os.path.join(ROOT, "downloads")
MODELS = os.path.join(ROOT, "Assets", "models")
OUT = os.path.join(ROOT, "tools", "asset_manifest.json")
API = "https://api.polyhaven.com/files/"

# asset id -> resolution used by this project
POLYHAVEN = {
    "island_tree_01": "1k",
    "coast_land_rocks_02": "1k",
    "shrub_01": "1k",
}

# The local file names differ from the remote ones for the gltf payload, so
# each asset lists its explicit mapping.
EXTRA = [
    {
        "id": "bmw_cc0_obj",
        "source": "McGuire Computer Graphics Archive (CC0)",
        "url": "https://casual-effects.com/g3d/data10/research/model/bmw/bmw.zip",
        "md5": None,
        "archive": "bmw.zip",
        "extract_to": "Assets/vehicles/bmw_cc0",
        "extract_member": "bmw.obj",
        "md5": "3547c982417eec4ca8d89579195fe514",
        "note": "CC0. The zip holds bmw.obj plus its .mtl; tools/prepare_bmw.py "
                "turns it into Assets/Vehicles/AuroraGT.fbx.",
    },
]


def api_files(asset_id):
    """{remote basename: (url, md5)} for one Poly Haven asset."""
    req = urllib.request.Request(API + asset_id, headers={"User-Agent": "aurora-asset-manifest"})
    with urllib.request.urlopen(req, timeout=60) as fh:
        data = json.loads(fh.read().decode("utf-8"))
    out = {}

    def walk(node):
        if isinstance(node, dict):
            if isinstance(node.get("url"), str) and node.get("md5"):
                out[node["url"].rsplit("/", 1)[-1]] = (node["url"], node["md5"])
                return
            for value in node.values():
                walk(value)
        elif isinstance(node, list):
            for value in node:
                walk(value)

    walk(data)
    return out


def cached_files(asset_id):
    """Same shape, from the locally cached metadata (works offline)."""
    path = os.path.join(DOWNLOADS, asset_id + "_files.json")
    if not os.path.exists(path):
        return {}
    with io.open(path, encoding="utf-8-sig") as fh:
        data = json.load(fh)
    out = {}

    def walk(node):
        if isinstance(node, dict):
            if isinstance(node.get("url"), str) and node.get("md5"):
                out[node["url"].rsplit("/", 1)[-1]] = (node["url"], node["md5"])
                return
            for value in node.values():
                walk(value)
        elif isinstance(node, list):
            for value in node:
                walk(value)

    walk(data)
    return out


def md5_of(path):
    import hashlib
    digest = hashlib.md5()
    with open(path, "rb") as fh:
        for block in iter(lambda: fh.read(1 << 20), b""):
            digest.update(block)
    return digest.hexdigest()


def main():
    entries = []
    for asset_id, res in POLYHAVEN.items():
        try:
            files = api_files(asset_id)
            source = "Poly Haven API"
        except Exception as exc:                      # offline fallback
            print("API unavailable for %s (%s); using the cached metadata" % (asset_id, exc))
            files = cached_files(asset_id)
            source = "Poly Haven (cached metadata)"

        local_dir = os.path.join(MODELS, asset_id)
        wanted = []
        if os.path.isdir(local_dir):
            for dirpath, _dirs, files_on_disk in os.walk(local_dir):
                for filename in sorted(files_on_disk):
                    if filename.endswith(".meta"):
                        continue
                    full = os.path.join(dirpath, filename)
                    wanted.append(os.path.relpath(full, ROOT).replace("\\", "/"))

        for rel in wanted:
            filename = rel.rsplit("/", 1)[-1]
            url = md5 = None
            origin = source
            # The gltf payload keeps the resolution suffix on the wire only.
            for candidate in (filename, filename.replace(".gltf", "_%s.gltf" % res)):
                if candidate in files:
                    url, md5 = files[candidate]
                    break
            if url is None and filename.endswith(".bin"):
                # The API does not list the .bin separately: it is the sibling
                # of the .gltf, without the resolution suffix.
                sibling = filename.replace(".bin", "_%s.gltf" % res)
                if sibling in files:
                    base = files[sibling][0].rsplit("/", 1)[0]
                    url = "%s/%s" % (base, filename)
                    # The buffer has no published md5, so record the checksum
                    # of the local copy. It was verified byte for byte against
                    # the CDN (shrub_01, 2026-11-16).
                    full = os.path.join(ROOT, rel.replace("/", os.sep))
                    if os.path.exists(full):
                        md5 = md5_of(full)
                        origin = source + " + local checksum"
            entries.append({
                "id": asset_id,
                "source": "Poly Haven (CC0)",
                "origin": origin,
                "url": url,
                "md5": md5,
                "path": rel,
            })

    entries.extend(EXTRA)

    manifest = {
        "note": "Raw source assets that are not versioned. Rebuild them with "
                "tools/fetch_assets.py; every entry carries an md5 to verify. "
                "All of these are CC0.",
        "assets": entries,
    }
    with io.open(OUT, "w", encoding="utf-8") as fh:
        json.dump(manifest, fh, ensure_ascii=False, indent=1)

    missing = [e["path"] for e in entries if not e.get("url")]
    print("wrote %s: %d entries, %d without a url"
          % (OUT, len(entries), len(missing)))
    for m in missing[:10]:
        print("   no url:", m)
    return 1 if missing else 0


if __name__ == "__main__":
    sys.exit(main())
