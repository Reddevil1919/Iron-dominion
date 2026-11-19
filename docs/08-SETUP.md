# Local Setup and First Milestone

## Prerequisites
Install a current stable Godot 4 **.NET** editor, matching .NET SDK supported by that Godot version, VS Code C# extension, Git, Blender. Pin versions in a `global.json` and CI after validating official compatibility. Godot 4 C# web export is not a target; Windows desktop is.

## Bootstrap checklist
1. Create solution and C# class libraries: Domain, Simulation, Application, AI, Infrastructure and tests.
2. Set references so Domain depends on nothing; Simulation on Domain; Application on Domain/Simulation; AI on Domain/Application contracts; Godot game on Application and adapters.
3. Create Godot project with 3D scene, `Camera3D`, `DirectionalLight3D`, `WorldEnvironment`, ground `StaticBody3D`, two simple HQ meshes and selection raycast.
4. Add xUnit tests for fixed tick and ownership before gameplay.
5. Add command bridge and view synchronization; verify selecting and moving a placeholder unit.
6. Commit initial architecture and CI workflow.

## Suggested initial commands (after installing SDK)
```bash
dotnet new sln -n IronDominion
dotnet new classlib -n IronDominion.Domain -o src/IronDominion.Domain
dotnet new classlib -n IronDominion.Simulation -o src/IronDominion.Simulation
dotnet new classlib -n IronDominion.Application -o src/IronDominion.Application
dotnet new classlib -n IronDominion.AI -o src/IronDominion.AI
dotnet new classlib -n IronDominion.Infrastructure -o src/IronDominion.Infrastructure
dotnet new xunit -n IronDominion.Simulation.Tests -o tests/IronDominion.Simulation.Tests
```
Then add projects/references with `dotnet sln add` and `dotnet add reference` syntax appropriate to installed SDK; create Godot project through editor.

## First playable milestone
One selectable infantry squad, right-click movement around one obstacle, an enemy dummy with HP, one attack command, win condition on dummy destruction. Only after this works, add sector economy and production.

## Known engineering risks
Navigation and pure simulation state synchronization; selection/terrain raycasting; large numbers of agents; cross-platform determinism; version mismatch of Godot/.NET. Mitigate with small spikes and profiling.