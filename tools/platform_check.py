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
import os
import subprocess
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


CLI = None


def find_cli():
    global CLI
    if CLI is not None:
        return CLI
    here = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    for name in ("origin_cli.exe", "origin_cli"):
        p = os.path.join(here, "tools", "bin", name)
        if os.path.exists(p):
            CLI = p
            return p
    CLI = ""
    return ""


_footprints = {}


def solo_footprints(lv):
    """What each origin grows on its own (rock in place, no other seed): the growth that exists when the
    player has grown the earlier stones and is on the way to plant this one. Needs the Odin CLI; without it,
    falls back to the final target minus a 3x3 around the origin (coarser, and wrong for enclosed stones)."""
    key = lv["name"]
    if key in _footprints:
        return _footprints[key]
    w, h = lv["w"], lv["h"]
    origins = parse_cells(lv["origins"])
    rock_rows = ";".join(lv["rock"][y * w:(y + 1) * w] for y in range(h))
    result = []
    cli = find_cli()
    for o in origins:
        cells = set()
        if cli:
            out = subprocess.run([cli, "--w", str(w), "--h", str(h), "--rule", lv["rule"], "--steps", str(lv["steps"]),
                                  "--seeds", f"{o[0]},{o[1]}", "--rock", rock_rows, "--ascii"], capture_output=True, text=True).stdout
            rows = [l for l in out.splitlines() if l and not l.startswith("alive")]
            if len(rows) == h:
                cells = {(x, y) for y, row in enumerate(rows) for x, ch in enumerate(row) if ch == "#"}
        if not cells:
            cells = {(x, y) for y in range(h) for x in range(w) if lv["target"][y * w + x] == "#"
                     and max(abs(x - o[0]), abs(y - o[1])) > 1}
        result.append(cells)
    _footprints[key] = result
    return result


class World:
    def __init__(self, lv, grown, carve=None):
        """grown=False: bare rock. grown=True: the final target is solid. grown=True with carve=origin:
        only the growth of the other origins is solid (each grown alone), this one's is not there yet."""
        self.w, self.h = lv["w"], lv["h"]
        self.rock = lv["rock"]
        self.grown = grown
        self.target = lv["target"]
        self.cells = None
        # hazards: acid and spikes kill on touch and are never solid; a spout's two flame cells are
        # passable between bursts (timing is the player's problem); crumble is ordinary rock here
        self.deadly = set(parse_cells(lv.get("acid", ""))) | set(parse_cells(lv.get("spikes", "")))
        self.not_solid = set(parse_cells(lv.get("acid", "")))
        if grown and carve is not None:
            origins = parse_cells(lv["origins"])
            prints = solo_footprints(lv)
            self.cells = set()
            for o, cells in zip(origins, prints):
                if tuple(o) != tuple(carve):
                    self.cells |= cells

    def solid(self, x, y):
        if x < 0 or x >= self.w:
            return True
        if y < 0 or y >= self.h:
            return False
        i = y * self.w + x
        if (x, y) in self.not_solid:
            return False
        if self.rock[i] == "#":
            return True
        if not self.grown:
            return False
        if self.cells is not None:
            return (x, y) in self.cells
        return self.target[i] == "#"

    def air(self, x, y):
        return 0 <= x < self.w and 0 <= y < self.h and not self.solid(x, y) and (x, y) not in self.deadly

    def standing(self, x, y):
        return self.air(x, y) and self.solid(x, y + 1) and (x, y) not in self.blocked

    blocked = frozenset()   # cells the wanderer may not stand in: used to ask "can you get here without stepping on that seed?"

    def land(self, x, y):
        """Fall from an air cell to the first standing cell below, or None into the void or a hazard."""
        while y < self.h:
            if (x, y) in self.deadly:
                return None
            if self.standing(x, y):
                return (x, y)
            if not self.air(x, y):
                return None
            y += 1
        return None

    def jumps_from(self, x, y):
        """The jump edges the coarse model allows from a standing cell."""
        out = []
        if not (self.air(x, y - 1) and self.air(x, y - 2)):
            return out
        for dx in range(-JUMP_ACROSS, JUMP_ACROSS + 1):
            for dy in range(-JUMP_UP, 1):
                c = (x + dx, y + dy)
                if c != (x, y) and self.standing(*c):
                    out.append(c)
            # a leap over a gap that ends in a fall
            for dy in range(1, 3):
                c = (x + dx, y + dy)
                if self.air(*c):
                    landed = self.land(*c)
                    if landed is not None:
                        out.append(landed)
        return out

    def reachable(self, start, parents=None, verified=None):
        """Standing cells reachable from start. With a dict, records each cell's (parent, kind):
        kind is 'walk' for a step or a drop, 'jump' for anything the jump model allowed.
        With a set of verified (from, to) pairs, only those jumps count (a physics replay's verdict)."""
        seen = set()
        q = deque()
        s = self.land(*start) if not self.standing(*start) else start
        if s is None:
            return seen
        q.append(s)
        seen.add(s)
        if parents is not None:
            parents[s] = (None, "start")
        while q:
            x, y = q.popleft()
            nxt = []
            for dx in (-1, 1):
                nx = x + dx
                if self.air(nx, y):
                    nxt.append((self.land(nx, y), "walk"))
            for c in self.jumps_from(x, y):
                if verified is None or ((x, y), c) in verified:
                    nxt.append((c, "jump"))
            for c, kind in nxt:
                if c is not None and c not in seen:
                    seen.add(c)
                    if parents is not None:
                        parents[c] = ((x, y), kind)
                    q.append(c)
        return seen


def path_edges(parents, goal):
    """The jump edges along the recorded path back from goal: [(from, to)], walking left out."""
    edges = []
    c = goal
    while c in parents and parents[c][0] is not None:
        p, kind = parents[c]
        if kind == "jump":
            edges.append((p, c))
        c = p
    edges.reverse()
    return edges


def world_key(grown, carve):
    return f"{'grown' if grown else 'bare'}" + (f"@{carve[0]},{carve[1]}" if carve else "")


def candidate_edges(lv):
    """Every jump the coarse model could use, per world, for a physics replay: the bare world, the grown
    world, and for each origin the grown world with that origin carved out (only jumps near it, the rest
    is the grown world's)."""
    start = parse_cells(lv["start"])[0]
    origins = parse_cells(lv["origins"])
    out = []
    worlds = [(False, None), (True, None)] + [(True, o) for o in origins]
    bare_cells = World(lv, grown=False).reachable(start)
    for grown, carve in worlds:
        world = World(lv, grown=grown, carve=carve)
        cells = set(world.reachable(start))
        if grown and carve is None:
            # the wanderer may already be standing anywhere bare rock allowed when the growth rises
            cells |= {c for c in bare_cells if world.standing(*c)}
        for (x, y) in cells:
            for c in world.jumps_from(x, y):
                if carve and max(abs(x - carve[0]), abs(y - carve[1]), abs(c[0] - carve[0]), abs(c[1] - carve[1])) > 6:
                    continue
                out.append({"grown": grown, "carve": list(carve) if carve else None, "from": [x, y], "to": list(c)})
    return out


def verified_for(verdicts, lv_index, grown, carve):
    """The set of physics-verified jumps for one world: a carved world falls back to the grown world's."""
    ok = set()
    for v in verdicts:
        if v["level"] != lv_index or v["grown"] != grown or not v["ok"]:
            continue
        if v["carve"] is None or (carve is not None and tuple(v["carve"]) == tuple(carve)):
            ok.add((tuple(v["from"]), tuple(v["to"])))
    return ok


def check(lv, edges_out=None, verdicts=None, lv_index=0):
    w, h = lv["w"], lv["h"]
    start = parse_cells(lv["start"])[0]
    origins = parse_cells(lv["origins"])
    pickups = parse_cells(lv["pickups"])
    exit_cell = parse_cells(lv.get("exit", ""))
    problems = []
    before = World(lv, grown=False)
    after = World(lv, grown=True)
    v0 = verified_for(verdicts, lv_index, False, None) if verdicts is not None else None
    v1 = verified_for(verdicts, lv_index, True, None) if verdicts is not None else None
    parents0 = {}
    reach0 = before.reachable(start, parents0, v0)
    parents_grown = {}
    reach_grown = after.reachable(start, parents_grown, v1)
    if edges_out is not None:
        # the jump edges the paths rely on, for a physics replay: bare-rock paths to every origin and seed
        # reachable that way, grown paths (the other origins' growth in place, this one's carved out) to the
        # origins that need them, and the grown path to the exit
        for g in [o for o in origins if o in reach0] + [k for k in pickups if k in reach0]:
            for e in path_edges(parents0, g):
                edges_out.append({"grown": False, "carve": None, "from": list(e[0]), "to": list(e[1]), "goal": list(g)})
        for o in origins:
            if o in reach0:
                continue
            parents_c = {}
            carved = World(lv, grown=True, carve=o)
            if o in carved.reachable(start, parents_c):
                for e in path_edges(parents_c, o):
                    edges_out.append({"grown": True, "carve": list(o), "from": list(e[0]), "to": list(e[1]), "goal": list(o)})
        for k in pickups:
            if k in reach0 or before.land(*k) in reach0:
                continue
            target = k if k in reach_grown else after.land(*k)
            if target in reach_grown:
                for e in path_edges(parents_grown, target):
                    edges_out.append({"grown": True, "carve": None, "from": list(e[0]), "to": list(e[1]), "goal": list(k)})
        for c in exit_cell:
            target = c if c in reach_grown else after.land(*c)
            if target in reach_grown:
                for e in path_edges(parents_grown, target):
                    edges_out.append({"grown": True, "carve": None, "from": list(e[0]), "to": list(e[1]), "goal": list(c)})
    # the first origin must be reachable on bare rock; later ones may need an earlier growth bridged
    # (grow, cross, plant, rewind, grow again), so they only need to be reachable once the growth exists
    first_ok = False
    for o in origins:
        if not before.standing(*o):
            problems.append(f"origin {o} is not a standing cell (air with solid below)")
            continue
        if o in reach0:
            first_ok = True
        else:
            vc = verified_for(verdicts, lv_index, True, o) if verdicts is not None else None
            if o not in World(lv, grown=True, carve=o).reachable(start, None, vc):
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
            reach1 |= after.reachable(c, None, v1)
        if exit_cell and exit_cell[0] not in reach1 and after.land(*exit_cell[0]) not in reach1:
            problems.append(f"exit {exit_cell[0]} is not reachable after growth")
    # no pockets: once the growth stands, the door must be reachable from every safe cell the wanderer could
    # have waited in (cells inside or beside the shape are their own lookout: a rewind frees them)
    if exit_cell:
        goal = exit_cell[0]
        starts = sorted(c for c in safe if after.standing(*c))
        good, dead = set(), set()
        for c in starts:
            if c in good or c in dead:
                continue
            r = after.reachable(c, None, v1)
            if goal in r or after.land(*goal) in r:
                good |= r
                good.add(c)
            else:
                dead |= r
                dead.add(c)
        stuck = sorted(dead & set(starts))
        if stuck:
            problems.append(f"{len(stuck)} standing cells are pockets after the growth (no way on to the door), e.g. {stuck[:4]}")
    # and before growing: from every cell reachable on bare rock, every origin that bare rock reaches must
    # still be reachable, and so must every seed that could have been passed by
    bare_origins = [o for o in origins if o in reach0]
    bare_seeds = [k for k in pickups if k in reach0]
    for goal, what in [(o, "origin") for o in bare_origins] + [(k, "seed") for k in bare_seeds]:
        if what == "seed":
            without = World(lv, grown=False)
            without.blocked = frozenset([goal])
            starts = sorted(without.reachable(start, None, v0))
        else:
            starts = sorted(reach0)
        good, dead = set(), set()
        for c in starts:
            if c in good or c in dead:
                continue
            r = before.reachable(c, None, v0)
            if goal in r:
                good |= r
                good.add(c)
            else:
                dead |= r
                dead.add(c)
        stuck = sorted(dead & set(starts))
        if stuck:
            problems.append(f"{what} {goal} cannot be reached from {len(stuck)} standing cells the wanderer can get to first, e.g. {stuck[:4]}")
    if not exit_cell:
        problems.append("no exit")
    # a secret, if the level has one, must be reachable on bare rock (it is off the road, not behind the growth)
    secret = parse_cells(lv.get("secret", ""))
    if secret and secret[0] not in reach0 and before.land(*secret[0]) not in reach0:
        problems.append(f"secret {secret[0]} is not reachable")
    return problems


def main():
    """
    python tools/platform_check.py [levels.json]                      coarse check
    python tools/platform_check.py --candidates=nx-out/candidates.json   also write every jump the model may use
    python tools/platform_check.py --verdicts=nx-out/verdicts.json       only count jumps the Editor replay confirmed
    """
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    path = args[0] if args else "unity/PointOfOrigin/Assets/StreamingAssets/levels.json"
    edges_path = verdicts_path = None
    for a in sys.argv[1:]:
        if a.startswith("--candidates="):
            edges_path = a[len("--candidates="):]
        if a.startswith("--verdicts="):
            verdicts_path = a[len("--verdicts="):]
    data = json.load(open(path, encoding="utf-8"))
    verdicts = json.load(open(verdicts_path, encoding="utf-8")) if verdicts_path else None
    bad = 0
    all_edges = []
    for i, lv in enumerate(data["levels"]):
        problems = check(lv, None, verdicts, i + 1)
        status = "ok" if not problems else "PROBLEMS"
        extra = ""
        if edges_path:
            edges = candidate_edges(lv)
            seen = set()
            for e in edges:
                key = (e["grown"], tuple(e["carve"] or ()), tuple(e["from"]), tuple(e["to"]))
                if key in seen:
                    continue
                seen.add(key)
                all_edges.append({"level": i + 1, **e})
            extra = f"  ({len(seen)} candidate jumps)"
        if verdicts is not None:
            n = sum(1 for v in verdicts if v["level"] == i + 1)
            k = sum(1 for v in verdicts if v["level"] == i + 1 and v["ok"])
            extra = f"  (physics: {k} of {n} candidate jumps confirmed)"
        print(f"{i + 1:02d} {lv['name']}: {status}{extra}")
        for p in problems:
            print("    -", p)
        bad += bool(problems)
    if edges_path:
        json.dump(all_edges, open(edges_path, "w", encoding="utf-8"))
        print(f"{len(all_edges)} candidate jumps written to {edges_path}")
    print(f"{len(data['levels']) - bad} of {len(data['levels'])} chapters pass" + (" with physics-verified jumps" if verdicts is not None else ""))
    sys.exit(1 if bad else 0)


if __name__ == "__main__":
    main()
