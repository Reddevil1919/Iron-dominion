#!/usr/bin/env bash
set -euo pipefail

repo_root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
output=${1:-"$repo_root/dist/windows"}
mkdir -p "$output"
output=$(cd "$output" && pwd)
godot=${GODOT_BIN:-godot}
version=4.6.3
if [[ $("$godot" --version) != "$version".stable.mono.* ]]; then
    printf 'Expected Godot %s .NET\n' "$version" >&2
    exit 1
fi

scratch=$(mktemp -d)
trap 'rm -rf "$scratch"' EXIT
templates="${XDG_DATA_HOME:-$HOME/.local/share}/godot/export_templates/$version.stable.mono"
template=windows_release_x86_64.exe
if [[ ! -s "$templates/$template" ]]; then
    archive=${GODOT_TEMPLATE_ARCHIVE:-"$scratch/templates.tpz"}
    if [[ -z ${GODOT_TEMPLATE_ARCHIVE:-} ]]; then
        curl --fail --location --silent --show-error --retry 3 --output "$archive" \
            "https://github.com/godotengine/godot-builds/releases/download/$version-stable/Godot_v$version-stable_mono_export_templates.tpz"
    fi
    checksum=6436f474ee085fb0a94d3d296b9821736bdd663b6e91b2228fdf77066f1d1400
    printf '%s  %s\n' "$checksum" "$archive" | sha256sum --check --status
    mkdir -p "$templates"
    unzip -joq "$archive" "templates/$template" "templates/windows_release_x86_64_console.exe" \
        "templates/version.txt" -d "$templates"
fi

run_godot() {
    "$godot" --headless --path "$repo_root/game" "$@" 2>&1 | tee "$scratch/godot.log"
    if grep -Eq '(^|[[:space:]])(ERROR|Error):' "$scratch/godot.log"; then
        return 1
    fi
}

dotnet build "$repo_root/game/IronDominion.Game.csproj" --configuration Debug
run_godot --editor --import
package="$scratch/IronDominion"
mkdir "$package"
run_godot --export-release "Windows Desktop" "$package/IronDominion.exe"

runtime="$package/data_IronDominion.Game_windows_x86_64"
test -s "$package/IronDominion.exe"
test -s "$package/IronDominion.pck"
test -s "$runtime/GodotSharp.dll"
test -s "$runtime/IronDominion.Game.dll"
test -s "$runtime/coreclr.dll"
file "$package/IronDominion.exe" | grep -Eq 'PE32\+.*x86-64'

commit=$(git -C "$repo_root" rev-parse HEAD)
runtime_version=$(sed -nE 's/.*"runtimepack\.Microsoft\.NETCore\.App\.Runtime\.win-x64\/([^"]+)".*/\1/p' \
    "$runtime/IronDominion.Game.deps.json" | sort -u)
[[ $runtime_version =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]
printf 'Commit %s\nGodot %s .NET\nRuntime .NET %s win-x64\n' "$commit" "$version" "$runtime_version" \
    > "$package/BUILD.txt"

mkdir "$package/LICENCES"
godot_source="https://raw.githubusercontent.com/godotengine/godot/$version-stable"
dotnet_source="https://raw.githubusercontent.com/dotnet/runtime/v$runtime_version"
curl --fail --location --silent --show-error --retry 3 "$godot_source/LICENSE.txt" \
    --output "$package/LICENCES/Godot-LICENSE.txt"
curl --fail --location --silent --show-error --retry 3 "$godot_source/COPYRIGHT.txt" \
    --output "$package/LICENCES/Godot-COPYRIGHT.txt"
curl --fail --location --silent --show-error --retry 3 "$dotnet_source/LICENSE.TXT" \
    --output "$package/LICENCES/dotnet-LICENSE.txt"
curl --fail --location --silent --show-error --retry 3 "$dotnet_source/THIRD-PARTY-NOTICES.TXT" \
    --output "$package/LICENCES/dotnet-THIRD-PARTY-NOTICES.txt"

cat > "$package/README.txt" <<'INSTRUCTIONS'
Iron Dominion for Windows x64

Extract the entire archive, then open IronDominion.exe. Keep the PCK and
data_IronDominion.Game_windows_x86_64 folder together with the executable.
The archive includes the .NET runtime.

To preview Forward+ on a Vulkan-capable Windows GPU, run this command
from the extracted directory:
IronDominion.exe --rendering-method forward_plus --rendering-driver vulkan

Left-click or drag to select. Right-click the ground to move, or a visible
enemy to attack. Arrow keys or WASD pan the map, and the mouse wheel zooms.
Escape opens the pause menu. Destroy the orange headquarters to win.

See BUILD.txt for the source commit and LICENCES for runtime notices.
INSTRUCTIONS

zip_name=IronDominion-Windows-x64.zip
(cd "$scratch" && zip -qr "$zip_name" IronDominion)
unzip -tq "$scratch/$zip_name"
mv -f "$scratch/$zip_name" "$output/$zip_name"
(cd "$output" && sha256sum "$zip_name" > SHA256SUMS.txt)
printf 'Windows archive %s\n' "$output/$zip_name"
