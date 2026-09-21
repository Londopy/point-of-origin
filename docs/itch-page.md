# itch.io page draft

**Title:** Point of Origin

**Tagline:** You are shown how it ended. Find where it began.

**Short description (for the jam listing):**
A platformer where the puzzle is the ground. Find where each ruin began, plant
a seed there, get clear, and grow it back: the growth is the bridge to the door.

**Description:**

Every pattern in *Point of Origin* grew from a few seeds under a simple law:
born next to exactly one neighbour, or alive only when the neighbour count is
odd, or keep everything you make. Something ended here, and you wake in the
dark among its ruins.

Your lantern shows only what is near you, so run and jump through the ruins to
find the ghost of what grew here and work out where it began. Stand on that
spot, plant the seed, get clear, and press Grow. The living cells rise as solid
ground: gold where they match the outline, red where they do not. Match it
exactly and the door opens, and the growth is the bridge that carries you
there. Ten chapters across four laws, and each asks a different question of
you: two blooms fused into one slab, three sparks whose rings merged, a coral
that grew in the shadow of a post, echoes cut short by cliffs or bent around
rock, two echoes that cancelled each other, a mesa grown against a wall.
Mind your feet: growth overgrows whoever stands inside it, the void takes
whoever falls, acid pools and spikes take the careless, vents breathe fire on
a count, some ledges crumble under you, and embers patrol the ruins (the pale
ones hunt along their row), though a bloom that catches one puts it out. Dress
your wanderer: robes, hats and lantern lights that open as you find origins. Three lanterns per chapter. The lantern
in your hand talks. The first chapter walks you through the loop, wrong turn
included. Chiptune throughout, synthesised at start-up in a different key per
chapter. Keyboard or gamepad. Progress is saved, and a tip appears after a
failed attempt if you want one. Eleven achievements, one of them hidden:
somewhere there is a place the road does not lead to, and on its wall a word
the spark law took once. Read it back and you can press your initials into
the rock for everyone who plays after you (that part goes through GitHub, so
it takes an account).

**Controls** (rebindable on the Controls page; arrows, W and Enter always work):

- A / D or arrows: run; Space: jump
- E: plant or take back a seed where you stand
- Enter: grow (again to finish instantly)
- R: rewind the growth (seeds stay planted)
- N: skip a level
- V: reveal the designer's origins after two failed attempts
- M: sound on or off, Esc: pause menu (settings, controls, credits)

**Made with:** Odin (the cellular-automaton core, as a native DLL), Nexium
(the build pipeline, the C# binding generator and the level compiler), Unity 6
(C#: the platformer, the world painter, the HUD, synthesised audio), Houdini
(the parallax ruin skylines, the simulated growth burst and the stone grain,
baked to text and replayed by the game) and Blender (the wanderer, the ember
and the seed, rendered flat-shaded). Made solo for CPGD's World's First Game
Jam, theme ORIGIN. Windows, 64-bit, or in the browser (the browser build runs
a C# copy of the Odin core, checked against it on every chapter).

**Source:** https://github.com/Londopy/point-of-origin

**Uploads:** `dist/PointOfOrigin-win64.zip`, marked as a Windows executable,
and `dist/PointOfOrigin-webgl.zip`, marked "This file will be played in the
browser", viewport 1280 x 720, fullscreen button on, mobile off. Kind of
project: HTML. Cover image: `docs/cover.png` (630x500). Trailer GIF:
`docs/loop.gif` (plant, get clear, grow, cross). Screenshots from
`docs/screenshots/`: `itch_01_tutorial`, `itch_02_fused_bloom`,
`itch_03_quench`, `itch_04_two_echoes`, `itch_05_ashfall`, `itch_06_shadow`,
`itch_07_wanderer` (the outfit page).

**itch fields:** Classification: Games. Genre: Platformer, Puzzle. Tags:
puzzle-platformer, cellular-automata, pixel-art, 2d, singleplayer, chiptune,
game-maker-not-used, odin, unity. Made with: Unity, Blender, Houdini. Average
session: about an hour. Inputs: keyboard, gamepad (any). Accessibility:
configurable controls, one-button. Multiplayer: no. Release status: released.
Pricing: no payments. Community: comments.
