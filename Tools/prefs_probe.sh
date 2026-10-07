#!/usr/bin/env bash
# Where do v0.1.0-style settings (PlayerPrefs, saved after every change) land, and does this version bring them over?
# Writes them with the player's -lafPrefsProbe, then starts the player on prepared prefs files. Scratch config folders
# under Logs/prefs_probe/ only; the real ~/.config/unity3d is compared before and after. Needs a Linux build.
#   Tools/prefs_probe.sh
set -u
P="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
BIN="$P/Builds/Linux/LostAndFound.x86_64"
OUT="$P/Logs/prefs_probe"; rm -rf "$OUT"; mkdir -p "$OUT"
[ -n "${WAYLAND_DISPLAY:-}" ] && export SDL_VIDEODRIVER=wayland
# the game's own real folder must not change at all; unknown/unknown is shared with other games (they write it any time),
# so there the check is only that no laf. key ever appears in it
real_state() { (cd "$HOME/.config/unity3d" && find "Nearby/Lost & Found" -type f -printf '%p %s %T@\n' | sort; find "Nearby/Lost & Found" -type f -print0 | sort -z | xargs -0 sha256sum; grep -c 'name="laf\.' unknown/unknown/prefs 2>/dev/null); }
before="$(real_state)"
lafkeys() { grep -o '<pref name="laf[^<]*</pref>' "$1" 2>/dev/null; }
pref() { printf '<unity_prefs version_major="1" version_minor="1">\n\t<pref name="laf.music" type="float">%s</pref>\n\t<pref name="laf.plain" type="int">1</pref>\n</unity_prefs>\n' "$1"; }
smoke() { # $1 = scratch XDG, $2 = log name
  XDG_CONFIG_HOME="$1" timeout -s KILL 180 "$BIN" -lafSmoke "$OUT/shots_$2" -lafSave "$OUT/save_$2.json" -lafSeconds 6 -lafNoVsync -logFile "$OUT/$2.log" >/dev/null 2>&1
  echo "  log: $(grep -h '\[Settings\]' "$OUT/$2.log" | head -3)"
  local s="$1/unity3d/Nearby/Lost & Found/settings.json"
  echo "  settings.json: $( [ -f "$s" ] && tr -d ' \n' < "$s" | grep -o '"key":"\(music\|plain\)","value":[0-9.]*' | tr '\n' ' ' || echo none)"
}

echo "== 1. write music=0.2, plain=1 the way v0.1.0 did (PlayerPrefs.SetFloat/SetInt + Save), then quit"
X="$OUT/xdg1"; mkdir -p "$X"
XDG_CONFIG_HOME="$X" timeout -s KILL 180 "$BIN" -lafPrefsProbe "music=0.2,plain=1" -logFile "$OUT/probe_write.log" >/dev/null 2>&1
grep -h 'PrefsProbe' "$OUT/probe_write.log"
echo "  files written:"; (cd "$X" && find . -type f | sort | sed 's/^/    /')
for f in $(cd "$X" && find . -name prefs | sort | tr ' ' '?'); do echo "  laf keys in $f: $(lafkeys "$X/${f//\?/ }" | tr '\n' ' ')"; done
# the probe run's own start-up already read settings (and so imported them); take that away so step 2 is a clean upgrade
echo "  (probe run's own import: $(grep -h '\[Settings\]' "$OUT/probe_write.log" | head -1); removing its settings.json)"
rm -f "$X/unity3d/Nearby/Lost & Found/settings.json" "$X/unity3d/Nearby/Lost & Found/settings.json.bak"
echo "== 2. start this version on that same folder (a v0.1.0 player upgrading: v0.1.0's prefs, no settings.json)"
smoke "$X" upgrade

echo "== 3. which file does the import read? music 0.3 only in unknown/unknown/prefs"
X="$OUT/xdg3"; mkdir -p "$X/unity3d/unknown/unknown"; pref 0.3 > "$X/unity3d/unknown/unknown/prefs"
smoke "$X" only_unknown
echo "== 4. music 0.4 only in Nearby/Lost & Found/prefs"
X="$OUT/xdg4"; mkdir -p "$X/unity3d/Nearby/Lost & Found"; pref 0.4 > "$X/unity3d/Nearby/Lost & Found/prefs"
smoke "$X" only_game
echo "== 5. both: 0.3 in unknown/unknown, 0.4 in the game's folder"
X="$OUT/xdg5"; mkdir -p "$X/unity3d/unknown/unknown" "$X/unity3d/Nearby/Lost & Found"; pref 0.3 > "$X/unity3d/unknown/unknown/prefs"; pref 0.4 > "$X/unity3d/Nearby/Lost & Found/prefs"
smoke "$X" both
echo "  unknown/unknown/prefs afterwards (never to be written): $(lafkeys "$X/unity3d/unknown/unknown/prefs" | tr '\n' ' ')  $(cmp -s <(pref 0.3) "$X/unity3d/unknown/unknown/prefs" && echo unchanged || echo CHANGED)"

if [ "$(real_state)" == "$before" ]; then echo "[guard] real Nearby/Lost & Found untouched, and no laf. key in the real unknown/unknown"; else echo "[guard] REAL FOLDERS CHANGED"; fi
