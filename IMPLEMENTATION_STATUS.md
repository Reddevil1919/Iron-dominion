# Implementation status

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

RTS-002 expands the domain entities and configuration. RTS-003 extends
the simulation contract with command scheduling. RTS-004 adds the
interactive RTS camera. A full match remains a later milestone.
