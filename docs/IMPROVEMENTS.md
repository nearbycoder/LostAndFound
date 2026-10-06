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
