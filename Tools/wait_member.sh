#!/usr/bin/env bash
# Wait until an instance member of a component equals a value:  Tools/wait_member.sh LostAndFound.Director CanUseStamps true [timeout]
P="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
unity command wait_for --project-path "$P" -- --condition "{\"findType\":\"$1\",\"member\":\"$2\",\"op\":\"equals\",\"value\":$3}" --timeout_s "${4:-60}" --tolerate_missing true 2>&1 | tail -1 | cut -c1-160
