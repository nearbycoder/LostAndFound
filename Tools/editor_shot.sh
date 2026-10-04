#!/usr/bin/env bash
# Capture the running play-mode game from the resident headless editor.
#   Tools/editor_shot.sh <name> [wait-until-time-seconds] [C# to eval first]
# Saves /tmp/laf/shots/<name>.png and prints console errors since the last call.
set -euo pipefail
P="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
NAME="$1"; T="${2:-0}"; CODE="${3:-}"
mkdir -p /tmp/laf/shots
if [[ "$T" == +* ]]; then
  NOW=$(echo 'return UnityEngine.Time.time;' | "$P/Tools/ueval.sh" | tail -1)
  T=$(python3 -c "print(float('$NOW') + float('${T#+}'))")
fi
if [ "$T" != "0" ]; then
  unity command wait_for --project-path "$P" -- --condition "{\"member\":\"UnityEngine.Time.time\",\"op\":\"greaterThan\",\"value\":$T}" --timeout_s 120 >/dev/null 2>&1 || true
fi
if [ -n "$CODE" ]; then unity command eval --project-path "$P" -- --code "$CODE" --timeout 20000 2>&1 | tail -1 | cut -c1-400; fi
unity command capture_game_view --project-path "$P" -- --source camera --width "${W:-640}" --height "${H:-480}" --save_path "Screenshots/$NAME.png" >/dev/null 2>&1 || echo "capture failed"
mv -f "$P/Assets/Screenshots/$NAME.png" /tmp/laf/shots/ 2>/dev/null || true
rm -f "$P/Assets/Screenshots/$NAME.png.meta"
unity command console --project-path "$P" --format json -- --tail 40 --level error 2>/dev/null | python3 -c "
import sys,json
try:
  d=json.load(sys.stdin); r=d['data'].get('result',d['data'])
  for e in r.get('entries',[]): print('ERR', e['message'][:300].replace('\n',' | '))
except Exception as ex: print('console read failed', ex)
"
unity command clear_console --project-path "$P" >/dev/null 2>&1 || true
echo "/tmp/laf/shots/$NAME.png"
