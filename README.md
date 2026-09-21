# Point of Origin

*You are shown how it ended. Find where it began.*

A side-scrolling platformer built on a reverse cellular-automaton puzzle,
made for the Cal Poly Game Development Club's **World's First Game Jam**
(September 2026, theme **ORIGIN**). You wake in the dark among the ruins of
something that grew. Your lantern shows the ghost of it only around you, so
you run and jump through the ruins to learn the full shape of what a simple
growth law left behind. Find the point it grew from, stand there, plant a
seed, get clear, and press Grow: the living cells rise as solid ground, gold
where they match the outline, red where they do not, and when the growth is
exact the door opens and the growth is the bridge that carries you to it.

It can go wrong. Living growth overgrows whoever stands inside it, so move
away from your seed before you grow, or run. The void below the world takes
anyone who falls. Embers drift along rows and columns in the later chapters
and burn what they touch. You carry three lanterns per chapter; lose them all
and the dark takes you, then you try again.

![First Bloom, the growth rising](docs/screenshots/03_growing.png)

## Three languages, one game

| Part | Language | Where | Why |
| --- | --- | --- | --- |
| Simulation core | [Odin](https://odin-lang.org) | `native/sim`, `native/plugin`, `native/cli` | A bounded Life-like automaton with rock cells and per-cell age, built once as `origin_sim.dll` for Unity and once as `origin_cli.exe` for the tools. One implementation, so a level's target is exactly what the game grows. |
| Tooling | [Nexium](https://github.com/Londopy/nexium) | `build.nx`, `tools/bindgen.nx`, `tools/levels.nx` | `bindgen` reads the `@(export)` procs in the Odin plugin and writes the C# `DllImport` surface. `levels` compiles the `.origin` level files into `levels.json`, growing each target through the Odin CLI. `build.nx` runs the whole pipeline. |
| Game | C# / Unity 6 (6000.6) | `unity/PointOfOrigin` | The isometric world (one vertex-coloured mesh rebuilt each frame by `WorldView`), the wanderer, input, HUD and synthesised audio. The scene holds only the template camera and light; everything else is built in code at load. |
| Props | Houdini 22 | `houdini/diorama_base.hipnc` | The stone slab under every level: a subdivided box, noise on its sides, faceted, with flat shading baked into vertex colours in VEX. Apprentice cannot export FBX, so it is written as a plain triangle list (`Assets/Resources/Models/DioramaBase.txt`) that `MeshText` loads. |

```
levels/*.origin ──► tools/levels.nx ──(origin_cli.exe)──► Assets/StreamingAssets/levels.json
native/plugin/plugin.odin ──► tools/bindgen.nx ──► Assets/Scripts/Native/PoNative.cs
native/plugin ──► odin build -build-mode:dll ──► Assets/Plugins/x86_64/origin_sim.dll
```

## Build

Requirements: Odin (nightly), Nexium 1.0+, Unity 6000.6 with the Windows
module, the `unity` CLI (optional, for driving the Editor).

```bash
nx run build.nx                 # DLL + CLI, bindings, levels
nx run build.nx -- --skip-odin  # just bindings and levels
```

Then open `unity/PointOfOrigin` and press Play, or build a Windows player:

```bash
unity build unity/PointOfOrigin --editor-version 6000.6.0f1 --target StandaloneWindows64 \
  --execute-method PointOfOrigin.EditorTools.Builder.PerformBuild
```

The Editor keeps a native plugin loaded until it exits, so if the DLL copy
fails, close Unity and run the build again.

## Controls

| Input | Action |
| --- | --- |
| A / D or arrows | Run |
| Space, W or Up | Jump (hold for height, let go early for a hop) |
| E | Plant or take back a seed in the cell you stand in (planting past the limit replaces the oldest) |
| Enter / G | Grow; while growing, finish instantly |
| R | Rewind the growth (planted seeds stay) |
| N | Skip the level |
| V | Reveal the designer's origins (after two failed attempts) |
| M | Sound on or off |
| Esc | Back to the menu; quit from the menu (player build) |

Progress is saved between runs; the menu has a chapter select and replays the
last solved chapter's origins growing. `tools/platform_check.py` checks every
chapter for reachability (stone, seeds, a safe place to stand, the door once
grown) with a coarse jump model; run it after editing a level.

## Levels

Levels are plain text in `levels/`, side view, row 0 at the top. `#` is rock,
`o` an origin, `s` where the wanderer wakes, `x` the door, `k` a seed lying in
the world (when a level has any, the wanderer starts empty-handed and must
fetch them), `e` an ember that patrols its row and `E` one that patrols its
column, and the tool grows the target for you. `intro` and `outro` lines carry
the story:

```
name Ember
rule B1/S
steps 3
hint Rock does not grow and does not count.
map
...............
........#......
.....o..#......
........#......
...............
end
```

Rules are Life-like rulestrings (`B3/S23`). The twenty jam levels use four
laws: `B1/S012345678` (ripples that keep everything), `B1/S` (sparks that live
one generation), `B1357/S1357` (a parity law whose echoes cancel like waves)
and `B1/S12345678` (coral). An optional `tip` line is shown after the first
failed attempt.

The level compiler also asks the CLI to count how many seed placements grow
the same target (`origin_cli --solve`, a search over the open cells inside the
target's bounding box), so ambiguous levels show up at build time. Eighteen of
the twenty levels have exactly one solution; the two three-seed finales have
too many placements to count.
