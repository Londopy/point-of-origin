# Roadmap to submission

Jam ends **Monday 21 September, 7:00 PM Pacific** (itch shows 22 Sept 02:00 UTC).
We submit by **5:00 PM Monday** and keep the last two hours as a buffer.
Every block ends with a build that could be uploaded as-is.

The game pivoted on Sunday evening from the isometric diorama to a **2D
side-scrolling platformer**: growth is solid ground, the door opens when the
growth is exact, the void, embers and overgrowth kill, three lanterns per
chapter. Six chapters exist and pass the reachability check. Everything below
assumes that shape; the diorama is in git history if we ever want it back.

Update this file as we go: tick boxes, move things, cut things. Cutting is
allowed; shipping late is not.

## Sunday night

- [x] 8:00 to 10:30 pm: **the pivot**. Tile platformer controller (run, jump,
      coyote time, variable jump), growth as terrain, doors, six side-view
      chapters, a reachability checker (`tools/platform_check.py`), scripted
      physics tests, build.
- [ ] 10:30 to 11:30 pm: **first hand playthrough (Londo)**. Play chapters 1 to 6
      with a notepad: does jumping feel right, is the stone reachable, did you
      understand "plant, get clear, grow", where did you die and was it fair.
      Hand the notes over; fixes land while you play the next chapter.
- [ ] 11:30 pm: commit, build, zip, stop. Sleep is part of the plan.

## Monday morning

- [ ] 9:00 to 10:00 am: **feel pass from the notes**. Jump height, run speed,
      ember speed, lantern radius, camera lead, the door's look.
- [ ] 10:00 to 12:00: **four more chapters** (target ten). Each new chapter needs:
      a growth law whose shape is walkable (bloom, coral, one-generation spark,
      parity), a stone you can reach, a safe place to stand, a door the growth
      reaches. Run the checker after every edit. Ideas ready to build: a chapter
      where the growth must be *avoided* (grow, then run from it); a vertical
      chapter climbing a coral; two embers on one crossing; a chapter where the
      seed is behind the door of the previous growth.
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
- [ ] 3:45 to 4:30 pm: **publish**. Push the repo to GitHub (public), tag `v1.0`,
      final build, final zip, upload, submit to the jam.
- [ ] 4:30 to 5:00 pm: confirm the submission shows on the jam page and the
      download works from the page itself.
- [ ] 5:00 to 7:00 pm: buffer. Nothing new goes in. Only fixes to a broken upload.

## If there is time left over (only after "publish" is ticked)

- Parallax background layers behind the world.
- A death counter per chapter on the chapter select.
- Ember variants: one that follows you along its row.
- The Houdini slab as a decorative backdrop behind the ground.
- A settings line: volume slider.

## Explicitly not doing

- 3D platforming or a 3D world (not reachable by Monday).
- WebGL (the Odin DLL cannot ship there without a second sim implementation).
- Multiplayer, leaderboards, cloud anything.

## Definition of done for the submission

- Every chapter passes `tools/platform_check.py` and has been finished by hand.
- The zip runs from a clean folder with no Unity Editor present.
- Progress saves, Esc always leads somewhere sensible, nothing traps the player.
- The itch page explains the controls in three lines and shows the world.
