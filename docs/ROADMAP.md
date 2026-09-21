# Roadmap to submission

Jam ends **Monday 21 September, 7:00 PM Pacific** (itch shows 22 Sept 02:00 UTC).
We submit by **5:00 PM Monday** and keep the last two hours as a buffer.
Every block ends with a build that could be uploaded as-is.

The game pivoted on Sunday evening from the isometric diorama to a **2D
side-scrolling platformer**: growth is solid ground, the door opens when the
growth is exact, the void, embers and overgrowth kill, three lanterns per
chapter. Ten chapters exist and pass the reachability check. Everything below
assumes that shape; the diorama is in git history if we ever want it back.

Update this file as we go: tick boxes, move things, cut things. Cutting is
allowed; shipping late is not.

## Sunday night

- [x] 8:00 to 10:30 pm: **the pivot**. Tile platformer controller (run, jump,
      coyote time, variable jump), growth as terrain, doors, six side-view
      chapters, a reachability checker (`tools/platform_check.py`), scripted
      physics tests, build.
- [x] **assets pass**: Houdini parallax skylines and a baked growth burst,
      a Blender-rendered wanderer, all replayed from text and PNGs in Resources.
- [x] **menu pass**: pause menu, Settings (volumes, sound, shake, fullscreen,
      progress reset), Controls with rebinding, Credits; the world pauses
      behind them. Blender sprite colours fixed (Standard view transform),
      ember and seed sprites, Houdini stone grain on the rock.
- [x] **four more chapters** (ten now): Quench, Ashfall (vertical), Two
      Echoes, Origin. Growth quenches embers and embers bounce off growth.
      All ten pass the checker and solve from the designer's origins.
- [x] 10:30 to 11:30 pm: **first hand playthrough (Londo)**. Verdict: fun,
      needs a tutorial and longer levels.
- [x] **tutorial + longer chapters**: chapter 1 is a guided run (prompts keyed
      to what you did, a beacon on the stone), How to Play page in the menus;
      every chapter got a platforming approach and a run after the growth
      (dips, fenced embers, seeds on ledges), Ashfall got a second stone.
      Maps 56 to 100 wide, all pass the checker, one solution each.
- [x] **harder origins + backdrop** (Londo: "finding the origin was too easy,
      the middle or the only place to stand"): chapters 5 to 10 now put each
      stone on the floor of a wide trench at a cliff, the cliff clips the
      growth (count generations from the open edge), Two Echoes cancels in the
      middle, Origin's mesa is cut by a wall. Fossils of older growths pressed
      into the rock (pale imprints, sediment rim) and drifting spores.
- [x] commit, build, zip, stop. Sleep is part of the plan.

- [x] (Londo) social preview uploaded.
- [ ] (Londo) delete `point-of-origin-old` when ready.

## Monday morning

- [x] (Sunday 11:30 PM to 12:30 AM) **physics-verified reachability**: every
      jump the checker could use replayed with the real physics inside the
      Editor (`tools/editor/verify_jumps.cs`), then the check re-run on the
      confirmed jumps only. Found chapter 5 unclimbable (a parity echo grows a
      three-tall column three from its stone); trenches there are now eight
      wide with the stone two from the cliff, and all ten chapters pass.
      Hints proofread against the new geometry.
- [x] (Monday 12:20 to 12:40 AM) **the second-playthrough pass**
      (Londo: fun, too easy, repetitive maps, origins too similar, not enough
      ways to die, tutorial does not explain completing the origin, rewind,
      E): hazards (acid, spikes, fire spouts, crumbling rock) as map glyphs;
      the tutorial makes you plant wrong first, see red, rewind, take back,
      reason, plant right; ten chapters rebuilt around six distinct origin
      deductions (fused slab, three sparks, post shadow, cut echo, bent echo,
      cancelling pair) with the roads between them; the lantern's voice
      (per-chapter lines on wake, seed, plant, grow, found, die); a 16-bit
      tracker tune (pulse, FM bell, triangle bass, lo-fi) in a key per chapter
      plus hazard sounds; gamepad support with d-pad menus; Nexium: hazard
      glyphs, `say` lines, a deduction report per chapter, `tools/chapters.nx`.
- [x] (Monday 1:15 to 1:45 AM) **achievements and the easter egg**: eleven
      achievements with toasts and a menu page, saved in PlayerPrefs; a hidden
      route in Two Echoes (rock mass over the shelf, a chimney on alternating
      blocks, a tunnel with spikes and a trapped ember, a chamber with the
      first seed) that unlocks the hidden one, gives the lantern a line and a
      new light. The level format has `?` and `say secret|`; the checker
      requires the secret to be reachable. A signature fossil in chapter 2.
- [x] (Monday, done 1:50 AM) **the wall of initials**: the secret cell
      reads a wall (`Overlay.Wall`): a word sparked once under B1/S, with
      ORIGIN plain and sparked as the key; typed answer checked against a
      hash; then initials (three letters, `PixelFont`) pressed into chapter
      2's rock beside the signature, and a "Share on GitHub" that opens a
      prefilled issue carrying a proof (word + account, hashed). `wall.yml`
      checks the proof against the `WALL_ANSWER` secret, appends to
      `docs/initials.txt` (one line per account), answers and closes the
      issue; every copy fetches the file at start-up (`Wall.cs`), with a
      shipped copy in StreamingAssets. Wall commits skip CI.
- [x] (Monday 2:00 to 3:00 AM) **third-playthrough pass** (Londo: acid buried
      under the terraces, chapter 4's coral read as a pinwheel symbol and its
      pit trapped him, Reveal appeared and vanished, Quench's bridge was two
      tiles, Two Echoes' trench pocket had no way back, the whole game too easy
      to survive): terrace acid pools brought to the surface in every chapter;
      Shadow rebuilt (four generations, a shallow pit, the post two wide against
      the cliff, a stair of two boulders at the pit's near wall so the floor
      climbs out in up-two hops and the coral's top is one hop from the stair);
      Quench's bridge crumbles tile by tile; a boulder in Two Echoes' right
      pocket and the ember moved left; Reveal always shown, counting down the
      tries; vents at 2.8 s with a longer flame, embers faster; new spikes and
      vents on most roads. The checker now also fails any standing cell that
      becomes a pocket after the growth, and any seed or origin that a cell you
      can reach first cannot reach back (that is what caught chapters 3 to 5 in
      the physics replay).
- [x] (Monday 3:05 to 3:40 PM) **Controls page text cut off**: the two
      footnotes overlapped and the gamepad line ran off its box; both have
      room now and the buttons sit below them.
- [x] (Monday 3:05 to 3:40 PM) **WebGL build**: `SimCore.cs` is the Odin
      automaton in C#; `Sim` delegates to it on WebGL (and can on the desktop,
      `Sim.UseManaged`, to check the two agree: every chapter grows
      identically, generation by generation, and all ten solve on it). The
      build copies levels.json and initials.txt into Resources, gzip with the
      decompression fallback, Minimal template, 1280x720, Quit hidden. Ran in
      the browser from a static server: title, story, tutorial, keyboard, the
      C# core reporting version -1. `dist/PointOfOrigin-webgl.zip` (14 MB).
- [x] Six fresh screenshots in `docs/screenshots/itch_*.png`; the itch draft
      lists both uploads and every page field.
- [x] (Monday 3:50 to 4:45 PM, on the `extras` branch, Londo's call) **character
      design**: `Look.cs` recolours the robe and the lantern's light on the
      loaded render and draws three hats over the head; a Wanderer page in
      the title menu (seven robes, three hats, four lights, `po.look.*`),
      locked choices shown with what opens them, more opening as origins are
      found, one light for the secret. **Death counter** under each chapter
      button on the title (`po.deaths.<i>`, nothing until a lantern is lost).
      **Hunter embers** (glyph `h`, JSON `H`): pale, they turn toward the
      wanderer whenever they share its row; one each in Bent Echo, Two Echoes
      and Origin. The Houdini slab backdrop was left out: seen head-on by an
      orthographic camera it is a rectangle behind skylines that already do
      that job, and it needed art time the deadline did not have.
- [ ] 9:00 to 10:00 am: **feel pass from the notes**, and a hand playthrough
      of the ten new chapters: hazard timing (spout period 3.2 s, flame 1 s;
      crumble 0.45 s then gone 2.8 s), ember speed, voice pacing, music mix.
- [ ] 10:00 to 12:00: **chapter polish** from that playthrough. If a chapter
      cannot be made fair by noon, cut it rather than ship it broken.
- [ ] 12:00 to 1:00 pm: **second playthrough, fresh eyes** (a housemate counts).
      Watch, do not help. Note every stall over thirty seconds.

## Monday afternoon

- [ ] 1:00 to 2:00 pm: **fix what the fresh eyes found**; proofread every
      intro, outro, hint and tip out loud.
- [ ] 2:00 to 3:00 pm: **itch page**. New cover from the side-view world
      (630x500), five screenshots, a 15 second GIF of a plant-grow-cross loop,
      the description from `docs/itch-page.md`, controls, credits (Odin, Nexium,
      Unity, Houdini). Mark the zip as a Windows executable.
- [ ] 3:00 to 3:45 pm: **clean-machine test**. Unzip `dist/PointOfOrigin-win64.zip`
      into a fresh folder and play three chapters.
- [x] (done Sunday 11 PM) the repo is public at
      https://github.com/Londopy/point-of-origin; every commit from here is pushed.
- [ ] 3:45 to 4:30 pm: **publish**. Tag `v1.0` and push the tag, final build,
      final zip, upload, submit to the jam.
- [ ] 4:30 to 5:00 pm: confirm the submission shows on the jam page and the
      download works from the page itself.
- [ ] 5:00 to 7:00 pm: buffer. Nothing new goes in. Only fixes to a broken upload.

## If there is time left over (only after "publish" is ticked)

- The Houdini slab as a decorative backdrop behind the ground (needs art
  time; see the Monday list).

## Explicitly not doing

- 3D platforming or a 3D world (not reachable by Monday).
- WebGL before the Windows build is submitted (see the Monday list: it needs a
  C# port of the core or a wasm build of the Odin plugin, and comes after).
- Multiplayer, leaderboards, cloud anything (the wall of initials is a text
  file in this repository fed by a GitHub issue; no server).

## Definition of done for the submission

- Every chapter passes `tools/platform_check.py` and has been finished by hand.
- The zip runs from a clean folder with no Unity Editor present.
- Progress saves, Esc always leads somewhere sensible, nothing traps the player.
- The itch page explains the controls in three lines and shows the world.
