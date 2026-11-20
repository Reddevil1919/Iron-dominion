#!/usr/bin/env bash
set -euo pipefail

repo_root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
godot=${GODOT_BIN:-godot}
version=$("$godot" --version)
if [[ $version != 4.6.3.stable.mono.* ]]; then
    printf 'Expected Godot 4.6.3 .NET, got %s\n' "$version" >&2
    exit 1
fi

log=$(mktemp)
trap 'rm -f "$log"' EXIT

run_godot() {
    "$godot" --headless --path "$repo_root/game" "$@" 2>&1 | tee "$log"
    # Engine and resource errors can appear in the log even when Godot exits zero.
    if grep -Eq '(^|[[:space:]])(ERROR|Error):' "$log"; then
        return 1
    fi
}

dotnet build "$repo_root/game/IronDominion.Game.csproj" --configuration Debug
run_godot --editor --import
run_godot --fixed-fps 20 --quit-after 10
run_godot --fixed-fps 60 --quit-after 600 --script "$repo_root/tests/godot/rts_camera_smoke.gd"
if ! grep -Fxq 'RTS camera smoke checks passed.' "$log"; then
    echo 'The RTS camera smoke test did not finish.' >&2
    exit 1
fi
