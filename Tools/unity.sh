#!/usr/bin/env bash
# Unity 6.6 launcher for this project.
#
# The editor links against libxml2.so.2 but this distro ships libxml2.so.16, so we point the
# loader at a local copy in .unity-libs/ (copied from libxml2-legacy). `sudo pacman -S libxml2-legacy`
# makes this unnecessary.
#
#   Tools/unity.sh                 open the project in the GUI editor
#   Tools/unity.sh headless        resident batch-mode editor (serves `unity command` via Pipeline)
#   Tools/unity.sh build-linux     batch-build Builds/Linux/LostAndFound.x86_64
#   Tools/unity.sh build-mac       batch-build Builds/macOS/LostAndFound.app (universal, unsigned; untested on a Mac)
#   Tools/unity.sh build-webgl     batch-build Builds/WebGL/ (a measuring spike: not shipped or packaged)
#   Tools/unity.sh build-windows   batch-build Builds/Windows/LostAndFound.exe (needs Windows Build Support installed)
#   Tools/unity.sh run <Method>    batch-run a static editor method and quit
#   Tools/unity.sh test            run EditMode tests (results in Logs/test-results.xml)
#   Tools/unity.sh smoke [secs] [quality]  run the Linux build hands-free (quality 0|1|2 overrides the setting), screenshots in Screenshots/smoke/
#   Tools/unity.sh autopilot [speed] [day] [best|worst|wait|refuse] [player args]
#                                  play the week hands-free, screenshots in Screenshots/autopilot/
#                                  (e.g. -screen-width 1440 -screen-height 900 -screen-fullscreen 0 for 16:10)
#   Tools/unity.sh audit           hold every object in the hand and check each hidden detail can be clicked;
#                                  coverage in Screenshots/hotspots/coverage.txt, pictures of any below the bar
#   Tools/unity.sh nudgetour [speed] [w] [h] [player args]  play the week asking Agnes for every nudge and doing what they say (glints clicked,
#                                  parts worked); log in Logs/nudgetour.log, screenshots in Screenshots/nudgetour/
#   Tools/unity.sh padtest         play Monday's first case with only a virtual gamepad; screenshots in Screenshots/padtest/
#   Tools/unity.sh taptest [w] [h] [player args]  play Monday's first case with quick taps (press and release in one input update) from
#                                  virtual keyboard, mouse and gamepad; screenshots in Screenshots/taptest/
#   Tools/unity.sh trailer         film Thursday's last case and the photographs changing to Recordings/the_ring.mp4
#   Tools/unity.sh demo            record the scripted first case to Recordings/demo.mp4 (needs ffmpeg)
#   Tools/unity.sh film <name> [player args]
#                                  film whole days played through simulated mouse and keyboard, without the score,
#                                  to Recordings/<name>/take.mp4 plus markers.tsv (e.g. -lafDay 2 -lafUntil 3;
#                                  no -lafDay starts at the title). Tools/make_trailer.py cuts the trailer from these.
#
# On a Wayland session the player's X11 backend hangs waiting for XWayland to map the window,
# so built players are launched with SDL's Wayland backend whenever WAYLAND_DISPLAY is set.
#
# Built players never touch the real save or settings: each run gets XDG_CONFIG_HOME under
# Logs/config/<command>/ (Unity's Linux player keeps its prefs, and persistentDataPath, there). Batch editor runs
# (builds, tests, run) get Logs/config/editor/, which links back to the real config folder for everything but this
# game's own. The real ~/.config/unity3d/Nearby/Lost & Found/ is checked before and after (hashes and timestamps);
# if anything there changed, the run fails with status 99. Only `open` and `headless` (interactive) aren't guarded.
set -euo pipefail
UNITY="${UNITY:-$HOME/Unity/Hub/Editor/6000.6.2f1/Editor/Unity}"
PROJECT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
LIBS="$PROJECT/.unity-libs"
[ -e "$LIBS/libxml2.so.2" ] || { mkdir -p "$LIBS"; cp "$HOME/.local/share/ptt-unity-libs/libxml2.so.2"* "$LIBS/" 2>/dev/null || true; }
export LD_LIBRARY_PATH="$LIBS${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
REAL_XDG="${XDG_CONFIG_HOME:-$HOME/.config}"
REAL_CONFIG="$REAL_XDG/unity3d/Nearby/Lost & Found"

# The real folder's files: names, sizes, timestamps and hashes. Nothing this script runs may change any of them.
real_config_state() {
  [ -d "$REAL_CONFIG" ] || return 0
  (cd "$REAL_CONFIG" && find . -type f -printf '%p %s %T@\n' | sort && find . -type f -print0 | sort -z | xargs -0 -r sha256sum)
}

# guarded <name> <command...>: run it, then prove the real save and settings weren't touched.
guarded() {
  local name="$1"; shift
  local before; before="$(real_config_state)"
  local status=0
  "$@" || status=$?
  if [ "$(real_config_state)" != "$before" ]; then
    echo "[guard] the real save or settings in $REAL_CONFIG CHANGED during this run" >&2
    return 99
  fi
  echo "[guard] real save and settings untouched (scratch prefs in Logs/config/$name)"
  return $status
}

# player <command> <args...>: run a built player with scratch prefs.
player() {
  local name="$1"; shift
  [ -n "${WAYLAND_DISPLAY:-}" ] && export SDL_VIDEODRIVER=wayland
  export XDG_CONFIG_HOME="$PROJECT/Logs/config/$name"; mkdir -p "$XDG_CONFIG_HOME"
  guarded "$name" "$@"
}

# editor <args...>: run the batch editor. The editor keeps this project's PlayerPrefs (and the test runner its
# TestResults.xml) in the same folder as the game, so it gets a config folder of its own under Logs/ that links back
# to everything in the real one except this game's folder: its licence, preferences and the other projects' folders
# are found where they live, and only "Lost & Found" is scratch. Links are rebuilt on every run.
editor() {
  local root="$PROJECT/Logs/config/editor"
  mkdir -p "$root/unity3d/Nearby/Lost & Found"
  find "$root" -maxdepth 3 -type l -delete
  (
    shopt -s dotglob nullglob
    local e
    for e in "$REAL_XDG"/*; do [ "${e##*/}" = unity3d ] || ln -s "$e" "$root/"; done
    for e in "$REAL_XDG/unity3d"/*; do [ "${e##*/}" = Nearby ] || ln -s "$e" "$root/unity3d/"; done
    for e in "$REAL_XDG/unity3d/Nearby"/*; do [ "${e##*/}" = "Lost & Found" ] || ln -s "$e" "$root/unity3d/Nearby/"; done
  )
  XDG_CONFIG_HOME="$root" guarded editor "$UNITY" "$@"
}

case "${1:-open}" in
  open)        exec "$UNITY" -projectPath "$PROJECT" ;;
  headless)    exec "$UNITY" -batchmode -projectPath "$PROJECT" -logFile "$PROJECT/Logs/headless.log" ;;
  build-linux) editor -batchmode -nographics -quit -projectPath "$PROJECT" -buildTarget Linux64 \
                 -executeMethod LostAndFound.EditorTools.BuildScript.BuildLinux -logFile "$PROJECT/Logs/build.log" ;;
  build-mac)   editor -batchmode -nographics -quit -projectPath "$PROJECT" -buildTarget OSXUniversal \
                 -executeMethod LostAndFound.EditorTools.BuildScript.BuildMac -logFile "$PROJECT/Logs/build-mac.log" ;;
  build-webgl) editor -batchmode -nographics -quit -projectPath "$PROJECT" -buildTarget WebGL \
                 -executeMethod LostAndFound.EditorTools.BuildScript.BuildWebGL -logFile "$PROJECT/Logs/build-webgl.log" ;;
  build-windows) editor -batchmode -nographics -quit -projectPath "$PROJECT" -buildTarget Win64 \
                 -executeMethod LostAndFound.EditorTools.BuildScript.BuildWindows -logFile "$PROJECT/Logs/build-windows.log" ;;
  run)         editor -batchmode -nographics -quit -projectPath "$PROJECT" \
                 -executeMethod "$2" -logFile "$PROJECT/Logs/run.log" ;;
  test)        editor -batchmode -nographics -projectPath "$PROJECT" -runTests -testPlatform EditMode \
                 -testResults "$PROJECT/Logs/test-results.xml" -logFile "$PROJECT/Logs/test.log" ;;
  smoke)       rm -rf "$PROJECT/Screenshots/smoke"
               q=(); [ -n "${3:-}" ] && q=(-lafQuality "$3")   # optional picture quality 0|1|2 for this run
               player smoke timeout -s KILL $(( ${2:-30} + 60 )) "$PROJECT/Builds/Linux/LostAndFound.x86_64" \
                 -lafSmoke "$PROJECT/Screenshots/smoke" -lafSave "$PROJECT/Screenshots/smoke/save.json" -lafSeconds "${2:-30}" -lafNoVsync -lafUncapped "${q[@]}" -logFile "$PROJECT/Logs/smoke.log" ;;
  autopilot)   rm -rf "$PROJECT/Screenshots/autopilot"; mkdir -p "$PROJECT/Screenshots/autopilot"
               player autopilot timeout -s KILL 2400 "$PROJECT/Builds/Linux/LostAndFound.x86_64" -lafAutopilot "$PROJECT/Screenshots/autopilot" \
                 -lafSave "$PROJECT/Screenshots/autopilot/save.json" -lafSpeed "${2:-2}" -lafDay "${3:-1}" -lafPolicy "${4:-best}" -lafNoVsync -logFile "$PROJECT/Logs/autopilot.log" "${@:5}" ;;
  nudgetour)   rm -rf "$PROJECT/Screenshots/nudgetour"; mkdir -p "$PROJECT/Screenshots/nudgetour"
               player nudgetour timeout -s KILL 2400 "$PROJECT/Builds/Linux/LostAndFound.x86_64" -lafAutopilot "$PROJECT/Screenshots/nudgetour" -lafNudgeTour \
                 -lafSave "$PROJECT/Screenshots/nudgetour/save.json" -lafSpeed "${2:-4}" -lafNoMusic -lafNoVsync -screen-width "${3:-1600}" -screen-height "${4:-900}" -screen-fullscreen 0 \
                 -logFile "$PROJECT/Logs/nudgetour.log" "${@:5}"
               grep -a "\[Tour\]\|\[Auto\] week\|\[Auto\] problem\|\[Auto\] PASS\|\[Auto\] FAIL" "$PROJECT/Logs/nudgetour.log" | tail -n 12
               grep -a -q "\[Auto\] PASS" "$PROJECT/Logs/nudgetour.log" ;;
  audit)       mkdir -p "$PROJECT/Screenshots/hotspots"
               player audit timeout -s KILL 900 "$PROJECT/Builds/Linux/LostAndFound.x86_64" -lafAuditHotspots "$PROJECT/Screenshots/hotspots" \
                 -lafSave "$PROJECT/Screenshots/hotspots/save.json" -lafNoMusic -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 \
                 -logFile "$PROJECT/Logs/audit.log"
               grep -a "\[Audit\]" "$PROJECT/Logs/audit.log" | grep -v "^\[Audit\]   picture" | tail -n 3
               grep -a -q "\[Audit\] PASS" "$PROJECT/Logs/audit.log" ;;
  padtest)     rm -rf "$PROJECT/Screenshots/padtest"; mkdir -p "$PROJECT/Screenshots/padtest"
               player padtest timeout -s KILL 600 "$PROJECT/Builds/Linux/LostAndFound.x86_64" -lafGamepadTest "$PROJECT/Screenshots/padtest" -lafDay 1 \
                 -lafSave "$PROJECT/Screenshots/padtest/save.json" -lafNoMusic -screen-width 1600 -screen-height 900 -screen-fullscreen 0 \
                 -logFile "$PROJECT/Logs/padtest.log"
               grep -a "\[PadTest\]\|\[Pad\]" "$PROJECT/Logs/padtest.log"
               grep -a -q "\[PadTest\] PASS" "$PROJECT/Logs/padtest.log" ;;
  taptest)     rm -rf "$PROJECT/Screenshots/taptest"; mkdir -p "$PROJECT/Screenshots/taptest"
               player taptest timeout -s KILL 600 "$PROJECT/Builds/Linux/LostAndFound.x86_64" -lafTapTest "$PROJECT/Screenshots/taptest" -lafDay 1 \
                 -lafSave "$PROJECT/Screenshots/taptest/save.json" -lafNoMusic -screen-width "${2:-1600}" -screen-height "${3:-900}" -screen-fullscreen 0 \
                 -logFile "$PROJECT/Logs/taptest.log" "${@:4}"
               grep -a "\[TapTest\]" "$PROJECT/Logs/taptest.log"
               grep -a -q "\[TapTest\] PASS" "$PROJECT/Logs/taptest.log" ;;
  trailer)     out="$PROJECT/Recordings"; rm -rf "$out/raw"; mkdir -p "$out/raw"
               player trailer timeout -s KILL 1500 "$PROJECT/Builds/Linux/LostAndFound.x86_64" -lafAutopilot "$out/raw/shots" -lafDay 4 -lafSpeed 1 \
                 -lafDemo "$out/raw" -lafRecordOnly -lafRecordFrom 4.5 -lafSave "$out/raw/save.json" -lafNoVsync \
                 -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 -logFile "$PROJECT/Logs/trailer.log"
               rate=$(grep -a -o '[0-9]* Hz x' "$PROJECT/Logs/trailer.log" | head -1 | cut -d' ' -f1)
               ch=$(grep -a -o 'Hz x[0-9]' "$PROJECT/Logs/trailer.log" | head -1 | tail -c 2)
               exec ffmpeg -y -loglevel error -i "$out/raw/video.mp4" -f f32le -ar "${rate:-48000}" -ac "${ch:-2}" -i "$out/raw/audio.f32" \
                 -c:v copy -c:a aac -b:a 192k -shortest -movflags +faststart "$out/the_ring.mp4" ;;
  demo)        out="$PROJECT/Recordings"; rm -rf "$out/raw"; mkdir -p "$out/raw"
               player demo timeout -s KILL 900 "$PROJECT/Builds/Linux/LostAndFound.x86_64" -lafDemo "$out/raw" \
                 -lafSave "$out/raw/save.json" -lafNoVsync -screen-width 1920 -screen-height 1080 \
                 -screen-fullscreen 0 -logFile "$PROJECT/Logs/demo.log"
               rate=$(grep -a -o '[0-9]* Hz x' "$PROJECT/Logs/demo.log" | head -1 | cut -d' ' -f1)
               ch=$(grep -a -o 'Hz x[0-9]' "$PROJECT/Logs/demo.log" | head -1 | tail -c 2)
               exec ffmpeg -y -loglevel error -i "$out/raw/video.mp4" -f f32le -ar "${rate:-48000}" -ac "${ch:-2}" -i "$out/raw/audio.f32" \
                 -c:v copy -c:a aac -b:a 192k -shortest -movflags +faststart "$out/demo.mp4" ;;
  film)        name="${2:?usage: $0 film <name> [player args]}"; shift 2
               out="$PROJECT/Recordings/$name"; rm -rf "$out"; mkdir -p "$out"
               player "film-$name" timeout -s KILL 3600 "$PROJECT/Builds/Linux/LostAndFound.x86_64" -lafDemo "$out" -lafPlay -lafNoMusic \
                 -lafSave "$out/save.json" -lafNoVsync -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 \
                 -logFile "$out/player.log" "$@"
               rate=$(grep -a -o '[0-9]* Hz x' "$out/player.log" | head -1 | cut -d' ' -f1)
               ch=$(grep -a -o 'Hz x[0-9]' "$out/player.log" | head -1 | tail -c 2)
               exec ffmpeg -y -loglevel error -i "$out/video.mp4" -f f32le -ar "${rate:-48000}" -ac "${ch:-2}" -i "$out/audio.f32" \
                 -c:v copy -c:a pcm_s16le -shortest "$out/take.mkv" ;;
  *) echo "usage: $0 [open|headless|build-linux|build-mac|build-windows|run <Method>|test|smoke [secs]|autopilot|audit|nudgetour|padtest|taptest|demo|trailer|film <name>]" >&2; exit 2 ;;
esac
