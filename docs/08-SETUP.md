# Development setup

RTS-001 uses the **Godot 4.6.3 stable .NET editor**, **.NET SDK 10.0.404** and
`net10.0`. `global.json` requires the exact SDK, and the game uses the matching
`Godot.NET.Sdk/4.6.3` package. The Godot C# documentation specifies .NET 8 or
later for desktop. This project uses the .NET 10 LTS line.

- [Godot C# requirements](https://docs.godotengine.org/en/4.6/tutorials/scripting/c_sharp/c_sharp_basics.html)
- [Godot 4.6.3 stable release and checksums](https://github.com/godotengine/godot-builds/releases/tag/4.6.3-stable)
- [.NET SDK 10.0.404 downloads](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)

## Linux x86-64 cloud setup

Run these commands in Bash from the repository root. The environment needs Git,
`curl`, `unzip`, `tar`, GNU coreutils, access to GitHub releases, Microsoft SDK
downloads and NuGet, and the usual Linux Godot runtime libraries. The Codex
cloud image already provided the required system libraries. Use a writable
directory alongside the checkout for tools, caches and export templates.

```bash
set -euo pipefail

toolchain_dir="$(cd .. && pwd)/iron-dominion-toolchain"
mkdir -p "$toolchain_dir"/{dotnet,cache,config,data,dotnet-cli,nuget}
export DOTNET_ROOT="$toolchain_dir/dotnet"
export DOTNET_CLI_HOME="$toolchain_dir/dotnet-cli"
export NUGET_PACKAGES="$toolchain_dir/nuget"
export XDG_CACHE_HOME="$toolchain_dir/cache"
export XDG_CONFIG_HOME="$toolchain_dir/config"
export XDG_DATA_HOME="$toolchain_dir/data"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_GENERATE_ASPNET_CERTIFICATE=false

sdk_version=10.0.404
sdk_archive="dotnet-sdk-$sdk_version-linux-x64.tar.gz"
sdk_sha512=577d0eb17fec33d029104c5c63a1ab0accc3e28776274abef2218dbac5af18d2109b5406d26b31d76f5c245215e3ba0926a94c20246c672c41847c9e1eb1dc50
curl -fLsS --retry 3 \
  "https://builds.dotnet.microsoft.com/dotnet/Sdk/$sdk_version/$sdk_archive" \
  -o "$toolchain_dir/$sdk_archive"
printf '%s  %s\n' "$sdk_sha512" "$toolchain_dir/$sdk_archive" | sha512sum -c -
tar -xzf "$toolchain_dir/$sdk_archive" -C "$DOTNET_ROOT"
export PATH="$DOTNET_ROOT:$PATH"

GODOT_BIN="$(scripts/install-godot-linux.sh "$toolchain_dir/godot")"
export GODOT_BIN
dotnet --version
"$GODOT_BIN" --version
```

The expected versions are `10.0.404` and `4.6.3.stable.mono.official.7d41c59c4`.
The SDK compiler and test runner can launch child processes and use OS-level
synchronization. If the first `dotnet` command reports an error creating the
`NuGet-Migrations` mutex in Codex, run that command with the cloud command
tool's approved sandbox escalation and retry.

## Build, test and validate

```bash
dotnet restore IronDominion.sln
dotnet build IronDominion.sln --configuration Release --no-restore
dotnet test IronDominion.sln --configuration Release --no-build
scripts/validate-godot.sh
```

The Godot script builds the Debug game assembly, imports the project with
`--headless --editor --import`, then runs the configured main scene with
`--headless --fixed-fps 20 --quit-after 10`. The script fails on a nonzero
exit code or a Godot error in the output. GitHub Actions runs the same commands
across the `dotnet` and `validate` jobs.

The script also runs headless camera, selection, movement, combat,
production, sector capture, infantry cover, enemy AI, HUD and match lifecycle checks against the main scene. They send keyboard and mouse
events to verify camera bounds, selection ownership, control groups,
obstacle navigation, a focused attack, a tank spawning at the HQ and
a blocked barracks queue resuming after a trained squad moves away. The
sector check moves infantry into a zone and verifies the HUD, map marker
and increased balance. The cover check moves a squad in and out of a
cover rectangle and verifies its indicator. The xUnit combat cases verify
the exact damage per hit. The focused checks launch a passive opponent
with `-- --passive-opponent` in the Debug game. The enemy AI smoke runs
the default scene and observes the orange side train a tank, capture
a sector and advance on the blue HQ. The HUD check verifies selection
health, compact window layout, rejected orders and temporary shot traces.
The fog check scouts around the roadblock, reveals a unit and a building
and hides them again on withdrawal. The lifecycle checks pause and resume
through the UI, win by destroying
the enemy HQ, restart from the result screen and confirm defeat against
the active opponent.

For a graphical launch on Linux, run `"$GODOT_BIN" --path game` after the
Debug build. On Windows, install the pinned .NET SDK and Godot .NET editor,
then import `game/project.godot` in the project manager and run the main scene.

## Windows x64 package

Download the `windows-x64-package` artifact from the **Windows x64 Build**
GitHub Actions workflow. Open the artifact and extract all of
`IronDominion-Windows-x64.zip`, then double-click `IronDominion.exe` inside
`IronDominion`. Keep the PCK and `data_IronDominion.Game_windows_x86_64`
directory alongside the executable. The package includes the .NET runtime.
`BUILD.txt` identifies the source commit.

To create the same package from Linux, set up the pinned editor and SDK above
and run `scripts/export-windows.sh`. The script installs the matching Windows
export templates with a checksum check if needed, imports the Godot project,
exports in Release and records the archive SHA-256 in `dist/windows/`.
Pass an output directory as the first argument to choose another location.

The Windows CI job verifies the archive's checksum and source commit before
launching the default battlefield and a 50-unit headless load check on native
Windows x64. See the [performance report](09-PERFORMANCE.md) for the graphical
profile and commands to reproduce it.

## First playable milestone

The prototype runs a 128 m × 128 m battlefield with two bases and a 20 Hz
simulation clock. Each base has one HQ and one barracks. Use the arrow
keys, the physical WASD positions (ZQSD on AZERTY), or the 24 pixels
inside the window edges to pan. The wheel zooms between the 32 m close
view and the 136 m overview while keeping the same ground focus. The
terrain bounds limit how far the focus can move.

The scene has two infantry squads and one tank per side. Left-click a blue
unit or drag a rectangle to select several. Hold Shift when releasing the
mouse to toggle the blue units under the click or rectangle. Clicking empty
ground clears the selection. Selected units show a yellow-green ring.

Use Ctrl+1 through Ctrl+9 to assign the current selection to a group. Press
the number by itself to recall it. Assign an empty selection to clear a
group. Unassigned number keys leave the selection as it is.

Right-click clear ground to move the selected blue units. Units route around
the central roadblock and the buildings. For groups, the first unit goes to the
clicked point and the others use adjacent 9 m slots in entity ID order. Pick
open ground with room for the group. A blocked destination leaves existing
orders in effect. Routes account for the static map. Moving units can pass
through each other in this milestone.

Right-click an orange unit or building to focus fire. An accepted attack order
stops the selected units. Move them within weapon range for the shot to
fire. Units also fire automatically at visible enemies in range.
The roadblock and buildings block line of sight. Brief shot traces show
where units are firing. Health bars shrink and change from green to amber
at 60% health, then red at 25%. The bottom-right panel shows exact hit
points, group composition and cover for the current selection. Rejected
orders briefly display a reason at the top right. Destroyed units leave
the battlefield and destroyed buildings leave solid ruins.

Use the panel at the bottom left to train infantry at the barracks for
100 credits (8 seconds) or a tank at the HQ for 500 credits (20 seconds).
You start with 500 credits. Costs are deducted when orders enter the
queue. The bar shows progress for the first order. A full amber bar and
**Exit blocked** mean training is complete. Move units away from the
producer's exit to let the finished unit spawn.

Each living HQ earns 5 credits per second. To capture one of the three
marked sectors, move an infantry squad inside its ring and hold it for
10 seconds uncontested. Tanks can fight in the area. Living infantry
determines capture. The top-left panel shows ownership, the progress bar
for each sector and contested sectors. Each owned sector adds 2 credits per second to the
rate shown next to your balance.

The two ochre rectangles provide cover. Move infantry inside one to
display **COVER** above its health bar and reduce incoming damage. A
10-damage hit deals 7 HP, and a 45-damage hit deals 32 HP after rounding.
Tanks and buildings take full damage.

The orange commander trains reinforcements, claims neutral sectors
and launches an attack once it has enough units. It responds to visible
threats near its HQ.

Move infantry or tanks forward to scout. A living unit can see enemies
within 42 m along a clear line of sight. The map is darkest in unexplored
areas and dims again once the scouts leave. Visible enemies can be targeted
with a right-click. Sector ownership stays on the HUD.

Destroy the orange HQ to win. If your HQ falls, the match ends in defeat.
If both fall on the same simulation tick, the match is a draw. Press
**Esc** or click **Pause** to freeze the match. The pause menu lets you
resume, restart or quit. The result screen lets you restart or quit.
