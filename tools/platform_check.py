"""Coarse platformer reachability over the compiled levels.

A standing cell is air with something solid directly below it. From one you can
walk to the neighbouring standing cell, drop off an edge onto the first solid
below, or jump to any standing cell up to JUMP_UP rows higher and JUMP_ACROSS
columns away, provided the two cells above you are clear for the leap.
Walls in mid-air are ignored, so this over-approximates: it catches gaps that
are too wide and origins you cannot stand in, not every trap.

    python tools/platform_check.py unity/PointOfOrigin/Assets/StreamingAssets/levels.json
"""
import json
import sys
from collections import deque

JUMP_UP = 2
JUMP_ACROSS = 4


def parse_cells(text):
    out = []
    for part in (text or "").split(";"):
        if not part:
            continue
        f = part.split(",")
        out.append((int(f[0]), int(f[1])))
    return out


class World:
    def __init__(self, lv, grown, carve=None):
        self.w, self.h = lv["w"], lv["h"]
        self.rock = lv["rock"]
        self.grown = grown
        self.target = lv["target"]
        # cells treated as air even when grown: the 3x3 around an origin still to be planted
        self.carve = set()
        if carve is not None:
            for dx in (-1, 0, 1):
                for dy in (-1, 0, 1):
                    self.carve.add((carve[0] + dx, carve[1] + dy))

    def solid(self, x, y):
        if x < 0 or x >= self.w:
            return True
        if y < 0 or y >= self.h:
            return False
        i = y * self.w + x
        if self.rock[i] == "#":
            return True
        return self.grown and self.target[i] == "#" and (x, y) not in self.carve

    def air(self, x, y):
        return 0 <= x < self.w and 0 <= y < self.h and not self.solid(x, y)

    def standing(self, x, y):
        return self.air(x, y) and self.solid(x, y + 1)

    def land(self, x, y):
        """Fall from an air cell to the first standing cell below, or None into the void."""
        while y < self.h:
            if self.standing(x, y):
                return (x, y)
            if not self.air(x, y):
                return None
            y += 1
        return None

    def reachable(self, start):
        seen = set()
        q = deque()
        s = self.land(*start) if not self.standing(*start) else start
        if s is None:
            return seen
        q.append(s)
        seen.add(s)
        while q:
            x, y = q.popleft()
            nxt = []
            for dx in (-1, 1):
                nx = x + dx
                if self.air(nx, y):
                    nxt.append(self.land(nx, y))
            headroom = self.air(x, y - 1) and self.air(x, y - 2)
            if headroom:
                for dx in range(-JUMP_ACROSS, JUMP_ACROSS + 1):
                    for dy in range(-JUMP_UP, 1):
                        c = (x + dx, y + dy)
                        if c != (x, y) and self.standing(*c):
                            nxt.append(c)
                    # a leap over a gap that ends in a fall
                    for dy in range(1, 3):
                        c = (x + dx, y + dy)
                        if self.air(*c):
                            nxt.append(self.land(*c))
            for c in nxt:
                if c is not None and c not in seen:
                    seen.add(c)
                    q.append(c)
        return seen


def check(lv):
    w, h = lv["w"], lv["h"]
    start = parse_cells(lv["start"])[0]
    origins = parse_cells(lv["origins"])
    pickups = parse_cells(lv["pickups"])
    exit_cell = parse_cells(lv.get("exit", ""))
    problems = []
    before = World(lv, grown=False)
    after = World(lv, grown=True)
    reach0 = before.reachable(start)
    reach_grown = after.reachable(start)
    # the first origin must be reachable on bare rock; later ones may need an earlier growth bridged
    # (grow, cross, plant, rewind, grow again), so they only need to be reachable once the growth exists
    first_ok = False
    for o in origins:
        if not before.standing(*o):
            problems.append(f"origin {o} is not a standing cell (air with solid below)")
            continue
        if o in reach0:
            first_ok = True
        elif o not in World(lv, grown=True, carve=o).reachable(start):
            problems.append(f"origin {o} is not reachable even with the other growth in place")
    if origins and not first_ok:
        problems.append("no origin is reachable before any growth")
    for k in pickups:
        if k not in reach0 and before.land(*k) not in reach0 and k not in reach_grown and after.land(*k) not in reach_grown:
            problems.append(f"seed {k} is not reachable")
    # somewhere safe to wait: a reachable standing cell outside the target and not adjacent to it
    safe = [c for c in reach0 if lv["target"][c[1] * w + c[0]] != "#" and lv["rock"][c[1] * w + c[0]] != "#"
            and not any(0 <= c[0] + dx < w and lv["target"][c[1] * w + c[0] + dx] == "#" for dx in (-1, 0, 1))]
    if not safe:
        problems.append("no safe standing cell outside the growth")
    if safe:
        reach1 = set()
        for c in safe[:6]:
            reach1 |= after.reachable(c)
        if exit_cell and exit_cell[0] not in reach1 and after.land(*exit_cell[0]) not in reach1:
            problems.append(f"exit {exit_cell[0]} is not reachable after growth")
    if not exit_cell:
        problems.append("no exit")
    return problems


def main():
    path = sys.argv[1] if len(sys.argv) > 1 else "unity/PointOfOrigin/Assets/StreamingAssets/levels.json"
    data = json.load(open(path, encoding="utf-8"))
    bad = 0
    for i, lv in enumerate(data["levels"]):
        problems = check(lv)
        status = "ok" if not problems else "PROBLEMS"
        print(f"{i + 1:02d} {lv['name']}: {status}")
        for p in problems:
            print("    -", p)
        bad += bool(problems)
    print(f"{len(data['levels']) - bad} of {len(data['levels'])} chapters pass")
    sys.exit(1 if bad else 0)


if __name__ == "__main__":
    main()
