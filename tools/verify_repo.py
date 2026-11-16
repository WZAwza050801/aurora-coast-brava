"""Repository health check -- the "doctor" for this project.

Verifies the things that are easy to break and hard to notice:

  * the licence files are present and say what they should,
  * every third-party asset named in NOTICE.md exists on disk,
  * no credential-looking string is tracked,
  * the heavy build products really are ignored,
  * the raw-asset manifest still has a URL for every entry,
  * the C# sources pass the offline lint.

Exit code 0 means green. Run it before pushing:

    python tools/verify_repo.py
"""

import io
import json
import os
import re
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

SECRET_PATTERNS = [
    r"[0-9a-f]{32}\.[A-Za-z0-9]{16}",
    r"sk-[A-Za-z0-9]{20,}",
    r"gho_[A-Za-z0-9]{20,}",
    r"ghp_[A-Za-z0-9]{30,}",
    r"AKIA[0-9A-Z]{16}",
    r"BEGIN [A-Z ]*PRIVATE KEY",
]

# Paths that must never be tracked.
MUST_BE_IGNORED = [
    "Assets/Scenes/CostaBrava.unity",
    "Assets/GeneratedMeshes",
    "Assets/models",
    "Assets/vehicles/bmw_cc0",
    "build",
    "Library",
    ".workbuddy/venvs",
]

# Files whose presence is part of the licence alignment.
REQUIRED_FILES = [
    "LICENSE",
    "NOTICE.md",
    "README.md",
    ".gitignore",
    ".gitattributes",
    "tools/asset_manifest.json",
    "tools/fetch_assets.py",
    "docs/ASSET_LICENSES.md",
]

SKIP_DIRS = {".git", "Library", "build", "Temp", "obj", "Logs", "UserSettings",
             "__pycache__", ".workbuddy"}


class Report(object):
    def __init__(self):
        self.failures = 0
        self.checks = 0

    def ok(self, label):
        self.checks += 1
        print("  OK    %s" % label)

    def fail(self, label, detail=""):
        self.checks += 1
        self.failures += 1
        print("  FAIL  %s" % label)
        for line in str(detail).splitlines()[:8]:
            print("        %s" % line)

    def section(self, title):
        print("\n== %s" % title)


def tracked_files():
    try:
        out = subprocess.check_output(["git", "ls-files"], cwd=ROOT,
                                      stderr=subprocess.STDOUT)
        return out.decode("utf-8", "replace").splitlines()
    except Exception as exc:
        return None


def is_ignored(path):
    try:
        res = subprocess.call(["git", "check-ignore", "-q", path], cwd=ROOT)
        return res == 0
    except Exception:
        return False


def scan_secrets(files):
    pattern = re.compile("|".join(SECRET_PATTERNS))
    hits = []
    for rel in files:
        full = os.path.join(ROOT, rel.replace("/", os.sep))
        if not os.path.isfile(full):
            continue
        if os.path.getsize(full) > 2 * 1024 * 1024:
            continue
        try:
            with io.open(full, encoding="utf-8", errors="replace") as fh:
                for lineno, line in enumerate(fh, 1):
                    if pattern.search(line):
                        hits.append("%s:%d" % (rel, lineno))
        except Exception:
            continue
    return hits


def main():
    rep = Report()
    print("AURORA / Costa Brava -- repository check")

    # ---- required files -------------------------------------------------
    rep.section("licence and documentation files")
    for rel in REQUIRED_FILES:
        if os.path.exists(os.path.join(ROOT, rel.replace("/", os.sep))):
            rep.ok(rel)
        else:
            rep.fail("missing %s" % rel)

    # ---- the licence actually says what it should -----------------------
    rep.section("licence content")
    licence = os.path.join(ROOT, "LICENSE")
    if os.path.exists(licence):
        with io.open(licence, encoding="utf-8", errors="replace") as fh:
            text = fh.read()
        if "Attribution-NonCommercial 4.0" in text and "Creative Commons" in text:
            rep.ok("LICENSE carries the CC BY-NC 4.0 text")
        else:
            rep.fail("LICENSE does not look like the CC BY-NC 4.0 text")
    notice = os.path.join(ROOT, "NOTICE.md")
    if os.path.exists(notice):
        with io.open(notice, encoding="utf-8", errors="replace") as fh:
            ntext = fh.read()
        needed = ["CC0", "CC BY", "Ferrari", "商标"]
        missing = [n for n in needed if n.lower() not in ntext.lower()]
        if missing:
            rep.fail("NOTICE.md does not mention %s" % ", ".join(missing))
        else:
            rep.ok("NOTICE.md covers CC0, CC BY, Ferrari and trademarks")
    else:
        rep.fail("NOTICE.md missing, cannot check its content")

    # ---- assets named in NOTICE exist -----------------------------------
    rep.section("assets referenced by NOTICE.md")
    if os.path.exists(notice):
        with io.open(notice, encoding="utf-8", errors="replace") as fh:
            ntext = fh.read()
        referenced = sorted(set(re.findall(r"`(Assets/[^`]+)`", ntext)))
        # NOTICE.md also quotes glob patterns to describe a whole folder;
        # only literal paths can be checked against the disk.
        literal = [r for r in referenced if "*" not in r]
        # A referenced path is fine when it is on disk OR when it is one of
        # the things the repository deliberately does not version (a fresh
        # CI checkout has none of them).
        problems = []
        for rel in literal:
            on_disk = os.path.exists(os.path.join(ROOT, rel.replace("/", os.sep)))
            if on_disk or is_ignored(rel):
                continue
            problems.append(rel)
        if problems:
            rep.fail("%d referenced asset(s) missing and not ignored" % len(problems),
                     "\n".join(problems))
        else:
            rep.ok("all %d referenced assets exist or are deliberately ignored"
                   % len(literal))

    # ---- gitignore behaviour --------------------------------------------
    rep.section("ignore rules")
    for rel in MUST_BE_IGNORED:
        if is_ignored(rel):
            rep.ok("ignored: %s" % rel)
        else:
            rep.fail("%s should be ignored but is not" % rel)

    # ---- manifest --------------------------------------------------------
    rep.section("raw asset manifest")
    manifest_path = os.path.join(ROOT, "tools", "asset_manifest.json")
    if os.path.exists(manifest_path):
        with io.open(manifest_path, encoding="utf-8") as fh:
            manifest = json.load(fh)
        entries = manifest.get("assets", [])
        no_url = [e.get("path") or e.get("id") for e in entries if not e.get("url")]
        if no_url:
            rep.fail("%d manifest entries have no url" % len(no_url), "\n".join(no_url))
        else:
            rep.ok("all %d manifest entries have a url" % len(entries))
        no_md5 = [e.get("path") or e.get("id") for e in entries if not e.get("md5")]
        if no_md5:
            rep.fail("%d manifest entries have no md5" % len(no_md5), "\n".join(no_md5))
        else:
            rep.ok("all %d manifest entries have an md5" % len(entries))
    else:
        rep.fail("tools/asset_manifest.json missing")

    # ---- secrets ---------------------------------------------------------
    rep.section("tracked content")
    files = tracked_files()
    if files is None:
        rep.fail("not a git repository, or git is unavailable")
    else:
        rep.ok("%d tracked files" % len(files))
        hits = scan_secrets(files)
        if hits:
            rep.fail("%d tracked file(s) contain a credential-looking string" % len(hits),
                     "\n".join(hits))
        else:
            rep.ok("no credential-looking strings in tracked files")
        big = [f for f in files
               if os.path.isfile(os.path.join(ROOT, f.replace("/", os.sep)))
               and os.path.getsize(os.path.join(ROOT, f.replace("/", os.sep))) > 50 * 1024 * 1024]
        if big:
            rep.fail("%d tracked file(s) exceed GitHub's 50 MB warning" % len(big),
                     "\n".join(big))
        else:
            rep.ok("no tracked file exceeds 50 MB")

    # ---- C# lint ---------------------------------------------------------
    rep.section("C# offline lint")
    lint = os.path.join(ROOT, "tools", "cs_lint.py")
    if os.path.exists(lint):
        res = subprocess.call([sys.executable, lint], cwd=ROOT,
                              stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        if res == 0:
            rep.ok("tools/cs_lint.py passed")
        else:
            rep.fail("tools/cs_lint.py reported problems")
    else:
        rep.fail("tools/cs_lint.py missing")

    print("\n%s" % ("-" * 56))
    if rep.failures:
        print("%d of %d checks FAILED." % (rep.failures, rep.checks))
        return 1
    print("All %d checks passed." % rep.checks)
    return 0


if __name__ == "__main__":
    sys.exit(main())
