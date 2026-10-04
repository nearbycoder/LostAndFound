#!/usr/bin/env bash
# Enter play mode in the resident editor and fast-forward to the first case's investigation.
P="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$P"
rm -f "$HOME/.config/unity3d/Nearby/Lost & Found/lostandfound_save.json"
unity command editor_stop --project-path "$P" >/dev/null 2>&1 || true
unity command editor_play --project-path "$P" 2>&1 | tail -1
echo 'LostAndFound.Director.I.autoAdvance = true; return "auto";' | Tools/ueval.sh
Tools/wait_member.sh LostAndFound.Director CanRing true 60
echo 'LostAndFound.Director.I.autoRing = true; return "ring";' | Tools/ueval.sh
Tools/wait_member.sh LostAndFound.Director CanUseStamps true 90
echo 'LostAndFound.Director.I.autoRing = false; return "investigating";' | Tools/ueval.sh
