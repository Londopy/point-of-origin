# Roadmap to submission

Jam ends **Monday 21 September, 7:00 PM Pacific** (itch shows 22 Sept 02:00 UTC).
We submit by **5:00 PM Monday** and keep the last two hours as a buffer.
Every block ends with a build that could be uploaded as-is: never leave the
game broken overnight or at lunch.

Update this file as we go: tick boxes, move things, cut things. Cutting is
allowed; shipping late is not.

## Sunday evening

- [x] 7:30 to 9:30 pm: **danger pass**. Living growth overgrows whoever stands on
      it, embers patrol rows and columns, three lanterns per chapter, death and
      game-over screens, embers placed in six chapters, tests, build.
- [ ] 9:30 to 10:30 pm: **first hand playthrough (Londo)**. Play chapters 1 to 20
      start to finish with a notepad: what confused you, what felt unfair, where
      you died and whether it was your fault, which tips give too much away.
      Hand the notes over; fixes land while you play the next chapter.
- [ ] 10:30 to 11:30 pm: **feel pass from the notes**. Tune walk speed, ember
      speed, lantern radius, lives; sounds for death, embers and pickups; the
      death animation if it reads badly.
- [ ] 11:30 pm: commit, build, zip, stop. Sleep is part of the plan.

## Monday morning

- [ ] 9:00 to 10:30 am: **second playthrough, fresh eyes**. Ideally someone who
      has never seen it (a housemate counts). Watch, do not help. Note every
      place they stall for more than thirty seconds.
- [ ] 10:30 to 12:00: **fix what the fresh eyes found**. Onboarding first
      (chapter 1 and 2 text, the first ember chapter), then difficulty spikes.
- [ ] 12:00 to 1:00 pm: **content proofread**. Every intro, outro, hint and tip
      read aloud once. Fix typos, cut anything that sounds like a manual.

## Monday afternoon

- [ ] 1:00 to 2:30 pm: **itch page**. Cover image (630x500, `docs/cover.png`
      needs remaking from the 3D world), five screenshots from
      `docs/screenshots`, a 15 second GIF of a walk-plant-grow loop, the
      description from `docs/itch-page.md`, controls, credits (Odin, Nexium,
      Unity, Houdini). Mark the zip as a Windows executable.
- [ ] 2:30 to 3:30 pm: **clean-machine test**. Unzip `dist/PointOfOrigin-win64.zip`
      into a fresh folder (or another PC) and play three chapters. This catches
      a missing DLL or levels file, which a build from the Editor folder never
      would.
- [ ] 3:30 to 4:30 pm: **publish**. Push the repo to GitHub (public, so judges
      can read it), tag `v1.0`, final build, final zip, upload, submit to the jam.
- [ ] 4:30 to 5:00 pm: confirm the submission shows on the jam page and the
      download works from the page itself.
- [ ] 5:00 to 7:00 pm: buffer. Nothing new goes in. Only fixes to a broken upload.

## If there is time left over (only after "publish" is ticked)

- A chapter select that shows which chapters were solved without a death.
- Ember variants: one that only moves when you move.
- A lantern that dims over a chapter and can be refilled at pickups.
- A settings line: volume slider, invert diagonal walk keys.
- A short trailer cut from the GIF footage for the itch page.

## Explicitly not doing

- WebGL (the Odin DLL cannot ship there without a second sim implementation).
- True 3D cellular automata.
- Multiplayer, leaderboards, cloud anything.

## Definition of done for the submission

- Twenty chapters solve in the scripted test and by hand.
- The zip runs from a clean folder with no Unity Editor present.
- Progress saves, Esc always leads somewhere sensible, nothing traps the player.
- The itch page explains the controls in three lines and shows the world.
