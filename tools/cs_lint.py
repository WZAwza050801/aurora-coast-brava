"""Offline syntax sanity check for the Unity C# scripts.

This is not a compiler. It only catches the two mistakes that actually cost
build cycles in this project:

  * unbalanced braces (a half-written method body),
  * a non-ASCII identifier accidentally typed into source.

It runs in a fraction of a second, so it is worth calling before every
`run_headless_build.py` invocation. The authoritative signal is still the
build log: `error CS` lines in `tests/mcp/build_*.out`, and
`warnings=0` in `tests/race/build_result.txt`.

Usage:
    python tools/cs_lint.py                 # lint the default script set
    python tools/cs_lint.py path/to/File.cs # lint specific files
"""

import os
import re
import sys

DEFAULT_FILES = [
    'Assets/Scripts/ArcadeCarController.cs',
    'Assets/Scripts/ChaseCamera.cs',
    'Assets/Scripts/FullRaceSmokeTest.cs',
    'Assets/Scripts/InputMap.cs',
    'Assets/Scripts/MinimalHud.cs',
    'Assets/Scripts/MinimalSmokeTest.cs',
    'Assets/Scripts/RaceHud.cs',
    'Assets/Scripts/RaceManager.cs',
    'Assets/Scripts/RivalDriver.cs',
    'Assets/Scripts/TrackPath.cs',
    'Assets/Scripts/VehicleAudio.cs',
    'Assets/Scripts/VehicleCatalog.cs',
    'Assets/Scripts/VehicleGarage.cs',
]

BS = chr(92)
QUOTE = chr(34)
APOS = chr(39)


def check(path):
    """Return (ok, messages)."""
    messages = []
    try:
        src = open(path, encoding='utf-8').read()
    except OSError as exc:
        return False, ['cannot read: %s' % exc]

    depth = 0
    i = 0
    n = len(src)
    instr = None
    line = 1
    # A copy of the source with every comment and string literal blanked
    # out. The non-ASCII check has to run on that, otherwise a Chinese
    # comment (which is perfectly legal) is reported as a bad identifier.
    code_only = list(src)
    while i < n:
        c = src[i]
        if c == '\n':
            line += 1
        if instr is not None:
            code_only[i] = ' '
            if c == BS:
                if i + 1 < n:
                    code_only[i + 1] = ' '
                i += 2
                continue
            if c == instr:
                instr = None
        else:
            if c == '/' and i + 1 < n and src[i + 1] == '/':
                while i < n and src[i] != '\n':
                    code_only[i] = ' '
                    i += 1
                continue
            if c == '/' and i + 1 < n and src[i + 1] == '*':
                i += 2
                while i + 1 < n and not (src[i] == '*' and src[i + 1] == '/'):
                    if src[i] == '\n':
                        line += 1
                    else:
                        code_only[i] = ' '
                    i += 1
                i += 2
                continue
            if c == QUOTE:
                instr = QUOTE
            elif c == APOS:
                instr = APOS
            elif c == '{':
                depth += 1
            elif c == '}':
                depth -= 1
                if depth < 0:
                    messages.append('unexpected } at line %d' % line)
                    return False, messages
        i += 1
    if depth != 0:
        messages.append('brace depth %d at end of file' % depth)

    # Only the code itself may contain non-ASCII characters. Comments and
    # string literals are allowed to hold any language.
    for idx, raw in enumerate("".join(code_only).splitlines(), 1):
        if any(ord(ch) > 0x2100 for ch in raw):
            messages.append('non-ascii identifier at line %d | %s'
                            % (idx, src.splitlines()[idx - 1].strip()[:110]))

    # foreach over a GameObject (or Transform) is a compile error: those are
    # not enumerable. Unity catches it, but only after a full build.
    for idx, raw in enumerate(src.splitlines(), 1):
        m = re.search(r'foreach\s*\(\s*Transform\s+\w+\s+in\s+([A-Za-z_][\w.]*)\s*\)', raw)
        if m and re.search(r'\b(GameObject|Component|Material|Renderer)\b', m.group(1)):
            messages.append('foreach over non-enumerable %s at line %d | %s'
                            % (m.group(1), idx, raw.strip()[:110]))

    return (not messages), messages


def discover(root):
    """Every C# source that ships with the project.

    The list used to be hard-coded, which meant a newly added script was
    never linted until it broke a build. Globbing keeps the check honest
    as the project grows.
    """
    found = []
    for sub in ("Assets/Scripts", "assets/Editor"):
        base = os.path.join(root, sub)
        if not os.path.isdir(base):
            continue
        for dirpath, _dirs, names in os.walk(base):
            for name in sorted(names):
                if name.endswith(".cs"):
                    full = os.path.join(dirpath, name)
                    found.append(os.path.relpath(full, root).replace(os.sep, "/"))
    return sorted(found)


def main(argv):
    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    files = argv[1:] if len(argv) > 1 else discover(root)
    if not files:
        print("No C# sources found under Assets/Scripts or assets/Editor.")
        return 1
    failed = 0
    for rel in files:
        path = rel if os.path.isabs(rel) else os.path.join(root, rel)
        if not os.path.exists(path):
            print('SKIP %-48s (not found)' % rel)
            continue
        ok, messages = check(path)
        if ok:
            print('OK   %s' % rel)
        else:
            failed += 1
            print('FAIL %s' % rel)
            for m in messages:
                print('       %s' % m)
    if failed:
        print('\n%d file(s) failed the offline lint.' % failed)
        return 1
    print('\nAll checked files passed the offline lint.')
    return 0


if __name__ == '__main__':
    sys.exit(main(sys.argv))
