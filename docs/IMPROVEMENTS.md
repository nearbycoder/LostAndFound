# Lost & Found: improvement plan (post-v0.1.0)

Written 6 October 2026 on the `improvements` branch. This round's job is to find what most raises the
game's quality for a real player. Everything below comes from the code, the automated checks run on
this date, the AutoPilot's screenshots, and the logs of the filmed takes behind the trailer.

## Baseline (6 Oct 2026)

| Check | Command | Result |
|---|---|---|
| Linux build and content validator | `Tools/unity.sh build-linux` | **Pass.** Build succeeded, 0 errors, 283 MB on disk. Validator: 28 objects, 5 days, 25 cases, 0 issues. |
| AutoPilot, `best` | `Tools/unity.sh autopilot 4 1 best` | **Pass.** 24 cases, 24 best, 0 skipped, ending *The 9:40*, 0 problems, 177 s, no exceptions. |
| AutoPilot, `worst` | `Tools/unity.sh autopilot 4 1 worst` | **Pass.** Ending *Grey Ninefold*, 1 best, 5 skipped ("it isn't here any more" vignettes), 0 problems, 154 s. |
| AutoPilot, `wait` | `Tools/unity.sh autopilot 4 1 wait` | **Pass.** Ending *The Long Wait*, 23 of 24 best, 0 problems, 166 s. |
| EditMode tests | `Tools/unity.sh test` | Runs, but there are **0 tests**: `testcasecount="0"`, reported as "Passed". |
| Smoke | `Tools/unity.sh smoke 30` | **Pass.** No exceptions, Vulkan. 36–55 fps uncapped at 1600×900 on the Radeon 8060S, but the machine's load average rose from about 0.4 to 53 during the run, so treat the frame rate as unreliable. |
| Filmed takes (existing logs in `Recordings/*/player.log`, not re-filmed) | `Tools/unity.sh film …` | Every take finished, but **9 hidden details were logged as `couldn't find`** (see item 1). |

What the AutoPilot screenshots (`Screenshots/autopilot/`) show:
- The bottom-of-screen hint is **unreadable whenever it sits over the claim slip**. In counter view the
  slip fills the bottom centre of the screen, and the hint is cream italic with a thin outline. Example:
  case 1.3, *"Something feels off? Click a finding on the claim slip to ask about it."*, which is the
  only hint that teaches asking. The object's name on the inspect bar has the same problem ("Silver
  locket", "Ring in a velvet box").
- The ledger, dialogue boxes, the characters at the window, and the photograph sequence all read well.

## Ranked improvements

Impact is for a real player picking up the release. Effort: S is under half a day, M is about a day,
L is several days. Risk covers regressions and things that can't be verified on this machine.

| # | Improvement | Impact | Effort | Risk |
|---|---|---|---|---|
| 1 | Make every hidden detail reachable in the hand | High | M | Low–Med |
| 2 | Keep hints and inspect titles legible over the slip | High | S | Low |
| 3 | Keep Agnes's rules and your progress at hand during a case | Med–High | S–M | Low |
| 4 | Real EditMode tests for the rules, story and save | Med | S–M | Low |
| 5 | macOS release build, app identity, release packaging | Med–High | M | Med |
| 6 | Windows release build | High | S once unblocked | **Blocked** |
| 7 | Graphics quality preset and frame cap | Med | S–M | Low |
| 8 | "Continue" resumes mid-day, not from the morning | Med | M | Med |
| 9 | Optional nudges for a stuck player | Med | M | Med |
| 10 | Curio Ledger: see which secrets you've found | Med | M | Low |
| 11 | Text size / UI scale setting | Med | S–M | Low–Med |
| 12 | Gamepad support (virtual cursor) | Med | L | Med |
| 13 | WebGL build | Med–High reach | L | High |
| 14 | Voice and music quality | Med | L | Med (subjective) |
| 15 | Smaller download | Low | S | Low |

### 1. Make every hidden detail reachable in the hand
**Evidence.** The filmed takes turn each object up to 10 times looking for each detail. When that fails,
the recorder logs a warning and quietly calls `DiscoverForDemo`, so the footage looks fine. The logs
record these failures:

| Object | Detail | Takes | Why it matters |
|---|---|---|---|
| Green duck umbrella | `beak` (chipped beak) | day1, day2 | Case detail on Mon and Tue |
| Battered suitcase | `sachet` | day1 | Case detail (Mrs Marsh) |
| Silver locket | `photo`, `note`, `hair` | day2 | **`note` decides the twins case (2.2)** |
| Violin case | `setlist`, `mother` | day4, grey | Case detail, plus the secret |
| Ring in velvet box | `ticket` | day4, grey | Its UV date (14.X.21) is the 1921 proof |
| Birdcage | `feather` | grey | Secret |

A person turns things more cleverly than 10 scripted drags, but the README already lists "hotspots that
are hard to bring into view". A case-deciding detail that can't be reached breaks the game's central
promise that every case can be solved from the desk. The content validator assumes every detail is
discoverable and never checks that physically.

**Likely causes** (to confirm with the audit): `InspectController.Occluded` ends its ray 4 mm short of
the hotspot in *world* units, but held items are scaled by up to 3.5×, so a hotspot sitting just under
its own surface counts as occluded. The facing test (`dot ≥ 0.12`) also fails for details inside lids
and linings that point into a cavity. The pan limit may also stop you reaching the ends of long items.

### 2. Keep hints and inspect titles legible over the slip
`HintBar` draws cream italic text with a thin outline at the bottom centre, exactly where the claim slip
sits in counter view. Only the inspect bar has a dark band behind it, and that band is too light over
the slip's paper. The onboarding hints that teach asking and two-claimant stamping are the worst
affected.

### 3. Keep Agnes's rules and your progress at hand
- The plan (§3) promised the rules on "a card pinned to the desk that you can read on hover at any
  time". In the build they're two clicks deep: Esc, then "Agnes's rules". From Wednesday a case can
  depend on rules 1 to 7.
- Nothing tells you whether you've found everything on an object. `CaseRecord.detailsFound/Total` is
  recorded but never shown, and the plan's "thoroughness" stat (§4.5) never reached the ledger.
  Without it, a player who can't find a detail (see item 1) can't tell whether to keep looking.

### 4. Real EditMode tests
`Assets/Game/Tests/EditMode` is an empty assembly, and `Tools/unity.sh test` "passes" with 0 tests.
The rules solver, `ApplyVerdict` flags, ending selection, scoring and save round-trip are all pure C#
and can be tested in about a second without the player. Today they're only covered by the 3-minute
AutoPilot and the validator.

### 5. macOS release build, app identity, release packaging
- macOS build support is installed. `BuildScript` only has `BuildLinux`, and nothing in the game code
  is platform-specific: saves use `persistentDataPath`, and only the debug recorder runs ffmpeg.
- The Standalone bundle identifier is still the URP template's
  (`com.Unity-Technologies.com.unity.template.urp-blank`). On macOS it becomes the app's
  `CFBundleIdentifier` and its prefs/save domain, so it has to change *before* the first Mac release.
- No app icon is set (`m_BuildTargetIcons: []`), so the Linux window and a Mac dock would show Unity's.
- Releases are zipped by hand. A script that builds and zips each platform with a predictable name
  would make the next release repeatable.
- **Can't be verified here:** there's no Mac to run it on, and an unsigned, un-notarised app needs the
  player to right-click and choose Open (or run `xattr -dr com.apple.quarantine`). Notarisation needs
  an Apple Developer account. The README would have to say both.

### 6. Windows release build (blocked)
Windows is probably the largest audience the blog's "Windows · macOS · Linux" promises. Windows
Build Support isn't installed for 6000.6.2f1, and installing it is the owner's call (Unity Hub →
Installs → Add modules). With the module installed it's a `BuildWindows` method next to item 5's,
plus a smoke run under Wine/Proton at most, since there's no Windows machine.

### 7. Graphics quality preset and frame cap
There's a single quality level with 2048 soft shadows (quality 3), additional-light shadows, HDR, and
Gaussian depth of field. Settings only offer "Film effects". On a weaker laptop (or an Intel Mac) there's
no way to trade quality for frame rate. A Low/Medium/High preset (render scale, shadow quality and
resolution, DoF) is cheap and safe.

### 8. "Continue" resumes mid-day
`DayRoutine` reloads the day-start snapshot, so quitting after case 4 of 5 replays the morning and all
four cases. A day takes about 12 minutes. Fixing it means saving the case index with a mid-day state and
skipping finished cases on load. The desk already rebuilds from `StoryState`, but the day's
already-discovered clues and a few one-shot flags need care.

### 9. Optional nudges for a stuck player
The tutorial only covers case 1.1. After that, a player who can't find the object or the deciding
detail has no help. An opt-in "Agnes's nudges" setting could escalate after inactivity: which drawer or
shelf, "there's more inside it", then the rule that applies. That needs design care to stay in tone and
not spoil anything.

### 10. Curio Ledger
The title only shows "Curiosities found: N of 25". A page listing found secrets, with blanks naming the
object and day for missing ones, gives a reason to replay days.

### 11. Text size / UI scale
The UI scales from 1920×1080, so at 1280×720 the inspect controls strip (19 pt) and ledger notes get
small. A "Larger text" option would scale the canvas. Hand-placed layouts need checking for overflow.

### 12. Gamepad support
The Input System is installed, but every interaction is point-and-click on 3D objects. That needs a
stick-driven virtual cursor with snapping to interactables, plus bindings for turning, inspecting and
stamping. It's worth doing for Steam Deck if the game goes on Steam, but not this round.

### 13. WebGL build
The game would suit a browser (no timers, mouse only), and WebGL support is installed. But the build is
283 MB uncompressed (about 180 MB of models, music and audio), it uses soft shadows plus HDR post on
WebGL2, and an IL2CPP/emscripten build is a long, heavy job on a shared machine. It needs a spike to
measure download size and frame rate before anyone promises it.

### 14. Voice and music quality
The synthesised formant voices and score are distinctive but not studio quality (README). Improving
them is open-ended and subjective. Leave it for a dedicated round.

### 15. Smaller download
The Linux zip is 137 MB. Music is already Vorbis and streamed. Lowering Vorbis quality on music, and
checking whether the debug-only code and assemblies are worth stripping, might save 20–40%. Low
priority.

## Proposed scope for this round

Five items plus one stretch goal. Every one can be verified on this machine, apart from running the
Mac build.

### A. Every hidden detail reachable (item 1)
- Add a hotspot audit (`-lafAuditHotspots`, run via `Tools/unity.sh audit`) to the built player. For
  every object, with its parts opened as needed, it sweeps a grid of hand orientations and zoom levels
  and tests each detail with the **same** `FindNearDetail`/`Occluded` code a player's click uses. It
  writes a coverage table to `Logs/hotspot_audit.log`.
- Fix the causes the audit finds: scale the occlusion tolerance with the held item, check the facing
  test against cavities, and as a last resort move the `HS_` empty in `ArtSource/build_objects.py` and
  re-export through the existing Blender pipeline.
- Make the filmed play's fallback count as a failure: the recorder summarises "N details needed the
  fallback" at the end.

**Acceptance:** every non-part-revealed detail (case and secret) is clickable from at least 10% of the
sampled orientations, at default or closer zoom. Re-filming days 1, 2 and 4 (takes go to the
gitignored `Recordings/`; the trailer isn't re-cut) logs **0** `couldn't find` warnings. The validator
and AutoPilot `best` still pass.
**Verify:** the audit log before and after, the three filmed takes' logs, and a look at a few frames
of the previously failing details.

### B. Legible hints over the slip (item 2)
- Give the hint bar a dark band behind it, sized to the text, and make the inspect bar's band strong
  enough over paper. Alternatively, move the persistent hint above the slip in counter view.

**Acceptance:** in AutoPilot screenshots of case 1.3 (window), 2.2 (inspect) and 4.5 (inspect), the
hint and object name are fully readable, with contrast of at least 4.5:1 against their local background
(measured by sampling the screenshot). Nothing else in the UI moves.
**Verify:** before and after screenshot pairs, and the measured contrast.

### C. Rules and progress at hand (item 3)
- A rules card on the desk (a small card by the calendar, which is where the plan put it). Hover it to
  read the currently known rules in Agnes's hand, using the existing `RulesCard` content. Also bind
  `R` to open it from anywhere, including while holding an object.
- The inspect bar shows "Findings 2 of 3" for the held object's case details (secrets aren't counted,
  to keep them secret), and the Day Ledger shows each case's findings count.

**Acceptance:** from any view on any day, the known rules can be read with one hover or one key, and
the card never lists a rule before it's unlocked (checked against `RulesKnownAt`). The counter matches
`CaseDetails` discovered, and the ledger numbers match `CaseRecord`. The README controls table is
updated.
**Verify:** AutoPilot `best` still passes, plus screenshots of the card on Monday (1 rule) and Friday
(8 rules) and of the counter mid-inspect. Unit tests from D cover the rule list.

### D. EditMode tests (item 4)
- NUnit tests in `Assets/Game/Tests/EditMode` covering: the content validator returns 0 issues;
  `Rules.Solve` reproduces every authored best verdict; whole-week simulations through `ApplyVerdict`
  reach *The 9:40*, *The Long Wait* and *Grey Ninefold* under the three AutoPilot policies; `Score`
  thresholds; `RulesKnownAt` day by day; and a `SaveGame` JSON round-trip.

**Acceptance:** `Tools/unity.sh test` reports at least 15 tests and 0 failures in under 2 minutes. A
deliberately broken verdict in a scratch copy of the content makes a test fail. The README's
"no unit tests" lines are rewritten to match.
**Verify:** `Logs/test-results.xml`.

### E. macOS build and release packaging (item 5; sets up item 6)
- Set a real bundle identifier (proposed: `com.nearbycoder.lostandfound`; **owner to confirm**) and an
  app icon generated by a script in `Tools/textures/`, committed like the other generated textures.
- Add `BuildScript.BuildMac` (Mono, Intel + Apple silicon), plus `BuildWindows` behind a check that
  fails with a clear message while the module is missing. Add `Tools/unity.sh build-mac` and
  `Tools/package_release.sh`, which zips each built platform to `dist/LostAndFound-<version>-<platform>.zip`
  and keeps the executable bit and the `.app` structure.
- Update the README's "Play it": how to open an unsigned Mac app, and that it hasn't been run on Mac
  hardware.

**Acceptance:** `build-mac` succeeds headless. The `.app` contains `Info.plist` with the new identifier,
version 0.1.0 and the icon, plus a universal (x86_64 + arm64) Mach-O executable, checked with `file`
and `plutil`/Python's `plistlib`. The Linux build still passes. No release is published.
**Verify:** build logs, a file listing of the bundle, the plist dump, and `file` output.

### F. Stretch: graphics quality preset (item 7)
- Settings get "Picture quality: Low / Medium / High", which sets render scale, soft-shadow quality,
  shadow resolution and depth of field at runtime through the URP asset. The default stays High.

**Acceptance:** switching takes effect without a restart and persists across launches. On a quiet
machine, the smoke run's fps on Low is measurably higher than on High.
**Verify:** two smoke runs, with load average noted, and screenshots of each preset.

## Needs a decision from the owner
- **Windows:** install Windows Build Support for 6000.6.2f1 (Unity Hub → Installs → 6000.6.2f1 → Add
  modules) if a Windows build is wanted. Until then the blog's "Windows" claim has no build behind it.
- **macOS:** confirm the bundle identifier, and whether an unsigned, un-notarised Mac build is acceptable
  to publish. Signing needs an Apple Developer account.
- **WebGL:** worth a measurement spike in a later round?
- Releases stay unpublished. This round only builds and packages locally.

## Round 1 results (6 Oct 2026)

All six items shipped on `improvements`. Screenshots are in [`media/improvements/`](media/improvements/).
Final checks on the final build: Linux build and validator pass (0 issues). The AutoPilot passes
`best` (24/24, *The 9:40*), `worst` (*Grey Ninefold*) and `wait` (*The Long Wait*) with 0 problems.
`Tools/unity.sh test` passes 42/42, and `Tools/unity.sh audit` passes.

| Item | Result | How it was verified |
|---|---|---|
| **A. Every hidden detail reachable** | The audit found the real problems, which weren't the ones the filmed takes had flagged. **The lunch tin was sealed shut** by a solid rim slab, so its sandwich and note (case 4.1's evidence) were never visible. **The frosted tin was stored in a slot the shelf doesn't have**, so it landed inside the hatbox and couldn't be picked up (cases 3.3 and 3.5). The violin case lay over the lunch tin, and in a full drawer the spectacles hid behind the toy rabbit. Glass blocked the snow globe's figure. All are fixed. The filmed play now aims at each detail through the real picking code and checks that a lid actually opened. | Audit: 84/84 details ≥ 10% of orientations (lowest: the ring ticket's UV date, 14.1%); every stored object on every day hoverable from ≥ 10% (lowest: the black umbrella, 22%). Re-filmed all five days: **53 details found by hand, 0 fallbacks, 0 missed pick-ups**. The original takes logged 16. Before and after: `lunch_tin_*.jpg`, `shelf_thursday_*.jpg`, `shelf_wednesday_after.jpg`. |
| **B. Legible hints over the slip** | A dark pill sized to the text behind the hint, the object's name, the part hint and the controls. | Contrast of the text against the median of the region behind it, on AutoPilot screenshots: case 1.3 hint 1.0 → **6.5:1**; case 2.2 title 1.1 → **5.7:1**, hint 1.5 → **8.7:1**; case 4.5 title 1.1 → **5.6:1**. `hint_*.jpg`, `inspect_title_*.jpg`. |
| **C. Rules and findings at hand** | Agnes's card on the felt beside the slip: hover to read, click or `R` to hold it up (`R`/`Esc` puts it back, without also putting the object down). It lists only the rules actually handed over; between cases, the old pause-menu card listed rules from cases not yet played (rules 2 and 3 on Monday morning). "findings n of m" on the inspect bar; per-case and per-day counts in the ledger. | AutoPilot screenshots of the card on each day's first case (Monday shows rule 1 only; Friday shows all 8), plus a filmed frame of the counter. `rules_card_*.jpg`, `findings_counter.jpg`, `ledger_findings.jpg`. |
| **D. Unit tests** | 42 EditMode tests (the acceptance asked for at least 15), about 0.2 s plus editor start-up. Ending choice and scoring moved into `Rules`. | `Logs/test-results.xml`: 42 passed. Making case 1.3's best verdict wrong in a scratch edit failed 4 tests; the content was restored afterwards. |
| **E. macOS build and packaging** | Bundle id `com.nearbycoder.lostandfound`, generated app icon, `build-mac`, `build-windows` (fails clearly without the module), and `Tools/package_release.py`. | `build-mac` succeeds (297 MB). The executable is a universal Mach-O (x86_64 + arm64); `Info.plist` has the id, version 0.1.0 and the icon (checked with `plistlib`); the `.icns` contains the new icon at 128–1024 px. The Linux zip, unzipped with `unzip`, runs a clean smoke test. **Not run on a Mac.** |
| **F. Picture quality** | Low / Medium / High in Settings: render scale, shadow resolution and softness, anti-aliasing, and depth of field on Low. | Uncapped smoke runs on this machine (load average 24–34 from other sessions): High 134 and 123 fps, Medium 160, Low 220 and 212, 0 errors. The setting switches live mid-run and the next launch starts on it (the prefs file was backed up and restored afterwards). `quality_*.jpg`, `settings_picture_quality.jpg`. |

Also fixed along the way: names on the slip and in the ledger ("Returned to Mr" is now "Returned to Mr Plum"),
and the filmed play's lid handling.

### Still open
- **Shelf space at maximum occupancy.** If the player refuses everything, the shelves can't hold it all: the suitcase overlaps the violin case (Thursday and Friday), the briefcase overlaps the birdcage (Friday), and the umbrellas, longer than their board, poke through the Iron Drawer and the cabinet side. These were there before this round. Everything stays clickable (the audit checks), but fixing the look needs new shelf geometry in `build_booth.py` (for example an umbrella stand), which was bigger than this round.
- How findable the details are **for people** is still untested. The audit proves reachability, not how quickly someone finds them.
- macOS is unverified on hardware. Windows is blocked on the module. Gamepad, mid-day resume, the Curio Ledger and text size are next in the ranked list above.
- The trailer and README screenshots predate these fixes. The trailer's Thursday was filmed with the sealed lunch tin.

## Round 2 scope (6 Oct 2026)

Five items, in order of risk (gamepad last). Branch `improvements-2`, one commit per item.
Screenshots go in [`media/improvements/round2/`](media/improvements/round2/).

### R2-A. Shelves that never overflow (round 1's open issue)
If you refuse everything, the shelves overflow: the suitcase overlaps the violin case and the
briefcase overlaps the birdcage. Those pairs are never needed on the same day, so:
- **Unclaimed strays go to the basement.** Once every case that wants an object is past, the object
  stops appearing in storage from the next morning, and Gus mentions it. Decoys (never claimed) stay
  all week, because they're what you search past. The object's recorded location doesn't change, so
  story conditions are unaffected.
- **The umbrellas move clear of the Iron Drawer.** At 0.87 m they're longer than their board, so the
  spare length runs out of the side of the cabinet, where the side panel hides it from the shelf view.

**Acceptance:** with nothing returned (the audit's fullest case), `Tools/unity.sh audit` reports
**0 overlap warnings** and 0 problems on every day. A unit test checks that no object leaves before its
last case and decoys never leave. All three AutoPilot policies still pass with their endings.
**Verify:** audit log, tests, AutoPilot, and screenshots of the shelf on Thursday and Friday plus the
counter view (no umbrella ends showing).

### R2-B. "Continue" resumes mid-day
Quitting after case 3 of 5 currently replays the morning and all three cases.
- The save records how many of today's cases are done. *Continue* restores the state after the last
  verdict and goes straight to the next case (today's rules and story visuals included).
  *Start the day again* and *Choose a Day* still begin from the morning snapshot.

**Acceptance:** quit after case 2 of Tuesday (AutoPilot `-lafQuitAfter 2.2`), relaunch with
*Continue* (`-lafContinue`): case 2.3 is the first case played, Tuesday's morning isn't replayed,
and the week ends 24/24 on *The 9:40*. Unit tests cover the save bookkeeping.
**Verify:** the two AutoPilot logs, tests, and the AutoPilot's screenshot of the first resumed case.

### R2-C. The Curio Ledger
The title says "Curiosities found: N of 25", but you can't see which. A **Curiosities** page off the
title lists all 25 objects. Found secrets are written out in full. Missing ones show the object and
the day it turns up, so a replay has a target without the secret being spoiled.

**Acceptance:** the page's count matches the title's and the save; it fits 25 rows at 1280×720.
A unit test covers the counting.
**Verify:** screenshot with a save holding some secrets (from a filmed day), and tests.

### R2-D. Larger text option
Settings, "Text size: Normal / Large". Large scales the screen-space reading UI by 1.25×:
dialogue, hint bar, inspect bar, tag card, Agnes's notes and the rules card.

**Acceptance:** at 1280×720 on Large, the dialogue, a tag card, the inspect bar and the hint are all
on screen, unclipped and unoverlapped. The setting applies at once and persists.
**Verify:** screenshots at Normal and Large (AutoPilot with a non-saving `-lafTextSize` override).

### R2-E. Gamepad support
The Input System is installed, but the game is mouse and keyboard only.
- The left stick drives an on-screen cursor (drawn by the game, with the same context icons), which
  slows near anything clickable.
- A = click / pick up, B = put down / back, X = on the tray, Y = Agnes's rules, the right stick turns
  the held object, the triggers lean in and out, LB/RB turn to the drawers and shelf, View = slip,
  Menu = pause, the d-pad's up = blue lamp.
- The mouse takes over again the moment it moves.

**Acceptance:** a scripted run with a **virtual** Input System gamepad, and no mouse or keyboard
events, completes Monday's first case: ring, open drawer A, pick up the wallet, open it, find the
photo, put it on the tray, stamp RETURN. It logs PASS with the right verdict. The README lists the
controls. **No physical controller is available here, so it's untested on real hardware**, and the
README says so.
**Verify:** the run's log and screenshots of the gamepad cursor.

## Round 2 results (6 Oct 2026)

All five items shipped on `improvements-2`. Screenshots are in [`media/improvements/round2/`](media/improvements/round2/).
Final checks on the final build: the Linux build and validator pass (0 issues), as do the audit (84/84 details,
0 storage problems, **0 overlap warnings**) and unit tests (45/45). The AutoPilot passes all four policies:
`best` (24/24, *The 9:40*), `worst` (*Grey Ninefold*), `wait` (*The Long Wait*) and `refuse` (everything
refused). So does the gamepad test, and Monday re-filmed through the simulated mouse is clean (11 details by
hand, 0 fallbacks, 0 missed pick-ups).

| Item | Result | How it was verified |
|---|---|---|
| **R2-A. Shelves that never overflow** | Unclaimed strays go to the basement the morning after their last case, and Gus says how many; decoys stay all week. The umbrellas stand in a new **umbrella stand** (`build_props.py`) on the floor beside the shelves, handles up. The suitcase sits fully inside the cabinet. Also fixed, a **round-1 bug**: refused shelf objects flew back to the bare slot, ignoring their turn and shift, which recreated the overlaps. | Audit with nothing returned: 0 overlap warnings on every day (round 1 left 3); everything hoverable (lowest the duck umbrella, 23%). A new unit test covers the archive rule. The new `refuse` AutoPilot policy logs and photographs Gus's line each morning (`gus_basement.jpg`). Shelf screenshots for Monday, Thursday and Friday at their fullest. The re-filmed Monday picks the duck umbrella out of the stand by hand. |
| **R2-B. Continue resumes mid-day** | The save records how many of today's cases are decided, in the same write as each verdict. *Continue* restores that state and goes to the next claimant, skipping the morning but keeping today's rules. The title says "Continue · Tuesday, claimant 3 of 5". Old saves start the morning, as before. | Quit right after 2.2's verdict (`-lafQuitAfter 2.2`), then `-lafContinue`: no Tuesday morning in the log, 2.3 was the first case, and the week finished "24 best of 24 decided" on *The 9:40*. The resumed rules card shows rules 1–5. Unit tests cover the count and an old save. `continue_title.jpg`, `continue_resumed_case_2_3.jpg`. |
| **R2-C. Curio Ledger** | A **Curiosities** page off the title: found secrets written out with a tick; missing ones name the object and the day it turns up. | Screenshot at 1280×720 with a save holding the seven secrets the filmed days found: all 25 rows fit, "7 of 25 found" (`curio_ledger_1280x720.jpg`). A unit test checks the count (25, case details don't count, arrival order). |
| **R2-D. Larger text** | Settings, "Large text": 1.25× on the dialogue, hint, inspect bar, tag card and notes (1.15× on the rules preview). The bubble and tag card are placed by their scaled size. | Full AutoPilot weeks at 1280×720 on Normal and Large (both 24/24). The Large screenshots show the dialogue, tag card, inspect bar and hint on screen, unclipped and not overlapping. Toggling it mid-run scaled the hint ×1.25 at once and the next launch started Large (prefs backed up and restored). |
| **R2-E. Gamepad** | A drawn cursor on the left stick that slows over clickables and drives a virtual mouse (A/B buttons, triggers scroll). Buttons map to keys: LB/RB turn, X tray, Y rules, View slip, Menu pause, d-pad lamp and ring/advance. The right stick turns the held object. Pad prompts appear while the pad is in use, and the mouse takes over when moved. | `Tools/unity.sh padtest`: a virtual gamepad alone plays case 1.1 to RETURN→Walter (best), with 0 keyboard/mouse events during play, then a moved mouse takes control back. PASS on two runs. The test found and fixed a bug: with two mice (say a touchpad and a USB mouse), only the first could take control back. **No physical controller tested.** `gamepad_*.jpg`. |

### Still open after round 2
- Gamepad on real hardware (and Steam Deck) is untested.
- The recorder and AutoPilot are the only "players": no human playtest yet, so how findable details are and how
  the pacing feels are unmeasured.
- macOS is unverified on hardware; Windows is blocked on the module. The trailer predates both rounds.
- Next in the ranked list: optional nudges for a stuck player (item 9), a WebGL spike (13), audio (14), and a
  smaller download (15).

## Round 3 scope (6 Oct 2026)

Branch `improvements-3`, one commit per item. Screenshots go in
[`media/improvements/round3/`](media/improvements/round3/). Items are in build order. The WebGL spike
comes last because it's the heaviest job on a shared machine and the least certain.

### R3-A. Tool runs never touch the real save or settings
Every player run (smoke, AutoPilot, audit, padtest, film) passes `-lafSave`, but Unity still writes its
prefs file (session counters, window size, and any setting a run changes) to the player's real
`~/.config/unity3d/Nearby/Lost & Found/`. A session on another game overwrote a real save last round.
- `Tools/unity.sh` runs every built player with `XDG_CONFIG_HOME` pointed at a scratch folder under
  `Logs/` (a probe showed Unity's Linux player honours it). It also hashes the real folder before and
  after each run, and fails loudly if anything there changed.

**Acceptance:** smoke, AutoPilot, audit and padtest runs leave the real folder's hashes unchanged
(today each run rewrites `prefs`). Their scratch prefs land under `Logs/`.
**Verify:** hashes before and after a full round of runs, and the guard's log line.

### R3-B. Agnes's nudges for a stuck player (ranked item 9)
After the tutorial, a player who can't find the object or the deciding detail gets no help.
- **`H`** (gamepad: d-pad ←) during a case shows a small note in Agnes's hand at the top right.
  It doesn't block clicks. Each press goes one step further for wherever you are in the case:
  1. **Find:** what to look for, then where (the drawers, the shelf or the stand), then exactly which drawer.
  2. **Examine:** that there's more to find and how (open it, the blue lamp, hold it to your ear, turn it
     right round). Then *show me* (R3-C).
  3. **Ask:** that a finding on the slip can be put to them, then which one.
  4. **Decide:** the rule that applies, then what to compare (which claim against the tag or the object).
     Then how stamping works (tray first for RETURN and SEAL).
- Nudges never name the verdict outright. They only cite rules Agnes has already handed over, and never
  quote a finding you haven't found.
- After 90 seconds in a case without progress (no new finding, pick-up, question or stamp), the hint bar
  offers one: "Stuck? Press H for a nudge from Agnes". A Settings toggle, *Offer nudges when stuck*
  (on by default), turns the offer off. `H` works either way.
- The logic is pure C# (`Nudges` in Core), so it's unit-tested without the player.

**Acceptance:**
- Unit tests walk every case on the `best` and `worst` paths through each stage. Every stage gives at
  least one nudge, and every cited rule is known by then. No nudge contains the fact of a detail not
  yet found. The *decide* nudge agrees with `Rules.Solve`: it names the contradicted claim or claimant,
  or says the story matches.
- In the player, an AutoPilot `-lafNudgeTour` asks for every nudge at every stage of every case of the
  week, logs them, and still ends 24/24 on *The 9:40*.
- The padtest presses d-pad ← and a nudge appears.
- `docs/nudges.md` lists every nudge's text for the owner to review the tone.

**Verify:** test results, the tour log and screenshots, the padtest log.

### R3-C. "Show me": the last nudge points
The last *find* nudge glows the drawer, then the object, the way the tutorial does. The last *examine*
nudge puts a glint on the hidden detail of the held object, using the same visibility test as a
click, so it only glints when a click there would work ("Turn it over until it glints" otherwise). If a
lid or catch reveals the detail, the glint lights that part instead.

**Acceptance:** tour screenshots of a drawer glow, a detail glint, a part glow, and the glint on the
ring ticket's UV date (the hardest detail in the audit). The glint shows only when the detail is
clickable: the tour clicks at the glint and the detail is discovered.
**Verify:** screenshots and the tour log (`glint → discovered` for every case detail it points at).

### R3-D. WebGL spike (ranked item 13): measure, don't ship
Time-boxed. Build a WebGL player into the gitignored `Builds/WebGL/` and measure: build time, download
size (raw and compressed), whether it loads and plays in Firefox on this machine, the frame rate, and
what breaks (saves, the shutter, audio streaming, shaders). Nothing is hosted and no settings that
change the desktop builds are committed.

**Acceptance:** the numbers and a go/no-go recommendation in "Round 3 results". The Linux build is
unchanged afterwards. If it can't be built within the time box, the reason is recorded instead.
**Verify:** build log, `du`, and a browser screenshot if it loads.

### R3-E. Stretch: a smaller download (ranked item 15)
Meshes are 45% of the build (86 MB, imported uncompressed) and UI textures are uncompressed
(the ledger page alone is 5 MB). Try mesh compression and compressing the large UI textures, keeping
anything that changes how the game looks or plays out.

**Acceptance:** the Linux zip shrinks measurably. The audit still passes 84/84 details with no overlap
warnings, and before-and-after screenshots of the ledger and a close-up object look the same.
**Verify:** zip sizes, the audit, screenshots.

## Round 3 results (6 Oct 2026)

R3-A to R3-E all landed on `improvements-3`. R3-B and R3-C share their code, so they're one commit.
Screenshots are in [`media/improvements/round3/`](media/improvements/round3/). Final checks on the final
build: Linux build and validator pass (0 issues); 53/53 unit tests; the audit passes (84/84 details, lowest
the ring's UV date at 14.1%, 0 overlap warnings, 0 storage problems). All four AutoPilot policies pass: `best` (24/24,
*The 9:40*), `worst` (*Grey Ninefold*), `wait` (*The Long Wait*) and `refuse`. The nudge tour and the
padtest pass too. Every player run printed `[guard] real save and settings untouched`.

| Item | Result | How it was verified |
|---|---|---|
| **R3-A. Tool runs never touch the real save or settings** | Every built-player command in `Tools/unity.sh` runs with `XDG_CONFIG_HOME` under `Logs/config/<command>/`. Unity's prefs go there, and so do any persistentDataPath writes. The real folder is hashed before and after, and the run fails with `[guard] … CHANGED` if anything there changed. | A probe run and every run since: the real save and prefs hashes are unchanged, and the scratch prefs appear under `Logs/`. A fake "real" folder modified during a run made the guard fail with status 99. |
| **R3-B. Agnes's nudges** | `H` (d-pad left) during a claim pins a nudge at the right of the screen without blocking clicks. It gets more specific with each press, through *find*, *examine*, *ask* and *decide*, then how stamping works. The hint bar offers a nudge after 90 s without progress; a Settings toggle turns the offer off. The note steps down below a lingering speech bubble and hides while someone is speaking. All 210 nudges on the best path are in [nudges.md](nudges.md), for review. | 8 new EditMode tests (53 in all). Every claim of the `best` and `worst` weeks is followed nudge by nudge to a decision, in keyboard and pad wording. The tests check that only known rules are cited, no unfound fact is quoted and no stamp is named, and that the decision nudge agrees with `Rules.Solve`. A deliberate leak (an unfound fact in a nudge) failed 6 tests. `Tools/unity.sh nudgetour` asked for all 210 in the player and still finished 24/24 on *The 9:40*. It saw the offer after 90 s. The padtest's d-pad left gives "Try the drawers on your left (LB)". The tour passes at 1280×720 with Large text too. |
| **R3-C. "Show me"** | The last nudge of a stage lights up the drawer, then the object. For a detail, it lights up the lid or catch that reveals it, or puts a glint on the spot. The glint uses the same distance, facing and occlusion test as a click, so it only shows where a click works. | The tour followed every *examine* nudge: **21 details found by clicking where the glint was** (in a frame it was showing), **2 by working the part that lit up**. Each glint stayed lit 30 of 30 frames held still. The ring ticket's UV date, the audit's hardest detail, was found the same way (`case4.5_nudge_glint_date.jpg`). |
| **R3-D. WebGL spike** | `Tools/unity.sh build-webgl` and `Tools/serve_webgl.py`. Measured: about **20 minutes** to build here (IL2CPP and emscripten, under load). An **89 MB download** (Brotli: data 81.6 MB, wasm 7.3 MB, 159 MB decompressed; before R3-E's compression). The title loads and draws correctly in headless Chrome about 3 s after the page on localhost. That browser renders with **SwiftShader (software)**, at 0.5 fps, so **real-GPU frame rate is unmeasured**. Things that break or need work: URP's depth-of-field shaders are stripped (no film blur), audio logs `getFrequency() is not supported for compressed sound`, the music sources warn about their filters, and saves go to the browser's IndexedDB, which nobody has checked. The build also rewrites URP's prefiltering in `Mobile_RPAsset.asset` and leaves a `Data/` folder at the project root; both were reverted and removed, and the method's comment says to. | Build log, file sizes, a server log of the requests, and `webgl_title_swiftshader.jpg`. The Linux build afterwards: 283 MB, validator clean, smoke run 0 exceptions, nothing left modified in git. |
| **R3-E. Smaller download** | Models import with medium mesh compression, and the paper and card UI textures as BC7 (cursors stay exact). Meshes in the build go from 86.5 to 30.9 MB, and the **Linux zip from 142.8 to 111.0 MB (−22%)**. | The audit is unchanged (84/84, lowest 14.1%, 0 overlaps). The `best` AutoPilot and the nudge tour pass with every glint steady. Side-by-side crops of the record, the birdcage and the ledger look the same (`smaller_download_*_before_after.jpg`). |

### Recommendation on WebGL (for the owner)
Worth a second, GPU-backed look, not a release yet. The download is reasonable (about 89 MB, smaller again
after R3-E), and the title works. But nothing here can measure real frame rate, the film look loses its depth
of field, the audio warnings need chasing, and browser saves need testing. The next step is someone opening
the build in desktop Chrome or Firefox with a GPU (`python3 Tools/serve_webgl.py Builds/WebGL`, then
`http://127.0.0.1:8764`) and playing Monday. Hosting it anywhere is the owner's call.

### Found along the way
- **The padtest failed three times in a row on a busy machine** (load average 30–40): once a press of A on
  the bell went unseen, and twice a d-pad press didn't dismiss Agnes's first note. Its presses now last at
  least three frames as well as 0.12 s, and it saves a screenshot and the UI state when it fails. It then
  passed four times, including twice at load 112. The note failure was never reproduced, so its cause isn't proven.
- During one baseline comparison the guard itself was stashed. That run used the real prefs folder: the
  `prefs` file's contents were unchanged (same hash) and its timestamp was restored from a backup.
  `TestResults.xml` in that folder is rewritten by the editor's test runner on every `Tools/unity.sh test`.
  That's Unity's test framework, not the game's save or settings.
- The guard covers built players, not the editor. Batch editor runs (`build-linux`, `test`) rewrite the real
  `prefs` file with **identical contents**: same hash, new timestamp. The guard can't redirect the editor
  without risking its licence files, which live under the same config folder. The save file was never
  touched. Both timestamps were put back from a backup at the end.

### Still open after round 3
- Nudges have only been read by their author and followed by the AutoPilot. Whether their tone and pacing
  help a real stuck player needs a person. [nudges.md](nudges.md) is there for a read-through.
- WebGL frame rate on a real GPU, audio, browser saves (above). Gamepad on real hardware, macOS on a Mac,
  Windows (blocked on the module), and a human playtest are all still open.
- The trailer and README screenshots predate rounds 1–3.
- Voice and music quality (ranked item 14) is still for a dedicated round.

## Round 4 scope (6 Oct 2026)

Branch `improvements-4`, one commit per item. Screenshots go in
[`media/improvements/round4/`](media/improvements/round4/). Before choosing, the AutoPilot played Monday at
16:10 (1440×900, every Mac and the Steam Deck), 4:3 (1024×768) and 21:9 (2560×1080). Every view framed
correctly and no UI clipped, so other aspect ratios aren't on the list.

### R4-A. Quick clicks and taps are never lost
`InputX` reads every button by polling whether it's held, once a frame. A click, key tap or pad press that
goes down and comes back up between two frames is never seen. Touchpad tap-to-click sends its press and release
almost together, and at a low frame rate (a weak laptop, a browser, a busy machine) ordinary clicks fit between
frames too. This would also explain round 3's unexplained padtest failures, all at high load.
- `InputX` also listens to the Input System's event stream and latches any press it sees, so a button that went
  down and up since the last frame still counts as one press for one frame. The same goes for the pad buttons
  that drive the virtual mouse and stand in for keys.

**Acceptance:** a new `Tools/unity.sh taptest` queues each press and release **in the same input update**
(nothing held across a frame) from fresh virtual devices: a key tap rings the bell, a key tap turns to the
drawers, a mouse tap opens a drawer and picks up the wallet, a pad A tap and a d-pad tap do their jobs. It
**fails on the current build** and passes after the fix. The padtest and AutoPilot `best` still pass.
**Verify:** taptest logs before and after, padtest, AutoPilot.

### R4-B. A crash can't cost you the week
The save is rewritten in place after every verdict (`File.WriteAllText`). A crash or power cut mid-write leaves
a truncated file. The game then can't read it, quietly starts with an empty save, and the title offers *Begin*,
which overwrites it. The week is gone.
- Writes go to a temporary file that replaces the save in one step, keeping the previous save as a backup.
- Loading falls back to the backup if the save can't be read. If neither can, the damaged file is kept aside
  (renamed, never deleted) and the title says so.

**Acceptance:** EditMode tests: a round trip; a truncated save loads from the backup; a garbage save with no
backup is set aside and not overwritten; a write never leaves a partial file at the save's path. In the
player, a truncated save with a good backup still *Continue*s to the right claimant.
**Verify:** test results, and an AutoPilot `-lafContinue` run against a deliberately truncated save.

### R4-C. The controls, in the game
The controls are listed only in the README. The inspect bar shows a few, but there's nowhere in the game to
check which key reads the slip, or how the blue lamp works, once the first-day hints have gone.
- A **Controls** card in the pause menu and on the title, in keyboard and mouse words, or pad words while the
  pad is in use, matching the README tables.

**Acceptance:** the card opens from both places, fits at 1280×720 with Large text, and closes with `Esc` or
B. Each line matches the README.
**Verify:** screenshots (keyboard and pad wording, Normal and Large text).

### R4-D. Editor runs don't touch the real settings either
Round 3's guard covers built players. The batch editor (`build-linux`, `test`) still rewrites the real `prefs`
file in `~/.config/unity3d/Nearby/Lost & Found/` (same contents, new timestamp), because the editor keeps the
project's PlayerPrefs there too.
- Batch editor commands get a scratch `XDG_CONFIG_HOME` that links back to everything in the real config
  folder except this game's own folder. Unity's licence and editor settings are still found where they live.
  The guard then covers editor runs as well.

**Acceptance:** `build-linux` and `test` leave the real folder's hashes **and timestamps** unchanged, and print
the guard's line. The editor still finds its licence and builds and tests as before.
**Verify:** `stat` and hashes before and after, plus the build and test logs.

### R4-E. WebGL, second look on a real GPU (time-boxed)
Round 3 could only measure WebGL with a software renderer. A headed Chrome window on this machine gets the
real Radeon GPU (`ANGLE (AMD, AMD Radeon 8060S …)`, checked with a probe page), so:
- On WebGL, the game's `-laf…` options can also come from the page's URL, so the AutoPilot can play in a browser.
- Measure the frame rate on the real GPU, and check that a save survives closing and reopening the browser
  (quit after a case, reopen with the same profile, *Continue*).
- Fix what's cheap: depth of field (its shaders are stripped from the WebGL build) and the audio warnings.

**Acceptance:** measured fps on the real GPU (with the load average), a save that survives a browser restart
or the reason it doesn't, and DoF and audio results, all in "Round 4 results". Nothing is hosted, and the
desktop build is unchanged. If it overruns its time box, what was learned is recorded instead.
**Verify:** the browser console log, screenshots, the build log.

## Round 4 results (6 Oct 2026)

All five items landed on `improvements-4`, one commit each. Screenshots and logs are in
[`media/improvements/round4/`](media/improvements/round4/). Final checks on the final Linux build are at the end
of this section.

| Item | Result | How it was verified |
|---|---|---|
| **R4-A. Quick clicks and taps are never lost** | **A real bug.** `InputX` only polled whether buttons were held, so a press that went down and up between two frames was dropped. That covers a touchpad's tap-to-click, and any quick click, key or pad press on a slow frame. Presses are now also latched from the Input System's event stream and count for one frame. Pad buttons go through the same latch. Unity's own menu clicks (uGUI) were already safe. | New `Tools/unity.sh taptest`: each press and its release are queued in the same input update from fresh virtual devices. **Before: 1 of 11 taps worked** (only the uGUI menu button). **After: 11 of 11**, then 14 of 14 once R4-C added the card, at 1600×900 and at 1280×720 with Large text (load average 24–30). [`taptest_before_after.txt`](media/improvements/round4/taptest_before_after.txt). The padtest and AutoPilot `best` (24/24, *The 9:40*) still pass. This is the likeliest cause of round 3's unexplained padtest failures at high load, but those weren't reproduced, so it isn't proven. |
| **R4-B. A crash can't cost you the week** | Saves are written to a temporary file, flushed to disk and swapped in with one rename. The previous save is kept as `.bak`. An unreadable save falls back to the backup, and the restored save becomes the save again. If neither can be read, the damaged file is moved to `.damaged` (or `.damaged-2`, and so on), never deleted. The title says what happened, on a dark band so it reads over the desk. | 9 new EditMode tests (62 in all): round trip, backup on every write, truncated, empty and missing saves falling back, damaged saves set aside and never overwritten, a failed write leaving the old save intact, and a save from another version. In the player, a save truncated mid-file after case 1.3 *Continue*d from the backup at claimant 1.3 and kept `save.json.damaged`. `save_restored_from_backup_title.jpg`, `save_damaged_set_aside_title.jpg`. |
| **R4-C. The controls, in the game** | A **Controls** card in the pause menu and on the title, in keyboard and mouse words, or the pad's while the pad is in use. Esc, right click or B puts it away. The title's items close up slightly when the menu is at its longest (seven items), so they stay clear of the save note. | The taptest opens it from the pause menu with a mouse tap, closes it with Esc (the pause menu stays open), and with the pad in use closes it with B. Screenshots at 1280×720 with Large text in both wordings, and the title at its longest with the save note: `controls_*.jpg`, `title_longest_menu_with_save_note.jpg`. |
| **R4-D. Editor runs don't touch the real settings** | Batch editor commands get `Logs/config/editor/`: links back to every entry in the real `~/.config`, `unity3d` and `Nearby` folders except `Lost & Found`. That folder is scratch, and it's where the editor's PlayerPrefs and the test runner's `TestResults.xml` now go. The guard now checks timestamps as well as hashes, and covers editor runs. | From the first sandboxed run onwards, the real folder's file sizes, timestamps and hashes were identical before and after every build and test (more than ten runs). The editor found its licence every time. **Not fully clean:** six `build-linux` runs earlier this session, before R4-D, rewrote the real `prefs` file's timestamp (round 3's known issue). Its contents are Unity's own player keys, with no game settings, and the save file was untouched. |
| **R4-E. WebGL, second look on a real GPU** | **Saves were lost in the browser.** IndexedDB stayed empty even while the game ran, despite the template's `autoSyncPersistentDataPath`. A small `.jslib` now flushes the file system to IndexedDB after every save write. **Depth of field:** WebGL used the *Mobile* quality level, whose pipeline asset strips DoF. It now uses *PC*, like the desktop (WebGL only; the desktop builds are unchanged). **Audio:** in a browser the pause muffle is a dip in volume rather than a low-pass filter (browsers have no audio filters), and looping sounds start at the beginning, because a browser can't seek compressed clips. In a browser the game reads its `-laf…` options from the page's query string, and `Tools/webgl_check.py` drives Chrome through its DevTools protocol (stdlib only). | **Download:** 66 MB Brotli (data 61.0, wasm 7.3 MB), 122 MB unpacked. Round 3's was 89 MB. Builds took 11 min, then 6 min. **Frame rate,** Chrome on the Radeon 8060S (headless, ANGLE on EGL, confirmed by the WebGL renderer string): High holds **60 fps**, the 60 Hz cap, at 960×600 (the template's canvas) and at 1600×900, load average 8–18. Low also holds 60. At load 30–75 from other sessions, High dipped to 35–60. A visible Chrome window wasn't usable here: on this shared desktop the window is never shown, so `requestAnimationFrame` never fires (that run measured 2 fps). **Picture:** a WebGL frame against Linux High at the same size and moment: vignette, grain and edge sharpness behind and in front of the focus all within 1% (`webgl_vs_linux_high_960x600.jpg`). **Console:** no audio warnings in a 45 s run. Round 3 had `getFrequency()` and filter warnings. One warning remains: URP's FSR upscaling shader is stripped. Upscaling is unused, and post-processing runs, as the comparison shows. **Saves:** quit after 1.2, closed Chrome, reopened it with the same profile and a different URL: *Continue* resumed at 1.3, and IndexedDB held the save and its backup. Before the flush, the same test restarted the morning. |

### WebGL: where it stands (for the owner)
Technically it's close to releasable: 66 MB, 60 fps on this iGPU, the desktop's picture, and saves that stick. Not
yet checked: Firefox and Safari, a person playing by hand, whether the sound is actually heard (headless Chrome
can't tell), touchpads and gamepads in a browser, and anything mobile. The page is still Unity's default white
template. Hosting is the owner's call. A host must send the `.br` files with `Content-Encoding: br`, as
`Tools/serve_webgl.py` does. Building WebGL still rewrites `Mobile_RPAsset.asset` and leaves `Data/` at the project
root; both were reverted and removed after each build.

### Final checks (final Linux build)
The Linux build and validator pass (28 objects, 5 days, 25 cases, 0 issues). **62/62 unit tests.** The audit passes
(84/84 details, 0 below 10%, 0 storage problems). All four AutoPilot policies pass: `best` (24/24, *The 9:40*),
`worst` (*Grey Ninefold*), `wait` (*The Long Wait*) and `refuse`. The nudge tour passes (210 nudges, 24/24, 21 details
found at the glint, 2 by the part that lit up), as do the padtest and the taptest (14/14). Load average 2–8 for all
of these. Every run printed `[guard] real save and settings untouched`. The real folder's sizes, timestamps and
hashes were identical at the end and at 20:28, when R4-D went in.

### Still open after round 4
- No human has played any of it. The nudges' tone, how findable details are, and now the Controls card's wording
  all want a person.
- The tap fix is proven with virtual devices. No physical touchpad or gamepad has been tried, so round 3's padtest
  failures can't be confirmed as this bug.
- WebGL: Firefox and Safari, audible sound, browser input devices, a proper page template, hosting (above).
- macOS on a Mac and Windows (blocked on the module), and the trailer and README screenshots, which predate rounds 1–4.
- Interactive editor sessions (`Tools/unity.sh` with no command, or `headless`) still use the real config folder.

## Round 5 scope (6 Oct 2026)

Branch `improvements-5`, one commit per item. Screenshots go in
[`media/improvements/round5/`](media/improvements/round5/). Before choosing, I reread the core loop in the
code. The thing it hinges on, what a claimant **answers** when you ask about a finding, is only spoken. It's
never written down, so a quick click past the answer loses the evidence, and the only way back is to ask
again. That's item A. The rest is about reading, the editor guard, the browser page and the README pictures.

### R5-A. What they said, beside the slip
Claims go on the claim slip, but answers to your questions, and everything else claimants say, scroll away.
- While you read the slip (hover, `Tab` or View, and while carrying a stamp over it), a card beside it lists
  everything said in this claim, in order: each claimant's lines, your questions and their answers. Two
  claimants are told apart by name. The card sits in the free space to the right of the slip, measured from
  where the slip actually is on screen, so it never covers the slip or the stamps. If a claim runs long, the
  oldest lines give way to the newest, and the claimant's opening is always kept.
- No new key or button: it comes with the slip.

**Acceptance:** the AutoPilot (`-lafTranscript`) asks about every finding in every case of the `best` week, then
reads the slip. At each claim, the card holds exactly the lines spoken in that claim (counted as the dialogue
box says them), and is fully on screen and clear of the slip. It still ends 24/24 on *The 9:40*. Screenshots at
1600×900, at 1280×720 with Large text, and at 4:3 (1024×768).
**Verify:** the AutoPilot log (`[Transcript]` lines, 0 problems) and the screenshots.

### R5-B. Plain lettering, and a check that no text overflows
The clerk's handwriting (Caveat) on tags and the slip, and Agnes's (Kalam) on notes, the rules and nudges,
are period-right but harder to read for some players (dyslexia, low vision, reading in a second language).
- Settings, *Plain lettering*: those two hands switch to the game's clear book face (Crimson Pro), at once,
  everywhere they're used. Signatures stay as signatures.
- A text audit for the AutoPilot (`-lafTextAudit`): at every screenshot it checks every visible piece of text
  and reports any that runs outside its box.

**Acceptance:** the switch applies live and persists. Full AutoPilot weeks with the text audit on report **no
overflowing text** with plain lettering, at 1600×900 and at 1280×720 with Large text, and no more than with the
normal hands (that baseline is measured first and recorded either way). The tag card, slip, a note, the rules
card, a nudge and the ledger look right in both, in screenshots.
**Verify:** AutoPilot logs (`[TextAudit]` lines), screenshot pairs, a prefs check in a scratch folder.

### R5-C. The interactive editor doesn't touch the real save or settings either
Rounds 3 and 4 sandboxed every built player and batch editor run. `Tools/unity.sh` with no command, or with
`headless`, still runs the editor against the real `~/.config/unity3d/Nearby/Lost & Found/`, so pressing Play in
the editor reads and writes the real save and settings.
- Both get the same scratch config folder as the batch editor (links back to everything else in `~/.config`),
  and the same before-and-after guard.

**Acceptance:** a `headless` session started and stopped through the script, and a GUI editor opened and
closed, leave the real folder's sizes, timestamps and hashes unchanged and print the guard's line. The editor
still finds its licence.
**Verify:** the guard lines, the real folder's state before and after, the editor logs.

### R5-D. A WebGL page of the game's own
The WebGL build still uses Unity's white default page with a fixed 960×600 canvas.
- A page template in the game's colours: the canvas fills the window at 16:9, a loading bar while the 66 MB
  downloads, a fullscreen button, a clear message if the browser has no WebGL 2 or the load fails, and a note
  on phones that the game wants a mouse and keyboard. The game's `?laf…` test options keep working, and so does
  the save flush to IndexedDB.

**Acceptance:** built with `build-webgl`, it loads in headless Chrome on the real GPU, shows the loading bar,
then the title filling the window at 1600×900 and at 1280×720, and the AutoPilot plays Monday through it with
0 errors in the console. The save still survives a browser restart (quit after 1.2, reopen, *Continue* at 1.3).
Nothing is hosted, and the desktop build is unchanged.
**Verify:** `Tools/webgl_check.py` console logs and screenshots of the loading and title states.

### R5-E. README screenshots of the game as it is now
The ten README screenshots were cut from the v0.1.0 takes, before rounds 1–4: the umbrellas still lie on the
shelf, the rules card isn't on the desk, and there's no findings count.
- Film new takes of Monday to Thursday into **new** take folders (`Recordings/r5_day1` and so on, so the
  original takes behind the trailer stay as they are) and cut the ten stills from them with `make_trailer.py
  stills`, which gets an option to read from those takes. The trailer, its poster and the teaser aren't
  touched: re-cutting them is the owner's call.

**Acceptance:** each new still shows the same moment as the old one, from the current build: the umbrella stand,
Agnes's card on the desk, the findings count where the object is held. Every take logs 0 details needing the
recorder's fallback. Only the ten `screenshot_*.jpg` files change in `docs/media/`.
**Verify:** the take logs, and a look at each still next to the old one.

Final checks after the last item: Linux build and validator, unit tests, the audit, the four AutoPilot policies,
the nudge tour, the padtest and the taptest, with the load average noted, and the real config folder compared
with its state at the start of the round.

## Round 5 results (7 Oct 2026)

All five planned items landed on `improvements-5`, plus one found along the way (R5-F) and two fixes to this round's own
work. Screenshots and browser logs are in [`media/improvements/round5/`](media/improvements/round5/).

| Item | Result | How it was verified |
|---|---|---|
| **R5-A. What they said, beside the slip** | While you read the slip (hover, `Tab`, View, or with a stamp over it), a card to its right lists everything said in the claim: each claimant's lines, your questions and their answers, with claims highlighted as in the speech bubble. The slip view now frames the slip 5 cm further left to make room. On screens narrower than 16:9 it widens to keep that framing side to side, because at 4:3 there was no room at all. A long claim drops to a smaller size, then keeps its opening and the newest lines. Also fixed: questions about another object lowercased its name ("About the mr vell's chit"; now "About Mr Vell's chit"). | AutoPilot `best -lafTranscript` asks about every finding in all 24 claims, then reads the slip. **Every claim's card held exactly the lines spoken** (counted as the dialogue box says them), was on screen and clear of the slip, and dropped **no lines**: at 1600×900, at 1280×720 with Large text, and at 1024×768. 24/24, *The 9:40*, each time. The first try at 4:3 failed every claim (no room), which led to the framing change. 5 new unit tests. `transcript_*.jpg`. |
| **R5-B. Plain lettering, and a text audit** | Settings › *Plain lettering* sets the clerk's and Agnes's handwriting (tags, the slip, notes, rules, nudges) in Crimson Pro. It switches live and keeps each hand's line pitch, so it still sits on ruled lines. A new `-lafTextAudit` checks every text on screen against its box at each AutoPilot screenshot. **It found three real problems:** (1) **the slip's writing was stuck at 42% of its size on every longer claim**, because TMP's autosize steps by at least 0.05 font units and the slip was sized in metres (0.05–0.12). It's now laid out in centimetres, and its box ends above the printed STAMP HERE rule, which it always ran into unseen. (2) Long Day Ledger explanations ran into the next row. (3) With plain lettering, a long ledger line wrapped onto the explanation. Settings also scales to fit a 21:9 window, where it ran off the top and bottom. | Text audit over full weeks (119 screenshots each): handwriting at 1600×900, before the fixes **4 overflowing texts + the slip at 42% on 6 claims**, after them **0**. Plain lettering at 1600×900 and at 1280×720 Large: **0 overflowing** (1 before the ledger-line fix). The only "small" text left is the rules hover card on Thursday/Friday (26 lines at 60%; `R` opens the full card). Live switch: the slip went Caveat → Crimson Pro and the rules card Kalam → Crimson Pro mid-claim. The next launch started in plain lettering, then switched back. A Friday nudge tour in plain lettering passed with 0 overflows. `lettering_*.jpg`, `slip_twins_before_after.jpg`, `settings_*.jpg`. |
| **R5-C. The interactive editor is sandboxed too** | `Tools/unity.sh` (GUI) and `headless` run with the same scratch config folder and before/after guard as batch editor runs. **Then I broke it, and fixed it in its own commit:** my first version let a still-running licensing client recreate `unity3d/Unity` as an empty folder between runs, and the next editor runs were refused a licence. Links are now kept between runs (broken ones pruned, missing ones added, a folder in the wrong place set aside). | `headless`: the licence was found, the guard printed "untouched", and the real folder was unchanged. **The GUI editor never got past start-up on this Wayland session** (idle after licensing, "Selected window backend: (null)"), so Play in the GUI editor is **unverified**. The guard still ran when I stopped it and passed. It looks environmental, but I couldn't confirm that without an unsandboxed run, which I didn't do. After the link fix, every batch build and test run (more than fifteen) found its licence. |
| **R5-D. A WebGL page of the game's own** | Unity's white page is replaced: the game fills the window at 16:9 on a dark ground, with a brass loading bar in IM Fell, a fullscreen button (focus goes back to the game), a plain message if there's no WebGL 2 or the load fails, and on phones and tablets a warning before the 66 MB download. Also fixed, from round 4: Emscripten's "2 FS.syncfs operations in flight" warning (overlapping save flushes now run one after another). | Headless Chrome on the Radeon (ANGLE): the loading bar (screenshot at 37%), then the title at 1600×900 and 1280×720, **60 fps**, load average 4. Quit after 1.2, closed Chrome, reopened it with the same profile: *Continue* resumed at claimant 1.3 and Monday finished best. IndexedDB held the save, its backup and `settings.json`. Console: only round 4's known FSR shader warning. `--disable-webgl` shows the WebGL 2 message, and an Android user agent shows the phone warning with nothing downloaded. `webgl_*.jpg`, `webgl_logs/`. |
| **R5-E. README screenshots of the game as it is now** | Monday to Thursday filmed again into `Recordings/r5_day1`–`r5_day4`. The ten stills were re-cut with `make_trailer.py stills --takes r5_`. The ledger still is now anchored to the finished page, not a fixed time after it opens. The re-cut ledger showed the Gazette headline's third line running into the article, so the headline now has its own band. | Takes: **43 details found by hand, 0 fallbacks**. Each new still shows the same moment as the old one (`stills_old_new_*.jpg`), now with the title's full menu, Agnes's card on the desk and the findings counts. The trailer, poster and teaser are byte-identical (md5), and the original takes are untouched. |
| **R5-F. Settings beside the save (found this round)** | The game's settings were PlayerPrefs. Unity's Linux player wrote explicit saves of them to `~/.config/unity3d/unknown/unknown/prefs`, **a file another game on this machine also writes its settings to**, not the game's folder that the README names. Settings are now `settings.json` next to the save: swapped in whole, written at most a few times a second while a slider is dragged, and flushed to IndexedDB in a browser. Values left in PlayerPrefs are brought over once. | 3 new unit tests (70 in all). In the player, a change made in a run landed in `settings.json` in the game's folder and was there at the next launch. Two old-style keys planted in a scratch prefs file were imported ("brought 2 setting(s) over"), and the scratch folder was reset afterwards. I couldn't pin down exactly when Unity reads which prefs file, so a v0.1.0 player whose settings only reached `unknown/unknown` may start with defaults. |

### Final checks (final Linux build, load average 2–7)
Linux build and validator: 0 issues. **70/70 unit tests.** Audit: 84/84 details, 0 below 10%. AutoPilot: `best` 24/24
*The 9:40*; `worst` *Grey Ninefold*; `wait` *The Long Wait* (23/24); `refuse` passes. Nudge tour: 210 nudges, 24/24, 21
details at the glint and 2 by the part that lit up. Padtest PASS, and taptest 14 of 14. Every run printed the guard's
"untouched". The real `~/.config/unity3d/Nearby/Lost & Found/` was **identical (sizes, timestamps, hashes) at the end of
the round and at its start**.

### Not done, and why
- **The WebGL build predates the Gazette headline fix.** It was rebuilt and checked before that one-line ledger change.
  It's a local, unreleased build, so I didn't spend another 10-minute build on it.
- **The GUI editor path** (R5-C) is unverified on this Wayland session (above).
- **The trailer and teaser** still show v0.1.0. Re-cutting them is the owner's call. The new takes are in `Recordings/r5_day*`
  (1.3 GB, gitignored), and the trailer would also need Friday and the Grey Ninefold takes.

### Still open after round 5
- No human has played any of it: the transcript card, plain lettering, the nudges, and the new WebGL page all want a person.
- WebGL in Firefox and Safari, audible sound, browser input devices, and hosting (owner).
- Gamepad and touchpad on real hardware, macOS on a Mac, and Windows (blocked on the module).
- The text audit checks each text against its own box, not texts against each other. The Gazette overlap was found by eye.

## Round 6 scope (7 Oct 2026)

Branch `improvements-6`, one commit per item. Screenshots go in
[`media/improvements/round6/`](media/improvements/round6/). Before choosing, I went back through round 5's own
screenshots and the last AutoPilot run. Two things a player would see at once: in round 5's
`ledger_plain_1280x720_large.jpg` a long verdict ("Returned to Constable Bramble") **runs under the "3 of 3 found"
column**, which the text audit can't catch because it only checks each text against its own box; and on Thursday and
Friday **Agnes's rules card shrinks to fit**, so the full card (`R`) writes the rules at about 18 px and their plain
glosses at about 12 px on a 1600×900 screen, with a third of the card left as blank lines. The rest: the week has no
summing-up, the open settings question from round 5, and the stale WebGL build.

### R6-A. No text runs into another
- The text audit (`-lafTextAudit`) also checks every pair of visible texts on the same screen and reports any whose
  drawn characters overlap (not their boxes, which overlap by design on cards).
- Fix what it finds, starting with the ledger's "found" column, which gets its own room: a long verdict line ends
  before it (autosizing down, as it already does for long names).

**Acceptance:** with the overlap check, full `best` weeks at 1600×900 and at 1280×720 with Large text and Plain
lettering report **0 overlapping texts and 0 overflowing**. The check is shown to catch the ledger overlap on the
current build first (so it's known to work). Before/after picture of the ledger.
**Verify:** `[TextAudit]` lines in the AutoPilot logs, screenshots.

### R6-B. Agnes's rules at a readable size all week
- The full rules card (`R`, the pause menu) uses its room: no blank line between rules, a wider card, and the gloss
  under each rule at a size you can read. The hover card beside the slip does the same within its space.

**Acceptance:** on Friday (all eight rules), with normal and Large text, the full card's rules are written at **at
least 28 units** (from 20) and the glosses at least 22, and the hover card is no longer the audit's one "small" text
(it was at 60%). Nothing overflows. Monday's card (one rule) is no larger than now.
**Verify:** the text audit's sizes, Friday screenshots before and after at 1600×900 and 1280×720 Large.

### R6-C. The week, summed up
After Friday's ending and its epilogue there's nothing about how the week went: it goes straight back to the title.
- After the epilogue, a page of the ledger book: each day's "by the rules" tally and stamps, the findings you noted,
  curiosities found, and which of the three endings you've reached so far (the others only as "another ending" so
  nothing is spoiled), with a pointer to *Choose a Day*. Endings reached are remembered in the save from now on
  (older saves load as before and start with the ending they finished on, if any).

**Acceptance:** after `best`, `worst` and `wait` weeks the page shows each day's tally matching that evening's ledger,
and the endings count grows from 1 to 3 across the three runs on one save. It fits at 1600×900 and 1280×720 Large
with 0 overflow. Unit tests: endings remembered across a round trip; an old save without the field loads.
**Verify:** AutoPilot screenshots and logs, test results.

### R6-D. Do v0.1.0 players keep their settings? (round 5's open question)
v0.1.0 kept settings in PlayerPrefs, saved after every change. Round 5 found those saves land in
`~/.config/unity3d/unknown/unknown/prefs`, and imports PlayerPrefs once, but couldn't say which file the player
reads them from at start-up. Same engine and company/product names as v0.1.0, so a probe run of the current player
can answer it exactly.
- In scratch config folders only: write `laf.*` keys through `PlayerPrefs.Save()` and trace (strace) which files the
  player writes and reads at start-up. If the import can't see v0.1.0's keys, read them from where v0.1.0 put them
  (only the `laf.` keys; that file is shared with other games and is never written).

**Acceptance:** a written answer (which file each step touches), and a player run that starts from a v0.1.0-style
prefs file in a scratch folder and comes up with those settings in `settings.json`. The real
`~/.config/unity3d` is untouched (guard, plus a before/after listing of `unknown/unknown`).
**Verify:** strace excerpts, the player log's import line, the scratch `settings.json`.

### R6-E. The WebGL build brought up to date
The local WebGL build predates round 5's Gazette fix. Rebuild it with everything above, and run the headless-Chrome
smoke and save checks again (one build, run alone, `nice`). Not hosted.

**Acceptance:** builds, loads, plays Monday in headless Chrome with only the known FSR warning, save survives a browser
restart. `Mobile_RPAsset.asset` and the `Data/` folder the build leaves are reverted/removed.
**Verify:** `webgl_check.py` logs and screenshots.

Final checks after the last item: Linux build and validator, unit tests, the audit, the four AutoPilot policies, the
nudge tour, the padtest and the taptest, with the load average noted, and the real config folder compared with its
state at the start of the round.

## Round 6 results (7 Oct 2026)

All five planned items landed on `improvements-6`, with one fix to this round's own tooling. Screenshots are in
[`media/improvements/round6/`](media/improvements/round6/). The machine was shared with about 15 other sessions: load
average 8–90 during this round, noted with each measurement below where it mattered.

| Item | Result | How it was verified |
|---|---|---|
| **R6-A. No text runs into another** | The text audit now compares texts on the same card or panel letter by letter on screen. On the current build it caught the ledger's long verdict running under "2 of 2 found". **It then found a bigger problem once the AutoPilot shot the finished ledger page**: it had been shooting 2.2 s after the ledger opened, before the tally and the Gazette, and `GameObject.Find("Ledger")` could return the ledger *prop* on the desk. On every five-case day **the Gazette landed on the last rows' explanations and "found" counts** (7 overlaps in a week). Fixes: a verdict line ends where the "found" column begins; the Gazette is now a tall clipping standing in the book's right margin, beside the rows (a new `gazette_tall` texture); a day without a Gazette centres the book; and the whole spread scales to fit. **At 21:9 the ledger's "Next morning"/"Close the desk" button was below the bottom of the screen**, which I only noticed while checking the new layout. | Full `best` week at 1600×900 with the audit: **7 texts running into another before, 0 after**, 0 overflowing. `worst` and `wait` weeks at 1600×900 and a `best` week at 1280×720 with Large text and Plain lettering: 0 and 0. Friday at 2560×1080 and 1024×768: 0, with the buttons on screen. A built-before/after at 21:9 shows the button missing, then present (`ledger_friday_21x9_before_after.jpg`). `ledger_day2_before_after_1600.jpg`, `ledger_day3_gazette_beside_1600.jpg`, `ledger_friday_1024x768.jpg`. |
| **R6-B. Agnes's rules readable all week** | A short spacer replaces the blank line between rules, the glosses are 80% of the rule (from 70%), the full card is 1500 wide (scaled to fit a short 21:9 window) and the hover card 1000 wide, ending above the slip. | AutoPilot `[Rules]` lines. Friday, 1600×900: **full card 21.6 → 27.7** (glosses 17.3 → 22.1), **hover card 17.5 → 21.8** (glosses 14 → 17.4), so the hover card is no longer the audit's "small" text. Monday's single rule is unchanged at 30. With Plain lettering (a wider face) at 1280×720 Large: full 25.7, hover 22.6. **Short of the 28 I set for the full card by 0.3**; it fills the card's height at 2560×1080. `rules_hover_thursday_before_after.jpg`, `rules_full_friday_1600.jpg`, `rules_full_friday_1280x720_large_plain.jpg`. |
| **R6-C. The week, summed up** | After Friday's epilogue, a ledger page: each day's tally and stamps, findings, curiosities, and endings reached (*n* of 3; the ones not reached shown only as "another ending"), and a pointer to *Choose a Day*. The save keeps `endingsSeen` across new weeks; an older save starts with the ending it finished on. `LAF_AUTOPILOT_SAVE` lets AutoPilot runs share a save. | `best`, `worst` and `wait` on one save: **endings 1, 2, 3 of 3** (`[Week]` lines). The `worst` week's five tallies (0/4, 0/4, 1/3, 0/4, 0/4) and findings (42) match its five evening ledgers. Fits with 0 overflow and 0 overlap at 1600×900, 1280×720 Large, 1024×768 and 2560×1080. 2 new unit tests (72 in all). `week_3_of_3_endings_1600.jpg`, `week_worst_2_of_3_1600.jpg`, `week_1024x768.jpg`. |
| **R6-D. Do v0.1.0 players keep their settings?** | **Yes.** No tracing tool (strace and the like) is installed, and installing one is the owner's call, so I planted values instead. The player's `-lafPrefsProbe` writes settings as v0.1.0 did (PlayerPrefs, saved at once; same engine and company/product names). They land in the game's own `Nearby/Lost & Found/prefs`, and starting this version on that folder brings them into `settings.json`. A value planted only in `unknown/unknown/prefs` is never read, and the file isn't written. Round 5's note that v0.1.0's settings went to `unknown/unknown` was wrong for the player; the README and `Settings.cs` are corrected. That shared file is written by other games on this machine at any time (it changed during this round while none of mine was running). | `Tools/prefs_probe.sh`, scratch config folders only: step 1 wrote `laf.music 0.2` and `laf.plain 1` to `Nearby/Lost & Found/prefs`, and step 2 imported them; with 0.3 planted in `unknown/unknown` and 0.4 in the game's folder, 0.4 was imported; with 0.3 only in `unknown/unknown`, nothing was. The real `Nearby/Lost & Found` was unchanged and no `laf.` key ever appeared in the real `unknown/unknown`. |
| **R6-E. The WebGL build brought up to date** | Rebuilt with rounds 5 and 6 in it: **65 MB** (data 61.0 MB, wasm 7.3 MB, Brotli), 12 min 24 s to build, load average 8–20. As before, the build rewrote `Mobile_RPAsset.asset` and left `Data/` at the project root; both were reverted and removed. Not hosted. | Headless Chrome on the Radeon (ANGLE, confirmed by the renderer string), 1600×900, load average 8: **60 fps** in a 25 s smoke run. AutoPilot quit after 1.2, Chrome closed and reopened on the same profile: *Continue* resumed at claimant 1.3 ("resuming after 2 case(s)"), Monday finished and Tuesday began, with the new ledger page (`webgl_ledger_monday_1600x900.jpg`). IndexedDB held the save and its backup. Console in all three runs: only round 4's known FSR shader warning. Logs in `webgl_logs/`. |

### Final checks (final Linux build, load average 8–27)
Linux build and validator: 28 objects, 5 days, 25 cases, **0 issues**. **72/72 unit tests.** Audit: PASS, 84/84 details, 0
below 10%. AutoPilot, each with the text audit: `best` 24/24 *The 9:40*, `worst` *Grey Ninefold*, `wait` *The Long Wait*
(23/24), `refuse` (6/24) — **0 overflowing and 0 overlapping texts** in every one. Nudge tour: 210 nudges, 24/24, 21 details
at the glint and 2 by the part that lit up. Padtest PASS (load 13). Taptest 14 of 14 (load 13). `Tools/prefs_probe.sh` as in
R6-D. Every run printed the guard's "untouched". The real `~/.config/unity3d/Nearby/Lost & Found/` was **identical (sizes,
timestamps, hashes) at the end of the round and at its start**.

### Found along the way
- **The AutoPilot never photographed a finished ledger page.** It shot 2.2 s after the ledger opened, and
  `GameObject.Find("Ledger")` could pick up the desk's ledger prop. While I was changing the shot, one run looked for the
  ledger's button on that prop, so it missed the ending, went back to the title, began a second week in the same process, and
  **crashed (signal 11) on Thursday morning** at load average 74. It hasn't happened in the 20-odd runs since, so it's
  recorded here, not explained.
- The text audit's first overlap version flagged the desk calendar while the camera was turned to the shelf (fixed: it now
  only checks 3D texts in view).
- **Starting *A New Week* clears the curiosities found** (the save is deleted, and `discovered` also drives what the slip shows
  as found, so it can't simply be kept). Noted in the README; whether curiosities should carry over is a design question.
- The README's ledger screenshot showed the old layout; it's replaced with the AutoPilot's 1920×1080 Monday ledger at the same
  moment (`readme_ledger_old_new.jpg`). The other nine stills and the trailer are unchanged.

### Not done, and why
- **The full rules card misses its 28-unit target by 0.3** (27.7). Going further means a smaller gloss or a taller card than
  a 21:9 window allows; it's 28% larger than before and reads well in the screenshots.
- **The trailer and teaser** still show v0.1.0 (and now an older ledger). Re-cutting them is the owner's call.
- **The GUI editor's Play** (round 5) is still unverified on this Wayland session. I didn't try again: the cause looks
  environmental, and testing it means changing the editor's licensing sandbox, which the rules for this round rule out.

### Still open after round 6
- No human has played any of it: the transcript card, plain lettering, the nudges, the WebGL page, and now the week summary.
- WebGL in Firefox and Safari, audible sound, browser input devices, and hosting (owner).
- Gamepad and touchpad on real hardware, macOS on a Mac, and Windows (blocked on the module).
- Whether curiosities should survive a new week (above).

### Needs a decision from the owner
- Re-cutting the trailer and teaser from current takes (`Recordings/r5_day*`, plus Friday and Grey Ninefold takes still to film).
- Hosting the WebGL build (it's current again as of this round; the host must send `.br` files with `Content-Encoding: br`).
- Whether a tracing tool (`strace`) may be installed for future checks like R6-D. It wasn't needed this time.
- Curiosities across weeks (above). Licence, releases and tags, signing, and Windows Build Support are unchanged from before.

## Round 7 scope (7 Oct 2026)

Branch `improvements-7`, one commit per item. Screenshots and logs go in
[`media/improvements/round7/`](media/improvements/round7/). Before choosing, I followed what the title and pause menus
do to the save, and how the game rebuilds itself. Two things a player could hit that no check covers: **the whole game is
torn down and rebuilt in the same process** whenever you start the day again, go back to the title or finish the week
(`Game.Restart`), and nothing has ever measured what that leaves behind. Round 6's one unexplained crash came on exactly
that path (a second week in one process). And **three menu choices throw away progress with one click**, with no warning:
*Start the day again* undoes every claim decided today; *Choose a Day* during a week rewinds the whole week to that
day's morning (on Thursday, replaying Monday leaves *Continue* pointing at Monday); and after a finished week a replay
can't be *Continue*d at all, because the save still says the week is finished.

### R7-A. Play on without quitting
- A soak test, `Tools/unity.sh soak [cycles]` (`-lafSoak`): in one process it goes round the menus' own paths again and
  again (begin, start the day again, back to the title, *Continue*, *Choose a Day*), and after each rebuild logs the
  managed heap, Unity's allocated memory and the live Materials, Meshes, Textures and GameObjects.
- AutoPilot `-lafWeeks 2`: two whole weeks back to back in one process (finish, the week summary, back to the title, a
  new week), the path that crashed in round 6.
- Fix whatever grows.

**Acceptance:** over 30 rebuilds, live object counts and allocated memory stop growing (the last 10 cycles within 2%
of each other, not climbing cycle on cycle); two `best` weeks in one process both PASS (24/24, *The 9:40*) with no
crash. Load average noted with each run.
**Verify:** `[Soak]` lines before and after any fix, the two-week AutoPilot log.

### R7-B. No progress lost by surprise
- *Start the day again* (pause menu), once claims have been decided today, asks for a second click and says what it
  undoes, as *A New Week* already does.
- *Choose a Day* during an unfinished week, for a day that would rewind it (an earlier day, or today's morning after
  decided claims), asks for a second click and says where the week is now.
- A replay after a finished week can be picked up with *Continue*, like any other day in progress.
- The wording comes from one pure function, unit-tested.

**Acceptance:** unit tests for the wording (nothing to lose means no second click) and for the save after a replay
begins; in the player, the soak clicks the real buttons and screenshots show each confirm; one click changes nothing,
two clicks do what they did before. *Continue* appears after a replay of a finished week, and resumes it.
**Verify:** test results, soak log and screenshots.

### R7-C. Agnes's full rules card reaches its size
Round 6 set 28 units for Friday's full rules card and reached 27.7. Trim the card's margins so it gets there.

**Acceptance:** AutoPilot `[Rules]` line for Friday's full card at 1600×900 ≥ 28, Monday's unchanged, 0 overflowing or
overlapping texts in the text audit.
**Verify:** AutoPilot log and a screenshot.

Final checks after the last item: Linux build and validator, unit tests, the audit, the four AutoPilot policies with the
text audit, the nudge tour, the padtest and the taptest, with the load average noted, and the real config folder compared
with its state at the start of the round.

## Round 7 results (7 Oct 2026)

All three planned items landed on `improvements-7`, one commit each. Screenshots and logs are in
[`media/improvements/round7/`](media/improvements/round7/). Load average 16–37 throughout, from the other sessions on the
machine; it's noted with each run below.

| Item | Result | How it was verified |
|---|---|---|
| **R7-A. Play on without quitting** | **A real leak.** The game rebuilds itself in place on *Start the day again*, *Back to the title* and the end of the week, and each rebuild left about 11 materials and 1–2 textures behind: 5 made at runtime for the stamp's shadow, stamp marks and the like, 4 TextMesh Pro material instances, the window glass's and the lamp bulb's instances, and the title's gradient. Nothing ever let go of them, because no scene is loaded. Unused assets are now unloaded behind the fade each morning and on the way back to the title, as a scene load would do. New: `Tools/unity.sh soak` goes round the menus' own buttons in one process and logs memory and live objects after each rebuild, and AutoPilot `-lafWeeks 2` plays whole weeks back to back. | Soak, 30 cycles. **Before** (load 17–36): materials 590 → 1012, textures 204 → 253, resident memory 414 → 465 MB, FAIL. **After** (load 28–31): flat over the last 10 cycles (materials 681, textures 203, meshes 199, GameObjects 547, Unity 384 MB, resident 415 MB), PASS. Which assets grew was logged by name. **Two `best` weeks in one process** (round 6's crash path, load 26–36): both 24/24, *The 9:40*, no crash, 0 overflowing or overlapping texts in 192 screenshots, and the live object counts at the end of week 2 match week 1 exactly. `soak_before_after.txt`. |
| **R7-B. No progress lost by surprise** | *Start the day again* in the pause menu, once a claim is decided, now reads "Click again to undo today's one claim" first. *Choose a Day* during a week asks before rewinding ("Monday · click again to rewind the week from Tuesday", or "…start it again (one claim undone)" for today). Choices that lose nothing act at once, as before. A replay after a finished week is a day under way again: the title offers *Continue* (before, it never appeared, so a half-played replay was lost). The ending stays reached. | 4 new unit tests (**76 in all**, all pass). The soak checks every click it makes: a confirm appeared exactly when it worked out that progress would be lost (20 of 20 in the 30-cycle run), a single click changed nothing, and a second click acted. Started from a finished `best` week: replaying Tuesday didn't ask, *Continue · Tuesday, claimant 2 of 5* was on the title and resumed after 1 claim, and the later rewinds asked. `confirm_start.jpg`, `confirm_monday.jpg`, `title_continue_after_finished_week.jpg`, `progress_guard_soak.txt`. |
| **R7-C. The full rules card's size** | The rules now run to just above *Put it back*. | AutoPilot `[Rules]`: Friday's full card **27.7 → 28.6** at 1600×900 (glosses 22.8) and at 2560×1080; 25.7 → 26.6 at 1280×720 with Large text and Plain lettering; Monday's card unchanged at 30. 0 overflowing or overlapping texts in every run. `rules_full_friday_*.jpg`. |

### Final checks (final Linux build, load average 16–37)
Linux build and validator: 28 objects, 5 days, 25 cases, **0 issues**. **76/76 unit tests.** Audit: PASS, 84/84 details, 0
below 10%. AutoPilot, each with the text audit: `best` 24/24 *The 9:40*; `worst` *Grey Ninefold*; `wait` *The Long Wait*
(23/24); `refuse` (6/24); `best` at 1280×720 with Large text and Plain lettering 24/24; Friday at 2560×1080. **0 overflowing and
0 overlapping texts** in every run. Nudge tour: 210 nudges, 24/24, 21 details at the glint and 2 by the part that lit up.
Padtest PASS and taptest 14 of 14, each started below load 24 (21 and 23). Every run printed the guard's "untouched". The real
`~/.config/unity3d/Nearby/Lost & Found/` was **identical (sizes, timestamps, hashes) at the end of the round and at its start**,
and no `laf.` key appeared in `unknown/unknown/prefs`. `final_checks.txt`.

### Not done, and why
- **Resident memory over two weeks.** The live object counts are identical at the end of each week, and Unity's own count of
  allocated memory rose 1.5 MB, but the process's resident memory rose 43 MB (486 → 528 MB). That looks like the allocator or
  the graphics driver keeping pages rather than a leak in the game, but nothing here can prove it without a native memory
  profiler. It's in the README's known issues.
- **Round 6's signal 11 wasn't reproduced.** Its log was overwritten in round 6. The same path (a second week in one process)
  ran cleanly once this round, alongside 36 soak rebuilds, and the leak it may have been near is fixed. That's not proof that the leak was the cause.
- **A first-launch window larger than a small screen** (the default is 1600×900, windowed) can't be checked here: there's no
  virtual X server (Xvfb) on this machine, and installing one is outside the repo.
- **The trailer and teaser** still show v0.1.0, and **the GUI editor's Play** is still unverified on Wayland. Both as before.

### Still open after round 7
- No human has played any of it. That now includes the new confirms' wording.
- WebGL in Firefox and Safari, audible sound, browser input devices, and hosting (owner). The local WebGL build predates this
  round. Nothing in it is WebGL-specific, so I didn't spend a 12-minute build on it.
- Gamepad and touchpad on real hardware, macOS on a Mac, and Windows (blocked on the module).
- Whether curiosities should survive a new week (owner).

### Needs a decision from the owner
Unchanged from round 6: re-cutting the trailer and teaser, hosting WebGL, curiosities across weeks, whether `strace` (or Xvfb,
for checking small screens) may be installed, and the licence, releases and tags, signing, and Windows Build Support.

## Round 8 scope (7 Oct 2026)

Branch `improvements-8`, one commit per item. Screenshots and logs go in
[`media/improvements/round8/`](media/improvements/round8/). Round 7 left "a first-launch window larger than a small screen"
unchecked because there's no Xvfb here. Before choosing, I found another way to check it without installing anything:
**KWin, already on this machine, runs headless** (`kwin_wayland --virtual`) at any screen size, inside its own D-Bus session
and scratch config folders, so nothing appears on the shared desktop and the real session's settings aren't touched. Its
own scripting interface reports where the game's window lands, and `spectacle` can photograph its screen. The first probe
answered round 7's question at once: **on a 1366×768 laptop the game opens a 1600×900 window** (KWin: frame 1600×928 on a
1366×768 output), so the shelf arrow, the claim slip, the hint bar and every control strip are off the screen
(`window_1366x768_before_after.jpg`, left). With the output scaled to 200% (a 2732×1536 panel), Unity sees a 1366×768 desktop and does
the same. That's the first item. Following what else a new player meets in the first minutes turned up the rest.

### R8-A. The window fits the screen
- At start-up, a window that wouldn't fit the desktop (Unity's desktop size, which is in scaled points on a HiDPI screen)
  becomes the largest 16:9 window that does, leaving room for a title bar and a panel. That covers the first launch,
  a window remembered from a bigger monitor, and leaving fullscreen from Settings (which otherwise keeps the screen's
  whole size as a window). A window that already fits is left alone, and so is a size given on the command line.
- The sizing is a pure function, unit-tested.
- A new `Tools/unity.sh smallscreen <w> <h> [scale] [player args]` runs the player in a headless, sandboxed KWin of that size
  and reports where KWin put the window, with a picture of the whole screen.

**Acceptance:** at 1366×768, 1280×800 at 200%, 1440×900 and 1280×720, KWin reports the window's frame (title bar
included) inside the output on first launch, and the screen picture shows the whole desk and its UI; at 1920×1080 the
window is still 1600×900. Turning Fullscreen on and off again in Settings at 1366×768 ends in a window that fits. A full
`best` week at the fitted 1366×768 size with the text audit: 24/24, 0 overflowing and 0 overlapping texts.
**Verify:** `smallscreen` logs and screen pictures before and after, unit tests, the AutoPilot log.

### R8-B. The ledger's *Replay the day* asks first
Round 7 made every menu choice that throws away claims ask for a second click, but missed one: **the Day Ledger's
*Replay the day*, right beside *Next morning*, rewinds the day just finished with one click** (every claim of the day
undone, and its morning played again).
- It asks as the pause menu's *Start the day again* does ("Click again to undo today's five claims"), from the same
  `ProgressGuard` wording.

**Acceptance:** a unit test for the wording; in the player, one click on *Replay the day* changes nothing and shows the
confirm, a second click replays the day as before (the soak clicks it).
**Verify:** test results, soak log and a screenshot of the confirm.

### R8-C. Keyboard layouts: the letters on the keys
Every letter shortcut is read by its position on a US keyboard. On a French (AZERTY) keyboard the controls card's
"`A` / `D` to turn" is the key printed **Q**, `Q` is printed **A** and `W` is **Z**; on Dvorak none of `H`, `R`, `L` and `T`
is where its letter is. A player who presses what the card says gets something else.
- The mnemonic letters (`H` nudge, `R` rules, `L` lamp, `T` tray) follow the letter printed on the player's keyboard.
- The turning keys (`A`/`D` and `Q` `E` `W` `S`) keep their places under the left hand, as WASD games do, but every prompt,
  the Controls card and the nudges name them by what's printed on the player's keyboard.
- Only if a probe first shows the Linux player can see the keyboard layout (in the sandboxed KWin with a French layout);
  otherwise this is recorded and dropped.

**Acceptance:** unit tests of the key resolution and labels for US, French, German and Dvorak layouts. In the sandboxed KWin
with a French layout, the player logs `H R L T` on their printed keys and the Controls card and inspect bar read "Q / D" and
"A E Z S"; with US, nothing changes. Nobody presses a physical key here, so that the right action happens on a real French
keyboard rests on Unity's own key mapping, and is said so.
**Verify:** test results, logs and screen pictures for each layout.

### R8-D. Stretch: quiet in the background
The game draws at full rate and keeps playing its music when you switch to another window (it's set to run in the
background, which the test tools need), and a pointer that leaves the window at its edge keeps turning the desk.
- Out of focus, it draws at 10 fps; a Settings toggle, *Sound when in the background* (on by default), mutes it there; no
  edge turns while out of focus. Test tools keep full speed.

**Acceptance:** in the sandboxed KWin, opening another window over the game drops it to about 10 fps and back to full on
return (logged), with the toggle off the master volume goes to 0 and back.
**Verify:** the `smallscreen` log.

### R8-E. The button under the pointer stays readable (added during the round)
Found while checking R8-B's confirm: the Day Ledger's *Next morning* and *Replay the day*, the ending's *Close the shutter*
and the week summary's *Back to the title* are light words on the dark below the page, but turn the paper buttons' oxblood
when pointed at, so the button you're about to click all but disappears.
- They turn gold instead, as the title's menu does.

**Acceptance:** the hovered *Next morning* reads at 4.5:1 or better against the dark behind it (measured on a soak
screenshot before and after, as in round 1).
**Verify:** the two screenshots and the measured contrast.

Final checks after the last item: Linux build and validator, unit tests, the audit, the four AutoPilot policies with the
text audit, the nudge tour, the padtest and the taptest, with the load average noted, and the real config folder compared
with its state at the start of the round.

## Round 8 results (7 Oct 2026)

Four of the five items landed on `improvements-8`, one commit each; R8-C was dropped after its probe, as the scope
allowed. Screenshots and logs are in [`media/improvements/round8/`](media/improvements/round8/). Load average 16–37 from
the other sessions on the machine (83 for a moment during the nudge tour), noted with each run.

| Item | Result | How it was verified |
|---|---|---|
| **R8-A. The window fits the screen** | **A real first-launch problem.** The player always asked for 1600×900, and KWin put that window's 1600×928 frame on a 1366×768 screen: the shelf arrow, the claim slip, the hint bar and every control strip were off the screen. The same happened at 1440×900 (a MacBook's default), 1280×720, and on a 2560×1600 panel at 200% (Unity sees a 1280×800 desktop there). A window that doesn't fit now becomes the largest 16:9 one that does, at the first launch, for a window remembered from a bigger screen, and on leaving fullscreen. **Also found: the *Fullscreen* setting did nothing on Wayland** (Unity reported `FullScreenWindow` while KWin showed an ordinary window); it now asks for the desktop's resolution in that mode. New `Tools/unity.sh smallscreen`: the player in a headless, sandboxed KWin of any size, with KWin's own report of the window and a picture of the whole screen. | KWin's frame on first launch, before → after: 1366×768 1600×928 → **1200×702** at (83, 33); 1280×800 at 200% → **1176×690**; 1440×900 → **1324×772**; 1280×720 → **1124×660**; 1920×1080 unchanged at 1600×928. Fullscreen on and off at 1366×768: KWin `fullscreen=true` at 1366×768, then back to 1200×702 (`fullscreen_round_trip_1366x768.jpg`). A game saved in fullscreen starts fullscreen; a remembered 1200×674 window is kept. 9 new unit tests. A full `best` week at the fitted 1200×674 with the text audit: 24/24, *The 9:40*, **0 overflowing and 0 overlapping texts** in 96 screenshots (load 23–31). `window_*_before_after.jpg`, `smallscreen_runs.txt`. |
| **R8-B. The ledger's *Replay the day* asks first** | One click beside *Next morning* used to rewind the whole day just finished. It now reads "Click again to undo today's five claims" (ProgressGuard's wording, in a warning colour, on one line clear of *Next morning*) and replays on the second click. | 1 new unit test (86 in all). The soak now finishes a day and clicks the real button: one click asked and changed nothing; the second replayed the day from its morning with 0 claims decided (12-, 3- and 30-cycle runs). `ledger_replay_confirm.jpg`, `ledger_replay_soak.txt`. |
| **R8-C. Keyboard layouts** | **Dropped.** Its probe: in the headless KWin with a French layout (KWin confirmed `fr`), and through the sandbox's own Xwayland set to French with `setxkbmap`, the Linux player reported US labels for every key and `layout 'us'`; German and Dvorak the same. The virtual seat has no physical keyboard, so this doesn't prove a real French keyboard is read the same way, but nothing here could show the layout-aware version working, so it wasn't built. The README's known issues now say what an AZERTY or Dvorak player meets. A `[Keys]` line in every player log records what the player sees. | `[Keys]` lines from the three runs and KWin's `getLayoutsList`. |
| **R8-D. Quiet in the background** | Out of focus the game draws 10 frames a second (vsync off, since it overrides the cap); Settings › *Sound when in the background* (on by default) silences it there; a pointer that left the window by its side no longer turns the desk while out of focus. Test tools keep full speed. | Headless KWin with a `kdialog` window over the game from 14 s to 26 s (`LAF_STEAL`): **60 → 10.0 → 60 fps**, volume 1 throughout; with the setting off, volume 0 behind the other window and 1 on return (`background_runs.txt`, load 20–24). The Settings card fits at 1920×1080 and in the fitted 1366×768 window (`settings_1366x768.jpg`). **Not checked:** the edge-turn guard, since nothing here can move a pointer out of a window in the sandbox. |
| **R8-E. The button under the pointer stays readable** (added during the round) | The ledger's buttons, the ending's *Close the shutter* and the week summary's *Back to the title* are light words on the dark, but turned the paper buttons' oxblood when pointed at. They turn gold now, as the title's menu does. | Soak screenshot of the ledger with *Next morning* under the pointer: **1.6:1 → 11.1:1** against the dark behind it (`ledger_hover_next_before_after.jpg`). Only the ledger's was photographed; the other two are the same one-line change. |

### Final checks (final Linux build, load average 15–37)
Linux build and validator: 28 objects, 5 days, 25 cases, **0 issues**. **86/86 unit tests.** Audit: PASS, 84/84 details, 0
below 10%. AutoPilot with the text audit: `best` 24/24 *The 9:40*; `worst` *Grey Ninefold*; `wait` *The Long Wait* (23/24);
`refuse` (6/24); **0 overflowing and 0 overlapping texts** in every one. Nudge tour: 210 nudges, 24/24, 21 details at the
glint and 2 by the part that lit up. Padtest PASS and taptest 14 of 14, each started below load 24 (21 and 15). Soak, 30
cycles plus the ledger's replay: PASS, live objects flat over the last 10 cycles (materials 681, textures 203, meshes 199,
GameObjects 547). Every run printed the guard's "untouched". The real `~/.config/unity3d/Nearby/Lost & Found/` was **identical
(sizes, timestamps, hashes) at the end of the round and at its start**, and no `laf.` key appeared in `unknown/unknown/prefs`.
`final_checks.txt`.

### About the headless KWin
It's `kwin_wayland --virtual`, already installed, run under `dbus-run-session` with `XDG_CONFIG_HOME`, `XDG_DATA_HOME`,
`XDG_CACHE_HOME` and `XDG_STATE_HOME` all under `Logs/config/`, a socket of its own, and `DISPLAY` and `WAYLAND_DISPLAY`
unset, so it never talks to the desktop's KWin or its D-Bus session and nothing appears on screen. Screen pictures come from
`spectacle` inside that session (`KWIN_SCREENSHOT_NO_PERMISSION_CHECKS=1`, which only affects that KWin). KWin keeps the last
run's output scale in its scratch `kwinoutputconfig.json`, which the tool deletes before each run. Nothing was installed.

### Not done, and why
- **Keyboard layouts** (R8-C, above).
- **The fitted window is small on a small screen.** At 1366×768 the game draws at 1200×674, so the UI is 62% of its
  1080p size. It fits and nothing overflows, but *Fullscreen* or *Large text* may suit such a screen better. Opening
  fullscreen on small screens by default is a design call I didn't make.
- **HiDPI sharpness.** On a 200% screen Unity draws at the scaled size and the compositor enlarges it. That was so before
  and is unchanged.
- **macOS:** the window fitting uses Unity's own desktop size and should apply there too, but it hasn't been run on a Mac.
- The local WebGL build still predates rounds 7 and 8 (neither changes the browser build), and resident memory over two
  weeks, round 6's crash, the trailer and the GUI editor's Play are as round 7 left them.

### Still open after round 8
- No human has played any of it.
- Keyboard layouts other than US (above). WebGL in Firefox and Safari, audible sound, browser input devices, and hosting
  (owner). Gamepad and touchpad on real hardware, macOS on a Mac, and Windows (blocked on the module).
- Whether curiosities should survive a new week (owner).

### Needs a decision from the owner
- Whether small screens should open fullscreen by default rather than in the fitted window.
- Whether to let players choose a keyboard layout (or rebind keys) by hand, since the Linux player doesn't report it.
- Unchanged from round 7: re-cutting the trailer and teaser, hosting WebGL, curiosities across weeks, installing `strace` or
  Xvfb (the headless KWin now covers small screens), and the licence, releases and tags, signing, and Windows Build Support.

## Round 9 scope (7 Oct 2026)

Branch `improvements-9`, one commit per item. Screenshots and logs go in
[`media/improvements/round9/`](media/improvements/round9/). Before choosing, I went through the latest AutoPilot screenshots
and the Settings, pause menu and Day Ledger code as a player would meet them. The desk is lit for mood: most of every
counter-view frame is near black, and there's **no brightness setting** for a dim laptop panel or a bright room. **What you
decided on earlier days can't be looked up**: each evening's ledger is shown once and gone until the week's summary, though the
story keeps coming back to it (strays refused on Monday return later, Vell keeps count, Thursday's choice changes Friday).
**Pushing the pointer to the side turns the desk**, with no way to turn that off, and in a window (the default since round 8)
the side of the window is where the pointer leaves for the rest of the desktop. And nothing in the game says which build it
is, which a bug report needs.

### R9-A. A brightness setting
- Settings › *Brightness*, a slider from darker to much brighter, applied to the 3D scene (the desk, the window, the objects
  in your hands) and not to the paper UI, which already reads on its own. The middle of the slider is the game as it is now.
- Applies at once, persists in `settings.json`, unit-tested (default, clamp, round trip). The Settings card still fits.

**Acceptance:** the counter view's mean luminance, measured on screenshots of the same moment, rises steadily from the lowest
to the highest setting, and the default is the same picture as before; the UI's text pixels are unchanged. The Settings card,
one row longer, fits with 0 overflowing and 0 overlapping texts at 1920×1080, the fitted 1200×674 window and 2560×1080.
**Verify:** screenshots and measured luminance, test results, the text audit on the Settings card.

### R9-B. The week's ledger at hand
- The ledger book on the desk (bottom right) can be opened: hover says what it is, a click shows the Day Ledger page of each
  day already closed this week, exactly as it was that evening (rows, explanations, marks, tally, the Gazette), with ‹ › to
  turn the pages and *Put it back* (or `Esc`, right click, B) to close. The pause menu has it too (*The week so far*).
- Only days whose evening you've seen: nothing about today's claims (their marks would give the answers away). On Monday, or
  after starting from a later day with nothing kept, the book says there's nothing in it yet.
- The evening ledger and the look-back are built by the same code, so they can't drift apart.

**Acceptance:** a unit test of which days the book holds (closed days only; a replay of an earlier day drops the later ones).
In the player, the AutoPilot opens the book each morning from Tuesday, photographs every page and logs each page's rows and
tally, which match that day's evening ledger line for line. 0 overflowing and 0 overlapping texts with the text audit, at
1600×900, at 1280×720 with Large text and Plain lettering, and at 1024×768. `best` still ends 24/24 on *The 9:40*.
**Verify:** AutoPilot logs (`[Ledger]` and `[LedgerBook]` lines compared), screenshots, test results.

### R9-C. Turning at the screen's edge, on or off
- Settings › *Turn at the screen's edge* (on by default, as now). Off, the desk turns only with the keys, the arrows on screen
  and the pad.
- In the player, a check (`-lafEdgeTest`) holds a virtual mouse at each side of the screen: the desk turns with the setting on,
  doesn't with it off, and doesn't while another window has the focus (round 8's guard, which nothing could exercise then).

**Acceptance:** the check passes all three in the headless KWin (`smallscreen` with `LAF_STEAL` for the focus case). Whether a
real pointer leaving a window by its side turns the desk can't be checked here (there's no pointer tool), and is said so.
**Verify:** the check's log lines.

### R9-D. Which build is this?
- The title shows the version and the commit the build was made from, small, in a corner (`v0.1.0 · ed71667`), and the player
  log's first lines say the same. The version itself isn't changed (that's the owner's).
- The README says where the player log is and what to send with a bug report.

**Acceptance:** the line is on the title at 1920×1080 and 1200×674, clear of the menu and the save note (text audit 0 overlap),
and matches `git rev-parse --short HEAD` at build time.
**Verify:** a title screenshot and the player log.

### R9-E. Stretch: hitches the first time something is used
Measure first: log every frame that takes over 100 ms during a whole `best` week in a fresh process, with what was happening.
If the first pick-up, drawer, ledger or photograph change stalls noticeably (say over 150 ms), warm those shaders up behind the
morning's fade, and measure again. If nothing stalls, record that and stop.

**Acceptance:** the counts and worst frames before (and after, if fixed), with the load average.
**Verify:** AutoPilot log lines.

Final checks after the last item: Linux build and validator, unit tests, the audit, the four AutoPilot policies with the text
audit, the nudge tour, the padtest, the taptest and the soak, with the load average noted, and the real config folder compared
with its state at the start of the round.

## Round 9 results (7 Oct 2026)

All four planned items and the stretch measurement landed on `improvements-9`, one commit each, plus one to the test tooling
(found before the first player run). Screenshots and logs are in [`media/improvements/round9/`](media/improvements/round9/).
Load average 13–31 from the other sessions on the machine, noted with each run.

| Item | Result | How it was verified |
|---|---|---|
| **Test windows off the desktop** (tooling, added) | Only `smallscreen` ran the player in a private headless KWin; the AutoPilot, audit, nudge tour, padtest, taptest, soak and filmed play opened ordinary windows on the shared desktop. Every built-player command in `Tools/unity.sh` now runs inside `kwin_wayland --virtual` (2560×1440, its own D-Bus session, socket and scratch XDG folders, `DISPLAY` and `WAYLAND_DISPLAY` unset), and passes the player's exit status back. `LAF_DESKTOP=1` opts out. | Every player run this round went through it: smoke at 113–126 fps, AutoPilot weeks at the usual pace, and the guard's "untouched" each time. Nothing appeared on the desktop. |
| **R9-A. Brightness** | Settings › *Brightness*: −0.6 to +1.2 stops of post exposure on the 3D scene, the middle being the game as it was; the screen-space paper UI isn't post-processed, so it's unchanged. Settings rows close up slightly to fit it (and R9-C's toggle). Smoke runs can now run the text audit too. | AutoPilot case 1.1 at five settings, same moments: the scene's mean luminance **0.151 → 0.169 → 0.189 → 0.230 → 0.275** (near-black pixels 52% → 25%), the speech bubble 0.847–0.848 throughout (`brightness.txt`, `brightness_darkest_default_brightest.jpg`). 1 new unit test (default, clamp, round trip). The Settings card at 1920×1080, 2560×1080 and the fitted 1200×674: 0 overflowing, 0 overlapping. |
| **R9-B. The week's ledger at hand** | The ledger book on the desk and the pause menu's *The week so far* open every closed day's Day Ledger page as it was that evening: drawn from the next morning's snapshot, so the Gazette is the one that ran even after later choices changed the flags. Nothing from today. ‹ › (arrows, A/D, LB/RB) turn the pages; Esc, right click, B or *Put it back* close it. The evening ledger now builds its page with the same code and reveals it as before. Gus mentions the book on Tuesday morning; the Controls card and README list it. | 5 new unit tests (92 in all): Monday's book is empty, closed days only (a claim decided today never shows), each page from its own evening, a replay drops the later days and rewrites its own, an older save without snapshots. The AutoPilot reads every page each morning: `best` at 1600×900, `worst` at 1024×768 and `best` at 1280×720 with Large text and Plain lettering each showed **10 pages, all 10 identical to their evening's** (the `worst` week's own headlines included), with 0 overflowing and 0 overlapping texts (`ledger_book_runs.txt`). On Tuesday it opened the book through the pause menu's real button. The game's own picking reaches the book on the desk from 11–43 points at 1600×900, 1024×768, 2560×1080 and 1200×674. `book_*.jpg`, `pause_menu_week_so_far.jpg`. |
| **R9-C. Turning at the screen's edge** | Settings › *Turn at the screen's edge* (on by default, as before). New `Tools/unity.sh edgetest`: a virtual mouse held at each side of the screen in the real player; the headless KWin's session opens another window once the test says it's ready, so the timing doesn't depend on load. | `edgetest` PASS, 7 holds: the desk turned left and right with the setting on, didn't with it off, **didn't while another window had the focus** (round 8's guard, which nothing could exercise then), and turned again once the focus came back (`edgetest.txt`). The first run failed only because the session's trigger was matched as a regular expression (`[EdgeTest]` is a character class); fixed with `grep -F`. **Not checked:** a real pointer leaving the window by its side (no pointer tool); the README says what's likely and that the toggle avoids it. |
| **R9-D. Which build is this?** | Each build writes the commit it's built from (`+` if the working tree had changes) to `Resources/BuildInfo.txt` (gitignored). The title shows `v0.1.0 · <commit>` small in its bottom-right corner, on a dark backing, and the player log's first lines say the same. The version is unchanged. The README says where the log is and what to send. | Built from a clean tree at `348c4f3`: the build log said "building commit 348c4f3", the player log `[Build] Lost & Found v0.1.0 · 348c4f3`, matching `git rev-parse --short HEAD`, and the tree stayed clean. Title at 1920×1080 and 1200×674: 0 overflowing, 0 overlapping (`title_build_line_*.jpg`; about 11 px tall in the small window). |
| **R9-E. Stretch: first-use hitches** | Measured, nothing to fix. `-lafHitches` logs frames over 100 ms with what was happening; `-lafNoShots` leaves out the AutoPilot's screenshots, which stall a frame themselves. | Two `best` weeks at 1600×900: Monday, where every first use falls, had **no frame over 100 ms in either**. Run 1 (load 18–31) had none at all in 20,261 frames; run 2 (load 26–30) had 52 over 100 ms (worst 224 ms), clustered on Tuesday and Wednesday as claimants walked in, at moments that were smooth in run 1: the machine's load, not first use (`hitches.txt`). |

### Final checks (final Linux build at `6aca5e3`, load average 15–59)
Linux build and validator: 28 objects, 5 days, 25 cases, **0 issues**. **92/92 unit tests.** Audit: PASS, 84/84 details, 0
below 10%. AutoPilot with the text audit at 1600×900: `best` 24/24 *The 9:40*; `worst` *Grey Ninefold*; `wait` *The Long Wait*
(23/24); `refuse` (6/24). **0 overflowing and 0 overlapping texts** in every one, and in each the ledger book showed 11 pages
(10 in the mornings, 1 from the pause menu), **all 11 identical to their evening's**. Nudge tour: 210 nudges, 24/24, 21 details
at the glint and 2 by the part that lit up. Soak, 30 cycles: PASS, live objects flat over the last 10 (textures 203,
GameObjects 547). Edge test PASS (7 holds). Padtest PASS and taptest 14 of 14, each started below load 24 (16 and 15). Every run
in the headless KWin, and every one printed the guard's "untouched". The real `~/.config/unity3d/Nearby/Lost & Found/` was
**identical (sizes, timestamps, hashes) at the end of the round and at its start**, and no `laf.` key appeared in
`unknown/unknown/prefs`. `final_checks.txt`.

### Found along the way
- **Test windows were on the shared desktop.** Only `smallscreen` used the private KWin; now every player command does (above).
- **My own loop passed one size as one argument** (`-screen-width "1920 1080"`) in the first Settings audit: zsh doesn't split
  words as bash does. It ran inside the headless KWin, so nothing reached the desktop; the three audits were redone with separate
  arguments, and only those results are reported.
- A screenshot named after the pause menu showed the ledger book: a capture is taken at the end of the frame, and the AutoPilot
  had clicked the menu's button in the same frame. It now waits a frame.

### Not done, and why
- **A real pointer leaving the window by its side** (R9-C) can't be checked: there's no pointer tool, and a uinput device would
  move the shared desktop's pointer. The README says what's likely, and the new toggle avoids it.
- **The ledger book stays where it was**, in the bottom-right corner, partly out of frame. Moving desk props changes framing the
  audit and the README stills rely on; the pause menu and Gus's line point to it. Whether to bring it into view is a design call.
- **Brightness's range** (−0.6 to +1.2 stops) was chosen by eye on this machine's screen captures; nobody has tried it on a dim
  laptop panel.
- **The WebGL build** still predates rounds 7–9. Brightness and the ledger book would work there too (and its builds would stamp
  their commit), but a 12-minute WebGL build wasn't worth it for an unhosted build on a busy machine.
- The trailer, the GUI editor's Play, macOS on a Mac, physical gamepads and touchpads: as round 8 left them.

### Still open after round 9
- No human has played any of it. That now includes the ledger book, the brightness range and Gus's new line.
- Keyboard layouts other than US, WebGL in other browsers and hosting, real input hardware, macOS, Windows: as before.

### Needs a decision from the owner
- **The version number.** The title now names the build as `v0.1.0 · <commit>`, but the game has had nine rounds since v0.1.0;
  bumping the version (and any release or tag) is the owner's call.
- **Undoing a misplaced stamp.** A stamp commits the verdict at once; the only way back is replaying the whole day. Whether a
  last-stamp undo fits the game's weight is a design call.
- Whether the ledger book should sit more prominently on the desk (above).
- Unchanged from round 8: fullscreen by default on small screens, choosing a keyboard layout or rebinding keys, re-cutting the
  trailer and teaser, hosting WebGL, curiosities across weeks, installing tools outside the repo, and the licence, releases and
  tags, signing, and Windows Build Support.

## Round 10 scope (7 Oct 2026)

Branch `improvements-10`, one commit per item. Screenshots and logs go in
[`media/improvements/round10/`](media/improvements/round10/). Before choosing, I ran Monday's first claim in the round 9 build
(`6aca5e3`) in the headless KWin at 1600×900, at 4:3 (1024×768) and at **960×1080, the shape a window takes when it's snapped to
half of a 1080p screen**, and looked at each frame as a player would. The window is resizable, and round 8 made it fit small
screens, but nothing yet looks after a window that's narrower than 16:9:
- At 960×1080 the **speech bubble covers the claimant's face**, the wallet in your hands is cut off at the left, and the lamp
  (Thursday's blue lamp), the ticket printer, the ledger book and the Iron Drawer's key are out of view. At 4:3 the lamp and the
  book are half out. The camera keeps its height of view and loses the sides; only the slip's close-up already widens.
- On Monday morning **Agnes's note opens on top of Gus's speech bubble**, at 16:9 too (at 1600×900 it covers the bubble's last
  line). The text audit compares texts on the same card only, so it never saw it.
- Separately, from the code: a stamp commits its verdict the moment it comes down, and the only way back is replaying the day.
  Whether to add an undo is the owner's call, but **nothing says what a stamp will do before it lands**: the hint reads "Click on
  the slip to stamp it" whatever you hold, and with two claimants it's the half of the slip you stamp that decides who gets it.

### R10-A. A narrow window sees the whole desk
- Below 16:9 the camera widens its view to keep the 16:9 view's width (as the slip's close-up already does), in every view: the
  counter, the drawers, the shelf, leaning in, the photographs. At 16:9 and wider nothing changes. The rule is one pure function,
  unit-tested.
- The speech bubble never covers the speaker's face: where there isn't room for it beside the head, it narrows, and failing that
  it sits above the head.

**Acceptance:** at 1024×768 and 960×1080, counter-view frames show the lamp, printer, bell, stamps and slip whole, and the object
in your hands whole; at 1600×900 and 2560×1080 the camera's field of view is the same as before. A new text-audit check (R10-B)
reports 0 frames where the bubble covers the speaker's face. A full `best` week at 960×1080 and one at 1024×768 with the text
audit: 24/24, 0 overflowing and 0 overlapping texts, and the ledger book reachable by the game's own picking.
**Verify:** screenshots before and after at each shape, unit tests, AutoPilot logs.

### R10-B. Agnes's notes never land on what's being said
- When one of Agnes's notes opens, the speech bubble still showing the line before it goes away (her rules already do this).
- The text audit gains two checks at every screenshot: a reading panel (speech bubble, Agnes's note, intake tag, nudge note, the
  card beside the slip, the rules peek) drawn over the letters of another, and the speech bubble covering the face of the
  person speaking.

**Acceptance:** the audit reports Monday's note over Gus's bubble in the round 9 build (proving it can see it), and 0 such frames
after, in the four AutoPilot policies at 1600×900 and at 1024×768 and 960×1080.
**Verify:** AutoPilot logs (`[TextAudit]` lines) before and after, screenshots.

### R10-C. Know what a stamp will do before it lands
- While you hold a stamp over the slip, the hint says what it will do there, in plain words: "RETURN: give the brown leather
  wallet to Walter Bix", "REFUSE: send Walter Bix away with nothing", "SEAL: lock the brown leather wallet in the Iron Drawer". With
  two claimants it names the one whose half you're over, and changes as you cross. If the stamp can't be used yet (nothing on the
  tray), it says why before you click, not after. Off the slip it reads as now.
- The wording is a pure function, unit-tested; it names only what's on the desk (the claimants, what's on the tray), never
  whether the verdict is right.

**Acceptance:** unit tests for every stamp with one and two claimants and with an empty tray. In the player, the AutoPilot logs
the hint at the moment each stamp comes down, and it names the verdict and the claimant the Director then records, for every
claim of the `best` and `worst` weeks; screenshots of a two-claimant claim with the stamp over each half.
**Verify:** test results, AutoPilot logs, screenshots.

### R10-D. Stretch: the WebGL build brought up to date
The local WebGL build predates rounds 7–9. Rebuild it with this round's changes and play it in headless Chrome on the real GPU
(the round 4 tooling): the smoke test and Monday through the AutoPilot, with the save surviving a reload. Only if the machine's
load allows a 12-minute build; nothing is published or hosted.

**Acceptance:** the build's size, frame rate and errors from `webgl_check.py`, and `[Auto]` lines from Chrome's console.
**Verify:** the check's logs and screenshots.

Final checks after the last item: Linux build and validator, unit tests, the audit, the four AutoPilot policies with the text
audit, the nudge tour, the padtest, the taptest, the edge test and the soak, with the load average noted, and the real config
folder compared with its state at the start of the round.

## Round 10 results (7 Oct 2026)

All three planned items and the WebGL stretch landed on `improvements-10`, with the text audit's new checks first (to measure the round 9 build) and
two follow-ups to the bubble found by the final runs. Screenshots and logs are in
[`media/improvements/round10/`](media/improvements/round10/). Load average 13–35 from the other sessions on the machine, noted
with each run.

| Item | Result | How it was verified |
|---|---|---|
| **Text audit: panels over panels, the bubble over a face** (built first) | At every AutoPilot screenshot, a reading panel (speech bubble, Agnes's note, intake tag, nudge note, the card beside the slip, the rules peek) drawn over another's letters is logged as "covers", and the speech bubble over the face of anyone at the window (a 22 cm box round their head, 10% or more of it) as "covers the face of". | On the round 9 behaviour (build `dd64f50`, Monday, `best`): at 1600×900, **Agnes's note over 6 of Gus's 90 letters**; at 960×1080, **the bubble over a face (the claimant's, or Gus's) in 13 of 14 screenshots (76–78%)** and over 21 letters of the rules peek. 1024×768 showed neither, though the note covered the bubble's paper there. `textaudit_before_after.txt`. |
| **R10-A. A narrow window sees the whole desk** | Below 16:9 the camera kept its height of view and lost the sides. Every view the game asks for (counter, drawers, shelf, leaning in, the slip, the photographs) is now framed at 16:9 and widened to keep that width (`CameraRig.FovFor`; the slip's close-up already did this and now uses it). At 16:9 and wider nothing changes. The bubble narrows (and grows taller) to fit beside the head, switches sides only if it can't, and sits above the head as a last resort; it steps aside where it would cover the open rules peek. **Follow-ups found by the final runs:** a line begun while the desk was turned to the drawers kept the bubble where that view had put it, over Mr Vell's face when you turned back; the bubble now follows its speaker every frame while the window is in view (a first try at four times a second still trailed after a turn at the AutoPilot's 4× speed, and the audit caught it in 50 screenshots across the six weeks). | 12 new unit tests (16:9 and wider unchanged; 4:3, 5:4, 16:10 and half-screen shapes keep the 16:9 width; the slip's framing as before). Screenshots before and after at 960×1080 and 1024×768 (`window_*_before_after.jpg`): the lamp, printer, bell, key, ledger book and the wallet in your hands are whole. The game's own picking reaches the ledger book from 50 of 91 points over it at 1024×768 and 56 of 147 at 960×1080 (28 of 49 at 1600×900, unchanged). Whole `best` weeks at 960×1080 and 1024×768 with the text audit: 24/24 *The 9:40*, **0 overflowing, 0 overlapping, 0 covered texts and 0 covered faces** in 114 screenshots each. |
| **R10-B. Agnes's notes never land on what's being said** | Her notes (Monday morning's, and any written mid-claim) opened over the speech bubble still showing the line before; now the bubble goes first, as it already did for her rules. The note also carries the bubble's bobbing quill, so it shows it's waiting for a click. | Monday at 1600×900: the audit's "Note covers … Gus" line gone (`note_over_bubble_1600x900_before_after.jpg`). 0 covered texts in all six final weeks. |
| **R10-C. Know what a stamp will do before it lands** | While a stamp is held over the slip, the hint says what it will do there: "RETURN: give the silver locket to Cecily Fairweather", "REFUSE: send Reggie Stokes away with nothing", "SEAL: lock the silver pocket watch in the Iron Drawer". With two claimants it follows the half of the slip under the stamp. With nothing on the tray it gives the reason before the click. Gamepad wording ("A to stamp") when the pad is in use. Each stamp is logged as `[Stamp]` in the player log, for bug reports. | 7 new unit tests (`StampPreview`: each stamp, one and two claimants, an empty tray, proper names, every case of the week reading cleanly). The AutoPilot now holds each decided stamp over the slip with a virtual mouse before every verdict and checks the hint (`[StampHint]`): **27 of 27 right** in the `best`, `wait` and `refuse` weeks and 22 of 22 in `worst` (each two-claimant case checks both halves), 0 wrong. Through the real input path, padtest and taptest each logged `[Stamp] RETURN: give the brown leather wallet to Walter Bix · A to stamp`. `stamp_hint_two_claimants_2.2.jpg`, `stamp_hints.txt`. |
| **R10-D. Stretch: the WebGL build brought up to date** | Rebuilt at `133561c` with rounds 7–10 in it: **65 MB** (data 61.0 MB, wasm 7.3 MB, Brotli), 11 min 37 s to build, load average 17–22. As before, the build rewrote `Mobile_RPAsset.asset` and left `Data/` at the project root; both were reverted and removed. Not hosted. | Headless Chrome on the Radeon (ANGLE, confirmed by the renderer string), 1600×900: the title names the build (`[Build] … 133561c, WebGLPlayer`); smoke run **43–52 fps** at load 20–22 (round 6 measured 60 at load 8). AutoPilot quit after 1.2, Chrome closed and reopened on the same profile: *Continue* resumed ("resuming after 2 case(s)"), Monday finished and Tuesday's first claim was decided; every `[StampHint]` line in the browser was right. IndexedDB held the save and its backup. Console in all three runs: only round 4's known FSR shader warning. `webgl_logs/`. |

### Final checks (final Linux build at `3ec12f1`, load average 14–31)
Linux build and validator: 28 objects, 5 days, 25 cases, **0 issues**. **111/111 unit tests** (92 before). Audit: PASS, 84/84
details, 0 below 10%. AutoPilot with the text audit at 1600×900: `best` 24/24 *The 9:40*; `worst` *Grey Ninefold*; `wait` *The
Long Wait* (23/24); `refuse` (6/24); and `best` 24/24 at 960×1080 and at 1024×768. **0 overflowing, 0 overlapping and 0 covered
texts** in every run, and **0 covered faces** in all but one screenshot: in `best` at 1600×900 the bubble's corner was over 17% of
the box round Hugo Haversham's head as it eased into place after a turn back from the drawers (his face is in full view in that
screenshot, `030_case2.3_inspect`). Every `[StampHint]` right (27, 22, 27, 27, 27, 27). Nudge tour: 210 nudges, 24/24, 21 details
at the glint and 2 by the part that lit up. Soak, 30 cycles: PASS, live objects flat over the last 10 (materials 681, textures
203, GameObjects 548). Edge test PASS (7 holds). Padtest PASS and taptest 14 of 14, started at load 17 and 18. Every player run
in the headless KWin, and every one printed the guard's "untouched". The real `~/.config/unity3d/Nearby/Lost & Found/` was
**identical (sizes, timestamps, hashes) at the end of the round and at its start**, and no `laf.` key appeared in
`unknown/unknown/prefs`. Logs: `textaudit_before_after.txt`, `stamp_hints.txt`, and under `Logs/r10/` locally.

### Found along the way
- **Two follow-ups to R10-A, from the final runs themselves.** The first full-week pass at 960×1080 found the bubble over Mr Vell's
  face three times and Big Sid's once (placed while the desk was turned to the drawers). Re-placing it four times a second fixed
  those but made it trail after every turn at 4× speed: the second pass found 50 such screenshots across the six weeks, at every
  window size. Placing it every frame brought that to the one near-miss above. Each pass is kept under `Logs/r10/first_pass` and
  `second_pass`.
- **At 4:3 a narrowed bubble first switched to the left side**, where the intake tag opens, and covered four of its letters. It now
  switches sides only if it can't fit at all.
- The new audit also saw **the bubble over the rules peek** at 960×1080 (21 letters); the bubble now steps aside while the peek
  is open over it. A turn arrow's label under the rules peek was reported too, and is left out: only reading panels are compared.

### Not done, and why
- **The desk is smaller in a narrow window.** Keeping the 16:9 width at 960×1080 draws everything at about 60% of its size in a
  1600×900 window, with more ceiling and floor. Nothing is cut off and the text audit is clean, but a 16:9 window or fullscreen
  shows it best; the README says so.
- **The bubble can graze a face for a moment** while it eases into place after a turn (above). Snapping it instead of easing would
  remove that and add a jump; I left the easing.
- As round 9 left them: a real pointer leaving the window by its side (no pointer tool), the ledger book's place on the desk at
  16:9, the brightness range on a dim panel, and no human playtest (now including the stamp's hint wording).

### Still open after round 10
- No human has played any of it.
- WebGL in other browsers and hosting, real input hardware, macOS on a Mac, Windows, keyboard layouts other than US: as before.

### Needs a decision from the owner
- Whether the stamp's hint is enough, or a last-stamp undo is still wanted (round 9's question; the hint now says what will
  happen before it does).
- Unchanged from round 9: the version number, the ledger book's place on the desk, fullscreen by default on small screens, choosing
  a keyboard layout or rebinding keys, re-cutting the trailer and teaser (which don't show these changes), hosting WebGL,
  curiosities across weeks, installing tools outside the repo, and the licence, releases and tags, signing, and Windows Build
  Support.

## Round 11 scope (8 Oct 2026)

Branch `improvements-11`, one commit per item. Screenshots and logs go in
[`media/improvements/round11/`](media/improvements/round11/). Every input test so far has used virtual Input System devices,
so the question every round has left open, whether **a real pointer leaving the window by its side turns the desk**, could
never be checked. KWin has a fake-input protocol that moves its own pointer, and each test window already runs in a private
headless KWin, so a real pointer can be driven there without touching the shared desktop. I built that first, to look before
choosing (`Tools/fakeptr.c`, `Tools/unity.sh pointertest`): in a 1600×900 window, **leaving by either side at an ordinary pace
turned the desk every time** (the player is told nothing when the pointer leaves and keeps its last position, which is at the
side); only a brisk exit, whose last reading was already past the 2.5% edge band, didn't.

### R11-A. A pointer leaving the window doesn't turn the desk
- The camera rig judges from the pointer's last step whether it has left the window (one more such step would take it onto or
  past the window's first or last pixel) and doesn't turn then. A person slowing to a stop near a side still turns it, and so
  does a pointer flung against the screen's side in a maximised or snapped window (it's stopped there in one big step). Never
  applied in fullscreen. The rule is one pure function, unit-tested. The turn arrows don't stay lit for a pointer that has
  gone, and the head-look eases back to the middle.
- `pointertest`: the headless KWin's own pointer rests inside each side, leaves by each side quickly, at an ordinary pace and
  slowly, comes back in, and is flung against the screen's side with the window snapped there.
- The headless KWin's helpers (ksecretd, xdg-desktop-portal) are stopped when its session ends: round 10's runs left about
  80 of them.

**Acceptance:** `pointertest` before the fix reports the turns on leaving (proving it can see them) and PASS after; the edge test
(virtual mouse) still passes; unit tests for the rule. No helper from this round's runs left running.
**Verify:** `Logs/pointertest.log` before and after, `edgetest`, test results, `pgrep` after the runs.

### R11-B. The speech bubble never slides over a face
Round 10's final runs left one screenshot where the bubble, easing back into place after a turn from the drawers, covered 17% of
Hugo Haversham's head box. The bubble is hidden while you're turned away, so it can wait until the turn back has finished and
appear in place instead of sliding across.

**Acceptance:** a new per-frame watch (`-lafFaceWatch` with the AutoPilot) counts frames in which the visible bubble covers 10% or
more of a face, over a whole `best` week at 1600×900: some before (to prove it sees them), 0 after; the text audit's covered
faces 0 at 1600×900, 1024×768 and 960×1080.
**Verify:** AutoPilot logs before and after.

### R11-C. Monday's first claim with real input
The drag to turn an object, the scroll wheel, right click, the keys and hovering have only ever been driven by virtual devices
queued inside the game. A new mode has the game play Monday's first claim by asking the driver for real input (pointer moves,
button presses, the wheel, key presses through KWin's fake keyboard) and checking each step did what it should.

**Acceptance:** PASS, with every step's result logged (the bell, a drawer, the wallet picked up, turned by a drag, looked at
closer with the wheel, a detail clicked, the tray, a stamp, Tab, `H`, `Esc`), in the headless KWin; anything that fails is
reported and fixed or written up.
**Verify:** the run's log and screenshots.

### R11-D. Stretch: the speech bubble fits its words
A two-line answer sits in a bubble sized for five (230 units high whatever it says), so much of the bubble is blank paper over
the window and the people behind it. Size it to the line it's saying (between a minimum and today's size).

**Acceptance:** the bubble's height follows its text; 0 overflowing, 0 overlapping, 0 covered texts and 0 covered faces in `best`
weeks at 1600×900, 1024×768 and 960×1080 and the `worst` week at 1600×900; screenshots before and after.
**Verify:** text audit, screenshots.

Final checks after the last item: Linux build and validator, unit tests, the audit, the four AutoPilot policies with the text
audit, the nudge tour, the padtest, the taptest, the edge test, the pointer test and the soak, with the load average noted, and
the real config folder compared with its state at the start of the round.
