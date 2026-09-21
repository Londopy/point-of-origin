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
there. Ten chapters across four laws, from a single bloom over a chasm to a
tall cave, two echoes that cancel each other, and a mesa grown against a wall.
Early on the stone is where you can stand; later the cliffs cut the growth
short and you have to count generations back from what is left.
Mind your feet: growth overgrows whoever stands inside it, the void takes
whoever falls, and embers patrol the ruins, though a bloom that catches one
puts it out. Three lanterns per chapter. The first chapter walks you through
the loop one prompt at a time. Progress is saved, and a tip appears after a
failed attempt if you want one.

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
Jam, theme ORIGIN. Windows, 64-bit.

**Upload:** `dist/PointOfOrigin-win64.zip` (the `Build/Windows` folder without
`PointOfOrigin_BackUpThisFolder_ButDontShipItWithYourGame`), marked as a
Windows executable. Cover image: `docs/cover.png` (630x500).
