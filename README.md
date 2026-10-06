<div align="center">

<img src="docs/media/teaser.webp" alt="Lost & Found: drawers, a wallet turned over in the hands, a stamp coming down, a photograph changing" width="100%">

# Lost & Found

**Run the lost-property desk at a 1962 railway station, where some belongings should never be returned.**

![Engine: Unity 6000.6 URP](https://img.shields.io/badge/engine-Unity%206000.6%20URP-222?logo=unity)
![Platform: Linux x86_64](https://img.shields.io/badge/platform-Linux%20x86__64-2f6b3a?logo=linux&logoColor=white)
![Art: Blender 4.5, scripted](https://img.shields.io/badge/art-Blender%204.5%2C%20scripted-c9a15a?logo=blender&logoColor=white)
![Audio: synthesised in Python](https://img.shields.io/badge/audio-synthesised%20in%20Python-7a2222)
![Status: v0.1.0](https://img.shields.io/badge/status-v0.1.0-1e2440)

</div>

## Trailer

[![Watch the Lost & Found trailer](docs/media/trailer_poster.jpg)](docs/media/trailer.mp4)

*Click the frame to watch the feature trailer: 1080p MP4 with sound, just under two minutes. It's also attached to the [v0.1.0 release](https://github.com/nearbycoder/LostAndFound/releases/tag/v0.1.0).*

## About

*Ninefold Junction, October 1962.* You have taken over the Lost Property desk from Agnes Pell, who ran it for forty-one years. Commuters come to the window to claim what they've lost. You search the drawers and the shelf, turn each object over in your hands, find what's hidden in it, and stamp the claim: **RETURN**, **REFUSE**, or **SEAL** it in the Iron Drawer.

Some people are lying. Some things hum when their owner is near. Some things come from tomorrow. And some belongings should never be returned at all, especially not to the polite grey gentleman who keeps asking for the keys.

It's a small deduction game about **looking closely**. There are no timers and no walls of text: every case can be solved from what's on the desk, which is the intake tag, the object's hidden details, the claimant's story, and Agnes's rules. The good moment is finding the one detail that breaks a story, or proves it.

- **One desk, 25 objects, a five-day story, three endings.** A full week takes about an hour.
- **Tactile.** Drawers slide and rattle, objects turn in your hands, latches click, stamps thump, and the ticket printer chatters out a receipt.
- **25 distinct characters**, each sculpted in Blender, with their own voice, silhouette and habits.
- **Everything is generated from code in this repository**: every model is a Blender script, every texture and photograph is processed in Python, and every sound and piece of music is synthesised.

## How to play

| Action | Control |
|---|---|
| Point, pick up, open, press | Mouse (left click) |
| Turn to the drawers / the shelf | `A` / `D`, `←` / `→`, or push the mouse to the screen edge |
| Turn an object over | Drag with the left button (or `Q` `E` `W` `S`) |
| Look closer | Scroll wheel |
| Put the object down / on the counter tray | Right click, `Esc` or `Backspace` / `T` |
| Read the claim slip | Hover it, or `Tab` |
| Ask about a finding | Click it on the slip |
| Agnes's blue lamp (from Thursday) | `L` while holding something |
| Ring for the next claimant / advance dialogue | Click the bell or `Space` / click or `Enter` |
| Read Agnes's rules | Hover her card beside the claim slip, or `R` at any time (`R` or `Esc` puts them back) |
| Pause, settings, Agnes's rules, restart the day | `Esc` |

Mouse and keyboard only for now: there is no gamepad or touch support.

**A case, start to finish:** ring the bell. The claimant describes what they lost, and their key claims are written onto the claim slip. Read the intake tag in the drawer or on the shelf (where it was found, when, on which train). Pick the object up, turn it over, open it, and click anything that glints. Each finding goes on the slip, and clicking a finding asks the claimant about it. An honest owner knows what's inside. A liar only knows what they could have seen. Put the object on the tray and stamp the slip.

### Agnes's rules

They arrive through the week as notes in Agnes's handwriting, most of them in the morning. The ones you've been given so far are on her card beside the claim slip: hover it to read them, or press `R` at any time, even with something in your hands. They're also in the pause menu.

1. Every stray has a tag. A real owner knows where and when they lost it.
2. Trust the object, not the story. Liars only know what they can see. Ask about what's hidden.
3. If it hums, it's home, whatever they say.
4. Two people claiming one thing? Let the object decide.
5. The Grey Gentleman gets nothing. Not even the time of day.
6. If it comes from tomorrow, it isn't lost yet. Lock it in the Iron Drawer, even if they're the owner.
7. Frost means they've gone on ahead. Cold things go only to the cold.
8. My blue lamp shows what ink tries to hide.

## Features

### Search the desk

<img src="docs/media/screenshot_drawers.jpg" alt="Drawer A open, with the wallet's intake tag swung up" width="49%"> <img src="docs/media/screenshot_inspect.jpg" alt="The brown wallet open in the hands, a discovery being written on the slip" width="49%">

Six drawers, three shelves and the Iron Drawer. Every stray has a manila **intake tag** that says where it was found, when and on which train, and the porter's notes. Pick the object up and it comes to your hands under the lamp. Drag to turn it, scroll to lean in, and open its lid, latch or clasp. A magnifier glints when you're near a **hidden detail**, such as a name strip inside an umbrella, a photo of a dachshund called Biscuit, or a date stamped on the back of a photograph. Each discovery is written onto the claim slip, and the object's name in your hands shows how many of its findings you've noted (secrets aren't counted). The evening ledger records the same for every case.

### Catch the liars

<img src="docs/media/screenshot_liar.jpg" alt="Asking Reggie Stokes about the name strip inside the umbrella" width="49%"> <img src="docs/media/screenshot_hum.jpg" alt="The battered suitcase glowing gold in your hands as Mrs Marsh asks: Is that humming?" width="49%">

Click a finding on the slip to ask about it with a neutral question. Honest owners answer correctly. Liars know only what they could see from across the counter, so they bluff. The game never says "contradiction"; you compare, and the evening ledger tells you whether you were right. Some objects **hum** when their owner is at the window, which overrules even a muddled story, like 94-year-old Mrs Marsh's description of a suitcase she lost in 1934.

### The uncanny, gently

<img src="docs/media/screenshot_vell.jpg" alt="Mr Vell, the Grey Gentleman, at the window while you hold up a pocket watch whose hands run backwards" width="49%"> <img src="docs/media/screenshot_frost.jpg" alt="Frost creeping over the window glass as a WWI lieutenant waits" width="49%">

Things come in from other times on Platform 9. A pocket watch found *tomorrow* runs backwards, and a photograph is developed on a date that hasn't happened yet: anything from tomorrow goes in the **Iron Drawer**, even if its owner is standing in front of you. When a cold visitor comes to the window, the glass frosts over. And **Mr Vell**, the Grey Gentleman, knows every detail of every object, and nothing ever hums for him.

### Agnes's blue lamp

<img src="docs/media/screenshot_lamp.jpg" alt="The desk lamp switched to its blue filter, revealing hidden ink on a chit" width="49%"> <img src="docs/media/screenshot_photographs.jpg" alt="A framed photograph on the desk changing" width="49%">

From Thursday, Agnes's lamp has a blue filter that shows what ink tries to hide: hidden dates, forged signatures, and a note she left about Mr Vell. And on Thursday there's a ring in a velvet box. Return it to the right person and **every photograph on your desk changes**.

### Choices that carry through the week

<img src="docs/media/screenshot_ledger.jpg" alt="The Day Ledger marking each case, and the Ninefold Gazette's headline" width="49%"> <img src="docs/media/screenshot_title.jpg" alt="The title screen: Agnes's desk at dusk" width="49%">

Every evening the **Day Ledger** marks each case against the rules and explains why, and the **Ninefold Gazette** reports what your choices did. Items you refuse stay in storage for their real owner later in the week. Whatever you give the Grey Gentleman makes him stronger, and the station visibly loses its colour. Days can be replayed from the title screen, and there are 25 optional curios (one secret per object) to find.

## Content

- **5 days**, each with its own title, morning and evening (Friday's evening is the ending):
  - **Monday, First Shift:** a guided first case, the first liar, and a suitcase that hums for a woman who lost it in 1934.
  - **Tuesday, Two of Everything:** twins and a locket, a briefcase that isn't Hugo's, and Mr Vell's first visit.
  - **Wednesday, Tomorrow's Photograph:** a photo dated Thursday, a frosted tin of letters from 1917, and a young man in a 1921 suit.
  - **Thursday, The Ring:** the blue lamp, a forged chit, a humming violin, a record from next year, and the ring.
  - **Friday, The Last Train:** everything at once, and Mr Vell's final requisition.
- **24 cases plus an alternate** that appears depending on Thursday's choice, and short "it isn't here any more" vignettes when an earlier decision took something away.
- **25 objects** (plus three documents claimants bring), each with moving parts, two to four case details, and one optional secret.
- **25 sculpted characters**, from Gus the porter to a WWI lieutenant, with per-character synthesised voices.
- **3 endings:** *The 9:40*, *The Long Wait* and *Grey Ninefold*, each with epilogue lines that react to the week's choices.

## Screenshots

| | |
|---|---|
| ![Title screen](docs/media/screenshot_title.jpg) | ![Drawer and intake tag](docs/media/screenshot_drawers.jpg) |
| ![Inspecting the wallet](docs/media/screenshot_inspect.jpg) | ![Asking a liar about a hidden detail](docs/media/screenshot_liar.jpg) |
| ![The humming suitcase](docs/media/screenshot_hum.jpg) | ![Mr Vell at the window](docs/media/screenshot_vell.jpg) |
| ![Frost on the window](docs/media/screenshot_frost.jpg) | ![The blue lamp](docs/media/screenshot_lamp.jpg) |
| ![A photograph changing](docs/media/screenshot_photographs.jpg) | ![The Day Ledger and the Gazette](docs/media/screenshot_ledger.jpg) |

## Play it

1. Download `LostAndFound-v0.1.0-linux-x86_64.zip` from the [latest release](https://github.com/nearbycoder/LostAndFound/releases/latest).
2. Unzip it and run `LostAndFound.x86_64` (`chmod +x LostAndFound.x86_64` first if your unzip tool dropped the permission).
3. On a Wayland desktop, if the window doesn't appear, run it with `SDL_VIDEODRIVER=wayland ./LostAndFound.x86_64`.

The build is 64-bit Linux with Vulkan. Saves and settings live in `~/.config/unity3d/Nearby/Lost & Found/`. There's no Windows or macOS build yet. The project has no Linux-specific code, but building for those platforms hasn't been tried.

## Build from source

**You need:** Unity **6000.6.2f1** (URP 17.6, Input System 1.20); Blender **4.5 LTS** on `PATH` as `blender` (only to regenerate models and photographs); Python 3.11 for the texture and audio generators; and `ffmpeg` for the recording tools.

```sh
# Python tools. Use 3.11, the same version as Blender 4.5's Python: the Blender
# scripts import scikit-image from this venv (needs numpy, e.g. from the system).
python3.11 -m venv --system-site-packages .venv
.venv/bin/pip install pillow scipy scikit-image==0.24.0

# open the project in the editor, or build the Linux player headless
Tools/unity.sh                 # GUI editor
Tools/unity.sh build-linux     # -> Builds/Linux/LostAndFound.x86_64 (also validates all content)
```

`Tools/unity.sh` assumes the editor is at `~/Unity/Hub/Editor/6000.6.2f1/Editor/Unity` (set `UNITY=` to override). Unity 6000.6 links against `libxml2.so.2`; on distros that only ship `libxml2.so.16`, put a copy of the legacy library in `.unity-libs/` and the script will add it to the loader path.

All generated assets are committed, so you only need to regenerate them if you change a generator:

| What | Command |
|---|---|
| Booth and concourse | `blender -b -P ArtSource/build_booth.py` |
| Desk props | `blender -b -P ArtSource/build_props.py` |
| The objects (add `-- --only id1,id2 --preview /tmp/p` to render previews) | `blender -b -P ArtSource/build_objects.py` |
| The commuters | `blender -b -P ArtSource/build_people.py` |
| Photographs (raw renders, then ageing) | `blender -b -P ArtSource/render_photos.py`, then `.venv/bin/python Tools/textures/finish_photos.py` |
| Material, UI and item textures | `.venv/bin/python Tools/textures/gen_textures.py` and `gen_item_textures.py` |
| Sound effects, voices, ambience; music | `.venv/bin/python Tools/audio/gen_sfx.py`; `gen_music.py` |

### Validation and automated play

There are no unit tests yet (the `Assets/Game/Tests/EditMode` assembly is empty). Instead, there are four checks:

| Check | Command | What it proves |
|---|---|---|
| **Content validator** | runs in every `build-linux`, or `Tools/unity.sh run LostAndFound.EditorTools.BuildScript.ValidateContent` | The rules solver, using only what's discoverable at the desk, derives each case's authored best verdict. Current result: 28 objects, 5 days, 25 cases, 0 issues. |
| **AutoPilot** | `Tools/unity.sh autopilot [speed] [startDay] [best\|worst\|wait]` | The built player plays the whole week through the real desk systems, with a screenshot of every case. `best` gets 24/24 and *The 9:40*; `worst` gives Vell everything and reaches *Grey Ninefold*; `wait` refuses Thomas the ring and reaches *The Long Wait*. Results are in `Logs/autopilot.log`. |
| **Smoke test** | `Tools/unity.sh smoke 30` | Frame times and errors over a hands-free run. |
| **Filmed play** | `Tools/unity.sh film <name> [-lafDay N] [-lafUntil N]` | Plays whole days through a simulated mouse and keyboard, the same input path a player uses, and records them. |

### The trailer and README media

```sh
Tools/unity.sh film day1 -lafUntil 1      # the title, then Monday
Tools/unity.sh film day2 -lafDay 2        # ...one take per day, through day5
Tools/unity.sh film grey -lafDay 4 -lafUntil 5 -lafVerdicts 4.2=return:vell,5.5=return:vell
.venv/bin/python Tools/make_trailer.py    # -> docs/media/trailer.mp4, screenshots, teaser.webp
```

Takes are frame-locked 30 fps captures without the score. `make_trailer.py` cuts every shot relative to event markers the recorder writes, so a re-filmed take keeps its cuts on the same moments. It then lays the game's own music under the shots and ducks it beneath the sound effects and voices.

## Project structure

```
Assets/Game/Scripts/      C# (one assembly, LostAndFound; editor tools in Assets/Game/Editor)
  Core/                   content model (JSON), story state, the rules solver and validator, save game
  Desk/                   interaction, camera, drawers, items, inspection, claim slip, stamps, gadgets, props
  Flow/                   Game (bootstrap, restart), Director (days, cases, verdicts, the photograph sequence)
  People/                 procedural commuter animation
  UI/                     code-built uGUI: dialogue, notes, ledger, title, pause, settings, ending
  Audio/, Visuals/, Util/ mixing, materials, fonts, post-processing, tweening, input
  Debug/                  SmokeTest, AutoPilot (plays the whole week), DemoRecorder (filmed play)
Assets/Game/Resources/
  Content/*.json          every object, commuter, rule, day, case, line of dialogue, gazette and ending
  Models/                 FBX exported from Blender (booth, props, 28 objects, 25 people)
  Textures/, Photos/      generated textures; the rendered and aged photographs
  Audio/, Music/          synthesised WAVs
  Fonts/                  OFL / Apache fonts (licences in docs/licenses)
ArtSource/                Blender build scripts (lib/laf.py helpers, lib/sdf.py sculpting), fonts, booth.blend
Tools/                    unity.sh, texture/photo/audio generators, editor test helpers, make_trailer.py
docs/                     BRIEF.md (the original brief), PLAN.md (design and technical plan), licences, media
```

## Tech highlights

- **The scene is empty.** `Boot` builds the whole game at runtime from code and the `Resources` folder: the camera rig, lights, post-processing, desk, props, UI and the director. There's no fragile scene wiring, and restarting a day just tears the game down and builds it again.
- **Content is data.** Objects, details, hotspots, parts, claims, answers, verdicts, story flags, Gazette headlines and endings are all JSON. Model hotspots are named empties (`HS_<detail>`) and animated parts are named nodes (`Lid`, `Flap`, `Key`), so the data and the Blender models line up by name.
- **A rules solver keeps every case fair.** `Rules.Solve` sees only what a player can learn at the desk (the tag, discoverable details, claims and answers, traits, and the rules unlocked by that point) and must derive each case's authored best verdict. The build fails validation if it can't. The same solver drives the AutoPilot.
- **Hidden-detail picking** projects each hotspot to the screen and checks that it faces the camera and isn't occluded by the object itself. A magnifier glints within a radius, so discovery guides you without spoiling anything.
- **Story state and consequences.** Verdicts set flags (`ring=thomas`, `vellItems+=1`, and so on) that move items, swap in alternate cases, rewrite later dialogue, change the photographs (each has a *before* and an *after* render), and drain the post-processing saturation for every gift to the Grey Gentleman.
- **Sculpted, not modelled.** `ArtSource/lib/sdf.py` builds heads, hands, hair and cloth as signed distance fields, then meshes them with marching cubes, so faces, lips and ears are one continuous surface. Characters are split into parts (`Head`, `EyeL`, `BrowR`, `Mouth`, hands) and animated procedurally: breathing, blinking, glances, talking and emotes, with no rigs.
- **Photographs are staged with the real cast.** `render_photos.py` poses the character models in small sets and renders them in Blender, and `finish_photos.py` ages them into 1921 sepia, 1951 silver and 1962 Polaroids.
- **Every sound is synthesised.** `Tools/audio/synth.py` is a small numpy toolkit (oscillators, filters, FM, Karplus-Strong strings, convolution reverb, formant voices). It produces about 170 SFX, voice and ambience clips, and a swing-jazz score built around a nine-note "Ninefold" motif. Voices are per-character formant babble.
- **Frame-locked recording.** `DemoRecorder` locks game time to 30 fps, pipes every frame to ffmpeg through async GPU readback, and captures the mixed audio with `AudioRenderer`, so footage is smooth however slowly the machine renders. Its `-lafPlay` mode drives a simulated mouse and keyboard through whole days, and that's how the trailer was shot.

## Credits

Design, code, models, textures, photographs, sound and music were all made for this project, and are generated by the scripts in this repository. The project was developed with [Claude Code](https://claude.com/claude-code).

**Fonts** (licence texts in [`docs/licenses/`](docs/licenses)):

| Font | Used for | Licence |
|---|---|---|
| [Caveat](https://fonts.google.com/specimen/Caveat) | the clerk's handwriting: tags, the slip | SIL OFL 1.1 |
| [Kalam](https://fonts.google.com/specimen/Kalam) | Agnes's notes | SIL OFL 1.1 |
| [Homemade Apple](https://fonts.google.com/specimen/Homemade+Apple) | signatures on letters and labels | Apache 2.0 |
| [Special Elite](https://fonts.google.com/specimen/Special+Elite) | typewritten forms | Apache 2.0 |
| [Courier Prime](https://fonts.google.com/specimen/Courier+Prime) | printer receipts | SIL OFL 1.1 |
| [IM Fell English](https://fonts.google.com/specimen/IM+Fell+English) | titles, the Gazette, the trailer's captions | SIL OFL 1.1 |
| [Limelight](https://fonts.google.com/specimen/Limelight) | station signage, the title | SIL OFL 1.1 |
| [Crimson Pro](https://fonts.google.com/specimen/Crimson+Pro) | UI body text | SIL OFL 1.1 |
| Liberation Sans (TextMesh Pro's default, licence in `Assets/TextMesh Pro/Fonts/`) | fallback | SIL OFL 1.1 |

**Engine and packages:** Unity 6000.6 with the Universal Render Pipeline, Input System, uGUI and TextMesh Pro, all under the Unity Companion License / Unity terms of service.

**Tools:** Blender 4.5 (GPL; output is unrestricted), Python with NumPy, SciPy, scikit-image and Pillow (BSD/HPND-style licences), and FFmpeg (for recording and the trailer).

## Status and known issues

v0.1.0 is the complete first version: all five days, 25 cases, three endings, menus, settings, save and replay. It's been played end to end by the AutoPilot on every branch and, for days at a time, through simulated mouse and keyboard; it has not had broad human playtesting yet.

Known gaps and rough edges:
- Characters are modelled from the waist up (they're always behind the counter, and every photograph hides them below the waist). Animation is procedural; there are no skeletal rigs.
- The synthesised music and voices are charming but not studio quality.
- Linux build only so far; no gamepad, touch or localisation.
- A few hotspots (for example the duck umbrella's chipped beak) sit where they're hard to bring into view, and the filmed play needed several turns to find them.
- No unit tests: correctness is checked by the content validator, the AutoPilot and the filmed play described above.

## License

No license has been chosen yet, so all rights are reserved by the author for now.
