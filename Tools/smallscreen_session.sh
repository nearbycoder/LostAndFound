#!/usr/bin/env bash
# Runs inside Tools/unity.sh smallscreen's headless KWin (its own D-Bus session, scratch XDG folders): set the output's
# scale, start the player, and at each of LAF_SHOTS seconds ask KWin where the window is (a KWin script, printed to
# KWin's log as "js: [geom] ...") and photograph the whole screen with spectacle. Not meant to be run on its own.
set -uo pipefail
out="$LAF_OUT"
kscreen-doctor "output.Virtual-0.scale.$LAF_SCALE" > "$out/kscreen.log" 2>&1
cat > "$out/geom.js" <<'JS'
for (const o of workspace.screens) print("[geom] output " + o.name + " " + JSON.stringify(o.geometry) + " scale=" + o.devicePixelRatio);
for (const w of workspace.stackingOrder) if (w.caption) print("[geom] window '" + w.caption + "' frame=" + JSON.stringify(w.frameGeometry) + " fullscreen=" + w.fullScreen);
JS
"$@" &
pid=$!
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
