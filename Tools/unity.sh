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
#   Tools/unity.sh run <Method>    batch-run a static editor method and quit
#   Tools/unity.sh test            run EditMode tests (results in Logs/test-results.xml)
#   Tools/unity.sh smoke [secs]    run the Linux build hands-free, screenshots in Screenshots/smoke/
#   Tools/unity.sh autopilot [speed] [day] [best|worst|wait]
#                                  play the week hands-free, screenshots in Screenshots/autopilot/
#   Tools/unity.sh trailer         film Thursday's last case and the photographs changing to Recordings/the_ring.mp4
#   Tools/unity.sh demo            record the scripted first case to Recordings/demo.mp4 (needs ffmpeg)
#
# On a Wayland session the player's X11 backend hangs waiting for XWayland to map the window,
# so built players are launched with SDL's Wayland backend whenever WAYLAND_DISPLAY is set.
set -euo pipefail
UNITY="${UNITY:-$HOME/Unity/Hub/Editor/6000.6.2f1/Editor/Unity}"
PROJECT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
LIBS="$PROJECT/.unity-libs"
[ -e "$LIBS/libxml2.so.2" ] || { mkdir -p "$LIBS"; cp "$HOME/.local/share/ptt-unity-libs/libxml2.so.2"* "$LIBS/" 2>/dev/null || true; }
export LD_LIBRARY_PATH="$LIBS${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"

case "${1:-open}" in
  open)        exec "$UNITY" -projectPath "$PROJECT" ;;
  headless)    exec "$UNITY" -batchmode -projectPath "$PROJECT" -logFile "$PROJECT/Logs/headless.log" ;;
  build-linux) exec "$UNITY" -batchmode -nographics -quit -projectPath "$PROJECT" \
                 -executeMethod LostAndFound.EditorTools.BuildScript.BuildLinux -logFile "$PROJECT/Logs/build.log" ;;
  run)         exec "$UNITY" -batchmode -nographics -quit -projectPath "$PROJECT" \
                 -executeMethod "$2" -logFile "$PROJECT/Logs/run.log" ;;
  test)        exec "$UNITY" -batchmode -nographics -projectPath "$PROJECT" -runTests -testPlatform EditMode \
                 -testResults "$PROJECT/Logs/test-results.xml" -logFile "$PROJECT/Logs/test.log" ;;
  smoke)       [ -n "${WAYLAND_DISPLAY:-}" ] && export SDL_VIDEODRIVER=wayland
               rm -rf "$PROJECT/Screenshots/smoke"
               exec timeout -s KILL $(( ${2:-30} + 60 )) "$PROJECT/Builds/Linux/LostAndFound.x86_64" \
                 -lafSmoke "$PROJECT/Screenshots/smoke" -lafSave "$PROJECT/Screenshots/smoke/save.json" -lafSeconds "${2:-30}" -lafNoVsync -logFile "$PROJECT/Logs/smoke.log" ;;
  autopilot)   [ -n "${WAYLAND_DISPLAY:-}" ] && export SDL_VIDEODRIVER=wayland
               rm -rf "$PROJECT/Screenshots/autopilot"; mkdir -p "$PROJECT/Screenshots/autopilot"
               exec timeout -s KILL 2400 "$PROJECT/Builds/Linux/LostAndFound.x86_64" -lafAutopilot "$PROJECT/Screenshots/autopilot" \
                 -lafSave "$PROJECT/Screenshots/autopilot/save.json" -lafSpeed "${2:-2}" -lafDay "${3:-1}" -lafPolicy "${4:-best}" -lafNoVsync -logFile "$PROJECT/Logs/autopilot.log" ;;
  trailer)     [ -n "${WAYLAND_DISPLAY:-}" ] && export SDL_VIDEODRIVER=wayland
               out="$PROJECT/Recordings"; rm -rf "$out/raw"; mkdir -p "$out/raw"
               timeout -s KILL 1500 "$PROJECT/Builds/Linux/LostAndFound.x86_64" -lafAutopilot "$out/raw/shots" -lafDay 4 -lafSpeed 1 \
                 -lafDemo "$out/raw" -lafRecordOnly -lafRecordFrom 4.5 -lafSave "$out/raw/save.json" -lafNoVsync \
                 -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 -logFile "$PROJECT/Logs/trailer.log"
               rate=$(grep -a -o '[0-9]* Hz x' "$PROJECT/Logs/trailer.log" | head -1 | cut -d' ' -f1)
               ch=$(grep -a -o 'Hz x[0-9]' "$PROJECT/Logs/trailer.log" | head -1 | tail -c 2)
               exec ffmpeg -y -loglevel error -i "$out/raw/video.mp4" -f f32le -ar "${rate:-48000}" -ac "${ch:-2}" -i "$out/raw/audio.f32" \
                 -c:v copy -c:a aac -b:a 192k -shortest -movflags +faststart "$out/the_ring.mp4" ;;
  demo)        [ -n "${WAYLAND_DISPLAY:-}" ] && export SDL_VIDEODRIVER=wayland
               out="$PROJECT/Recordings"; rm -rf "$out/raw"; mkdir -p "$out/raw"
               timeout -s KILL 900 "$PROJECT/Builds/Linux/LostAndFound.x86_64" -lafDemo "$out/raw" \
                 -lafSave "$out/raw/save.json" -lafNoVsync -screen-width 1920 -screen-height 1080 \
                 -screen-fullscreen 0 -logFile "$PROJECT/Logs/demo.log"
               rate=$(grep -a -o '[0-9]* Hz x' "$PROJECT/Logs/demo.log" | head -1 | cut -d' ' -f1)
               ch=$(grep -a -o 'Hz x[0-9]' "$PROJECT/Logs/demo.log" | head -1 | tail -c 2)
               exec ffmpeg -y -loglevel error -i "$out/raw/video.mp4" -f f32le -ar "${rate:-48000}" -ac "${ch:-2}" -i "$out/raw/audio.f32" \
                 -c:v copy -c:a aac -b:a 192k -shortest -movflags +faststart "$out/demo.mp4" ;;
  *) echo "usage: $0 [open|headless|build-linux|run <Method>|test|smoke [secs]|demo]" >&2; exit 2 ;;
esac
