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
| Turn to the drawers / the shelf | `A` / `D`, `←` / `→`, or rest the mouse at the side of the screen (*Turn at the screen's edge* in Settings turns that off; a pointer leaving a window by its side doesn't count) |
| Turn an object over | Drag with the left button (or `Q` `E` `W` `S`) |
| Look closer | Scroll wheel |
| Put the object down / on the counter tray | Right click, `Esc` or `Backspace` / `T` |
| Read the slip and what they said | Hover it, or `Tab` |
| Ask about a finding | Click it on the slip |
| Agnes's blue lamp (from Thursday) | `L` while holding something |
| Ring for the next claimant / advance dialogue | Click the bell or `Space` / click or `Enter` |
| Read Agnes's rules | Hover her card beside the claim slip, or `R` at any time (`R` or `Esc` puts them back) |
| Stuck? A nudge from Agnes | `H` during a claim (press again for a stronger one) |
| Look back at earlier days' Day Ledger pages | Click the ledger book on the desk (bottom right), or *The week so far* in the pause menu; `←` `→` turn the pages |
| Pause: Agnes's rules, the controls, settings, restart the day | `Esc` (`Esc` again to carry on) |
| Menus (the title, the pause menu, Settings and every card) | The mouse, or `↑` `↓` to move between buttons and sliders, `Enter` or `Space` to press, `←` `→` to move a slider, `Esc` to close |

**With a gamepad** (Xbox-style layout; tested only with a virtual Input System gamepad, as no physical controller was to hand):

| Action | Button |
|---|---|
| Move the cursor (it slows over anything you can click) | Left stick |
| Click: pick up, open, note a detail, stamp, menus | A |
| Put the object down / the stamp back | B |
| On the counter tray | X |
| Agnes's rules | Y |
| Turn to the drawers / the shelf | LB / RB |
| Turn a held object · lean in or out | Right stick · RT / LT |
| Ring for the next claimant / move the dialogue on | D-pad down |
| Agnes's blue lamp | D-pad up |
| A nudge from Agnes | D-pad left |
| Earlier days' ledger pages · turn them | A on the desk's ledger, or Menu › *The week so far* · LB / RB |
| Read the slip and what they said · pause | View · Menu |
| Menus: move between buttons and sliders · press · move a slider | D-pad up and down · A · d-pad left and right (the cursor follows) |

Pick up the mouse and it takes over again at once. There's no touch support.

Both tables are in the game too: **Controls** in the pause menu and on the title shows the one for whatever you're holding. Quick taps count: a touchpad's tap-to-click, or a click on a slow frame, is never lost.

**Stuck?** Press `H` (d-pad left) during a claim for a nudge from Agnes, pinned at the right of the screen. It doesn't block anything. Each press goes a step further for wherever you've got to: finding the object, examining it, asking about it, deciding. The last nudge of each step shows you: the drawer or the object glows, or a glint marks the hidden detail once a click there would find it. Nudges only cite rules you've been given, never quote a finding you haven't found, and never say which stamp to use. After 90 seconds on a claim without progress, the hint bar offers one (*Offer Agnes's nudges when stuck* in Settings turns the offer off). Every nudge on the best path through the week is listed in [docs/nudges.md](docs/nudges.md) (spoilers).

*Continue* on the title picks up where you left off, mid-day included: at the next claimant, with the morning already done.

**A case, start to finish:** ring the bell. The claimant describes what they lost, and their key claims are written onto the claim slip. Read the intake tag in the drawer or on the shelf (where it was found, when, on which train). Pick the object up, turn it over, open it, and click anything that glints. Each finding goes on the slip, and clicking a finding asks the claimant about it. An honest owner knows what's inside. A liar only knows what they could have seen. Whenever you read the slip, everything said in the claim (their story, your questions, their answers) is written on a card beside it, so you can compare an answer with a finding without asking again. Put the object on the tray and stamp the slip. A stamp can't be taken back, so while you hold one over the slip the hint says what it will do there (*RETURN: give the silver locket to Cecily Fairweather*); when two people claim one thing, the half of the slip you stamp decides who gets it.

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

Click a finding on the slip to ask about it with a neutral question, and read the answers back on the card beside the slip. Honest owners answer correctly. Liars know only what they could see from across the counter, so they bluff. The game never says "contradiction"; you compare, and the evening ledger tells you whether you were right. Some objects **hum** when their owner is at the window, which overrules even a muddled story, like 94-year-old Mrs Marsh's description of a suitcase she lost in 1934.

### The uncanny, gently

<img src="docs/media/screenshot_vell.jpg" alt="Mr Vell, the Grey Gentleman, at the window while you hold up a pocket watch whose hands run backwards" width="49%"> <img src="docs/media/screenshot_frost.jpg" alt="Frost creeping over the window glass as a WWI lieutenant waits" width="49%">

Things come in from other times on Platform 9. A pocket watch found *tomorrow* runs backwards, and a photograph is developed on a date that hasn't happened yet: anything from tomorrow goes in the **Iron Drawer**, even if its owner is standing in front of you. When a cold visitor comes to the window, the glass frosts over. And **Mr Vell**, the Grey Gentleman, knows every detail of every object, and nothing ever hums for him.

### Agnes's blue lamp

<img src="docs/media/screenshot_lamp.jpg" alt="The desk lamp switched to its blue filter, revealing hidden ink on a chit" width="49%"> <img src="docs/media/screenshot_photographs.jpg" alt="A framed photograph on the desk changing" width="49%">

From Thursday, Agnes's lamp has a blue filter that shows what ink tries to hide: hidden dates, forged signatures, and a note she left about Mr Vell. And on Thursday there's a ring in a velvet box. Return it to the right person and **every photograph on your desk changes**.

### Choices that carry through the week

<img src="docs/media/screenshot_ledger.jpg" alt="The Day Ledger marking each case, and the Ninefold Gazette's headline" width="49%"> <img src="docs/media/screenshot_title.jpg" alt="The title screen: Agnes's desk at dusk" width="49%">

Every evening the **Day Ledger** marks each case against the rules and explains why, and the **Ninefold Gazette** reports what your choices did. After Friday's ending, a last page sums up the week: each day's tally, your findings and curiosities, and how many of the three endings you've reached. The ledger book on the desk keeps every evening's page, so you can look back at what you decided earlier in the week (click it, or *The week so far* in the pause menu). Items you refuse stay in storage for their real owner later in the week. Whatever you give the Grey Gentleman makes him stronger, and the station visibly loses its colour. Days can be replayed from the title screen. There are 25 optional curios (one secret per object) to find, and the title's **Curiosities** page shows which you've found and when the rest turn up. Unclaimed strays don't clutter the shelves forever: once nobody else will come for something, Gus takes it down to the basement.

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

The build is 64-bit Linux with Vulkan. Saves and settings live in `~/.config/unity3d/Nearby/Lost & Found/` (`lostandfound_save.json` and `settings.json`; v0.1.0 kept its settings in Unity's `prefs` file in the same folder, and they're brought over the first time this version starts). Each save swaps in whole and keeps the one before as `lostandfound_save.json.bak`, so a crash mid-save can't lose the week. A save that can't be read is set aside as `.damaged` (never deleted), and the title says so. The window opens at 1600×900, or smaller on a smaller screen so it always fits with its title bar; *Fullscreen* in Settings fills the screen. The window can be any shape: one narrower than 16:9 (4:3, or snapped to half the screen) sees the whole width of the desk, with more above and below. While another window has the focus the game draws only 10 frames a second, and *Sound when in the background* in Settings can silence it there. If the desk looks too dark on your screen (or too bright), *Brightness* in Settings lifts or lowers the scene without touching the paper and text; *Turn at the screen's edge* there stops the desk turning when the pointer rests at the side of the window. If it runs slowly, try a lower *Picture quality* in Settings; *Large text* there makes the dialogue, hints, tags and notes 25% bigger, and *Plain lettering* writes the tags, the claim slip and Agnes's notes, rules and nudges in a clear book face instead of handwriting.

**Reporting a problem:** the title's bottom-right corner says which build you're playing, as `v0.1.0 · ed71667`: the version and the commit it was built from (a `+` after it means the build had changes that weren't committed). Put that in a bug report, with what you were doing (the day and the claimant) and the player log: on Linux `~/.config/unity3d/Nearby/Lost & Found/Player.log` (the run before is `Player-prev.log`), and on a Mac, where it hasn't been tried, Unity writes it to `~/Library/Logs/Nearby/Lost and Found/Player.log`. The log's first lines name the build too. If the save is involved, `lostandfound_save.json` from the Linux folder above helps.

**macOS and Windows:** there's no download for either yet. A universal (Intel and Apple silicon) macOS app can be built from source with `Tools/unity.sh build-mac`, but it **hasn't been run on a Mac**. It isn't signed or notarised, so macOS will refuse to open it until you right-click it and choose *Open* (or run `xattr -dr com.apple.quarantine "LostAndFound.app"`). On a Mac the app is called "Lost and Found". The Windows build is wired up (`Tools/unity.sh build-windows`) but needs Unity's Windows Build Support module, which the machine this was made on doesn't have, so it has never been built.

## Build from source

**You need:** Unity **6000.6.2f1** (URP 17.6, Input System 1.20); Blender **4.5 LTS** on `PATH` as `blender` (only to regenerate models and photographs); Python 3.11 for the texture and audio generators; and `ffmpeg` for the recording tools.

```sh
# Python tools. Use 3.11, the same version as Blender 4.5's Python: the Blender
# scripts import scikit-image from this venv (needs numpy, e.g. from the system).
python3.11 -m venv --system-site-packages .venv
.venv/bin/pip install pillow scipy scikit-image==0.24.0

# open the project in the editor, or build a player headless (each build also validates all content)
Tools/unity.sh                 # GUI editor
Tools/unity.sh build-linux     # -> Builds/Linux/LostAndFound.x86_64
Tools/unity.sh build-mac       # -> Builds/macOS/LostAndFound.app (universal, unsigned; untested on a Mac)
Tools/unity.sh build-windows   # -> Builds/Windows/LostAndFound.exe (needs Windows Build Support installed)
Tools/unity.sh build-webgl     # -> Builds/WebGL/ (not a release: see docs/IMPROVEMENTS.md, rounds 3 and 4)
python3 Tools/package_release.py   # zip whatever's built into dist/LostAndFound-v<version>-<platform>.zip
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
| The app icon | `.venv/bin/python Tools/textures/gen_icon.py` |
| Sound effects, voices, ambience; music | `.venv/bin/python Tools/audio/gen_sfx.py`; `gen_music.py` |

### Validation and automated play

| Check | Command | What it proves |
|---|---|---|
| **Unit tests** | `Tools/unity.sh test` (results in `Logs/test-results.xml`) | 131 EditMode tests over the real content, no player needed: the validator, the solver deriving every case's best verdict, all three endings from whole weeks played in memory (the AutoPilot's three policies), Thursday's choice swapping Friday's case, scoring, when each rule arrives, story-flag expressions, every detail having a hotspot or a part that reveals it, short names, the save file's round trip (including mid-day progress, endings reached, and old saves) and its crash safety (a truncated, empty or garbage save, a failed write), strays only leaving after their last case, the Curiosities count, and Agnes's nudges (every claim of the `best` and `worst` weeks followed nudge by nudge to a decision, with keyboard and gamepad wording: only rules already handed over, no unfound finding quoted, no stamp named, and the decision nudge agreeing with the solver), the transcript beside the slip (every speaker named, two claimants never sharing a name), the settings file (round trip, a damaged file giving the defaults), which menu choices ask before undoing claims, the size of the window on a small screen, the ledger book's pages (only days already closed, each drawn from the state its own evening left, a replay dropping the days after it), the camera keeping the 16:9 view's width on a narrower window, what the hint says a stamp will do over each half of the slip, and when a pointer has left the window by its side. |
| **Content validator** | runs in every `build-linux`, or `Tools/unity.sh run LostAndFound.EditorTools.BuildScript.ValidateContent` | The rules solver, using only what's discoverable at the desk, derives each case's authored best verdict. Current result: 28 objects, 5 days, 25 cases, 0 issues. |
| **AutoPilot** | `Tools/unity.sh autopilot [speed] [startDay] [best\|worst\|wait\|refuse]` | The built player plays the whole week through the real desk systems, with a screenshot of every case (plus each day's rules card and first tag). `best` gets 24/24 and *The 9:40*; `worst` gives Vell everything and reaches *Grey Ninefold*; `wait` refuses Thomas the ring and reaches *The Long Wait*; `refuse` refuses everything (the fullest the shelves get, and Gus's basement trips). With the player's `-lafQuitAfter <case>` and then `-lafContinue`, it checks that *Continue* resumes mid-day. Add `-lafTranscript` to ask about every finding and check that the card beside the slip holds every line said, on screen and clear of the slip; `-lafTextAudit` to check every text on screen against its box, and against the other texts on the same card or panel, at each screenshot, and that no reading panel (the speech bubble, Agnes's notes, the tag, the nudge, the card beside the slip, the rules peek) is drawn over another's letters and the speech bubble isn't over anyone's face; `-lafFaceWatch` to make that last check at the end of every frame rather than at screenshots (`[FaceWatch]`); `-lafPlainLettering` or `-lafTextSize 1` (or `-lafBrightness -1..1`) for one run in those settings. Before each verdict it holds the stamp over the slip with a virtual mouse (over both halves when two people claim) and checks the hint names the verdict, the item and the claimant (`[StampHint]`). Each morning it opens the ledger book on the desk, photographs every page and logs it as `[LedgerBook]`, to compare with that evening's `[Ledger]` line, and on the first morning checks the game's own picking can reach the book. `-lafHitches` logs every frame over 100 ms with what was happening, and `-lafNoShots` leaves out the screenshots (which stall a frame themselves) while it does. `LAF_AUTOPILOT_SAVE=<path>` lets several runs share one save (the week summary's endings count). Results are in `Logs/autopilot.log`. |
| **Gamepad test** | `Tools/unity.sh padtest` | Plays Monday's first case with only a virtual Input System gamepad (cursor, right stick, buttons, and the d-pad's left for two nudges in pad wording), counts any keyboard or mouse events during play (there should be none), then checks that moving a mouse takes control back. |
| **Tap test** | `Tools/unity.sh taptest [w] [h] [player args]` | Plays Monday's first case with quick taps: each press and its release reach the game in the same input update, as a touchpad's tap-to-click does. Keys ring the bell, ask for a nudge, turn, use the tray and pause. Mouse taps open a drawer, pick up the wallet and work the pause menu and the Controls card. Pad taps take control, ask for a nudge, stamp the slip and put the card away. Before round 4's fix, 1 of 11 such taps worked. |
| **Soak** | `Tools/unity.sh soak [cycles]` | Plays on without quitting, as someone who leaves the game open all evening does: in one process it goes round the menus' own buttons again and again (decide a claim, *Start the day again*, *Back to the title*, *Continue*, *Choose a Day*), each of which tears the game down and builds it again. After every cycle it logs memory and the live materials, meshes, textures and objects, and passes only if the last ten cycles hold steady and every choice that would lose progress asked for a second click (and only those). At the end it finishes a day and checks the Day Ledger's *Replay the day* the same way. `LAF_SOAK_SAVE=<path>` starts it from an existing save, such as a finished week. AutoPilot `-lafWeeks 2` plays two whole weeks back to back in one process. Log in `Logs/soak.log`. |
| **Nudge tour** | `Tools/unity.sh nudgetour [speed] [w] [h] [player args]` | The AutoPilot plays the week as a stuck player: at every stage of every claim it asks for every nudge, then does what they say. It picks up what glows, works the part that lights up, turns the object until the glint appears and clicks on the glint, and asks what it's told to ask. It checks the glint only shows where a click finds the detail, and that the hint bar offers a nudge after 90 s idle. Log in `Logs/nudgetour.log`. |
| **Hotspot audit** | `Tools/unity.sh audit` (table in `Screenshots/hotspots/coverage.txt`) | Holds every object as the inspect view does, lids shut and open, through 1,500 orientations at two zooms, and asks the game's own picking code whether each hidden detail could be clicked. Then, for every day with nothing yet returned (the fullest storage gets), it checks that every stored object has a real slot and can be hovered from its shelf or open drawer. Fails if any detail or object falls under 10%, and saves a picture of each detail that does. |
| **WebGL in Chrome** | `python3 Tools/serve_webgl.py Builds/WebGL &` then `python3 Tools/webgl_check.py --url 'http://127.0.0.1:8764/?lafSmoke=smoke' --out Logs/webgl/smoke --profile Logs/webgl/profile` | Plays the WebGL build in Chrome on the real GPU (headless, ANGLE on EGL), records the console, screenshots every `[Shot]` the game logs, and lists what the page keeps in IndexedDB. In a browser the game reads its `-laf…` options from the query string (and `?lafShot` makes the page itself log screenshots of its loading and error states), so the smoke test and AutoPilot run there too (`?lafAutopilot=x&lafQuitAfter=1.2`, then `?lafAutopilot=x&lafContinue` with the same `--profile` to check the save survived). |
| **Smoke test** | `Tools/unity.sh smoke 30 [quality]` | Frame rate (uncapped) and errors over a hands-free run; `quality` 0, 1 or 2 overrides the picture quality for that run only. With `-lafShowSettings` or `-lafShowControls` it opens that card over the title, and `-lafTextAudit` audits its texts. |
| **Pointer test** | `Tools/unity.sh pointertest [w] [h]` | The real pointer at the window's sides: KWin's own pointer, moved through its fake-input protocol (`Tools/fakeptr.c`, which only connects to this script's private KWins), so the game hears enter, motion and leave as it would from a mouse. It rests just inside each side (the desk turns), leaves by each side briskly, at an ordinary pace and slowly and stays out (it mustn't), comes back in, and, with the window snapped against the screen's side, is flung against it (it turns). Before round 11, every exit at an ordinary pace turned the desk. Log in `Logs/pointertest.log`. |
| **Real-input play** | `Tools/unity.sh realplay [until day] [w] [h]` | The filmed play's whole days (from the title, by default to Tuesday morning; `5` plays the week to the ending), with real input: the play's pointer moves, clicks, drags and key presses go to KWin's own pointer and keyboard, and the game reads them through SDL as it would a mouse and keyboard. On the first claim it also presses `H`, `Tab` and `Esc`, brings the object closer and back with the wheel, and puts it down with a right click. Passes with no fallbacks (nothing the play had to do directly because the input didn't) and every claim decided as the play meant. Nothing is recorded. Log in `Logs/realplay.log`. |
| **Edge test** | `Tools/unity.sh edgetest [w] [h]` | Holds a virtual mouse at each side of the screen on Tuesday at the bell (slowing to a stop there, as a person does): the desk turns with *Turn at the screen's edge* on, doesn't with it off, and doesn't while another window has the focus (the headless KWin opens one once the test says it's ready), then turns again when the focus comes back. Log in `Logs/edgetest.log`. |
| **Settings upgrade probe** | `Tools/prefs_probe.sh` (after `build-linux`) | Writes settings the way v0.1.0 did (PlayerPrefs, saved at once) in a scratch config folder, shows which file they land in, and starts this version on them to check they're brought over into `settings.json`. Also shows that `~/.config/unity3d/unknown/unknown/` is never read or written. |
| **Small screens** | `Tools/unity.sh smallscreen <w> <h> [scale] [player args]` | A first launch on a screen of that size (a short smoke run), in a headless KWin of its own: its own D-Bus session and scratch folders, so nothing shows on the desktop and the real session's settings aren't touched. KWin reports where it put the window, and `spectacle` photographs the whole screen. `scale 2` is a HiDPI screen at 200%. `LAF_STEAL="10 20"` puts another window over the game for a while (with `-lafBackgroundTest`, to see it go quiet behind it), and `LAF_SETTINGS` starts from given settings. Needs `kwin_wayland`, `kscreen-doctor`, `spectacle` and `kdialog` (KDE Plasma). Results in `Screenshots/smallscreen/`. |
| **Filmed play** | `Tools/unity.sh film <name> [-lafDay N] [-lafUntil N]` | Plays whole days through a simulated mouse and keyboard, the same input path a player uses, and records them. It turns each object until a hidden detail faces it and clicks it. The log ends with how many details were found by hand and how many needed the recorder's fallback, which should be none. |

Every player run above keeps its prefs in `Logs/config/<command>/` rather than your real `~/.config/unity3d/Nearby/Lost & Found/`. Each one also runs inside a headless KWin of its own (as `smallscreen` does: its own D-Bus session, socket and scratch folders), so no test window ever opens on your desktop; `LAF_DESKTOP=1` runs it as an ordinary window instead. The script points `XDG_CONFIG_HOME` there and passes `-lafSave` for the save. Batch editor runs (builds, `test`, `run`) get `Logs/config/editor/`, which links back to the real config folder for everything except this game's own, so the editor still finds its licence. The interactive editor (`Tools/unity.sh` with no command, or `headless`) gets the same folder, so pressing Play in the editor uses a scratch save and settings too. Every one of these runs checks the real folder's hashes and timestamps before and after, and fails (`[guard] … CHANGED`) if anything in it changed, so testing can't overwrite your own save or settings.

### The trailer and README media

```sh
Tools/unity.sh film day1 -lafUntil 1      # the title, then Monday
Tools/unity.sh film day2 -lafDay 2        # ...one take per day, through day5
Tools/unity.sh film grey -lafDay 4 -lafUntil 5 -lafVerdicts 4.2=return:vell,5.5=return:vell
.venv/bin/python Tools/make_trailer.py    # -> docs/media/trailer.mp4, screenshots, teaser.webp
```

Takes are frame-locked 30 fps captures without the score. `make_trailer.py stills --takes r5_` cuts only the README stills, from takes filmed under other names (`film r5_day1 -lafUntil 1` and so on), leaving the trailer's takes alone. `make_trailer.py` cuts every shot relative to event markers the recorder writes, so a re-filmed take keeps its cuts on the same moments. It then lays the game's own music under the shots and ducks it beneath the sound effects and voices.

## Project structure

```
Assets/Game/Scripts/      C# (one assembly, LostAndFound; editor tools in Assets/Game/Editor)
  Core/                   content model (JSON), story state, the rules solver and validator, save game
  Desk/                   interaction, camera, drawers, items, inspection, claim slip, stamps, gadgets, props
  Flow/                   Game (bootstrap, restart), Director (days, cases, verdicts, the photograph sequence)
  People/                 procedural commuter animation
  UI/                     code-built uGUI: dialogue, notes, ledger, title, pause, settings, ending
  Audio/, Visuals/, Util/ mixing, materials, fonts, post-processing, tweening, input
  Debug/                  SmokeTest, AutoPilot (plays the whole week), DemoRecorder (filmed play), HotspotAudit
Assets/Game/Tests/EditMode/  unit tests (NUnit, run with Tools/unity.sh test)
Assets/Game/Icon/         the app icon (generated)
Assets/Game/Resources/
  Content/*.json          every object, commuter, rule, day, case, line of dialogue, gazette and ending
  Models/                 FBX exported from Blender (booth, props, 28 objects, 25 people)
  Textures/, Photos/      generated textures; the rendered and aged photographs
  Audio/, Music/          synthesised WAVs
  Fonts/                  OFL / Apache fonts (licences in docs/licenses)
ArtSource/                Blender build scripts (lib/laf.py helpers, lib/sdf.py sculpting), fonts, booth.blend
Tools/                    unity.sh, texture/photo/audio/icon generators, editor test helpers, make_trailer.py, package_release.py
docs/                     BRIEF.md (the original brief), PLAN.md (design and technical plan), IMPROVEMENTS.md, licences, media
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

Since v0.1.0 (not released yet; see [docs/IMPROVEMENTS.md](docs/IMPROVEMENTS.md)):
- **Fairness fixes.** The lunch tin's rim was modelled as a solid slab that sealed it shut, so its sandwich and note (the evidence for Thursday's first case) couldn't be seen, and the frosted tin was stored inside the hatbox, where Wednesday's two cases couldn't pick it up. Both are fixed, along with other shelf and drawer overlaps. The new hotspot audit checks every hidden detail and every stored object, and the filmed play now finds all 53 details it looks for across the week by hand. The original takes needed the recorder's fallback 16 times.
- Hints and the inspect bar are legible over the claim slip (6.5:1 contrast, from about 1:1).
- Agnes's rules are on a card on the desk (hover it, or press `R` at any time), and the inspect bar and Day Ledger count your findings.
- 42 unit tests; a macOS build target, app icon and bundle identifier; a release packaging script; a *Picture quality* setting.
- Round 2: shelves that never overflow (an umbrella stand, and Gus's basement for unclaimed strays); *Continue* resumes mid-day; the Curiosities page; a *Large text* option; gamepad support.
- Round 4: **quick clicks and taps are never lost** (a touchpad's tap-to-click, or any click on a slow frame, used to be dropped); **a crash can't cost you the week** (saves swap in whole and keep a backup); a **Controls** card in the pause menu and on the title; editor test runs no longer touch your settings either; and the WebGL build plays at 60 fps on a real GPU and keeps its save in the browser.
- Round 5: **what they said is written beside the slip** whenever you read it (your questions and their answers included), so an answer can be read again; the slip's own writing is no longer stuck at its smallest size on longer claims; a *Plain lettering* option for the handwriting; settings kept in `settings.json` beside the save (v0.1.0's ended up in a folder Unity shares between games); a WebGL page of the game's own; and README screenshots from the current build. A text audit now checks every text against its box during the AutoPilot. There are 70 unit tests.
- Round 6: **the Day Ledger's Gazette stands beside the rows** instead of landing on the last of them, and the ledger fits 21:9 and 4:3 screens (at 21:9 its buttons were off the bottom of the screen); **Agnes's rules stay readable on Thursday and Friday** (the full card about 28% larger, the card beside the slip 25%); **a page summing up the week** after the ending, with the endings you've reached; the text audit now catches texts running into each other; and a check that v0.1.0 players keep their settings (they do). There are 72 unit tests.
- Round 7: **playing on without quitting no longer leaks**: the game rebuilds itself whenever you start the day again, go back to the title or finish the week, and each time it left about a dozen materials and textures behind (resident memory grew 51 MB over 30 rebuilds; now flat). A new soak test checks this, and two whole weeks now play back to back in one process. **No progress is lost by surprise**: *Start the day again* and going back to an earlier day with *Choose a Day* ask for a second click and say what would be undone, and a replay after a finished week can be *Continue*d. Agnes's full rules card is a little larger again on Friday. There are 76 unit tests.
- Round 8: **the window fits the screen**: on a 1366×768 laptop, a 1440×900 MacBook or a HiDPI screen at 200%, the game used to open a 1600×900 window that ran off the screen, taking the shelf arrow, the claim slip and the hints with it; it now opens the largest 16:9 window that fits. The *Fullscreen* setting, which did nothing on Wayland, now works. The Day Ledger's *Replay the day* asks before undoing the day, the buttons on the dark (ledger, ending, week summary) stay readable under the pointer, and the game goes quiet (10 fps, optionally silent) while another window has the focus. There are 86 unit tests.
- Round 9: **the week's ledger at hand**: the ledger book on the desk (or *The week so far* in the pause menu) holds every closed day's Day Ledger page as it was that evening, Gazette included, and Gus mentions it on Tuesday. A **Brightness** setting for the dark desk, and **Turn at the screen's edge** to stop the pointer turning the desk. The title's corner names the build (version and commit), and the README says where the log is for a bug report. Every test window now opens in a private headless KWin, never on the desktop. There are 92 unit tests.
- Round 10: **a window of any shape sees the whole desk**: one narrower than 16:9 (4:3, or snapped to half the screen) used to lose the lamp, the printer, the ledger book and half the object in your hands off its sides, and at half a 1080p screen the speech bubble sat over every claimant's face. **What a stamp will do is said before it lands** ("RETURN: give the silver locket to Cecily Fairweather"), following the half of the slip when two people claim one thing. Agnes's notes no longer open over the line before them. The text audit now also checks reading panels over each other and the bubble over faces. There are 111 unit tests.
- Round 11: **a pointer leaving the window doesn't turn the desk**: checked at last with a real pointer (KWin's own, in the private test desktop), leaving a window by its side at an ordinary pace turned the desk every time; now it doesn't, while resting at the side still turns it. **The speech bubble never slides across a face** (a new check of every frame found it doing so at the start of most claims, for a moment), and **it's as tall as its line** instead of a box sized for five. **`Esc` closes the pause menu**, as it does every other card. And a whole week has been played with **real input**, the compositor's own pointer and keyboard rather than devices simulated inside the game: every claim decided by real clicks, drags, keys and the wheel. There are 131 unit tests.
- Round 3: **Agnes's nudges** for a stuck player (`H` or the d-pad's left): step by step, ending with a glint on the spot a click would find. The Linux download is **22% smaller** (111 MB zipped, from 143 MB) through compressed meshes. A WebGL build can be made and measured (`build-webgl`, not released). The test tools can no longer touch your real save or settings. There are now 53 unit tests.

Known gaps and rough edges:
- Characters are modelled from the waist up (they're always behind the counter, and every photograph hides them below the waist). Animation is procedural; there are no skeletal rigs.
- The synthesised music and voices are charming but not studio quality.
- Only Linux has a release. The macOS build is untested on a Mac and unsigned; Windows needs a Unity module that isn't installed here.
- Gamepad support has only been driven by a virtual Input System gamepad; no physical controller (or Steam Deck) has been tried. There's no touch support or localisation.
- How easy the hidden details are for a person to find hasn't been tested with people. The audit only proves each can be brought into view, and the hardest (the date on the ring's ticket, under the blue lamp) is clickable from about 14% of orientations. Agnes's nudges now lead a stuck player to every deciding detail, but only the AutoPilot has followed them so far.
- Two weeks played back to back in one process keep the same live objects, but the process's resident memory still rose by about 43 MB between the end of the first week and the second (Unity's own count rose 1.5 MB). It looks like the allocator or driver keeping memory rather than a leak in the game, but it isn't proven. Restarting the game resets it.
- **Keyboard layouts.** Shortcuts are read by where the key sits on a US keyboard, and the prompts name the US letters. On a French (AZERTY) keyboard `A`/`D` (turn) are the keys printed **Q**/**D**, and `Q`/`W` (turn the object) are printed **A**/**Z**; `H`, `R`, `L` and `T` are where they're printed on AZERTY and QWERTZ, but not on Dvorak. The arrow keys, the screen edges and dragging do the same jobs. Round 8 tried to name the keys as printed on the player's keyboard, but the Linux player reported US letters under French, German and Dvorak layouts in every test here, so it couldn't be done or checked.
- **The pointer at a window's side.** The game isn't told when the pointer leaves its window, and keeps its last position, so it judges from the pointer's last step whether it has gone (round 11; checked with KWin's own pointer, which leaving at an ordinary pace used to turn the desk). Two cases can't be told apart from a pointer leaving: one stopped on the window's very last pixel, and one pushed slowly against the side of the screen in a maximised or snapped window. Neither turns the desk; pull back a pixel, use `A`/`D` or the arrows, or play fullscreen, where the pointer can't leave. A pointer flung against the screen's side still turns it. *Turn at the screen's edge* in Settings turns edge turning off, and while another window has the focus it never turns.
- **Real input, not real hardware.** The real-input play drives the compositor's own pointer and keyboard, so everything from KWin through SDL into the game is checked, but the events don't come from a physical mouse, touchpad or keyboard, and pointer acceleration isn't involved. In that play the turning search couldn't bring one hidden detail into view (the ring's date, which shows from about one turn in seven) and found it directly; with simulated input it happens to find it at once. The drags themselves turn the object by what they should. Once in twelve runs the play's click took up the stamp beside the one it aimed at (the retry took the right one); why isn't known.
- The ledger book on the desk sits in the bottom-right corner of the counter view, partly out of frame at 16:9 and wider (a narrower window shows it whole): the game's picking reaches it at every window shape tried, but a player may not notice it. Gus mentions it on Tuesday morning, and the pause menu has *The week so far*.
- **A window narrower than 16:9** shows the whole width of the desk by showing more above and below it, so at half a 1080p screen (960×1080) the desk, the people and the object in your hands are drawn at about 60% of their size in a 1600×900 window (half what that window showed before round 10, when the sides were cut off). Everything fits and reads, but a 16:9 window or *Fullscreen* shows it best.
- Starting *A New Week* clears the curiosities you've found along with the rest of the save (the endings you've reached are kept). Whether curiosities should carry over between weeks is an open design question.
- WebGL isn't released or hosted. It builds as a 65 MB download (last rebuilt in round 10, with everything above), plays at 60 fps in Chrome on this machine's GPU (a Radeon 8060S iGPU) when the machine is quiet (43–52 fps with other work running), with the desktop's picture, and its save survives closing the browser. It's been checked in Chrome only (headless, on the real GPU), not Firefox or Safari, and nobody has played it by hand. It has a page of its own: the game fills the window at 16:9, with a loading bar, a fullscreen button, plain words if the browser has no WebGL 2, and a warning (before the 65 MB download) on phones and tablets. See [docs/IMPROVEMENTS.md](docs/IMPROVEMENTS.md), round 4.
- The trailer and the teaser loop at the top were filmed for v0.1.0, before these fixes (the trailer's umbrellas still lie on the shelf, its lunch tin was sealed, and there are no nudges). The screenshots were re-cut in round 5 from new takes of the current build (`make_trailer.py stills --takes r5_`), and the ledger's again in round 6 from the AutoPilot, for its new layout; re-cutting the trailer is the owner's call.

## License

No license has been chosen yet, so all rights are reserved by the author for now.
