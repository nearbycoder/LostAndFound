#!/usr/bin/env bash
# Press a key in the running game (down, short pause, up):  Tools/key.sh Space
P="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
unity command simulate_key --project-path "$P" -- --key "$1" --action down >/dev/null 2>&1
sleep 0.12
unity command simulate_key --project-path "$P" -- --key "$1" --action up >/dev/null 2>&1
