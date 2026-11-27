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

Visibility uses line of sight from living allies. See [Focused attack
approach](#focused-attack-approach) for current movement on attack orders.
Cover and match outcomes are tracked in RTS-010 and RTS-013.

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

Firing displays temporary combat effects. Failed local orders display a brief
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

## RTS-015 Windows x64 package and performance

The Windows release ZIP includes the executable, PCK, self-contained .NET
runtime, launch instructions, license notices and the source commit. CI
checks the ZIP checksum and runs the packaged executable twice on a native
Windows Server 2025 x64 runner. First it loads the default battlefield.
Then it runs a 50-unit benchmark for eight seconds after warm-up.

The Release benchmark measures frame and fixed-tick times, navigation and
memory at 50, 100 and 200 active units. The complete results, workload,
cloud hardware and launch commands are in the [performance report](docs/09-PERFORMANCE.md).

### Verification

| Check | Observed result |
| --- | --- |
| `dotnet restore IronDominion.sln` and `dotnet build IronDominion.sln --configuration Release --no-restore` | Restore and Release build passed with 0 warnings and 0 errors. |
| `dotnet test IronDominion.sln --configuration Release --no-build` | All 116 xUnit cases passed. |
| `scripts/validate-godot.sh` | Godot 4.6.3 .NET imported and launched the Linux project headlessly. All ten smoke scenarios passed. |
| Windows x64 build and native headless check | Exported, checksum-verified and launched the ZIP. The runner loaded the exported battlefield with the bundled runtime. The 50-unit check processed 160 simulation ticks and 100 path requests with zero rejected moves. |
| T-015 on the Linux cloud desktop | The 50-unit graphical Release profile completed 300.06 seconds at 12.3 FPS with 6,002 simulation ticks and 3,800 path requests. The p95 simulation tick took 0.59 ms. |

The first Windows CI attempt loaded the scene but the validator expected
the editor scene path. The corrected check verifies Godot's exported scene
path. The runner verified Windows execution headlessly. Graphical metrics
come from Mesa llvmpipe software rendering on Linux. Desktop FPS on Windows
and the 60 FPS target on Windows hardware remain unmeasured.

## RTS-016 fog of war

Battlefield vision comes from living units within 42 m and respects the
roadblock and buildings. Both sides use the same query for hostile contacts
and attack order validation. The local view hides enemy actors, health bars
and firing effects when a contact is lost. Hidden buildings remain solid obstacles.
The map distinguishes visible, explored and unexplored terrain. Sector
ownership is public and capture progress requires visibility of the sector.

| Check | Observed result |
| --- | --- |
| `dotnet build IronDominion.sln --configuration Release --no-restore` | Build passed with 0 warnings and 0 errors. |
| `dotnet test IronDominion.sln --configuration Release --no-build` | All 116 xUnit cases passed, including AI visibility and deterministic replay. |
| `scripts/validate-godot.sh` | Godot 4.6.3 .NET imported and launched the scene. All eleven headless scenarios passed. The new smoke checked obstacle occlusion, scouting, attack picking and lost contacts. |

The Linux desktop displayed the fog with Mesa softpipe. Its default
llvmpipe driver hit an illegal instruction in both the MVP and RTS-016.
Windows graphical performance remains unmeasured.

The 42 m sight radius is an initial gameplay value. The terrain uses a
48 × 48 grid that refreshes up to five times per second. Unit and building
visibility follows the authoritative contact query every tick.

## RTS-017 tactical orders

Attack-move reuses ordinary movement routes and group slots. Each unit pauses
when it can fire on an enemy in weapon range. The unit stays through its weapon
cooldown and resumes its route when contact ends. Hold position cancels
movement and focused attacks. Automatic fire continues from the held spot.

The selection HUD offers both orders. Shift+A arms a ground click for
attack-move, leaving the ordinary WASD camera keys available. H issues hold
position to the current selection.

| Check | Observed result |
| --- | --- |
| `dotnet build IronDominion.sln --configuration Release --no-restore` | Build passed with 0 warnings and 0 errors. |
| `dotnet test IronDominion.sln --configuration Release --no-build` | All 123 xUnit cases passed. The tests covered both orders and deterministic replay, including preservation of the golden match hash. |
| `scripts/validate-godot.sh` | Godot 4.6.3 .NET imported and started the game. The tactical scenario checked the shortcuts and HUD buttons, engagement on an attack-move route and cancelling movement with hold position. |

The cloud desktop uses Mesa softpipe for graphical checks. Windows graphical
performance remains unmeasured.

## RTS-018 versioned save and load

The Pause menu saves to a single local JSON slot. Loading restores the saved
tick and leaves the match paused. The checkpoint includes units, building
health, cached paths, targets, cooldowns, pending orders, sectors, credits,
production, AI objectives and explored ground. Loading resets selection
and control groups.

The game checks the save version, battlefield and configuration before
replacing the current session. Saving writes the complete file before
replacing the slot. The UI reports incompatible and corrupt saves.

| Check | Observed result |
| --- | --- |
| `dotnet build IronDominion.sln --configuration Release --no-restore` | Release build passed with 0 warnings and 0 errors. |
| `dotnet test IronDominion.sln --configuration Release --no-build` | All 131 xUnit cases passed. Restoring the golden match at ticks 213, 331 and 795 preserved every subsequent tick and the original victory at tick 855. |
| `scripts/validate-godot.sh` | All 13 Godot headless scenarios passed. The save scenario restored movement, balance and production and preserved the active match when a load was rejected. |

The cloud desktop uses Mesa softpipe for graphical checks. Windows graphical
performance remains unmeasured.

## RTS-019 directional cover and suppression

The dark stripe on a cover zone marks the protected side. Infantry inside
takes 30% less damage from shots within 45° of that direction. Flank shots
deal full damage. A damaging hit slows surviving infantry to 65% of normal
movement speed for 24 ticks and refreshes the timer on further hits.
The unit marker and selection HUD show suppression. These values are the
initial balance for the feature.

New saves use format 2 and store active suppression timers. Format 1 saves
with the prior balance remain loadable. They resume under the current
battlefield rules.

| Check | Observed result |
| --- | --- |
| `dotnet build IronDominion.sln --configuration Release --no-restore` | Release build passed with 0 warnings and 0 errors. |
| `dotnet test IronDominion.sln --configuration Release --no-build` | All 145 xUnit cases passed. New cases cover protected and exposed angles, suppression timing and refresh, and restoration of both save formats. |
| Godot infantry cover scenario | The scenario found the marked edge, moved a squad into cover, checked suppression in the unit marker and selection HUD, and moved the squad out. |

The desktop verification uses Mesa softpipe. Windows graphical performance
remains unmeasured.

## Contemporary battlefield art

The first art pass depicts a contemporary logistics depot. The battlefield
adds asphalt, concrete yards, freight containers and perimeter vegetation.
Both fictional forces use muted equipment colors with the existing blue
and orange identification. Infantry gait follows rendered travel and lowers
under suppression. Tanks have separate hull and turret motion with recoil
from visible shot events. The battlefield keeps the same navigation,
picking, line-of-sight shapes and save identity.

The art uses original procedural meshes and shaders. See
[Contemporary art direction](docs/10-ART-DIRECTION.md) for the palette,
silhouettes, motion and remaining close-up production work.

| Check | Observed result |
| --- | --- |
| Release build and xUnit | Build passed with zero warnings and errors. All 145 tests passed. |
| Godot headless | All 13 scenarios passed for the art pass. The combat scenario now checks independent turret rotation, gun recoil and recovery through normal combat. |
| Linux desktop | Inspected the overview and tactical close-up in Godot on Mesa softpipe and checked that the infantry foot transforms change during a real movement order. Native hardware frame rates still need measurement. |

## Reference infantry

Both fictional forces now render four weighted soldiers using one skinned
mesh and one skeleton for each squad. The uniform has distinct fabric,
equipment and metal shader responses. The original colors and outline
remain readable at the tactical camera scale.

The presentation samples separate run, braking, aiming and suppression
poses. Leg placement uses two-segment inverse kinematics and a grounded
stance phase. Hands track the equipment while it rises and recovers.
See [Contemporary art direction](docs/10-ART-DIRECTION.md).

| Check | Observed result |
| --- | --- |
| Release build and xUnit | Zero build warnings and errors. All 145 tests passed. |
| Godot headless | All 13 scenarios passed. The updated cover scenario checks skin binds, alternating stance and swing, gear raised during combat and a lowered torso under suppression. |
| Linux desktop | Checked the weighted model in the playable depot at tactical zoom and in close-up, with Mesa softpipe. Native hardware presentation performance still needs measurement. |

## Tracked armor and combat effects

The two track belts advance by distance and turn. The suspended armor
rocks on starts and stops, and retains independent turret traverse and
barrel recoil. Armor panels use a weathered paint finish. Visible shots
add short muzzle and impact bursts. A moving tank leaves brief ground
dust. The effects are limited in count and exist in the presentation.

| Check | Observed result |
| --- | --- |
| Release build and xUnit | Zero build warnings and errors. All 145 tests passed. |
| Godot headless | All 13 scenarios passed. Movement checks track phases, suspension and dust expiry. Combat checks recoil and the burst lifetime. |
| Linux desktop | Rendered the moving tank with dust and the tank firing at an opposing squad in the playable depot. The cloud rendered with Mesa softpipe. Graphical frame rates on Windows hardware remain unmeasured. |

## Focused attack approach

With a visible enemy squad about 29 m away, a right-click previously
accepted the attack and left the tank stationary outside its 16 m range.
An attack click now sends units toward viable firing positions and keeps
the clicked target as their focus. Units stop once they can fire, and a
moving target can trigger a new route. Losing team line of sight or
destroying the target ends the approach. If the entire selection is
unable to reach a firing position, the HUD shows the rejection.

The Release build completed with zero warnings and errors, and all 148
xUnit tests passed. The Godot headless fog scenario uses one right-click
from outside weapon range and checks the approach, cannon recoil and
stationary firing position. It also passed with the localized firing effects.

## Depot landmarks and wayfinding

The freight loading frame, relay mast and perimeter gates give the depot
specific landmarks. Asphalt approaches have arrows and stop markings.
Relay, Service and Checkpoint use painted ground names and segmented
capture outlines. The floating letter shrinks at close zoom. Cover uses
gravel, with ochre markings on the protective edge and a dashed opposite
boundary. The map layout for navigation, line of sight and saves is the
same.

| Check | Observed result |
| --- | --- |
| Release build and xUnit | Zero build warnings and errors. All 145 tests passed. |
| Godot headless | All 13 scenarios passed. The sector scenario checks outline dimensions, ground labels and close-zoom scaling. The cover scenario checks paint inside both parcels and below the fog plane. |
| Linux desktop | Rendered the playable depot at overview and tactical zoom on Mesa softpipe. Graphical frame rates on Windows hardware remain unmeasured. |

## Local firing and impact effects

Infantry and tanks fire with a directional muzzle flash, drifting smoke
and a local impact. A brief tracer moves toward the target on tank shots
and some infantry shots. Tank impacts add sparks. These effects use the
existing combat events and do not change damage timing or line of sight.
A lethal shot can finish its effect at the last visible position of its
target. The fog mask shader bindings release their temporary texture
reference during setup so Godot can free the mask when closing the scene.

| Check | Observed result |
| --- | --- |
| Release build and xUnit | Zero build warnings and errors. All 145 tests passed. |
| Godot headless | All 13 scenarios passed. Combat checks a moving tracer and localized bursts. The HUD check verifies tracer expiry and the absence of the old beam node. |
| Linux desktop | Captured a tank shot from muzzle flash to impact in the playable depot. The cloud rendered with Mesa softpipe. Graphical frame rates on Windows hardware remain unmeasured. |

## Targeting cursor, tank clearance and weapon effects

The world pointer changes to a crosshair using the attack order's picking
rules. Ground, friendly units and fogged contacts use the ordinary
pointer. HUD controls retain their own cursors. Tanks keep 6.6 m between hull centers and
find a detour through the existing navigation mesh when there is room.
Blocked destinations wait for clearance. This movement uses entity order
and the existing saved waypoints.

Infantry shots use staggered recoil and flashes at two rifle muzzles, with
small impact puffs. Cannon fire has a larger flash, muzzle smoke and
ground dust, a compact shell and a heavier impact. The effects follow the existing
combat events and visibility rules.

The Release build completed with zero warnings and errors. All 151 xUnit
tests passed, including stationary, head-on and blocked-destination tank
cases. All 13 Godot headless scenarios passed. The focused checks verify
the cursor, rifle and cannon effects, and a detour around the opposing tank
in the depot. Graphical frame rates on Windows hardware remain unmeasured.

## Unit destruction effects

A visible tank loss produces a blast with dust, sparks and rising smoke.
The turret and smaller fragments tumble, land, and remain for three
seconds after landing. The squad's four existing soldier rigs buckle and
fall with different poses and a three-second hold on the ground. Their
uniforms show localized blood marks. Ground stains stay red for three
seconds from contact, turn brown by ten seconds, and finish fading by
thirteen seconds. Temporary effects use match time and clear on load.

The combat smoke checks these timelines during normal attacks, including
the moving debris and the infantry rig's final pose. The Linux desktop
capture shows real gameplay effects with Mesa software rendering.

## Surface-dependent impacts

The depot uses its authored pavement bounds to choose impact effects on
dirt, gravel, concrete and asphalt. Hits on tank armor produce sparks and
leave a temporary soot mark that moves with the hull. Rifle impacts use
smaller particles and marks than cannon impacts.

The combat smoke checks the dirt and armor effects during a real engagement.
The match lifecycle smoke checks concrete impacts on the orange headquarters.

## Progressive tank damage

At 65% of starting health or below, the hull and turret show paint loss
and exposed metal. At 30% or below, scorch marks darken the armor and
smoke rises from the rear grille. The smoke batch follows match time
and actor visibility. A destroyed tank switches to the existing blast
and debris. Loading restores the armor from health in the checkpoint
and clears the previous smoke. The simulation and save schema are unchanged.

The Release solution build passed with zero warnings and errors. All 151
xUnit tests passed, and `scripts/validate-godot.sh` passed all 13 headless
scenarios. The combat smoke observes both damage stages before destruction.
The save smoke checks the critical threshold at load and verifies that an
enemy outside sight contributes no smoke. The Linux desktop capture used
Mesa llvmpipe and compares all three stages in the depot, including a
critical tank at tactical zoom. Windows graphical performance remains
unmeasured.

## Infantry cover animations

The squad shortens its steps and lowers its silhouette inside marked
cover. At a halt, the front pair kneel on opposite knees and the rear
pair hold a higher crouch and watch the flanks. They arrive in their poses
at staggered times. Fire and suppression still drive the rifle and upper
body, while foot placement keeps the low stance grounded. A new order
brings soldiers up into motion and they return to their ordinary stance
after leaving cover. Godot reads the existing simulation state.

The Release build passed with zero warnings and errors. All 151 xUnit
tests and all 13 Godot headless scenarios passed. The cover smoke checks
quiet entry, staggered settling, planted boots, the rise into movement,
and aiming low under fire. The close-up capture runs the original
battlefield on Linux with Mesa llvmpipe. Windows graphical performance
remains unmeasured.

## Tread marks and footstep dust

Tanks stamp two parallel tread marks and lift dust from the rear of the
belts. Turf shows compressed tracks and warm dust. Gravel shows a weaker
imprint and paler dust. Paved areas stay clean. Infantry lifts small
puffs from the leading soldier's footfalls on loose ground. The terrain
classifier uses the same authored parcels and paving as the impact
effects. Marks stay on the ground and fade away within twelve match
seconds. A bounded pool keeps the rendering cost predictable. Enemy
marks respect ground visibility, and loading clears old trails.

The Release build passed with zero warnings and errors. The 151 xUnit
tests and all 13 Godot headless scenarios passed. Movement crosses both
pavement types and checks tread expiry. The cover and save scenarios check
footstep dust and trail reset. The Linux desktop capture uses Mesa
llvmpipe. Graphical performance on Windows remains unmeasured.

## Contemporary depot details

The freight yard contains a forklift, strapped pallets, a marked aisle
and container doors with lock bars. Weathering adds streaks and settled
dust to the painted scenery. The barracks have shuttered maintenance bays,
protective bollards and rooftop ventilation. The western relay roof adds
photovoltaic panels beside the mast. Glazed guard booths frame the two
perimeter gates. Cargo equipment fits inside the existing roadblock and
the gate details stand outside the navigable edge. The scenery shader
uses the visible ground around the freight yard to light its interior.

The Release build passed with zero warnings and errors. All 151 xUnit
tests and all 13 Godot headless scenarios passed. The battlefield HUD
scenario checks the equipment footprint, the scenery material and the
perimeter boundary. The depot was captured from the playable scene on
Linux with Mesa llvmpipe. Graphical performance on Windows remains
unmeasured.

## Reference tank and PBR atlas

The original Blender model adds sloped armor, bevels, separate optics,
wheel hubs, cooling ribs and stowage. It mounts to the existing turret,
barrel and suspension pivots. Olive and ochre faction finishes share the
meshes and a 2K albedo, normal, occlusion, roughness and metallic atlas.
The damage stages keep the faction markings readable. The combat smoke
checks the authored atlas tiles, shared surfaces and muzzle alignment
alongside the normal combat and casualty timeline.

The Release build passed with zero warnings and errors. All 151 xUnit
tests and all 13 Godot headless scenarios passed. Godot's desktop renderer
captured the old and new model from matching tactical and close cameras,
as well as movement, recoil and the two damage stages in combat. The
capture used Mesa llvmpipe on Linux. Windows graphical throughput
remains unmeasured.

## Depot lighting and atmosphere

The playable depot uses a warm, low lateral sun, cool ambient light and
fixed exposure. Soft ground shadows keep tank tracks, boots and pallets
anchored to the terrain. Unit shadows follow the animated model and
match its tactical visibility. Compatibility remains the default renderer.
The optional Forward+ profile adds ambient occlusion around surfaces and
low mist at the north apron and west treeline. The fog-of-war overlay and
its visibility rules remain independent of the atmosphere.

The Release build passed with zero warnings and errors. All 151 xUnit
tests and all 13 Godot headless scenarios passed. The HUD smoke checks
fixed exposure, shadows under visible units and pallets, and renderer-specific
effects. Desktop captures compare the previous version, Compatibility and
Forward+ with matching cameras in the playable depot. Both new modes drew
50 active units at an internal 1920 × 1080 on Linux software rendering.
The [performance report](docs/09-PERFORMANCE.md) records the test conditions
and the Windows test commands. The 60 FPS target remains unmeasured on
Windows with a dedicated GPU.

## Infantry turn animation

Movement orders turn the squad progressively. Each soldier's step pace and
direction account for their place in the formation. The boots take shorter
lateral steps to follow the turn. The hips lean and the heads look into
the new direction. After a hold order, soldiers can finish the turn with
short steps at the halt. The visual rig reads the existing movement data.

The Release build passed with zero warnings and errors. All 151 xUnit
tests and all 14 Godot headless scenarios passed. The new scenario issues
a right-angle move and a reversal, then checks lateral support, staggered
boot lifts and settling after a hold order. The before-and-after comparison
was captured in the playable depot using Linux Mesa llvmpipe. Graphical
frame rates on Windows hardware remain unmeasured.

## Infantry weight transfer and tank suspension

The lead soldiers leave a settled kneel by loading the forward boot at
knee height, pushing off it and entering the gait. An anchor in world
space keeps that boot on the ground during the weight transfer. The pair
lead with opposite feet and start a fraction of a second apart.

The tank hull loads the outside track in a turn. Speed and heading rate
drive the sideways tilt, and damping brings the hull back to level as the
turn ends. The track belts stay on the ground as the hull pitches under
braking and rolls over them. Both effects use the existing visual rig.

The Release build passed with zero warnings and errors. All 151 xUnit
tests and all 14 Godot headless scenarios passed. The cover test measures
the planted boot in world space. The movement test checks the outside
load in both turn directions, the dip at braking and a level stop.
The before-and-after footage comes from the playable depot under Linux
Mesa llvmpipe. Graphical frame rates on Windows hardware remain unmeasured.

## Engineers and compact health bars

The barracks trains a four-person engineer squad in its shared production
queue for 150 credits and ten seconds. With the squad selected, the pointer
becomes a wrench over a living friendly tank. Right-click orders a repair.
The squad approaches, follows the tank when needed, and restores up to
30 HP per second at working range. Move and hold orders interrupt repair.
The torch tracks the hull with a blue-white arc and falling sparks.

Overhead health bars are shorter and thinner. The selection health strip
is smaller, and the interface no longer prints the suppression status.
Cover and suppression continue to affect eligible squads. Save format 3
includes engineer queues and repair orders. Matching format 1 and 2 saves
remain compatible.

The Release build passed with zero warnings and errors. All 156 xUnit
tests and 15 Godot headless scenarios passed. The depot capture confirmed
the wrench cursor and the weld with OpenGL Compatibility on Linux Mesa llvmpipe.
Graphical frame rates on Windows hardware remain unmeasured.

## Next

Continue refining the existing units' close-up motion and battlefield
effects. RTS-020 remains in the backlog.
