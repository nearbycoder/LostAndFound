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
