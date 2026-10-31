#!/usr/bin/env bash
# Compila el plugin y genera el zip instalable en dist/.
#
# Necesita el zip oficial de BepInEx 6 (Unity Mono x64, build 791):
#   vendor/BepInEx_win_x64_6.0.0-be.791.zip
# o indicarlo con BEPINEX_ZIP=/ruta/al/zip.
#
# Uso:
#   ./scripts/package.sh
#   BEPINEX_ZIP="/ruta/BepInEx_win_x64_6.0.0-be.791.zip" ./scripts/package.sh
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT="${OUT:-$ROOT/dist/GambleWithYourFriends-NameHider-BepInEx6.zip}"
BEPINEX_ZIP="${BEPINEX_ZIP:-$ROOT/vendor/BepInEx_win_x64_6.0.0-be.791.zip}"
PLUGIN_DLL="$ROOT/src/bin/Release/netstandard2.1/GwyfNameHider.dll"

if [[ ! -f "$BEPINEX_ZIP" ]]; then
  echo "error: falta el zip de BepInEx: $BEPINEX_ZIP" >&2
  echo "Descarga BepInEx 6 build 791 (Unity Mono x64) desde https://builds.bepinex.dev/projects/bepinex_be" >&2
  echo "y guardalo en vendor/, o define BEPINEX_ZIP." >&2
  exit 1
fi

"$ROOT/scripts/build.sh"

STAGE="$(mktemp -d)"
trap 'rm -rf "$STAGE"' EXIT

unzip -q "$BEPINEX_ZIP" -d "$STAGE"

# El changelog de BepInEx no hace falta para instalar.
rm -f "$STAGE/changelog.txt"

mkdir -p "$STAGE/BepInEx/plugins/GwyfNameHider"
cp "$PLUGIN_DLL" "$STAGE/BepInEx/plugins/GwyfNameHider/GwyfNameHider.dll"

cp "$ROOT/README.md" "$STAGE/README.md"
cp "$ROOT/LICENSE" "$STAGE/LICENSE"
cp "$ROOT/third_party/LICENSE-BepInEx.txt" "$STAGE/LICENSE-BepInEx.txt"

mkdir -p "$STAGE/assets"
cp "$ROOT/assets/preview.jpg" "$STAGE/assets/preview.jpg"

mkdir -p "$(dirname "$OUT")"
rm -f "$OUT"
(cd "$STAGE" && zip -q -r -X "$OUT" .)

echo
echo "Zip generado:"
echo "  $OUT"
