#!/usr/bin/env bash
set -euo pipefail

# Official Godot .NET Linux x86_64 editor archive.
version=4.6.3
archive="Godot_v${version}-stable_mono_linux_x86_64.zip"
checksum=702a8a6785060203fd46373adf864586ecf0c377ac685d7107dde66b1eba0a9e
destination=${1:?Usage: install-godot-linux.sh <destination>}

scratch=$(mktemp -d)
trap 'rm -rf "$scratch"' EXIT

curl --fail --location --silent --show-error --retry 3 \
    "https://github.com/godotengine/godot-builds/releases/download/${version}-stable/${archive}" \
    --output "$scratch/$archive"
printf '%s  %s\n' "$checksum" "$scratch/$archive" | sha256sum --check --status
mkdir -p "$destination"
unzip -oq "$scratch/$archive" -d "$destination"

realpath "$destination/${archive%.zip}/Godot_v${version}-stable_mono_linux.x86_64"
