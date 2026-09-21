// Tests for the simulation core. Run with:  odin test native/sim
package sim

import "core:testing"

@(test)
bloom_grows_a_square :: proc(t: ^testing.T) {
	g := create(9, 9)
	defer destroy(g)
	b, s, ok := parse_rule("B1234/S012345678")
	testing.expect(t, ok, "the bloom rule parses")
	g.birth, g.survive = b, s
	set_cell(g, 4, 4, ALIVE)
	step(g)
	testing.expect_value(t, alive_count(g), i32(9))
	step(g)
	testing.expect_value(t, alive_count(g), i32(25))
	testing.expect_value(t, g.generation, i32(2))
}

@(test)
spark_leaves_a_ring :: proc(t: ^testing.T) {
	g := create(7, 7)
	defer destroy(g)
	g.birth, g.survive, _ = parse_rule("B1/S")
	set_cell(g, 3, 3, ALIVE)
	step(g)
	testing.expect_value(t, alive_count(g), i32(8))
	testing.expect_value(t, get_cell(g, 3, 3), DEAD)
	testing.expect_value(t, get_cell(g, 2, 2), ALIVE)
}

@(test)
rock_blocks_growth :: proc(t: ^testing.T) {
	g := create(9, 5)
	defer destroy(g)
	g.birth, g.survive, _ = parse_rule("B1234/S012345678")
	for y: i32 = 0; y < 5; y += 1 { set_cell(g, 5, y, ROCK) }
	set_cell(g, 3, 2, ALIVE)
	step_n(g, 3)
	for y: i32 = 0; y < 5; y += 1 {
		testing.expect_value(t, get_cell(g, 5, y), ROCK)
		for x: i32 = 6; x < 9; x += 1 {
			testing.expect_value(t, get_cell(g, x, y), DEAD)
		}
	}
	// the bloom on the open side reached the wall
	testing.expect_value(t, get_cell(g, 4, 2), ALIVE)
}

@(test)
ages_count_generations_alive :: proc(t: ^testing.T) {
	g := create(5, 5)
	defer destroy(g)
	g.birth, g.survive, _ = parse_rule("B1234/S012345678")
	set_cell(g, 2, 2, ALIVE)
	step_n(g, 2)
	testing.expect_value(t, g.ages[2 * 5 + 2], u16(3))   // planted, then survived twice
	testing.expect_value(t, g.ages[1 * 5 + 2], u16(2))   // born in the first generation
	testing.expect_value(t, g.ages[0 * 5 + 2], u16(1))   // born in the second
	clear_life(g)
	testing.expect_value(t, alive_count(g), i32(0))
	testing.expect_value(t, g.generation, i32(0))
}

@(test)
compare_is_jaccard :: proc(t: ^testing.T) {
	g := create(5, 5)
	defer destroy(g)
	g.birth, g.survive, _ = parse_rule("B1234/S012345678")
	set_cell(g, 2, 2, ALIVE)
	step(g)
	target := make([]u8, 25)
	defer delete(target)
	copy(target, g.cells)
	testing.expect_value(t, compare(g, target), f32(1))
	set_cell(g, 0, 0, ALIVE)                  // one stray cell: 9 shared of 10
	testing.expect_value(t, compare(g, target), f32(0.9))
	set_cell(g, 0, 0, DEAD)
	set_cell(g, 1, 1, DEAD)                   // one missing: 8 shared of 9
	testing.expect(t, abs(compare(g, target) - 8.0 / 9.0) < 1e-6, "one missing cell")
}

@(test)
rules_parse :: proc(t: ^testing.T) {
	b, s, ok := parse_rule("B3/S23")
	testing.expect(t, ok, "Life parses")
	testing.expect_value(t, b, u32(1 << 3))
	testing.expect_value(t, s, u32((1 << 2) | (1 << 3)))
	b, s, ok = parse_rule("b1357/s1357")
	testing.expect(t, ok, "lower case parses")
	testing.expect_value(t, b, u32((1 << 1) | (1 << 3) | (1 << 5) | (1 << 7)))
	_, _, ok = parse_rule("B9/S")
	testing.expect(t, !ok, "a digit above 8 is rejected")
	_, _, ok = parse_rule("hello")
	testing.expect(t, !ok, "garbage is rejected")
}
