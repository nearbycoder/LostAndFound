#!/usr/bin/env bash
# Runs inside Tools/unity.sh pointertest's headless KWin: starts the player (-lafPointerTest), asks KWin where its window is,
# and moves KWin's own pointer with Tools/fakeptr.c: rest just inside each side (the desk should turn), then leave the window
# by each side, quickly and slowly, and stay out (it shouldn't). Before each move it writes the phase to LAF_PHASE, which the
# game reads and judges. Not meant to be run on its own.
set -uo pipefail
log="$LAF_PLAYER_LOG"; kwinlog="$LAF_KWIN_LOG"; phasefile="$LAF_PHASE"; dir="$(dirname "$phasefile")"
rm -f "$phasefile"
"$@" &
pid=$!
say() { echo "[ptr] $*" >&2; }
for _ in $(seq 240); do grep -aqF "[PointerTest] ready" "$log" 2>/dev/null && break; kill -0 $pid 2>/dev/null || break; sleep 0.5; done
grep -aqF "[PointerTest] ready" "$log" 2>/dev/null || { say "the game never said it was ready"; wait $pid; exit 1; }

# the window away from the screen's sides (KWin puts it at the top left), so the pointer can leave it by either side; then
# where it is (its client area, without the title bar)
cat > "$dir/geom.js" <<'JS'
const area = workspace.screens[0].geometry;
for (const w of workspace.stackingOrder) if (w.normalWindow) {
  w.frameGeometry = { x: Math.round((area.width - w.frameGeometry.width) / 2), y: Math.round((area.height - w.frameGeometry.height) / 2), width: w.frameGeometry.width, height: w.frameGeometry.height };
}
for (const w of workspace.stackingOrder) if (w.normalWindow) print("[ptrgeom] " + Math.round(w.clientGeometry.x) + " " + Math.round(w.clientGeometry.y) + " " + Math.round(w.clientGeometry.width) + " " + Math.round(w.clientGeometry.height) + " '" + w.caption + "' active=" + w.active);
JS
dbus-send --session --print-reply --dest=org.kde.KWin /Scripting org.kde.kwin.Scripting.loadScript string:"$dir/geom.js" string:ptrgeom > /dev/null
dbus-send --session --print-reply --dest=org.kde.KWin /Scripting org.kde.kwin.Scripting.start > /dev/null
sleep 1
read -r wx wy ww wh _ <<< "$(grep -a "\[ptrgeom\]" "$kwinlog" | tail -n 1 | sed 's/.*\[ptrgeom\] //')"
[ -n "${wh:-}" ] || { say "KWin didn't say where the window is"; kill $pid; wait $pid; exit 1; }
say "window at $wx,$wy ${ww}x${wh}"

fifo="$dir/fakeptr.fifo" back="$dir/fakeptr.back"; rm -f "$fifo" "$back"; mkfifo "$fifo" "$back"
"$LAF_FAKEPTR" "$WAYLAND_DISPLAY" < "$fifo" > "$back" &
fp=$!
exec 3> "$fifo" 4< "$back"
cmd() { echo "$*" >&3; }
sync_() { cmd sync; read -r -t 60 -u 4 _; }
n=0
phase() { n=$((n + 1)); echo "$n $1 $2" > "$phasefile"; say "phase $n $1 (expect $2)"; sleep 0.6; }
judge() { n=$((n + 1)); echo "$n judge -" > "$phasefile"; sleep 0.4; }
cy=$((wy + wh / 2)); cx=$((wx + ww / 2)); L=$wx; R=$((wx + ww))   # R is the first pixel right of the window
# to <x> <step px> <ms per step>: along the window's middle row, from wherever the last move ended (fractions allowed)
at=$cx
to() { cmd "slide $at $cy $1 $cy $2 $3"; at=$1; sync_; }
home() { cmd "abs $cx $cy"; sleep 0.15; cmd "abs $((cx + 30)) $((cy + 10))"; sleep 0.15; cmd "abs $cx $cy"; at=$cx; sync_; sleep 0.4; }
# a person's approach: quick, then slowing to a stop at <x>
ease() { local d=$(( $1 < cx ? 1 : -1 )); to $(( $1 + d * 120 )) 37.3 8; to $(( $1 + d * 24 )) 9.7 12; to $1 2.3 16; }

home; sleep 1
phase settle -
# resting just inside a side, after slowing down to it: the desk should turn
home; phase ease-in-left turn;  ease $((L + 12));  sleep 2.5; judge
home; phase ease-in-right turn; ease $((R - 13));  sleep 2.5; judge
home; phase ease-in-near-left turn;  ease $((L + 3));  sleep 2.5; judge
# stopping on the window's very last pixel reads like creeping out past it a pixel at a time (logged, not judged)
home; phase ease-to-last-pixel-left any;  ease $L;  sleep 2.5; judge
# leaving by a side and staying out, at a brisk pace and slowly, in steps of a fraction of a pixel as a mouse gives: it shouldn't
home; phase leave-left-fast still;  to $((L - 80)) 61.7 8;  sleep 3; judge
home; phase leave-right-fast still; to $((R + 80)) 61.7 8;  sleep 3; judge
home; phase leave-left-slow still;  to $((L - 40)) 6.3 16;  sleep 3; judge
home; phase leave-right-slow still; to $((R + 40)) 6.3 16;  sleep 3; judge
home; phase leave-left-slower still;  to $((L + 300)) 20 16; to $((L - 20)) 3.7 12;  sleep 3; judge
home; phase leave-right-slower still; to $((R - 300)) 20 16; to $((R + 20)) 3.7 12;  sleep 3; judge
# out by the side and back in to rest there: it should turn once the pointer is back
home; phase out-left still;  to $((L - 40)) 6.3 16; sleep 2; judge
phase back-in-left turn;  to $((L + 10)) 2.9 16;  sleep 2.5; judge
home; phase back-in-centre still; sleep 2; judge

# the window against the screen's left side (snapped there, or maximised): a pointer flung left is stopped by the screen on
# the window's first pixel and should turn the desk. One pushed there slowly reads like one leaving slowly (logged, not judged)
cat > "$dir/snap.js" <<'JS'
for (const w of workspace.stackingOrder) if (w.normalWindow) {
  w.frameGeometry = { x: 0, y: w.frameGeometry.y, width: w.frameGeometry.width, height: w.frameGeometry.height };
  print("[ptrgeom] " + Math.round(w.clientGeometry.x) + " " + Math.round(w.clientGeometry.y) + " " + Math.round(w.clientGeometry.width) + " " + Math.round(w.clientGeometry.height) + " '" + w.caption + "' active=" + w.active);
}
JS
dbus-send --session --print-reply --dest=org.kde.KWin /Scripting org.kde.kwin.Scripting.loadScript string:"$dir/snap.js" string:ptrsnap > /dev/null
dbus-send --session --print-reply --dest=org.kde.KWin /Scripting org.kde.kwin.Scripting.start > /dev/null
sleep 1
read -r wx wy ww wh _ <<< "$(grep -a "\[ptrgeom\]" "$kwinlog" | tail -n 1 | sed 's/.*\[ptrgeom\] //')"
cy=$((wy + wh / 2)); cx=$((wx + ww / 2)); L=$wx; R=$((wx + ww))
say "window moved to $wx,$wy ${ww}x${wh}, against the screen's left side"
home; phase fling-to-screen-side turn;  to $((L - 100)) 33.3 8;  sleep 2.5; judge
home; phase push-slowly-to-screen-side any;  to $((L + 200)) 20 16; to $((L - 20)) 3.7 12;  sleep 2.5; judge
home; phase end -
exec 3>&- 4<&-
wait $fp
for _ in $(seq 30); do kill -0 $pid 2>/dev/null || break; sleep 0.5; done
kill $pid 2>/dev/null
wait $pid
