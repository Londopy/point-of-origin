// origin_cli: grow a level from its origins and print the result, so the
// Nexium level compiler (tools/levels.nx) gets targets from the exact same
// simulation the game runs.
//
//     origin_cli --w 13 --h 13 --rule B1/S --steps 4 --seeds "6,6;2,3" [--rock "row;row;..."] [--ascii]
//
// Without --ascii the output is one JSON object:
//     {"w":13,"h":13,"rule":"B1/S","birth":2,"survive":0,"steps":4,"alive":24,"target":"...#..."}
// where target is row-major, '#' alive and '.' anything else.
package cli

import "core:fmt"
import "core:os"
import "core:strconv"
import "core:strings"
import sim "../sim"

USAGE :: `origin_cli --w W --h H --rule B3/S23 --steps N --seeds "x,y;x,y" [--rock "row;row"] [--ascii] [--frames]
  --seeds   origin cells, zero-based x,y pairs separated by ;
  --rock    rock rows top to bottom, '#' is rock, anything else open
  --ascii   print the grown grid as text instead of JSON
  --frames  with --ascii, print every generation`

next_arg :: proc(args: []string, i: ^int) -> string {
	if i^ + 1 < len(args) {
		i^ += 1
		return args[i^]
	}
	fmt.eprintfln("missing value after %s", args[i^])
	os.exit(2)
}

to_int :: proc(s: string) -> int {
	v, ok := strconv.parse_int(strings.trim_space(s))
	if !ok {
		fmt.eprintfln("not a number: %q", s)
		os.exit(2)
	}
	return v
}

main :: proc() {
	w, h, steps := 13, 13, 4
	rule := "B1/S012345678"
	seeds_arg, rock_arg := "", ""
	ascii, frames := false, false

	args := os.args[1:]
	for i := 0; i < len(args); i += 1 {
		switch args[i] {
		case "--w":      w = to_int(next_arg(args, &i))
		case "--h":      h = to_int(next_arg(args, &i))
		case "--steps":  steps = to_int(next_arg(args, &i))
		case "--rule":   rule = next_arg(args, &i)
		case "--seeds":  seeds_arg = next_arg(args, &i)
		case "--rock":   rock_arg = next_arg(args, &i)
		case "--ascii":  ascii = true
		case "--frames": frames = true
		case "--help", "-h":
			fmt.println(USAGE)
			return
		case:
			fmt.eprintfln("unknown argument: %s\n%s", args[i], USAGE)
			os.exit(2)
		}
	}
	if w <= 0 || h <= 0 || w > 1024 || h > 1024 || steps < 0 {
		fmt.eprintln("bad size or step count")
		os.exit(2)
	}
	birth, survive, ok := sim.parse_rule(rule)
	if !ok {
		fmt.eprintfln("bad rule %q: expected something like B3/S23", rule)
		os.exit(2)
	}

	g := sim.create(i32(w), i32(h))
	defer sim.destroy(g)
	g.birth, g.survive = birth, survive

	if rock_arg != "" {
		rows := strings.split(rock_arg, ";")
		for row, y in rows {
			if y >= h { break }
			for ch, x in row {
				if x >= w { break }
				if ch == '#' { sim.set_cell(g, i32(x), i32(y), sim.ROCK) }
			}
		}
	}
	if seeds_arg != "" {
		for part in strings.split(seeds_arg, ";") {
			p := strings.trim_space(part)
			if p == "" { continue }
			xy := strings.split(p, ",")
			if len(xy) != 2 {
				fmt.eprintfln("bad seed %q: expected x,y", part)
				os.exit(2)
			}
			x := to_int(xy[0])
			y := to_int(xy[1])
			if sim.get_cell(g, i32(x), i32(y)) != sim.ROCK {
				sim.set_cell(g, i32(x), i32(y), sim.ALIVE)
			}
		}
	}

	if ascii {
		if frames {
			fmt.printfln("generation 0")
			print_grid(g)
		}
		for s := 0; s < steps; s += 1 {
			sim.step(g)
			if frames {
				fmt.printfln("generation %d", g.generation)
				print_grid(g)
			}
		}
		if !frames { print_grid(g) }
		fmt.printfln("alive %d", sim.alive_count(g))
		return
	}

	sim.step_n(g, i32(steps))
	b := strings.builder_make()
	for v in g.cells {
		strings.write_byte(&b, '#' if v == sim.ALIVE else '.')
	}
	// Odin's fmt reads `{` as a placeholder, so the braces are doubled.
	fmt.printf(
		`{{"w":%d,"h":%d,"rule":"%s","birth":%d,"survive":%d,"steps":%d,"alive":%d,"target":"%s"}}` + "\n",
		w, h, rule, birth, survive, steps, sim.alive_count(g), strings.to_string(b),
	)
}

print_grid :: proc(g: ^sim.Grid) {
	for y: i32 = 0; y < g.h; y += 1 {
		for x: i32 = 0; x < g.w; x += 1 {
			switch sim.get_cell(g, x, y) {
			case sim.ALIVE: fmt.print("#")
			case sim.ROCK:  fmt.print("X")
			case:           fmt.print(".")
			}
		}
		fmt.println()
	}
}
