# Point of Origin v1.0

Made in a weekend for the Cal Poly Game Development Club's World's First Game
Jam, theme **ORIGIN**.

You are shown how it ended. Find where it began. A side-scrolling platformer
where the puzzle is the ground: each ruin grew from a stone under a simple
law, and if you plant a seed where it began and grow it back exactly, the
growth is the bridge, the stair, the mesa that carries you to the door.

**Ten chapters, four laws.** Blooms, sparks, corals and echoes. The first
chapter walks you through the loop; from the fifth on, the cliffs cut the
growth short and you count generations back from what is left.

**It can go wrong.** Growth overgrows whoever stands inside it. The void takes
whoever falls. Embers burn, though a bloom that reaches one puts it out.
Three lanterns per chapter.

**Under the hood.** The cellular automaton is written in Odin and shipped as
a native DLL; the level compiler and the C# binding generator are written in
Nexium; the game is Unity 6; the ruin skylines, the growth burst and the
stone grain come out of Houdini; the wanderer, the ember and the seed out of
Blender. All sound is synthesised at start-up.

**Controls.** A/D run, Space jump, E plant, Enter grow, R rewind, N skip,
V reveal (after two failed tries), M sound, Esc menu. Every key except Esc
can be rebound on the Controls page.

Windows, 64-bit. Unzip, run `PointOfOrigin.exe`. Progress and settings are
saved between runs.
