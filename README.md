# Lost & Found

*Ninefold Junction, October 1962.* You've taken over the Lost Property desk from Agnes Pell, who ran it for forty-one years. Commuters come to the window to claim what they've lost. You search the drawers and shelf, turn each object over in your hands, find what's hidden in it, and stamp the claim: **RETURN**, **REFUSE**, or **SEAL** it in the Iron Drawer. Some people are lying. Some things hum when their owner is near. Some things come from tomorrow. And some belongings should never be returned at all, especially not to the polite grey gentleman who keeps asking for the keys.

One desk, 25 objects, a five-day story, three endings. Made in Unity 6 (URP). Every 3D model is scripted in Blender, and every sound and piece of music is synthesised in code.

## Playing

Run `Builds/Linux/LostAndFound.x86_64`. On a Wayland desktop you may need `SDL_VIDEODRIVER=wayland` (the `Tools/unity.sh` commands set it for you).

| Action | Control |
|---|---|
| Point, pick up, open, press | Mouse (left click) |
| Turn to the drawers / the shelf | `A` / `D`, `←` / `→`, or push the mouse to the screen edge |
| Turn an object over | Drag with the left button (or `Q` `E` `W` `S`) |
| Look closer | Scroll wheel |
| Put the object down / on the counter tray | Right click or `Esc` / `T` |
| Read the claim slip | Hover it, or `Tab` |
| Ask about a finding | Click it on the slip |
| Agnes's blue lamp (from Thursday) | `L` while holding something |
| Ring for the next claimant / advance dialogue | Click the bell or `Space` / click or `Enter` |
| Pause, settings, Agnes's rules, restart the day | `Esc` |

### Agnes's rules

They arrive one at a time, as notes in her handwriting. You can reread them from the pause menu.

1. Every stray has a tag. A real owner knows where and when they lost it.
2. Trust the object, not the story. Liars only know what they can see. Ask about what's hidden.
3. If it hums, it's home, whatever they say.
4. Two people claiming one thing? Let the object decide.
5. The Grey Gentleman gets nothing. Not even the time of day.
6. If it comes from tomorrow, it isn't lost yet. Lock it in the Iron Drawer, even if they're the owner.
7. Frost means they've gone on ahead. Cold things go only to the cold.
8. My blue lamp shows what ink tries to hide.

### The week

- **Monday, First Shift:** a guided first case, the first liar, a suitcase that hums for a woman who lost it in 1934, and a photograph of *you* that arrives off the last train.
- **Tuesday, Two of Everything:** twins and a locket, a briefcase that isn't Hugo's, and Mr Vell's first visit (a watch found tomorrow).
- **Wednesday, Tomorrow's Photograph:** a young mother's photo dated Thursday, a frosted tin of letters from 1917, and a young man in a 1921 suit looking for a ring that isn't here yet.
- **Thursday, The Ring:** the blue lamp, a forged chit, a humming violin, a record from next year, and the ring. Return it to Thomas and every photograph on the desk changes.
- **Friday, The Last Train:** everything at once, Agnes at the window, and Mr Vell's final requisition for the key to the Iron Drawer.

Each evening the Day Ledger marks every case against the rules and explains why, and the *Ninefold Gazette* reports what your choices did. You can replay any day from the title screen. The endings are **The 9:40**, **The Long Wait** and **Grey Ninefold**.

## Project layout

```
Assets/Game/Scripts/      C# (one assembly, LostAndFound; editor tools in Assets/Game/Editor)
  Core/                   content model (JSON), story state, the rules solver/validator, save game
  Desk/                   interaction, camera, drawers, items, inspection, claim slip, stamps, gadgets, props
  Flow/                   Game (bootstrap, restart), Director (days, cases, verdicts, the photo-change sequence)
  People/                 procedural commuter animation
  UI/                     code-built uGUI: dialogue, notes, ledger, title, pause, settings, ending
  Audio/, Visuals/, Util/ mixing, materials, fonts, post-processing, tweening, input
  Debug/                  SmokeTest, AutoPilot (plays the whole week), DemoRecorder (video)
Assets/Game/Resources/
  Content/*.json          every object, commuter, rule, day, case, line of dialogue, gazette and ending
  Models/                 FBX exported from Blender (booth, props, 28 objects, 25 people)
  Textures/, Photos/      generated textures; the rendered and aged photographs
  Audio/, Music/          synthesised WAVs
  Fonts/                  OFL / Apache fonts (licences in docs/licenses)
ArtSource/                Blender build scripts (lib/laf.py helpers, lib/sdf.py sculpting)
Tools/                    unity.sh, texture/photo/audio generators, editor test helpers
docs/                     BRIEF.md, PLAN.md (design and technical plan), font licences
```

The scene is empty: `Boot` builds the whole game at runtime from code and the `Resources` folder.

## Rebuilding

Everything is generated from source in this repository. Blender 4.5 is on `PATH` as `blender`. Python tools use a venv made from Blender's Python: `python3.11 -m venv --system-site-packages .venv && .venv/bin/pip install pillow scipy scikit-image==0.24.0`.

| What | Command |
|---|---|
| Booth and concourse | `blender -b -P ArtSource/build_booth.py` |
| Desk props | `blender -b -P ArtSource/build_props.py` |
| The 28 objects (add `-- --only id1,id2 --preview /tmp/p` for renders) | `blender -b -P ArtSource/build_objects.py` |
| The 25 commuters | `blender -b -P ArtSource/build_people.py` |
| Photographs (raw renders, then ageing) | `blender -b -P ArtSource/render_photos.py` then `.venv/bin/python Tools/textures/finish_photos.py` |
| Material, UI and item textures | `.venv/bin/python Tools/textures/gen_textures.py` and `gen_item_textures.py` |
| Sound effects, voices, ambience; music | `.venv/bin/python Tools/audio/gen_sfx.py`; `gen_music.py` |
| Linux build (also validates all content) | `Tools/unity.sh build-linux` → `Builds/Linux/LostAndFound.x86_64` |
| Play the whole week hands-free, with screenshots | `Tools/unity.sh autopilot [speed] [startDay]` → `Screenshots/autopilot/`, result in `Logs/autopilot.log` |
| Record the first case to video | `Tools/unity.sh demo` → `Recordings/demo.mp4` |
| Performance smoke test | `Tools/unity.sh smoke 30` |

Unity 6000.6.2f1 needs `libxml2.so.2`; `Tools/unity.sh` points the loader at a local copy in `.unity-libs/`.

### Verification

- **Content validation** (`Lost & Found ▸ Validate Content`, run by every build): the rules solver plays the whole week using only what's discoverable at the desk and checks that it derives each case's authored best verdict. Current result: 28 objects, 5 days, 25 cases (24 plus one alternate), 0 issues.
- **AutoPilot** in the built player plays every case through the real desk systems (pick up, open, inspect, tray, stamp). Last run: 24 of 24 cases best, 0 errors, ending *The 9:40*.

## Status: what's done and what isn't

Done: the full week (24 cases plus an alternate, branching on earlier choices), 25 objects with moving parts, hidden details and UV ink, 25 sculpted characters, the trailer moment, the ledger, gazette and endings, title, pause and settings menus, save and replay, synthesised audio, automated validation and playthrough.

Known gaps and rough edges:
- Characters are modelled from the waist up (they're always behind the counter); animation is procedural (breathing, blinking, talking, gestures), with no skeletal rigs.
- Only the best-verdict path through the week is played automatically. Wrong-verdict branches (Vell getting items, the ring sealed, the *Long Wait* and *Grey Ninefold* endings) are validated as content but haven't been played start to finish.
- The music and voices are synthesised: charming, but not studio quality.
- No controller support and no localisation.
