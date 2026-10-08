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
#   Tools/unity.sh smoke [secs] [quality] [player args]  run the Linux build hands-free (quality 0|1|2 overrides the setting),
#                                  screenshots in Screenshots/smoke/ (e.g. 6 2 -lafShowSettings -screen-width 1280 -screen-height 720)
#   Tools/unity.sh autopilot [speed] [day] [best|worst|wait|refuse] [player args]
#                                  play the week hands-free, screenshots in Screenshots/autopilot/
#                                  (e.g. -screen-width 1440 -screen-height 900 -screen-fullscreen 0 for 16:10)
#                                  LAF_AUTOPILOT_SAVE=<path> keeps the save elsewhere, so several runs can share one (endings reached)
#   Tools/unity.sh audit           hold every object in the hand and check each hidden detail can be clicked;
#                                  coverage in Screenshots/hotspots/coverage.txt, pictures of any below the bar
#   Tools/unity.sh nudgetour [speed] [w] [h] [player args]  play the week asking Agnes for every nudge and doing what they say (glints clicked,
#                                  parts worked); log in Logs/nudgetour.log, screenshots in Screenshots/nudgetour/
#   Tools/unity.sh padtest         play Monday's first case with only a virtual gamepad; screenshots in Screenshots/padtest/
#   Tools/unity.sh taptest [w] [h] [player args]  play Monday's first case with quick taps (press and release in one input update) from
#                                  virtual keyboard, mouse and gamepad; screenshots in Screenshots/taptest/
#   Tools/unity.sh edgetest [w] [h]  hold a virtual mouse at the screen's sides: the desk turns with Settings > Turn at the
#                                  screen's edge on, not with it off, and not while another window (opened in the headless KWin
#                                  once the test is ready) has the focus; log in Logs/edgetest.log
#   Tools/unity.sh pointertest [w] [h]  the headless KWin's own pointer (Tools/fakeptr.c, fake input granted to that KWin only)
#                                  rests inside each side of the window (the desk turns) and leaves it by each side, quickly and
#                                  slowly (it mustn't); log in Logs/pointertest.log
#   Tools/unity.sh soak [cycles] [player args]  play on without quitting: restart the day, back to the title, Continue and
#                                  Choose a Day again and again in one process, logging memory and live objects after each
#                                  rebuild; log in Logs/soak.log, screenshots in Screenshots/soak/
#                                  LAF_SOAK_SAVE=<path> starts it from an existing save (a finished week, say) instead of a new one
#   Tools/unity.sh smallscreen [w] [h] [scale] [player args]
#                                  a first launch on a screen of that size: the player (a short smoke run) inside a headless KWin of
#                                  its own (its own D-Bus session and scratch folders: nothing shows on the desktop, and the
#                                  real session's settings aren't touched). KWin reports where the window is, and spectacle
#                                  photographs the whole screen, at each of LAF_SHOTS seconds (default 14). Results in
#                                  Screenshots/smallscreen/<w>x<h>@<scale>/. LAF_KB_LAYOUT=fr (and LAF_KB_VARIANT) sets the
#                                  keyboard layout, LAF_KEEP_PREFS=1 keeps the last run's window prefs, LAF_SECONDS the run's length,
#                                  LAF_SETTINGS='<settings.json>' starts from those settings, LAF_STEAL="10 20" puts another window
#                                  over the game from 10 s to 20 s (add -lafBackgroundTest to see the game go quiet behind it).
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
# Logs/config/<command>/ (Unity's Linux player keeps its prefs, and persistentDataPath, there). Batch editor
# runs, the GUI editor and `headless` get Logs/config/editor/, which links back to the real config folder for
# everything but this game's own, so pressing Play in the editor uses a scratch save and settings too. The real
# ~/.config/unity3d/Nearby/Lost & Found/ is checked before and after every command (hashes and timestamps); if
# anything there changed, the run fails with status 99.
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

# player <command> <args...>: run a built player with scratch prefs, inside a headless KWin of its own (see nested) when
# this machine has one, so no test window ever shows on the desktop. LAF_DESKTOP=1 runs it as an ordinary window instead.
player() {
  local name="$1"; shift
  if [ "${LAF_DESKTOP:-0}" != 1 ] && command -v kwin_wayland > /dev/null && command -v dbus-run-session > /dev/null; then
    nested "$name" "$@"; return
  fi
  [ -n "${WAYLAND_DISPLAY:-}" ] && export SDL_VIDEODRIVER=wayland
  export XDG_CONFIG_HOME="$PROJECT/Logs/config/$name"; mkdir -p "$XDG_CONFIG_HOME"
  guarded "$name" "$@"
}

# helpers_of <cfg>: this user's processes whose XDG_CONFIG_HOME is <cfg>, one of this script's scratch folders. A headless KWin's
# session activates helpers on its private D-Bus (ksecretd and xdg-desktop-portal, for kdialog say) that outlive it; round
# 10's runs left about 80. stop_new_helpers <cfg> <pids before> stops the ones that appeared during this run, and only those.
helpers_of() { local p; for p in $(pgrep -u "$(id -u)" .); do { tr '\0' '\n' < "/proc/$p/environ"; } 2>/dev/null | grep -qxF "XDG_CONFIG_HOME=$1" && echo "$p"; done; true; }
stop_new_helpers() {
  local p names=""
  for p in $(helpers_of "$1"); do
    grep -qxF "$p" <<< "$2" && continue
    names="$names $(cat "/proc/$p/comm" 2>/dev/null)"; kill "$p" 2>/dev/null
  done
  [ -z "$names" ] || echo "[nested] stopped what this run's session left running:$names"
}

# nested <command> <args...>: the player in `kwin_wayland --virtual` (as smallscreen does): its own D-Bus session, socket and
# scratch XDG folders under Logs/config/, DISPLAY and WAYLAND_DISPLAY unset, so it never talks to the desktop's KWin and
# nothing appears on screen. The screen is 2560x1440 (LAF_NESTED_SIZE="w h"), room for any window the tools ask for. KWin's
# own output goes to Logs/config/<command>-xdg/kwin.log; the player's exit status is passed back.
nested() {
  local name="$1"; shift
  local cfg="$PROJECT/Logs/config/$name" xdg="$PROJECT/Logs/config/$name-xdg" w h st
  mkdir -p "$cfg" "$xdg/data" "$xdg/cache" "$xdg/state"
  rm -f "$cfg/kwinoutputconfig.json" "$xdg/status"
  # LAF_STEAL_WHEN=<text> LAF_STEAL_LOG=<file>: once the log says that, another window takes the focus for LAF_STEAL_FOR seconds
  { printf '#!/bin/bash\nsteal=\n'
    printf 'if [ -n "${LAF_STEAL_WHEN:-}" ]; then (until grep -aqF "$LAF_STEAL_WHEN" "$LAF_STEAL_LOG" 2>/dev/null; do sleep 0.5; done\n'
    printf '  echo "[steal] another window opens" >&2; kdialog --title "Another window" --msgbox "Another window has the focus" & k=$!\n'
    printf '  sleep "${LAF_STEAL_FOR:-10}"; kill $k; echo "[steal] it closed" >&2) & steal=$!; fi\n'
    for a in "$@"; do printf '%q ' "$a"; done
    printf '\nst=$?\n[ -n "$steal" ] && { pkill -P $steal; kill $steal; } 2>/dev/null\necho $st > %q\n' "$xdg/status"; } > "$xdg/session.sh"
  chmod +x "$xdg/session.sh"
  read -r w h <<< "${LAF_NESTED_SIZE:-2560 1440}"
  local before; before="$(helpers_of "$cfg")"
  guarded "$name" sh -c 'log=$1; shift; exec "$@" > "$log" 2>&1' _ "$xdg/kwin.log" env -u DISPLAY -u WAYLAND_DISPLAY \
    XDG_CONFIG_HOME="$cfg" XDG_DATA_HOME="$xdg/data" XDG_CACHE_HOME="$xdg/cache" XDG_STATE_HOME="$xdg/state" SDL_VIDEODRIVER=wayland \
    timeout -s KILL 7300 dbus-run-session -- kwin_wayland --virtual --width "$w" --height "$h" --socket "laf-$name-$$" --no-lockscreen \
    --exit-with-session "$xdg/session.sh" || { st=$?; stop_new_helpers "$cfg" "$before"; [ $st -eq 99 ] && return 99; }
  stop_new_helpers "$cfg" "$before"
  st="$(cat "$xdg/status" 2>/dev/null || echo 1)"
  return "$st"
}

# editor <args...>: run the editor (batch, headless or GUI). The editor keeps this project's PlayerPrefs (and the test runner its
# TestResults.xml) in the same folder as the game, so it gets a config folder of its own under Logs/ that links back
# to everything in the real one except this game's folder: its licence, preferences and the other projects' folders
# are found where they live, and only "Lost & Found" is scratch. Links are kept from run to run: only broken ones are
# pruned and missing ones added, so there's never a moment without them. (Deleting and relinking them on every run
# once let a licensing client still running from the previous run recreate unity3d/Unity as an empty folder in that
# moment, and every editor that then asked it for a licence was refused.) A folder found where a real one should be
# linked is set aside as .shadowed.<name>.<time> and the link put back; a file the editor made here itself (the GUI
# editor rotates Editor.log) is left alone.
editor() {
  local root="$PROJECT/Logs/config/editor"
  mkdir -p "$root/unity3d/Nearby/Lost & Found"
  find "$root" -maxdepth 3 -xtype l -delete
  (
    shopt -s dotglob nullglob
    link_all() {   # link_all <from> <into> <except>
      local e n t
      for e in "$1"/*; do
        n="${e##*/}"; t="$2/$n"
        [ "$n" = "$3" ] && continue
        if [ -L "$t" ]; then continue
        elif [ -d "$e" ] && [ -e "$t" ]; then mv "$t" "$2/.shadowed.$n.$(date +%s)"; ln -s "$e" "$t"
        elif [ -e "$t" ]; then continue
        else ln -s "$e" "$t"
        fi
      done
    }
    link_all "$REAL_XDG" "$root" unity3d
    link_all "$REAL_XDG/unity3d" "$root/unity3d" Nearby
    link_all "$REAL_XDG/unity3d/Nearby" "$root/unity3d/Nearby" "Lost & Found"
  )
  XDG_CONFIG_HOME="$root" guarded editor "$UNITY" "$@"
}

case "${1:-open}" in
  open)        editor -projectPath "$PROJECT" ;;
  headless)    editor -batchmode -projectPath "$PROJECT" -logFile "$PROJECT/Logs/headless.log" ;;
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
                 -lafSmoke "$PROJECT/Screenshots/smoke" -lafSave "$PROJECT/Screenshots/smoke/save.json" -lafSeconds "${2:-30}" -lafNoVsync -lafUncapped "${q[@]}" -logFile "$PROJECT/Logs/smoke.log" "${@:4}" ;;
  autopilot)   rm -rf "$PROJECT/Screenshots/autopilot"; mkdir -p "$PROJECT/Screenshots/autopilot"
               player autopilot timeout -s KILL 2400 "$PROJECT/Builds/Linux/LostAndFound.x86_64" -lafAutopilot "$PROJECT/Screenshots/autopilot" \
                 -lafSave "${LAF_AUTOPILOT_SAVE:-$PROJECT/Screenshots/autopilot/save.json}" -lafSpeed "${2:-2}" -lafDay "${3:-1}" -lafPolicy "${4:-best}" -lafNoVsync -logFile "$PROJECT/Logs/autopilot.log" "${@:5}" ;;
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
  edgetest)    mkdir -p "$PROJECT/Screenshots/edgetest"; rm -f "$PROJECT/Logs/edgetest.log"
               export LAF_STEAL_WHEN="[EdgeTest] waiting for another window" LAF_STEAL_LOG="$PROJECT/Logs/edgetest.log" LAF_STEAL_FOR=12
               st=0; player edgetest timeout -s KILL 400 "$PROJECT/Builds/Linux/LostAndFound.x86_64" -lafEdgeTest -lafDay 2 \
                 -lafSave "$PROJECT/Screenshots/edgetest/save.json" -lafNoMusic -screen-width "${2:-1600}" -screen-height "${3:-900}" -screen-fullscreen 0 \
                 -logFile "$PROJECT/Logs/edgetest.log" "${@:4}" || st=$?
               [ $st -eq 99 ] && exit 99   # the guard: the real save or settings changed
               grep -a "\[EdgeTest\]" "$PROJECT/Logs/edgetest.log"; grep -a "\[steal\]" "$PROJECT/Logs/config/edgetest-xdg/kwin.log"
               grep -a -q "\[EdgeTest\] PASS" "$PROJECT/Logs/edgetest.log" ;;
  pointertest) out="$PROJECT/Screenshots/pointertest"; rm -rf "$out"; mkdir -p "$out" "$PROJECT/Builds/tools"; rm -f "$PROJECT/Logs/pointertest.log"
               cc -O2 -o "$PROJECT/Builds/tools/fakeptr" "$PROJECT/Tools/fakeptr.c" -lwayland-client -lm
               # fake input is a restricted interface: let this private KWin (only) grant it; its scripts' print() goes to its log
               export KWIN_WAYLAND_NO_PERMISSION_CHECKS=1 QT_FORCE_STDERR_LOGGING=1 QT_LOGGING_RULES="js.debug=true" \
                 LAF_FAKEPTR="$PROJECT/Builds/tools/fakeptr" LAF_PHASE="$out/phase.txt" LAF_PLAYER_LOG="$PROJECT/Logs/pointertest.log" \
                 LAF_KWIN_LOG="$PROJECT/Logs/config/pointertest-xdg/kwin.log"
               st=0; player pointertest "$PROJECT/Tools/pointertest_session.sh" timeout -s KILL 400 "$PROJECT/Builds/Linux/LostAndFound.x86_64" \
                 -lafPointerTest "$out/phase.txt" -lafDay 2 -lafSave "$out/save.json" -lafNoMusic -screen-width "${2:-1600}" -screen-height "${3:-900}" \
                 -screen-fullscreen 0 -logFile "$PROJECT/Logs/pointertest.log" "${@:4}" || st=$?
               [ $st -eq 99 ] && exit 99   # the guard: the real save or settings changed
               grep -a "\[ptr\]\|fakeptr" "$PROJECT/Logs/config/pointertest-xdg/kwin.log"; grep -a "\[PointerTest\]" "$PROJECT/Logs/pointertest.log"
               grep -a -q "\[PointerTest\] PASS" "$PROJECT/Logs/pointertest.log" ;;
  soak)        rm -rf "$PROJECT/Screenshots/soak"; mkdir -p "$PROJECT/Screenshots/soak"
               player soak timeout -s KILL 3600 "$PROJECT/Builds/Linux/LostAndFound.x86_64" -lafSoak "$PROJECT/Screenshots/soak" -lafCycles "${2:-30}" \
                 -lafSave "${LAF_SOAK_SAVE:-$PROJECT/Screenshots/soak/save.json}" -lafNoMusic -lafNoVsync -screen-width 1600 -screen-height 900 -screen-fullscreen 0 \
                 -logFile "$PROJECT/Logs/soak.log" "${@:3}"
               grep -a "\[Soak\]" "$PROJECT/Logs/soak.log" | tail -n 14
               grep -a -q "\[Soak\] PASS" "$PROJECT/Logs/soak.log" ;;
  smallscreen) w="${2:-1366}"; h="${3:-768}"; scale="${4:-1}"
               out="$PROJECT/Screenshots/smallscreen/${w}x${h}@${scale}${LAF_TAG:+_$LAF_TAG}"; rm -rf "$out"; mkdir -p "$out/shots"
               cfg="$PROJECT/Logs/config/smallscreen"; xdg="$PROJECT/Logs/config/smallscreen-xdg"
               [ "${LAF_KEEP_PREFS:-0}" = 1 ] || rm -rf "$cfg/unity3d"   # a first launch, unless asked to keep the last run's prefs
               rm -f "$cfg/kwinoutputconfig.json"   # KWin remembers the last run's output scale there
               mkdir -p "$cfg" "$xdg/data" "$xdg/cache" "$xdg/state"
               if [ -n "${LAF_SETTINGS:-}" ]; then mkdir -p "$cfg/unity3d/Nearby/Lost & Found"; printf '%s' "$LAF_SETTINGS" > "$cfg/unity3d/Nearby/Lost & Found/settings.json"; fi
               printf '[Layout]\nLayoutList=%s\nVariantList=%s\nUse=true\n' "${LAF_KB_LAYOUT:-us}" "${LAF_KB_VARIANT:-}" > "$cfg/kxkbrc"
               printf '#!/bin/bash\nexec "%s" timeout -s KILL %s "%s" -lafSmoke "%s" -lafSave "%s" -lafSeconds %s -logFile "%s"' \
                 "$PROJECT/Tools/smallscreen_session.sh" $(( ${LAF_SECONDS:-12} + 90 )) "$PROJECT/Builds/Linux/LostAndFound.x86_64" \
                 "$out/shots" "$out/shots/save.json" "${LAF_SECONDS:-12}" "$out/player.log" > "$out/session.sh"
               for a in "${@:5}"; do printf ' %q' "$a" >> "$out/session.sh"; done; chmod +x "$out/session.sh"
               st=0; before="$(helpers_of "$cfg")"
               guarded smallscreen sh -c 'log=$1; shift; exec "$@" > "$log" 2>&1' _ "$out/kwin.log" env -u DISPLAY -u WAYLAND_DISPLAY XDG_CONFIG_HOME="$cfg" XDG_DATA_HOME="$xdg/data" XDG_CACHE_HOME="$xdg/cache" \
                 XDG_STATE_HOME="$xdg/state" SDL_VIDEODRIVER=wayland KWIN_SCREENSHOT_NO_PERMISSION_CHECKS=1 QT_FORCE_STDERR_LOGGING=1 \
                 QT_LOGGING_RULES="js.debug=true" LAF_SMALLSCREEN=1 LAF_OUT="$out" LAF_SCALE="$scale" LAF_SHOTS="${LAF_SHOTS:-14}" \
                 timeout -s KILL $(( ${LAF_SECONDS:-12} + 150 )) dbus-run-session -- kwin_wayland --virtual --width "$w" --height "$h" \
                 --socket "laf-smallscreen-$$" --no-lockscreen --exit-with-session "$out/session.sh" || st=$?
               stop_new_helpers "$cfg" "$before"
               grep -a "\[steal\]" "$out/kwin.log"
               grep -a "Desktop is\|\[Window\]\|\[Keys\]\|\[Background\]" "$out/player.log" | awk '!seen[$0]++' | head -n 12
               grep -a "js: \[geom\]\|^\[geom\] at" "$out/kwin.log" | sed 's/^js: //'
               ls "$out" | grep screen_
               [ $st -ne 99 ] ;;
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
  *) echo "usage: $0 [open|headless|build-linux|build-mac|build-windows|run <Method>|test|smoke [secs]|autopilot|audit|nudgetour|padtest|taptest|edgetest [w h]|pointertest [w h]|soak|smallscreen [w h scale]|demo|trailer|film <name>]" >&2; exit 2 ;;
esac
