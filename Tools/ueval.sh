#!/usr/bin/env bash
# Evaluate C# in the resident editor. Code comes from stdin (or a file argument). Prints the result.
#   echo 'return 1+1;' | Tools/ueval.sh
set -euo pipefail
P="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
F="$(mktemp /tmp/laf_eval_XXXX.cs)"
if [ $# -ge 1 ]; then cp "$1" "$F"; else cat > "$F"; fi
unity command eval_file --project-path "$P" --format json -- --file "$F" --timeout 60000 2>&1 | python3 -c "
import sys,json
t=sys.stdin.read()
try:
  d=json.loads(t)
  r=d.get('data',{}).get('result',{})
  if isinstance(r,dict):
    if r.get('diagnostics'): print('DIAG', r['diagnostics'])
    print(r.get('result'))
  else: print(r)
  if not d.get('success'): print('ERRORS', d.get('errors'))
except Exception: print(t[-2000:])
"
rm -f "$F"
