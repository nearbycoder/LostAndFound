#!/usr/bin/env bash
# Build the browser version and lay it out as the GitHub Pages site (https://nearbycoder.github.io/LostAndFound/):
#
#   Tools/build-pages.sh [out]        default out: Builds/Pages (gitignored)
#
# The site is static: index.html at its root, a .nojekyll (so Pages serves it as it is), relative URLs only, and
# Brotli files that Unity's loader unpacks itself (Pages can't send Content-Encoding). Nothing is pushed or deployed.
# Then check it as it will be served:  node Tools/check-pages.mjs --serve Builds/Pages
#
# Unity's WebGL build rewrites URP's shader prefiltering in Assets/Settings/Mobile_RPAsset.asset and leaves Burst output in
# Data/ at the project root; this puts back the one (if it was unmodified before) and removes the other (if it wasn't there).
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT="$(realpath -m "${1:-$ROOT/Builds/Pages}")"
cd "$ROOT"

RP=Assets/Settings/Mobile_RPAsset.asset
rp_clean=0; git diff --quiet -- "$RP" && rp_clean=1
data_was=0; [ -e Data ] && data_was=1

rm -rf Builds/WebGL
start=$(date +%s)
status=0
nice -n 10 Tools/unity.sh build-webgl || status=$?
echo "[pages] Unity finished in $(( $(date +%s) - start )) s (exit $status), load average $(cut -d' ' -f1-3 /proc/loadavg)"

[ $rp_clean = 1 ] && git checkout -q -- "$RP" && echo "[pages] put back $RP"
[ $data_was = 0 ] && [ -d Data ] && rm -rf Data && echo "[pages] removed the Data/ folder the build left"
grep -a "\[LostAndFound\] \(building commit\|WebGL build\)" Logs/build-webgl.log || true
[ $status = 0 ] && [ -f Builds/WebGL/index.html ] || { echo "[pages] the WebGL build failed: see Logs/build-webgl.log" >&2; exit 1; }

rm -rf "$OUT"
mkdir -p "$OUT"
cp -r Builds/WebGL/. "$OUT/"
touch "$OUT/.nojekyll"

# the phone and tablet note on the page says how big the download is
mb=$(du -cb "$OUT"/Build/* | tail -n 1 | awk '{ printf "%d", $1 / 1000000 + 0.5 }')
sed -i "s|<span data-laf-size>[0-9]*</span>|<span data-laf-size>$mb</span>|" "$OUT/index.html"

echo "[pages] $OUT"
(cd "$OUT" && find . -type f -printf '%s\t%p\n' | sort -rn | awk -F'\t' '{ printf "  %8.1f MB  %s\n", $1 / 1e6, $2 }')
echo "[pages] download (Build/): $mb MB; whole site: $(du -sb "$OUT" | awk '{ printf "%.1f", $1 / 1e6 }') MB"
# GitHub refuses files of 100 MB or more, and warns from 50 MB
big=$(find "$OUT" -type f -size +99999999c)
[ -z "$big" ] || { echo "[pages] FAIL: files of 100 MB or more, which GitHub won't take: $big" >&2; exit 1; }
warn=$(find "$OUT" -type f -size +49999999c)
[ -z "$warn" ] || echo "[pages] note: over 50 MB (GitHub warns, but takes it): ${warn#$OUT/}"
grep -q 'src="Build/' "$OUT/index.html" && ! grep -q '="/' "$OUT/index.html" || { echo "[pages] FAIL: index.html has absolute URLs" >&2; exit 1; }
echo "[pages] OK"
