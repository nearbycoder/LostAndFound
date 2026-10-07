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
