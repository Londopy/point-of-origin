// The C ABI Unity calls through P/Invoke. Built with
//     odin build native/plugin -build-mode:dll -out:nx-out/native/origin_sim.dll
// tools/bindgen.nx reads this file and writes the matching C# declarations, so
// keep every export as `@(export)` on one line and the signature on the next.
package plugin

import "base:runtime"
import sim "../sim"

VERSION :: 1

@(export)
po_version :: proc "c" () -> i32 {
	return VERSION
}

@(export)
po_create :: proc "c" (w, h: i32) -> rawptr {
	context = runtime.default_context()
	if w <= 0 || h <= 0 || w > 1024 || h > 1024 { return nil }
	return rawptr(sim.create(w, h))
}

@(export)
po_destroy :: proc "c" (g: rawptr) {
	context = runtime.default_context()
	if g == nil { return }
	sim.destroy((^sim.Grid)(g))
}

@(export)
po_width :: proc "c" (g: rawptr) -> i32 {
	if g == nil { return 0 }
	return (^sim.Grid)(g).w
}

@(export)
po_height :: proc "c" (g: rawptr) -> i32 {
	if g == nil { return 0 }
	return (^sim.Grid)(g).h
}

@(export)
po_set_rule :: proc "c" (g: rawptr, birth, survive: u32) {
	if g == nil { return }
	gr := (^sim.Grid)(g)
	gr.birth = birth
	gr.survive = survive
}

@(export)
po_clear_life :: proc "c" (g: rawptr) {
	context = runtime.default_context()
	if g == nil { return }
	sim.clear_life((^sim.Grid)(g))
}

@(export)
po_clear_all :: proc "c" (g: rawptr) {
	context = runtime.default_context()
	if g == nil { return }
	sim.clear_all((^sim.Grid)(g))
}

@(export)
po_set_cell :: proc "c" (g: rawptr, x, y: i32, v: u8) {
	context = runtime.default_context()
	if g == nil { return }
	sim.set_cell((^sim.Grid)(g), x, y, v)
}

@(export)
po_get_cell :: proc "c" (g: rawptr, x, y: i32) -> u8 {
	context = runtime.default_context()
	if g == nil { return 0 }
	return sim.get_cell((^sim.Grid)(g), x, y)
}

@(export)
po_load :: proc "c" (g: rawptr, cells: [^]u8, count: i32) {
	context = runtime.default_context()
	if g == nil || cells == nil || count <= 0 { return }
	sim.load((^sim.Grid)(g), cells[:count])
}

@(export)
po_copy_cells :: proc "c" (g: rawptr, dst: [^]u8, count: i32) -> i32 {
	if g == nil || dst == nil || count <= 0 { return 0 }
	gr := (^sim.Grid)(g)
	n := min(int(count), len(gr.cells))
	copy(dst[:n], gr.cells[:n])
	return i32(n)
}

@(export)
po_copy_ages :: proc "c" (g: rawptr, dst: [^]u16, count: i32) -> i32 {
	if g == nil || dst == nil || count <= 0 { return 0 }
	gr := (^sim.Grid)(g)
	n := min(int(count), len(gr.ages))
	copy(dst[:n], gr.ages[:n])
	return i32(n)
}

@(export)
po_step :: proc "c" (g: rawptr, n: i32) -> i32 {
	context = runtime.default_context()
	if g == nil { return 0 }
	gr := (^sim.Grid)(g)
	sim.step_n(gr, n)
	return gr.generation
}

@(export)
po_generation :: proc "c" (g: rawptr) -> i32 {
	if g == nil { return 0 }
	return (^sim.Grid)(g).generation
}

@(export)
po_alive_count :: proc "c" (g: rawptr) -> i32 {
	context = runtime.default_context()
	if g == nil { return 0 }
	return sim.alive_count((^sim.Grid)(g))
}

@(export)
po_compare :: proc "c" (g: rawptr, target: [^]u8, count: i32) -> f32 {
	context = runtime.default_context()
	if g == nil || target == nil || count <= 0 { return 0 }
	return sim.compare((^sim.Grid)(g), target[:count])
}
