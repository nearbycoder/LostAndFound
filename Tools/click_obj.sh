#!/usr/bin/env bash
# Move the simulated mouse onto a world object (by GameObject name or a C# expression giving a Vector3)
# and optionally click:   Tools/click_obj.sh "Bell" [move|click] [dx dy]
P="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TARGET="$1"; ACTION="${2:-click}"; DX="${3:-0}"; DY="${4:-0}"
if [[ "$TARGET" == item:* ]]; then EXPR="LostAndFound.Desk.I.items[\"${TARGET#item:}\"].Center"
elif [[ "$TARGET" == hs:* ]]; then IFS=: read _ OBJ DET <<< "$TARGET"; EXPR="LostAndFound.Desk.I.items[\"$OBJ\"].Hotspot(LostAndFound.Desk.I.items[\"$OBJ\"].def.Detail(\"$DET\")).position"
elif [[ "$TARGET" == part:* ]]; then IFS=: read _ OBJ PART <<< "$TARGET"; EXPR="LostAndFound.ModelLibrary.Find(LostAndFound.Desk.I.items[\"$OBJ\"].transform, \"$PART\").GetComponentInChildren<UnityEngine.Renderer>().bounds.center"
elif [[ "$TARGET" == *"("* ]]; then EXPR="$TARGET"
else EXPR="UnityEngine.GameObject.Find(\"$TARGET\").transform.position"; fi
XY=$(printf 'var p = UnityEngine.Camera.main.WorldToScreenPoint(%s); return ((int)p.x) + " " + ((int)p.y);' "$EXPR" | "$P/Tools/ueval.sh" | tail -1)
read X Y <<< "$XY"
X=$((X + DX)); Y=$((Y + DY))
unity command simulate_pointer --project-path "$P" -- --x "$X" --y "$Y" --action move >/dev/null 2>&1
if [ "$ACTION" = "click" ]; then
  sleep 0.2
  unity command simulate_pointer --project-path "$P" -- --x "$X" --y "$Y" --action down >/dev/null 2>&1
  sleep 0.1
  unity command simulate_pointer --project-path "$P" -- --x "$X" --y "$Y" --action up >/dev/null 2>&1
fi
echo "$ACTION at $X $Y"
