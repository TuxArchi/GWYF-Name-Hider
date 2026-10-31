#!/usr/bin/env bash
# Genera el zip y lo deja listo en release/ con su SHA256.
# Es lo que se sube a GitHub Releases.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ZIP_NAME="GambleWithYourFriends-NameHider-BepInEx6.zip"

"$ROOT/scripts/package.sh"

mkdir -p "$ROOT/release"
cp "$ROOT/dist/$ZIP_NAME" "$ROOT/release/$ZIP_NAME"

(
  cd "$ROOT/release"
  sha256sum "$ZIP_NAME" > SHA256SUMS.txt
)

echo
echo "Release listo:"
echo "  $ROOT/release/$ZIP_NAME"
echo "  $ROOT/release/SHA256SUMS.txt"
cat "$ROOT/release/SHA256SUMS.txt"
