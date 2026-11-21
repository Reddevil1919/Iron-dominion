# Implementation status

## RTS-003 fixed ticks and command scheduling

The simulation processes due commands every 50 ms in the order of their
scheduled tick, player ID and sequence. `GameSession.Commands` assigns a
sequence per player. Replaying the same pre-sequenced orders in reverse
arrival order produces the same processing events. Stale ticks and duplicate
sequences are rejected before enqueueing. Each processing event records the
initial validation result, including ownership and whether the actors are alive.

### Verification on 20 November 2026

| Check | Observed result |
| --- | --- |
| `dotnet restore IronDominion.sln` | Restored all eight projects in an isolated checkout. |
| `dotnet build IronDominion.sln --configuration Release --no-restore` | Passed with 0 warnings and 0 errors. |
| `dotnet test IronDominion.sln --configuration Release --no-build` | 52 xUnit cases passed, including 13 new scheduling cases. Covered replay order, tick boundaries, duplicate rejection, ownership, actor health and immutable selections. VSTest used a private temporary artifacts directory. |
| `scripts/validate-godot.sh` | Debug build, headless editor import and main-scene startup passed with Godot 4.6.3 .NET on Linux. |

Godot drives the fixed tick from the existing battlefield scene. Gameplay
input and order effects arrive in the subsequent selection, movement, combat
and production milestones. The runtime check was headless on Linux.

## RTS-002 domain and configuration

The domain defines players, units, buildings and sectors. Entity IDs are
positive and the entity index rejects duplicates across entity kinds. Units
and buildings require an owner. Sectors can start neutral. Positions and
configuration data are independent of Godot.

The Godot scene loads the embedded, versioned JSON at startup and uses its
validated 20 Hz tick rate. The configuration contains the MVP balance
values. Match initialization and gameplay systems will consume the remaining
settings when those features are implemented.

### Verification on 20 November 2026

| Check | Observed result |
| --- | --- |
| `dotnet restore IronDominion.sln` | All eight projects restored or up to date. |
| `dotnet build IronDominion.sln --configuration Release --no-restore` | Passed with 0 warnings and 0 errors. |
| `dotnet test IronDominion.sln --configuration Release --no-build` | 39 xUnit cases passed, including 36 new domain and configuration cases. VSTest used a private temporary artifacts directory. |
| `scripts/validate-godot.sh` | Godot 4.6.3 .NET imported the project and started the main scene headlessly on Linux. |
| Headless launch with a deliberately invalid embedded configuration | In an isolated copy, changing `tickRate` to 60 logged the offending setting and stopped Godot with exit code 1. |
| Godot headless Windows x64 release export | Passed from Linux. The published Infrastructure assembly contains the schema-1 JSON. Native Windows execution and graphical rendering were outside this check. |

## RTS-004 map and camera

The orthographic camera pans with the arrow keys, physical WASD positions
or the inside edges of the focused game window. Its ground focus stays
within the 128 m × 128 m terrain collider. The mouse wheel changes the
camera size between 32 m and 136 m around the current focus.

### Verification on 20 November 2026

| Check | Observed result |
| --- | --- |
| `dotnet build IronDominion.sln --configuration Release --no-restore` | Passed with 0 warnings and 0 errors. |
| `dotnet test IronDominion.sln --configuration Release --no-build` | All 39 xUnit cases on this branch passed. VSTest used a private temporary artifacts directory. |
| `scripts/validate-godot.sh` | Debug build, headless import and scene startup passed. The camera smoke test sent key and wheel events and verified movement direction, terrain bounds, both zoom limits, and a stable focus and viewing angle. |
| Graphical Linux smoke test | Launched the game in Godot .NET and observed the view change with an arrow key, the mouse wheel and the pointer at the left edge. |
| Headless Windows x64 export | Generated the executable and PCK from Linux and verified that the published game assembly contains `RtsCamera`. Native Windows execution remains to be tested. |

## RTS-005 unit selection and control groups

The battlefield starts with two infantry squads and one tank per side.
The local player can select living blue units with a click or a rectangle.
Shift toggles the matching units. Selection rings identify the active
selection, and Ctrl+1 through Ctrl+9 save groups that the corresponding
number keys recall. The selection model lives in the Application project.

### Verification on 20 November 2026

| Check | Observed result |
| --- | --- |
| `dotnet build IronDominion.sln --configuration Release --no-restore` | Passed with 0 warnings and 0 errors. |
| `dotnet test IronDominion.sln --configuration Release --no-build` | All 46 xUnit cases passed. The 7 new cases cover ownership, living units, toggling and control groups. |
| `scripts/validate-godot.sh` | Debug build, headless import, scene launch and camera checks passed. The selection check sent clicks, Shift-clicks, drags and group keys to the main scene and verified which rings were visible. |
| Graphical Linux smoke test | Clicked a blue squad, dragged a rectangle over both sides and observed rings on the three blue units. Clicking an orange squad cleared the rings. After Ctrl+9 had saved the group, pressing 9 brought the three rings back. |

## RTS-006 movement and navigation

Right-click ground to move the selection. RTS-006 brings the RTS-003
scheduler into this branch from the commit reviewed in PR #5. An order
checks ownership and reachability, computes the routes once and advances the
units at their configured speeds every 50 ms. Godot renders the positions
from the read-only snapshots. The navigation region keeps a 5 m clearance
from the terrain edges, both HQs and the central obstacle. Group destinations
use 9 m spacing. Navigation currently accounts for the static map.

### Verification on 20 November 2026

| Check | Observed result |
| --- | --- |
| `dotnet restore IronDominion.sln` | Restored all nine projects. |
| `dotnet build IronDominion.sln --configuration Release --no-restore` | Passed with 0 warnings and 0 errors. |
| `dotnet test IronDominion.sln --configuration Release --no-build` | All 65 xUnit cases passed, including six movement cases. Covered tick speeds, cached routes, inaccessible destinations, ownership and a ten-unit order with distinct destinations. |
| `scripts/validate-godot.sh` | Headless import, scene launch, camera, selection and movement passed. A right-click sent the blue tank around the roadblock at its configured speed, while blocked targets left it stationary. |
| Graphical Linux smoke test | Selected the blue tank in the game window and sent it to ground on the opposite side of the central block. Observed the tank arrive at the clicked destination. |

## RTS-007 combat, health and destruction

Right-click an orange unit or HQ to focus fire. The simulation checks
ownership, visibility, weapon range and cooldowns. It resolves each tick's
damage simultaneously in entity ID order and reports destruction once.
Godot shows health bars, removes destroyed units and leaves HQ ruins as
solid terrain obstacles. The roadblock and HQs block fire.

The current visibility rule uses line of sight from living allies. Move
units within range before giving an attack order. Cover and match outcomes
are tracked in RTS-010 and RTS-013.

### Verification on 20 November 2026

| Check | Observed result |
| --- | --- |
| `dotnet build IronDominion.sln --configuration Release --no-restore` | Passed with 0 warnings and 0 errors. |
| `dotnet test IronDominion.sln --configuration Release --no-build` | All 76 xUnit cases passed, including 11 new cases for cooldown, range, sight, targeting, simultaneous damage and destroyed actors. |
| `scripts/validate-godot.sh` | Import, camera, selection, movement and combat checks passed. The focused attack destroyed the clicked squad while the closer tank survived. The navigation checks use synchronous map initialization in the test harness. |
| Graphical Linux smoke test | Observed both tanks lose health, then disappear with their bars during the exchange. |

## RTS-008 headquarters, barracks and production

Each side starts with 500 credits, one HQ and one barracks. The player's
production panel lists the balance, cost, queue length and time remaining.
Barracks train infantry for 100 credits in 8 seconds. HQs train tanks for
500 credits in 20 seconds.

Orders reserve their cost when accepted and advance in FIFO order per
building. A finished order waits for a free, walkable exit. Each unit
spawns once with a new entity ID and can be selected, moved and used in
combat. Both building types obstruct movement and line of sight. A
destroyed producer discards its queue and leaves reserved credits spent.

### Verification on 20 November 2026

| Check | Observed result |
| --- | --- |
| `dotnet restore IronDominion.sln` and `dotnet build IronDominion.sln --configuration Release --no-restore` | Restore and Release build passed with 0 warnings and 0 errors. |
| `dotnet test IronDominion.sln --configuration Release --no-build` | All 84 xUnit cases passed, including eight new cases for BR-009, BR-010, T-002 and T-011. They verify reservation, ownership, FIFO, spawn IDs, blocked exits and producer destruction. |
| `scripts/validate-godot.sh` | Godot 4.6.3 .NET imported and launched the scene headlessly. Camera, selection, movement, combat and production checks passed. Clicks queued two squads and a tank across two matches. Moving the first squad cleared the second order to spawn. |
| Graphical Linux smoke test | Clicked the infantry button, saw the credits fall and the first squad spawn. Observed the next order wait at the occupied exit, then spawn after moving the first squad away. |

The graphical cloud driver reported that changing VSync is unsupported.
The graphical test ran on Linux. Windows runtime verification remains
under RTS-015.

## RTS-009 passive income and sector capture

A living HQ earns 5 credits per second and each owned sector adds 2.
Income accrues on simulation ticks and carries fractional credits forward.
The production panel shows the current balance and combined rate.

The battlefield has three initially neutral sectors. Living infantry captures
within a 6 m radius after 200 uncontested ticks. Opposing infantry freeze
progress. When only the rival remains, it removes prior progress one tick at
a time before it can capture. An empty sector resets unfinished progress.
Ownership and income transfer at completion. Colored rings and the sector
panel show ownership, progress and contested states.

### Verification on 21 November 2026

| Check | Observed result |
| --- | --- |
| `dotnet restore IronDominion.sln` and `dotnet build IronDominion.sln --configuration Release --no-restore` | Restore and Release build passed with 0 warnings and 0 errors. |
| `dotnet test IronDominion.sln --configuration Release --no-build` | All 91 xUnit cases passed. Seven new cases cover BR-011 through BR-013 and T-003 through T-005, including an opponent contesting on the last tick and income transferring on recapture. |
| `scripts/validate-godot.sh` | Debug build, import, startup and all six smoke checks passed. The new check sends infantry to sector A, observes its progress and verifies the map marker and the balance after income rises from 5 to 7 credits per second. |
| Graphical Linux smoke test | Sent a blue squad into sector A and observed the blue marker, ownership change and the displayed rate rising to 7 credits per second. |

The graphical check ran in the Linux cloud environment. Windows runtime
verification remains under RTS-015.

## RTS-010 static infantry cover

Two traversable ochre rectangles mark cover on the battlefield. A living
infantry squad inside either zone shows **COVER** above its health bar.
The simulation applies the configured 0.7 multiplier to each incoming
hit after movement and rounds halves away from zero before aggregating
damage. A 10-damage hit deals 7 HP, and a 45-damage hit deals 32 HP. Tanks
and buildings take full damage.

### Verification on 21 November 2026

| Check | Observed result |
| --- | --- |
| `dotnet restore IronDominion.sln` and `dotnet build IronDominion.sln --configuration Release --no-restore` | Restore and Release build passed with 0 warnings and 0 errors. |
| `dotnet test IronDominion.sln --configuration Release --no-build` | All 99 xUnit cases passed. Eight new cases cover BR-008 and T-006, including inclusive borders, overlapping zones, per-hit rounding, moving into and out of cover, and full damage to tanks and buildings. |
| `scripts/validate-godot.sh` | Godot 4.6.3 .NET imported and launched the scene headlessly. All seven smoke checks passed. The cover check moved an infantry squad into the east zone and out again, and verified the indicator in both positions. |
| Graphical Linux smoke test | Moved a blue infantry squad into and out of the west rectangle and observed the **COVER** label appear and disappear. |

The headless renderer returned transparent pixels when reading the health
texture, so the xUnit suite verifies exact damage. The rendered smoke test
ran on Linux. Windows runtime verification remains under RTS-015.

## RTS-011 enemy AI

The orange commander makes decisions every 40 ticks. It uses the same
production, move and attack commands as the player. It trains
reinforcements, sends its nearest infantry to neutral sectors and
defends its HQ from visible nearby units. With at least four living
units and a tank, it advances on a visible enemy HQ. It scouts via public
sector locations when the HQ is hidden.

The commander receives an immutable player observation. Opponent
positions and IDs are available only for contacts seen by a living ally.
The source of visibility is the same line-of-sight check that validates
attack orders. On-screen fog rendering follows in RTS-016.

### Verification on 21 November 2026

| Check | Observed result |
| --- | --- |
| `dotnet restore IronDominion.sln` and `dotnet build IronDominion.sln --configuration Release --no-restore` | Restore and Release build passed with 0 warnings and 0 errors. |
| `dotnet test IronDominion.sln --configuration Release --no-build` | All 108 xUnit cases passed. Nine new cases verify scheduling, affordable production, capture, defense, assault, path retry and hidden contacts for T-014. |
| `scripts/validate-godot.sh` | Godot 4.6.3 .NET imported and launched the scene headlessly. All eight smoke checks passed. The default AI trained an orange tank, took sector C and destroyed the blue HQ without human orders. The seven focused gameplay checks use a passive opponent in Debug. |
| Graphical Linux smoke test | Saw the orange infantry capture C and B, and saw orange reinforcements advance toward the blue side. |

The rendered check ran on Linux. Windows runtime verification remains
under RTS-015. Match outcomes and restart follow in RTS-013.

## RTS-012 battlefield HUD and feedback

The production panel shows the balance, income, time remaining and a
progress bar for each queue. A completed order waiting for a free exit
shows a full amber bar. The sector panel shows each flag's owner and
capture progress, including contested states. The bottom-right panel
shows selected units, exact hit points and cover. World health bars
change color at 60% and 25% health.

Firing draws short-lived traces. Failed local orders display a brief
reason. The HUD reads the existing snapshot and command events.

### Verification on 21 November 2026

| Check | Observed result |
| --- | --- |
| `dotnet restore IronDominion.sln` and `dotnet build IronDominion.sln --configuration Release --no-restore` | Restore and Release build passed with 0 warnings and 0 errors. |
| `dotnet test IronDominion.sln --configuration Release --no-build` | All 108 xUnit cases passed. |
| `scripts/validate-godot.sh` | Godot 4.6.3 .NET imported and launched the scene headlessly. All nine smoke checks passed. The HUD test covered single and group hit points, panel separation at 960 × 540, rejected orders, combat damage and shot-trace expiry. The production and capture tests also checked the new progress bars. |
| Graphical Linux smoke test | Checked credits and the tank queue, selected a mixed group, saw a blocked-order message and watched the selected tank lose health in combat. |

The Linux graphics driver reported that changing VSync is unsupported.
Windows runtime verification remains under RTS-015.

## RTS-013 match lifecycle

Destroying the opponent's HQ ends the match. The simulation resolves
simultaneous damage before choosing the result, so both HQs falling
on the same tick produces a draw. The final snapshot is frozen and
later commands are ignored.

The Pause button and Escape open a menu for resume, restart and quit.
The result screen shows victory, defeat or a draw. Restart loads a
fresh battlefield and resets the session, units, credits, sectors,
selection, queued commands and AI.

### Verification on 21 November 2026

| Check | Observed result |
| --- | --- |
| `dotnet restore IronDominion.sln` and `dotnet build IronDominion.sln --configuration Release --no-restore` | Restore and Release build passed with 0 warnings and 0 errors. |
| `dotnet test IronDominion.sln --configuration Release --no-build` | All 114 xUnit cases passed. Six new cases cover wins for both sides, a simultaneous draw, the final snapshot, barracks destruction and pause with a pending command. |
| `scripts/validate-godot.sh` | Godot 4.6.3 .NET imported and launched the scene headlessly. All ten smoke checks passed. Player input paused and resumed the match, moved a tank into range of the orange HQ, won against the passive opponent and restarted. The active AI scenario ended on the defeat screen. |
| Graphical Linux smoke test | Paused and resumed from the menu, watched the active AI destroy the blue HQ, then used the result screen to start a new match. |

The Linux graphics driver reported that changing VSync is unsupported.
Windows runtime verification remains under RTS-015.

## RTS-014 golden match and deterministic replay

The xUnit integration project runs a complete match through
`GameSession` and the orange AI with deterministic navigation and line
of sight. It checks selection and order validation, an obstacle detour,
income from captured sectors, a blocked production exit and its
recovery, combat and a frozen victory result.

The recorded configuration schema, seed and processed orders round-trip
through JSON. Two fresh simulations replay the orders in recorded and
reverse arrival order. Both match the original per-tick snapshots and
event order. The golden result is a blue victory at tick 855 after 34
processed orders, with timeline SHA-256
`DD3E9A65A582A55322AEFA4A0B3C58E9EEF2B7F0A1FC2C5B03B5EF899B37819F`.

### Verification on 21 November 2026

| Check | Observed result |
| --- | --- |
| `dotnet restore IronDominion.sln` and `dotnet build IronDominion.sln --configuration Release --no-restore` | Restore and Release build passed with 0 warnings and 0 errors. |
| `dotnet test IronDominion.sln --configuration Release --no-build` | All 116 xUnit cases passed, including the golden scenario and both replay orders. |
| `scripts/validate-godot.sh` | Godot 4.6.3 .NET imported and launched the game headlessly. All ten smoke checks passed, including AI and match lifecycle. |

The replay guarantee applies to the test adapters. Godot navigation is
covered by the separate Linux headless checks. Windows export and
runtime profiling are tracked in RTS-015.

## RTS-001 bootstrap

RTS-001 adds the Godot 4.6.3 .NET project, the .NET SDK 10.0.404 solution and
the first automated build and validation jobs. The main scene contains a
128 m × 128 m terrain with collision, an orthographic camera, lighting and
two HQs made from Godot primitives. The C# scene advances the pure C#
simulation at 20 Hz. Each simulation step produces a new read-only tick
snapshot.

## Verification

These commands ran in Codex Cloud on Debian 13, Linux x86-64, on
19 November 2026.

| Check | Observed result |
| --- | --- |
| `dotnet restore IronDominion.sln` | Restored all seven projects. |
| `dotnet build IronDominion.sln --configuration Release --no-restore` | Passed with 0 warnings and 0 errors. |
| `dotnet test IronDominion.sln --configuration Release --no-build` | 3 xUnit cases passed, 0 failed, 0 skipped. Covered tick advancement, snapshot stability and the absence of Godot assembly references in Domain and Simulation. |
| `scripts/validate-godot.sh` | Debug game build, Godot headless editor import and a 10-iteration headless main scene launch all passed. The final import and launch logs contained no missing resource errors. |
| Godot headless Windows Desktop release export | Passed from Linux with the pinned .NET export templates. Produced an x86-64 PE executable, a PCK and the managed assemblies. |
| Staged file review | Scene uses only project source and Godot primitives. All tracked files in the candidate were text. No credentials or third-party art were added. |

The `dotnet` and `validate` jobs run the bootstrap acceptance checks on GitHub
Actions. The Windows export and its template installation are documented in
[Development setup](docs/08-SETUP.md).

## Environment details

The cloud image supplied the standard, non-.NET Godot editor and had no
.NET SDK. The pinned .NET editor, SDK and export templates were downloaded
and checksum-verified in the workspace. The first .NET CLI operation needed
sandbox escalation for the NuGet migration mutex. Subsequent builds and
tests ran in the regular sandbox. VSTest reported using a user-private
temporary directory for its artifacts and returned success.

Rendering and Windows execution were outside the headless validation.
The Linux cloud run verified the Windows package contents. Windows
playtesting remains under RTS-015.

## Next

RTS-015 adds the Windows build and performance profiling.
