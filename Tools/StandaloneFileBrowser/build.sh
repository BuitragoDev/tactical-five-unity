#!/usr/bin/env bash
#
# Compila libStandaloneFileBrowser.so (parcheada) y la copia al proyecto.
#
# Requisitos (Ubuntu/Debian):
#   sudo apt install build-essential cmake pkg-config libgtk-3-dev libx11-dev
#
set -euo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(cd "$HERE/../.." && pwd)"
TARGET_DIR="$PROJECT_ROOT/Assets/StandaloneFileBrowser/Plugins/Linux/x86_64"
TARGET_SO="$TARGET_DIR/libStandaloneFileBrowser.so"

echo "==> Compilando en $HERE/build"
rm -rf "$HERE/build"
mkdir -p "$HERE/build"
cd "$HERE/build"

cmake .. -DCMAKE_BUILD_TYPE=Release
cmake --build . --config Release -j"$(nproc)"

if [ ! -f "$HERE/build/libStandaloneFileBrowser.so" ]; then
    echo "ERROR: no se generó libStandaloneFileBrowser.so" >&2
    exit 1
fi

cp "$HERE/build/libStandaloneFileBrowser.so" "$TARGET_SO"
echo "==> OK: $TARGET_SO actualizada"
echo "    Vuelve a Unity (reimporta el plugin) y recompila la build."
