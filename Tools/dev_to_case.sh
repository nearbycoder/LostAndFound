#!/usr/bin/env bash
# Enter play mode and fast-forward to the first case's investigation (or stop at the bell with --bell).
P="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$P"
Tools/play.sh || exit 1
echo 'LostAndFound.Director.I.autoAdvance = true; return "auto";' | Tools/ueval.sh
Tools/wait_member.sh LostAndFound.Director CanRing true 60
if [ "${1:-}" = "--bell" ]; then echo 'LostAndFound.Director.I.autoAdvance = false; return "at bell";' | Tools/ueval.sh; exit 0; fi
echo 'LostAndFound.Director.I.autoRing = true; return "ring";' | Tools/ueval.sh
Tools/wait_member.sh LostAndFound.Director CanUseStamps true 90
echo 'LostAndFound.Director.I.autoRing = false; LostAndFound.Director.I.autoAdvance = false; return "investigating";' | Tools/ueval.sh
