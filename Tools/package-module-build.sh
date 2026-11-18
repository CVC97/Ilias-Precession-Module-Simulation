#!/usr/bin/env bash
# Packs a web build into a zip for a GitHub release of this repo. The learning module downloads the release named in
# its version pin (tools/fetch-sim.sh there, also used by its deploy).
#
#   Tools/package-module-build.sh              # Builds/ModuleWeb/  (profile "Web - Module - Release")  → Builds/ModuleWeb.zip
#   Tools/package-module-build.sh LectureWeb   # Builds/LectureWeb/ (profile "Web - Lecture - Release") → Builds/LectureWeb.zip
#
# Then: commit, tag (module-sim-vX.Y.Z or lecture-sim-vX.Y.Z), push the tag, create the release on GitHub for that tag
# and attach the zip (the file name must stay ModuleWeb.zip / LectureWeb.zip).
set -euo pipefail

cd "$(dirname "$0")/.."
NAME=${1:-ModuleWeb}
BUILD=Builds/$NAME

if [ ! -f "$BUILD/Build/$NAME.loader.js" ] || ! grep -q "precession-module" "$BUILD/index.html"; then
    echo "$BUILD is missing or not a module build with the bridge." >&2
    exit 1
fi

rm -f "Builds/$NAME.zip"
(cd "$BUILD" && zip -qr "../$NAME.zip" .)                   # files at the zip's root
echo "Builds/$NAME.zip ($(du -h "Builds/$NAME.zip" | cut -f1)), built $(date -r "$BUILD/Build/$NAME.wasm.unityweb" '+%Y-%m-%d %H:%M')"
