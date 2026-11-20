# Implementation status

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

RTS-003 extends the simulation contract with command scheduling. RTS-005
adds click and drag selection. A full match remains a later milestone.
