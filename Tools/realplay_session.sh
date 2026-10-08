#!/usr/bin/env bash
# Runs inside Tools/unity.sh realplay's headless KWin: starts the player (-lafPlay -lafRealInput <file>), puts its window in the
# middle of the screen and asks KWin where it is, then turns each line the game writes to <file> into KWin's own pointer and
# keyboard input (Tools/fakeptr.c). The game's lines are in window pixels from the bottom left; KWin's pointer is in screen
# pixels from the top left. Not meant to be run on its own.
set -uo pipefail
log="$LAF_PLAYER_LOG"; kwinlog="$LAF_KWIN_LOG"; cmds="$LAF_INPUT"; dir="$(dirname "$cmds")"
rm -f "$cmds" "$cmds.go"
"$@" &
pid=$!
say() { echo "[real] $*" >&2; }
for _ in $(seq 240); do grep -aqF "[RealInput] ready" "$log" 2>/dev/null && break; kill -0 $pid 2>/dev/null || break; sleep 0.5; done
grep -aqF "[RealInput] ready" "$log" 2>/dev/null || { say "the game never said it was ready"; wait $pid; exit 1; }

cat > "$dir/geom.js" <<'JS'
const area = workspace.screens[0].geometry;
for (const w of workspace.stackingOrder) if (w.normalWindow) {
  w.frameGeometry = { x: Math.round((area.width - w.frameGeometry.width) / 2), y: Math.round((area.height - w.frameGeometry.height) / 2), width: w.frameGeometry.width, height: w.frameGeometry.height };
}
for (const w of workspace.stackingOrder) if (w.normalWindow) print("[realgeom] " + Math.round(w.clientGeometry.x) + " " + Math.round(w.clientGeometry.y) + " " + Math.round(w.clientGeometry.width) + " " + Math.round(w.clientGeometry.height) + " active=" + w.active);
JS
# (the game says it's ready before its window is up: ask until KWin has it)
for _ in $(seq 60); do
  dbus-send --session --print-reply --dest=org.kde.KWin /Scripting org.kde.kwin.Scripting.unloadScript string:realgeom > /dev/null 2>&1
  dbus-send --session --print-reply --dest=org.kde.KWin /Scripting org.kde.kwin.Scripting.loadScript string:"$dir/geom.js" string:realgeom > /dev/null
  dbus-send --session --print-reply --dest=org.kde.KWin /Scripting org.kde.kwin.Scripting.start > /dev/null
  sleep 1
  grep -aq "\[realgeom\]" "$kwinlog" && break
done
read -r wx wy ww wh _ <<< "$(grep -a "\[realgeom\]" "$kwinlog" | tail -n 1 | sed 's/.*\[realgeom\] //')"
[ -n "${wh:-}" ] || { say "KWin didn't say where the window is"; kill $pid; wait $pid; exit 1; }
say "window at $wx,$wy ${ww}x${wh}"

fifo="$dir/fakeptr.fifo"; rm -f "$fifo"; mkfifo "$fifo"
"$LAF_FAKEPTR" "$WAYLAND_DISPLAY" < "$fifo" &
fp=$!
exec 3> "$fifo"
touch "$cmds"
tail -n +1 -F --pid="$pid" "$cmds" 2>/dev/null | awk -v wx="$wx" -v wy="$wy" -v wh="$wh" '
  $1 == "m" { printf "abs %.2f %.2f\n", wx + $2, wy + wh - 1 - $3 }
  $1 == "b" { print ($2 == 1 ? "down 272" : "up 272") }
  $1 == "r" { print ($2 == 1 ? "down 273" : "up 273") }
  $1 == "k" { print "key " $2 " " $3 }
  $1 == "w" { print "axis 0 " $2 }
  $1 == "end" { exit }
  { fflush() }' >&3 &
tr=$!
touch "$cmds.go"
say "go"
wait $pid
st=$?
kill $tr 2>/dev/null   # (tail stops by itself with the player: --pid)
exec 3>&-
wait $fp 2>/dev/null
exit $st
