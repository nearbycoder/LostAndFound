#!/usr/bin/env bash
# Unity 6.6 launcher for this project.
#
# The editor links against libxml2.so.2 but this distro ships libxml2.so.16, so we point the
# loader at a local copy in .unity-libs/ (copied from libxml2-legacy). `sudo pacman -S libxml2-legacy`
# makes this unnecessary.
#
#   Tools/unity.sh                 open the project in the GUI editor
#   Tools/unity.sh headless        resident batch-mode editor (serves `unity command` via Pipeline)
#   Tools/unity.sh build-linux     batch-build Builds/Linux/LostAndFound.x86_64
#   Tools/unity.sh run <Method>    batch-run a static editor method and quit
#   Tools/unity.sh test            run EditMode tests (results in Logs/test-results.xml)
set -euo pipefail
UNITY="${UNITY:-$HOME/Unity/Hub/Editor/6000.6.2f1/Editor/Unity}"
PROJECT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
LIBS="$PROJECT/.unity-libs"
[ -e "$LIBS/libxml2.so.2" ] || { mkdir -p "$LIBS"; cp "$HOME/.local/share/ptt-unity-libs/libxml2.so.2"* "$LIBS/" 2>/dev/null || true; }
export LD_LIBRARY_PATH="$LIBS${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"

case "${1:-open}" in
  open)        exec "$UNITY" -projectPath "$PROJECT" ;;
  headless)    exec "$UNITY" -batchmode -projectPath "$PROJECT" -logFile "$PROJECT/Logs/headless.log" ;;
  build-linux) exec "$UNITY" -batchmode -nographics -quit -projectPath "$PROJECT" \
                 -executeMethod LostAndFound.EditorTools.BuildScript.BuildLinux -logFile "$PROJECT/Logs/build.log" ;;
  run)         exec "$UNITY" -batchmode -nographics -quit -projectPath "$PROJECT" \
                 -executeMethod "$2" -logFile "$PROJECT/Logs/run.log" ;;
  test)        exec "$UNITY" -batchmode -nographics -projectPath "$PROJECT" -runTests -testPlatform EditMode \
                 -testResults "$PROJECT/Logs/test-results.xml" -logFile "$PROJECT/Logs/test.log" ;;
  *) echo "usage: $0 [open|headless|build-linux|run <Method>|test]" >&2; exit 2 ;;
esac
