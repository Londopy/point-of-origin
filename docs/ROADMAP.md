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

- A death counter per chapter on the chapter select.
- Ember variants: one that follows you along its row.
- The Houdini slab as a decorative backdrop behind the ground.
- Gamepad bindings (the input layer is per action already).

## Explicitly not doing

- 3D platforming or a 3D world (not reachable by Monday).
- WebGL (the Odin DLL cannot ship there without a second sim implementation).
- Multiplayer, leaderboards, cloud anything.

## Definition of done for the submission

- Every chapter passes `tools/platform_check.py` and has been finished by hand.
- The zip runs from a clean folder with no Unity Editor present.
- Progress saves, Esc always leads somewhere sensible, nothing traps the player.
- The itch page explains the controls in three lines and shows the world.
