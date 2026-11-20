#!/usr/bin/env bash
# Packs the module build (Builds/ModuleWeb/, build profile "Web - Module - Release") into
# Builds/ModuleWeb.zip for a GitHub release of this repo. The learning module downloads the
# release named in its sim.version (tools/fetch-sim.sh there, also used by its deploy).
#
#   Tools/package-module-build.sh
#
# Then: commit, tag (module-sim-vX.Y.Z), push the tag, create the release on GitHub for that
# tag and attach Builds/ModuleWeb.zip (the file name must stay ModuleWeb.zip).
set -euo pipefail

cd "$(dirname "$0")/.."
BUILD=Builds/ModuleWeb

if [ ! -f "$BUILD/Build/ModuleWeb.loader.js" ] || ! grep -q "precession-module" "$BUILD/index.html"; then
    echo "$BUILD is missing or not a module build with the bridge." >&2
    exit 1
fi

rm -f Builds/ModuleWeb.zip
(cd "$BUILD" && zip -qr ../ModuleWeb.zip .)                 # files at the zip's root
echo "Builds/ModuleWeb.zip ($(du -h Builds/ModuleWeb.zip | cut -f1)), built $(date -r "$BUILD/Build/ModuleWeb.wasm.unityweb" '+%Y-%m-%d %H:%M')"
