#!/usr/bin/env bash
# Runs inside Tools/unity.sh smallscreen's headless KWin (its own D-Bus session, scratch XDG folders): set the output's
# scale, start the player, and at each of LAF_SHOTS seconds ask KWin where the window is (a KWin script, printed to
# KWin's log as "js: [geom] ...") and photograph the whole screen with spectacle. LAF_STEAL="<open> <close>" opens another
# window over the game at the first second and closes it at the second. Not meant to be run on its own.
set -uo pipefail
out="$LAF_OUT"
kscreen-doctor "output.Virtual-0.scale.$LAF_SCALE" > "$out/kscreen.log" 2>&1
cat > "$out/geom.js" <<'JS'
for (const o of workspace.screens) print("[geom] output " + o.name + " " + JSON.stringify(o.geometry) + " scale=" + o.devicePixelRatio);
for (const w of workspace.stackingOrder) if (w.caption) print("[geom] window '" + w.caption + "' frame=" + JSON.stringify(w.frameGeometry) + " fullscreen=" + w.fullScreen);
JS
"$@" &
pid=$!
if [ -n "${LAF_STEAL:-}" ]; then
  # another window takes the focus at the first time and closes at the second
  read -r s_on s_off <<< "$LAF_STEAL"
  ( sleep "$s_on"; echo "[steal] another window opened at ${s_on}s" >&2
    kdialog --title "Another window" --msgbox "Another window has the focus" & k=$!
    sleep $(( s_off - s_on )); kill $k; echo "[steal] it closed at ${s_off}s" >&2 ) &
fi
t=0; i=0
for at in ${LAF_SHOTS:-14}; do
  sleep $(( at - t )); t=$at
  kill -0 $pid 2>/dev/null || break
  echo "[geom] at ${at}s" >&2
  dbus-send --session --print-reply --dest=org.kde.KWin /Scripting org.kde.kwin.Scripting.unloadScript string:lafgeom > /dev/null 2>&1
  dbus-send --session --print-reply --dest=org.kde.KWin /Scripting org.kde.kwin.Scripting.loadScript string:"$out/geom.js" string:lafgeom > /dev/null
  dbus-send --session --print-reply --dest=org.kde.KWin /Scripting org.kde.kwin.Scripting.start > /dev/null
  timeout 20 spectacle -b -n -f -o "$out/screen_$(printf %02d $i)_${at}s.png" > /dev/null 2>&1
  i=$((i + 1))
done
wait $pid
