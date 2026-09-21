# Point of Origin v1.0

Made in a weekend for the Cal Poly Game Development Club's World's First Game
Jam, theme **ORIGIN**.

You are shown how it ended. Find where it began. A side-scrolling platformer
where the puzzle is the ground: each ruin grew from a seed under a simple
law, and if you plant a seed where it began and grow it back exactly, the
growth is the bridge, the stair, the mesa that carries you to the door.

**Ten chapters, four laws.** Blooms, sparks, corals and echoes, and each
chapter asks a different question: two blooms fused into one slab, three
sparks whose rings merged, a coral in the shadow of a post, echoes cut short
by cliffs or bent around rock, two echoes that cancelled each other, a mesa
grown against a wall. The first chapter walks you through the loop, wrong
turn included.

**It can go wrong.** Growth overgrows whoever stands inside it. The void takes
whoever falls. Acid pools and spikes take the careless, vents breathe fire on
a count, some ledges crumble under you, embers patrol the ruins and the pale
ones hunt along their row, though a bloom that reaches an ember puts it out.
Three lanterns per chapter, and the chapter select remembers how many each
one took from you.

**Company.** The lantern in your hand talks. Chiptune throughout, synthesised
at start-up in a different key per chapter. Eleven achievements, one of them
hidden: somewhere there is a place the road does not lead to, and on its
wall a word the spark law took once. Read it back and you can press your
initials into the rock for everyone who plays after you. Dress your wanderer
on the Wanderer page: robes, hats and lantern lights that open as you find
origins.

**Under the hood.** The cellular automaton is written in Odin and shipped as
a native DLL on Windows; the browser build runs a C# copy checked against it
on every chapter. The level compiler and the C# binding generator are
written in Nexium; the game is Unity 6; the ruin skylines, the growth burst,
the stone grain and the far plateau come out of Houdini; the wanderer, the
ember and the seed out of Blender. All sound is synthesised at start-up.

**Controls.** A/D run, Space jump, E plant, Enter grow, R rewind, N skip,
V reveal (after two failed tries), M sound, Esc menu. Every key except Esc
can be rebound on the Controls page. Gamepads work: stick or d-pad, A jump,
X plant, B grow, Y rewind, LB reveal, RB skip, Start menu.

**Downloads.** `PointOfOrigin-win64.zip`: Windows, 64-bit; unzip and run
`PointOfOrigin.exe`. `PointOfOrigin-webgl.zip`: the browser build, as
uploaded to itch. Progress and settings are saved between runs.
