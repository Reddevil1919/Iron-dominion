# Technical Architecture — ADRs and flows

## Stack
Godot 4 .NET desktop, C# and compatible .NET SDK, xUnit, GitHub Actions, Blender/glTF. Pin exact Godot/.NET versions at implementation time after verifying compatibility in official docs.

## Projects and dependencies
```text
IronDominion.Game (Godot) ───> Application ───> Simulation ───> Domain
          │                        │                 │
          └── GodotAdapters ───────┘                 └── AI (optional adapter/service)
Infrastructure ───────────────> Domain/Application interfaces
Tests ───────────────> Domain/Simulation/Application
```
No Godot namespace in Domain, Simulation or AI. Dependency inversion for navigation, line-of-sight and clocks.

## Proposed repository
```text
/game/{project.godot,Scenes/,Scripts/,Assets/,UI/}
/src/IronDominion.Domain/{Entities/,Commands/,Events/,ValueObjects/}
/src/IronDominion.Simulation/{Systems/,World/,Rules/}
/src/IronDominion.Application/{GameSession.cs,CommandScheduler.cs,Queries/}
/src/IronDominion.AI/{Strategy/,Tactics/}
/src/IronDominion.GodotAdapters/{Navigation/,Visibility/,Views/}
/src/IronDominion.Infrastructure/{Serialization/,Configuration/}
/tests/{Domain.Tests/,Simulation.Tests/,Integration.Tests/}
/docs/ and /.github/instructions/
```
The Godot project references the C# projects using relative paths. If Godot's assembly loading requires adjustment, keep adapters inside the Game project rather than introducing a circular dependency.

## Tick pipeline (20 Hz)
1. Read scheduled player/AI commands, sort deterministically, validate ownership and game state.
2. Advance path-following movement (path requests cached; avoid pathfinding every tick).
3. Update vision/line-of-sight for targeting.
4. Resolve targeting, cooldown and damage; aggregate simultaneous damage before destruction resolution.
5. Advance sector capture and economy; advance production and spawn.
6. Resolve victory; append ordered events; publish immutable snapshot.
7. AI plans against snapshot every 40 ticks, scheduling commands for a subsequent tick.

## Adapter boundaries
`INavigationService` returns waypoint paths using immutable input; Godot NavMesh calculations run on appropriate Godot thread. Domain stores paths/waypoints as plain vectors, never Node references. `ILineOfSightService` uses map obstacle data or Godot physics queries via adapter. To preserve replay repeatability, record authoritative path responses or use a deterministic test adapter; Godot navigation is not guaranteed cross-platform deterministic.

## State and data
WorldState stores players, units, buildings, sectors, map data, tick counter, seeded PRNG state. All money is integer. Prefer explicit integer tick cooldowns. Float positions are adequate for single-player MVP but prevent claims of bitwise determinism across hardware. Static definitions in JSON, parsed and validated into immutable C# objects. Save files use schema version, config version, seed, tick, entities and pending commands; persistence can be post-MVP if schedule is tight.

## Performance
Pool unit visuals/projectiles, cache scene resources, avoid per-unit Node trees for individual infantry soldiers, group path requests, throttle AI, update HUD on changes. Profile with 50/100/200 units; record frame time, simulation tick duration, memory and path request count. 60 FPS at 50 units is a target on target hardware, not an SLA.

## Security and extensibility
Never execute arbitrary data files as code; validate config bounds; sanitize save paths. For future multiplayer, authoritative server and command protocol can be introduced; do not assume current single-player architecture is network-safe.

## ADR-001 Pure C# simulation
Decision: decouple gameplay from Godot. Consequence: extra adapter code; enables headless tests and balancing.

## ADR-002 Fixed tick
Decision: 20Hz authoritative simulation, rendering interpolation separately. Consequence: command latency up to one tick plus path computation.

## ADR-003 Squad entity
Decision: infantry squad is one simulation entity; members are visual instances. Consequence: simpler selection/combat; less granular casualty simulation.

## ADR-004 Simplified cover
Decision: static cover zones with infantry damage reduction. Consequence: tactical positioning with limited complexity; directional cover deferred.

## ADR-005 No server in MVP
Decision: offline single-player, no Spring Boot or database service. Consequence: simpler deployment; multiplayer needs separate design.