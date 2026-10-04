#!/usr/bin/env bash
# Enter play mode in the resident editor (fresh save) and wait until the game is up and reachable.
P="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
rm -f "$HOME/.config/unity3d/Nearby/Lost & Found/lostandfound_save.json"
unity command editor_stop --project-path "$P" >/dev/null 2>&1 || true
unity command editor_play --project-path "$P" >/dev/null 2>&1 || true
for i in $(seq 1 60); do
  R=$(unity command eval --project-path "$P" -- --code 'return (LostAndFound.Director.I != null && UnityEngine.Application.isPlaying) ? "up" : "no";' 2>/dev/null | tail -1)
  if [[ "$R" == *'"up"'* ]]; then echo "game up"; exit 0; fi
  sleep 1
done
echo "game did not come up"; exit 1
