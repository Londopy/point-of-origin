# itch.io page draft

**Title:** Point of Origin

**Tagline:** You are shown how it ended. Find where it began.

**Short description (for the jam listing):**
A reverse cellular-automaton puzzle. Every level shows the aftermath of a simple
growth law. Plant the seeds, press Grow, and match the ghost outline exactly to
find the origin.

**Description:**

Every pattern in *Point of Origin* grew from one or two seeds under a simple
law: born next to exactly one neighbour, or alive only when the neighbour count
is odd, or keep everything you make. You see the result. You get the seeds.
Where did it start?

Plant your seeds on the grid, press Grow, and watch the generations unfold. Gold
where your growth matches the outline, red where it does not. Match it exactly
and you have found the point of origin. Ten levels across four laws, from a
single ripple to two springs on either side of a river.

**Controls:**

- Left click: plant or remove a seed
- Space: grow (again to finish instantly, again for the next level)
- R: rewind to your seeds
- N: skip a level
- V: reveal the designer's origins after two failed attempts

**Made with:** Odin (simulation core, as a native DLL), Nexium (build pipeline,
binding generator and level compiler), Unity 6 (C#). Made solo for CPGD's
World's First Game Jam, theme ORIGIN. Windows, 64-bit.

**Upload:** zip the `Build/Windows` folder produced by the Unity build
(`PointOfOrigin.exe`, `PointOfOrigin_Data`, `MonoBleedingEdge`, `UnityPlayer.dll`,
`UnityCrashHandler64.exe`) and mark it as a Windows executable.
