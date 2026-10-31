#!/usr/bin/env bash
# Compila GwyfNameHider en modo Release.
#
# Uso:
#   ./scripts/build.sh
#   GAME_DIR="/ruta/al/juego" ./scripts/build.sh
#   GWYF_DIR="/ruta/al/juego" ./scripts/build.sh
#   DOTNET="$HOME/.dotnet/dotnet" ./scripts/build.sh
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DOTNET="${DOTNET:-dotnet}"
GAME_DIR="${GAME_DIR:-${GWYF_DIR:-}}"

if ! command -v "$DOTNET" >/dev/null 2>&1; then
  echo "error: no se encontro '$DOTNET'. Instala el .NET SDK 8+ o define DOTNET=/ruta/dotnet." >&2
  exit 1
fi

EXTRA=()
if [[ -n "$GAME_DIR" ]]; then
  EXTRA+=("-p:GameDir=$GAME_DIR")
fi

"$DOTNET" build "$ROOT/src/GwyfNameHider.csproj" \
  -c Release --nologo "${EXTRA[@]}" "$@"

echo
echo "DLL generada:"
echo "  $ROOT/src/bin/Release/netstandard2.1/GwyfNameHider.dll"
