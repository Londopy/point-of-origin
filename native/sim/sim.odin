// The simulation core of Point of Origin: a bounded Life-like cellular
// automaton on a grid of dead, alive and rock cells. Rock never changes and
// never counts as a neighbour. The same package is built twice: as the DLL
// Unity calls (native/plugin) and as the command-line tool the Nexium level
// compiler calls (native/cli), so the target the tool computes is exactly the
// pattern the game grows.
package sim

import "core:strings"

DEAD  :: u8(0)
ALIVE :: u8(1)
ROCK  :: u8(2)

MAX_AGE :: u16(65535)

Grid :: struct {
	w, h:       i32,
	cells:      []u8,
	ages:       []u16, // generations a cell has been alive; 0 when dead
	next_cells: []u8,
	next_ages:  []u16,
	birth:      u32,   // bit n set: a dead cell with n live neighbours is born
	survive:    u32,   // bit n set: a live cell with n live neighbours survives
	generation: i32,
}

create :: proc(w, h: i32) -> ^Grid {
	g := new(Grid)
	g.w, g.h = w, h
	n := int(w) * int(h)
	g.cells = make([]u8, n)
	g.ages = make([]u16, n)
	g.next_cells = make([]u8, n)
	g.next_ages = make([]u16, n)
	g.birth = 1 << 3            // Conway's Life until told otherwise
	g.survive = (1 << 2) | (1 << 3)
	return g
}

destroy :: proc(g: ^Grid) {
	if g == nil { return }
	delete(g.cells)
	delete(g.ages)
	delete(g.next_cells)
	delete(g.next_ages)
	free(g)
}

// Kill every living cell, keep the rock, rewind the generation counter.
clear_life :: proc(g: ^Grid) {
	for i := 0; i < len(g.cells); i += 1 {
		if g.cells[i] == ALIVE { g.cells[i] = DEAD }
		g.ages[i] = 0
	}
	g.generation = 0
}

// Everything dead, no rock.
clear_all :: proc(g: ^Grid) {
	for i := 0; i < len(g.cells); i += 1 {
		g.cells[i] = DEAD
		g.ages[i] = 0
	}
	g.generation = 0
}

in_bounds :: proc(g: ^Grid, x, y: i32) -> bool {
	return x >= 0 && y >= 0 && x < g.w && y < g.h
}

set_cell :: proc(g: ^Grid, x, y: i32, v: u8) {
	if !in_bounds(g, x, y) { return }
	i := int(y) * int(g.w) + int(x)
	g.cells[i] = v
	g.ages[i] = 1 if v == ALIVE else 0
}

get_cell :: proc(g: ^Grid, x, y: i32) -> u8 {
	if !in_bounds(g, x, y) { return DEAD }
	return g.cells[int(y) * int(g.w) + int(x)]
}

// Replace the whole grid (row-major, w*h values) and rewind to generation 0.
load :: proc(g: ^Grid, src: []u8) {
	n := min(len(src), len(g.cells))
	for i := 0; i < n; i += 1 {
		g.cells[i] = src[i]
		g.ages[i] = 1 if src[i] == ALIVE else 0
	}
	g.generation = 0
}

neighbours :: proc(g: ^Grid, x, y: i32) -> u32 {
	count: u32 = 0
	for dy: i32 = -1; dy <= 1; dy += 1 {
		for dx: i32 = -1; dx <= 1; dx += 1 {
			if dx == 0 && dy == 0 { continue }
			nx, ny := x + dx, y + dy
			if nx < 0 || ny < 0 || nx >= g.w || ny >= g.h { continue }
			if g.cells[int(ny) * int(g.w) + int(nx)] == ALIVE { count += 1 }
		}
	}
	return count
}

step :: proc(g: ^Grid) {
	for y: i32 = 0; y < g.h; y += 1 {
		for x: i32 = 0; x < g.w; x += 1 {
			i := int(y) * int(g.w) + int(x)
			c := g.cells[i]
			if c == ROCK {
				g.next_cells[i] = ROCK
				g.next_ages[i] = 0
				continue
			}
			mask := u32(1) << neighbours(g, x, y)
			if c == ALIVE {
				if g.survive & mask != 0 {
					g.next_cells[i] = ALIVE
					g.next_ages[i] = g.ages[i] + 1 if g.ages[i] < MAX_AGE else MAX_AGE
				} else {
					g.next_cells[i] = DEAD
					g.next_ages[i] = 0
				}
			} else {
				if g.birth & mask != 0 {
					g.next_cells[i] = ALIVE
					g.next_ages[i] = 1
				} else {
					g.next_cells[i] = DEAD
					g.next_ages[i] = 0
				}
			}
		}
	}
	g.cells, g.next_cells = g.next_cells, g.cells
	g.ages, g.next_ages = g.next_ages, g.ages
	g.generation += 1
}

step_n :: proc(g: ^Grid, n: i32) {
	for _ in 0..<n { step(g) }
}

alive_count :: proc(g: ^Grid) -> i32 {
	c: i32 = 0
	for v in g.cells {
		if v == ALIVE { c += 1 }
	}
	return c
}

// Jaccard similarity of the living cells and the target's living cells:
// 1 when they are the same set (also when both are empty), 0 when disjoint.
compare :: proc(g: ^Grid, target: []u8) -> f32 {
	inter, total := 0, 0
	n := min(len(target), len(g.cells))
	for i := 0; i < n; i += 1 {
		a := g.cells[i] == ALIVE
		b := target[i] == ALIVE
		if a && b { inter += 1 }
		if a || b { total += 1 }
	}
	// living cells past the end of a short target still count against the match
	for i := n; i < len(g.cells); i += 1 {
		if g.cells[i] == ALIVE { total += 1 }
	}
	if total == 0 { return 1.0 }
	return f32(inter) / f32(total)
}

// A Life-like rulestring such as "B3/S23" or "B1/S" into birth and survival
// masks. ok is false when a character is not B, S, /, or a digit 0-8.
parse_rule :: proc(rule: string) -> (birth, survive: u32, ok: bool) {
	upper := strings.to_upper(rule, context.temp_allocator)
	mode := 0 // 0 nothing yet, 1 reading births, 2 reading survivals
	for ch in upper {
		switch ch {
		case 'B':
			mode = 1
		case 'S':
			mode = 2
		case '/', ' ':
			continue
		case '0'..='8':
			bit := u32(1) << u32(ch - '0')
			if mode == 1 {
				birth |= bit
			} else if mode == 2 {
				survive |= bit
			} else {
				return 0, 0, false
			}
		case:
			return 0, 0, false
		}
	}
	return birth, survive, mode != 0
}
