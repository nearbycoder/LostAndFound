#!/usr/bin/env bash
# Build the browser version and lay it out as the GitHub Pages site (https://nearbycoder.github.io/LostAndFound/):
#
#   Tools/build-pages.sh [out]        default out: Builds/Pages (gitignored)
#   Tools/build-pages.sh --no-build [out]   lay the site out again from the last builds in Builds/WebGL and Builds/WebGL-astc
#
# The site is static: index.html at its root, a .nojekyll (so Pages serves it as it is), relative URLs only, and
# Brotli files that Unity's loader unpacks itself (Pages can't send Content-Encoding). Nothing is pushed or deployed.
# Unity builds it twice (BuildScript.BuildWebGL): with the desktop's texture formats, and with ASTC for phones and tablets.
# The site has both data files (the code is the same build, so the same files) and the page loads the one the GPU reads;
# the phones' set is there gzipped as well, for the browser to unpack natively (much less memory than the loader's Brotli).
# Then check it as it will be served:  node Tools/check-pages.mjs --serve Builds/Pages
#
# Unity's WebGL build rewrites URP's shader prefiltering in Assets/Settings/Mobile_RPAsset.asset and leaves Burst output in
# Data/ at the project root; this puts back the one (if it was unmodified before) and removes the other (if it wasn't there).
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
build=1; [ "${1:-}" = --no-build ] && { build=0; shift; }
OUT="$(realpath -m "${1:-$ROOT/Builds/Pages}")"
cd "$ROOT"

if [ $build = 1 ]; then
  RP=Assets/Settings/Mobile_RPAsset.asset
  rp_clean=0; git diff --quiet -- "$RP" && rp_clean=1
  data_was=0; [ -e Data ] && data_was=1

  rm -rf Builds/WebGL Builds/WebGL-astc
  start=$(date +%s)
  status=0
  nice -n 10 Tools/unity.sh build-webgl || status=$?
  echo "[pages] Unity finished in $(( $(date +%s) - start )) s (exit $status), load average $(cut -d' ' -f1-3 /proc/loadavg)"

  [ $rp_clean = 1 ] && git checkout -q -- "$RP" && echo "[pages] put back $RP"
  [ $data_was = 0 ] && [ -d Data ] && rm -rf Data && echo "[pages] removed the Data/ folder the build left"
  grep -a "\[LostAndFound\] \(building commit\|WebGL build\)" Logs/build-webgl.log || true
  [ $status = 0 ] && [ -f Builds/WebGL/index.html ] || { echo "[pages] the WebGL build failed: see Logs/build-webgl.log" >&2; exit 1; }
fi
[ -f Builds/WebGL/index.html ] || { echo "[pages] no build in Builds/WebGL" >&2; exit 1; }

rm -rf "$OUT"
mkdir -p "$OUT"
cp -r Builds/WebGL/. "$OUT/"
touch "$OUT/.nojekyll"

# the files a page loads from a build's index.html: loader, data, framework, code
files() { grep -o 'Build/[0-9a-f]\{32\}\.\(loader\.js\|data\.unityweb\|framework\.js\.unityweb\|wasm\.unityweb\)' "$1" | sort -u; }
set_mb() { local t=0 f; for f in "$@"; do t=$(( t + $(stat -c %s "$OUT/$f") )); done; echo $(( (t + 500000) / 1000000 )); }
desktop_files=$(files Builds/WebGL/index.html)
mb=$(set_mb $desktop_files)
if [ -f Builds/WebGL-astc/index.html ]; then
  astc_files=$(files Builds/WebGL-astc/index.html)
  cp -n Builds/WebGL-astc/Build/* "$OUT/Build/"
  pick() { echo "$astc_files" | grep "\.$1\$"; }
  [ "$(pick loader.js)" = "$(echo "$desktop_files" | grep '\.loader\.js$')" ] || echo "[pages] note: the ASTC build's loader differs; the page uses the desktop one"
  json="{ dataUrl: \"$(pick data.unityweb)\", frameworkUrl: \"$(pick framework.js.unityweb)\", codeUrl: \"$(pick wasm.unityweb)\" }"
  sed -i "s|null /\*laf-astc\*/|$json /*laf-astc*/|" "$OUT/index.html"
  grep -q "dataUrl: \"Build/" "$OUT/index.html" || { echo "[pages] FAIL: the ASTC build's files aren't in index.html" >&2; exit 1; }
  astc_mb=$(set_mb $astc_files)
  echo "[pages] two texture sets: desktop $mb MB, ASTC (phones and tablets) $astc_mb MB to download"
  # and the ASTC set's files as gzip (<hash>.data.gz and so on), which a phone's browser unpacks itself as they arrive
  # (DecompressionStream; the page falls back to the Brotli files without it)
  gz_json=$(cd "$OUT" && node -e '
    const z = require("zlib"), fs = require("fs");
    const out = {}; let bytes = 0;
    for (const f of process.argv.slice(1)) {
      const kind = f.match(/\.(data|framework\.js|wasm)\.unityweb$/);
      if (!kind) continue;
      const gz = f.replace(/\.unityweb$/, ".gz");
      fs.writeFileSync(gz, z.gzipSync(z.brotliDecompressSync(fs.readFileSync(f)), { level: 9 }));
      bytes += fs.statSync(gz).size;
      out[{ data: "dataUrl", "framework.js": "frameworkUrl", wasm: "codeUrl" }[kind[1]]] = gz;
    }
    out.bytes = bytes;
    process.stdout.write(JSON.stringify(out));' $astc_files)
  sed -i "s|null /\*laf-gz\*/|$gz_json /*laf-gz*/|" "$OUT/index.html"
  grep -q '"dataUrl":"Build/[0-9a-f]*\.data\.gz"' "$OUT/index.html" || { echo "[pages] FAIL: the gzip files aren't in index.html" >&2; exit 1; }
  gz_mb=$(echo "$gz_json" | grep -o '"bytes":[0-9]*' | cut -d: -f2 | awk '{ printf "%d", $1 / 1e6 + 0.5 }')
  echo "[pages] the ASTC set gzipped, for browsers that unpack it themselves: $gz_mb MB to download"
  if [ "$gz_mb" -gt "$astc_mb" ]; then astc_mb=$gz_mb; fi
  if [ "$astc_mb" -gt "$mb" ]; then mb=$astc_mb; fi
else
  echo "[pages] note: no ASTC build (Builds/WebGL-astc): phones will unpack the desktop textures"
fi

# the phone and tablet note on the page says how big the download is
sed -i "s|<span data-laf-size>[0-9]*</span>|<span data-laf-size>$mb</span>|" "$OUT/index.html"

echo "[pages] $OUT"
(cd "$OUT" && find . -type f -printf '%s\t%p\n' | sort -rn | awk -F'\t' '{ printf "  %8.1f MB  %s\n", $1 / 1e6, $2 }')
echo "[pages] download: $mb MB; whole site: $(du -sb "$OUT" | awk '{ printf "%.1f", $1 / 1e6 }') MB"
# GitHub refuses files of 100 MB or more, and warns from 50 MB
big=$(find "$OUT" -type f -size +99999999c)
[ -z "$big" ] || { echo "[pages] FAIL: files of 100 MB or more, which GitHub won't take: $big" >&2; exit 1; }
warn=$(find "$OUT" -type f -size +49999999c)
[ -z "$warn" ] || echo "[pages] note: over 50 MB (GitHub warns, but takes it): $(echo "$warn" | sed "s|^$OUT/||" | tr '\n' ' ')"
grep -q 'src="Build/' "$OUT/index.html" && ! grep -q '="/' "$OUT/index.html" || { echo "[pages] FAIL: index.html has absolute URLs" >&2; exit 1; }
echo "[pages] OK"
