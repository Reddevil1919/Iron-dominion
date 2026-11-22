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
run_smoke_test() {
    local script=$1
    local success=$2
    local frame_limit=${3:-600}
    local mode=${4:-passive}
    local user_args=()
    if [[ $mode == passive ]]; then
        user_args=(-- --passive-opponent)
    fi
    run_godot --fixed-fps 60 --quit-after "$frame_limit" --script "$repo_root/tests/godot/$script" "${user_args[@]}"
    if ! grep -Fxq "$success" "$log"; then
        printf 'The Godot smoke test %s did not finish.\n' "$script" >&2
        return 1
    fi
}

run_smoke_test rts_camera_smoke.gd 'RTS camera smoke checks passed.'
run_smoke_test unit_selection_smoke.gd 'Unit selection smoke checks passed.'
run_smoke_test unit_movement_smoke.gd 'Unit movement smoke checks passed.' 1500
run_smoke_test unit_combat_smoke.gd 'Unit combat smoke checks passed.' 2800
run_smoke_test fog_of_war_smoke.gd 'Fog of war smoke checks passed.' 2900
run_smoke_test unit_production_smoke.gd 'Unit production smoke checks passed.' 3000
run_smoke_test sector_capture_smoke.gd 'Sector capture smoke checks passed.' 1500
run_smoke_test infantry_cover_smoke.gd 'Infantry cover smoke checks passed.' 1600
run_smoke_test enemy_ai_smoke.gd 'Enemy AI smoke checks passed.' 8500 active
run_smoke_test battlefield_hud_smoke.gd 'Battlefield HUD smoke checks passed.' 2000
run_smoke_test match_lifecycle_smoke.gd 'Match lifecycle smoke checks passed.' 8000
