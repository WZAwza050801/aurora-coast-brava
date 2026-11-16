"""Rebuild the raw source assets that are not versioned.

The repository ships the converted, LOD-reduced assets that Unity actually
uses. The heavy originals (Poly Haven models, the McGuire BMW OBJ) are left
out of git and re-downloaded with this script. Every download is checked
against the md5 recorded in tools/asset_manifest.json.

    python tools/fetch_assets.py            # fetch whatever is missing
    python tools/fetch_assets.py --verify   # only check, download nothing

Everything fetched here is CC0. See NOTICE.md for the full licence table.
"""

import argparse
import hashlib
import io
import json
import os
import shutil
import sys
import tempfile
import urllib.request

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MANIFEST = os.path.join(ROOT, "tools", "asset_manifest.json")
CHUNK = 1 << 20


def md5_of(path):
    digest = hashlib.md5()
    with open(path, "rb") as fh:
        for block in iter(lambda: fh.read(CHUNK), b""):
            digest.update(block)
    return digest.hexdigest()


def download(url, dest, expect_md5=None):
    """Fetch one file, verify it, and only then move it into place."""
    os.makedirs(os.path.dirname(dest), exist_ok=True)
    tmp = dest + ".part"
    req = urllib.request.Request(url, headers={"User-Agent": "aurora-asset-fetch"})
    with urllib.request.urlopen(req, timeout=180) as resp, open(tmp, "wb") as out:
        shutil.copyfileobj(resp, out, CHUNK)
    if expect_md5:
        got = md5_of(tmp)
        if got.lower() != expect_md5.lower():
            os.remove(tmp)
            raise RuntimeError("md5 mismatch for %s\n  expected %s\n  got      %s"
                               % (url, expect_md5, got))
    os.replace(tmp, dest)
    return os.path.getsize(dest)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--verify", action="store_true",
                        help="only report what is missing, do not download")
    parser.add_argument("--force", action="store_true",
                        help="re-download even if the file is already present")
    args = parser.parse_args()

    if not os.path.exists(MANIFEST):
        print("missing %s -- run tools/build_asset_manifest.py first" % MANIFEST)
        return 2
    with io.open(MANIFEST, encoding="utf-8") as fh:
        manifest = json.load(fh)

    ok = missing = failed = 0
    total_bytes = 0

    for entry in manifest.get("assets", []):
        url = entry.get("url")
        if not url:
            print("  SKIP  no url for %s" % entry.get("id"))
            continue

        if "extract_to" in entry:
            # An archive: fetch it and unpack the member we need.
            dest_dir = os.path.join(ROOT, entry["extract_to"])
            member = entry.get("extract_member")
            target = os.path.join(dest_dir, member) if member else None
            if target and os.path.exists(target) and not args.force:
                print("  OK    %s (cached)" % entry["id"])
                ok += 1
                continue
            if args.verify:
                print("  MISS  %s (archive)" % entry["id"])
                missing += 1
                continue
            tmpdir = tempfile.mkdtemp(prefix="aurora_fetch_")
            try:
                archive = os.path.join(tmpdir, entry.get("archive", "asset.zip"))
                download(url, archive, entry.get("md5"))
                os.makedirs(dest_dir, exist_ok=True)
                import zipfile
                with zipfile.ZipFile(archive) as zf:
                    names = zf.namelist()
                    wanted = [n for n in names if n.rsplit("/", 1)[-1] == member] \
                        if member else names
                    if not wanted:
                        raise RuntimeError("%s not inside %s" % (member, archive))
                    for name in wanted:
                        with zf.open(name) as src, \
                                open(os.path.join(dest_dir, name.rsplit("/", 1)[-1]), "wb") as dst:
                            shutil.copyfileobj(src, dst)
                print("  DONE  %s" % entry["id"])
                ok += 1
            except Exception as exc:
                print("  FAIL  %s: %s" % (entry["id"], exc))
                failed += 1
            finally:
                shutil.rmtree(tmpdir, ignore_errors=True)
            continue

        rel = entry["path"]
        dest = os.path.join(ROOT, rel.replace("/", os.sep))
        if os.path.exists(dest) and not args.force:
            expect = entry.get("md5")
            if not expect or md5_of(dest).lower() == expect.lower():
                print("  OK    %s (cached)" % rel)
                ok += 1
                continue
            print("  STALE %s (md5 differs)" % rel)
        if args.verify:
            print("  MISS  %s" % rel)
            missing += 1
            continue
        try:
            size = download(url, dest, entry.get("md5"))
            total_bytes += size
            print("  DONE  %s (%.1f MB)" % (rel, size / 1048576.0))
            ok += 1
        except Exception as exc:
            print("  FAIL  %s: %s" % (rel, exc))
            failed += 1

    print("\n%d ok, %d missing, %d failed, %.1f MB downloaded"
          % (ok, missing, failed, total_bytes / 1048576.0))
    if failed:
        return 1
    if missing:
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
