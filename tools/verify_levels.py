"""Is levels.json what the level files say? Re-grow every chapter's target with the Odin CLI and compare.

    python tools/verify_levels.py [--cli tools/bin/origin_cli.exe] [--json unity/PointOfOrigin/Assets/StreamingAssets/levels.json]

The level compiler (tools/levels.nx) needs Nexium; this check needs only Python and
the CLI, so CI can catch a stale levels.json without the whole toolchain.
"""
import argparse
import glob
import json
import os
import subprocess
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def parse_origin(path):
    meta, rows, in_map = {}, [], False
    with open(path, encoding="utf-8") as f:
        for raw in f:
            line = raw.rstrip("\n")
            if in_map:
                if line.strip() == "end":
                    break
                rows.append(line)
                continue
            if line.startswith("//") or not line.strip():
                continue
            if line.strip() == "map":
                in_map = True
                continue
            key, _, value = line.partition(" ")
            meta[key] = value.strip()
    return meta, rows


def cells(text):
    out = []
    for part in (text or "").split(";"):
        if part:
            x, y = part.split(",")[:2]
            out.append((int(x), int(y)))
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--cli", default=os.path.join(ROOT, "tools", "bin", "origin_cli.exe" if os.name == "nt" else "origin_cli"))
    ap.add_argument("--json", default=os.path.join(ROOT, "unity", "PointOfOrigin", "Assets", "StreamingAssets", "levels.json"))
    args = ap.parse_args()

    data = json.load(open(args.json, encoding="utf-8"))
    compiled = {lv["name"]: lv for lv in data["levels"]}
    files = sorted(glob.glob(os.path.join(ROOT, "levels", "*.origin")))
    bad = 0
    for path in files:
        meta, rows = parse_origin(path)
        name = meta["name"]
        w, h = len(rows[0]), len(rows)
        origins = [(x, y) for y, row in enumerate(rows) for x, ch in enumerate(row) if ch == "o"]
        rock = ";".join("".join("#" if ch == "#" else "." for ch in row) for row in rows)
        lv = compiled.get(name)
        problems = []
        if lv is None:
            problems.append("missing from levels.json")
        else:
            if (lv["w"], lv["h"]) != (w, h):
                problems.append(f"size {lv['w']}x{lv['h']} in json, {w}x{h} in file")
            if lv["rule"] != meta["rule"] or int(lv["steps"]) != int(meta["steps"]):
                problems.append(f"rule/steps {lv['rule']} x{lv['steps']} in json, {meta['rule']} x{meta['steps']} in file")
            if sorted(cells(lv["origins"])) != sorted(origins):
                problems.append(f"origins {lv['origins']} in json, {origins} in file")
            out = subprocess.run(
                [args.cli, "--w", str(w), "--h", str(h), "--rule", meta["rule"], "--steps", meta["steps"],
                 "--seeds", ";".join(f"{x},{y}" for x, y in origins), "--rock", rock, "--ascii"],
                capture_output=True, text=True)
            grown = [l for l in out.stdout.splitlines() if l and not l.startswith("alive")]
            if len(grown) != h:
                problems.append(f"CLI returned {len(grown)} rows: {out.stderr.strip()[:120]}")
            else:
                target = "".join("#" if ch == "#" else "." for line in grown for ch in line)
                if target != lv["target"]:
                    diff = sum(1 for a, b in zip(target, lv["target"]) if a != b)
                    problems.append(f"target differs in {diff} cells: levels.json is stale, run nx run tools/levels.nx")
        status = "ok" if not problems else "STALE"
        print(f"{os.path.basename(path)}: {status}")
        for p in problems:
            print("    -", p)
        bad += bool(problems)
    extra = set(compiled) - {parse_origin(p)[0]["name"] for p in files}
    for name in sorted(extra):
        print(f"levels.json has '{name}' with no level file")
        bad += 1
    print(f"{len(files) - bad} of {len(files)} chapters match levels.json")
    sys.exit(1 if bad else 0)


if __name__ == "__main__":
    main()
