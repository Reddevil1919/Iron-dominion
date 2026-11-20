# C# Contracts — proposed compilation baseline

Namespaces and signatures are intended as starting contracts; add implementation and validation in the assigned modules. Keep public APIs stable once adopted.

```csharp
namespace IronDominion.Domain;
public readonly record struct EntityId(int Value);
public readonly record struct PlayerId(int Value);
public readonly record struct SimVector(float X, float Z);
public enum UnitKind { InfantrySquad, Tank }
public enum BuildingKind { Headquarters, Barracks }
public enum MatchResult { Running, PlayerOneWon, PlayerTwoWon, Draw }
public sealed record UnitDefinition(UnitKind Kind, int MaxHp, int Cost, int BuildTicks,
    float SpeedMetersPerSecond, float AttackRangeMeters, int DamagePerShot, int CooldownTicks);
public abstract record GameCommand(PlayerId Player, long ExecuteAtTick, long Sequence);
public sealed record MoveCommand(PlayerId Player, long ExecuteAtTick, long Sequence,
    IReadOnlyList<EntityId> Units, SimVector Destination) : GameCommand(Player, ExecuteAtTick, Sequence);
public sealed record AttackCommand(PlayerId Player, long ExecuteAtTick, long Sequence,
    IReadOnlyList<EntityId> Units, EntityId Target) : GameCommand(Player, ExecuteAtTick, Sequence);
public sealed record TrainUnitCommand(PlayerId Player, long ExecuteAtTick, long Sequence,
    EntityId Producer, UnitKind Kind) : GameCommand(Player, ExecuteAtTick, Sequence);
public sealed record CommandResult(bool Accepted, string? RejectionCode);
public interface ICommandValidator { CommandResult Validate(GameCommand command, IReadOnlyWorld world); }
public interface IReadOnlyWorld {
    long Tick { get; }
    MatchResult Result { get; }
    bool IsVisible(PlayerId player, EntityId entity);
    int Credits(PlayerId player);
}
public interface INavigationService {
    IReadOnlyList<SimVector> FindPath(SimVector start, SimVector end, int movementMask);
}
public interface ILineOfSightService { bool HasLineOfSight(SimVector origin, SimVector target); }
public interface IGameEvent { long Tick { get; } }
public sealed record EntityDestroyed(long Tick, EntityId Entity) : IGameEvent;
public sealed record MatchEnded(long Tick, MatchResult Result) : IGameEvent;
public interface IGameSimulation {
    IReadOnlyWorld World { get; }
    void Schedule(GameCommand command);
    IReadOnlyList<IGameEvent> Tick();
}
```

## Contract decisions
- No `Godot.Vector3`, `Node3D`, `NavigationAgent3D` or `SceneTree` in domain contracts.
- `ExecuteAtTick` must be >= next admissible tick; reject stale commands.
- A single monotonically increasing `Sequence` per player is assigned at the application boundary.
- `Tick()` processes one fixed 50ms step; no wall-clock argument.
- `IReadOnlyWorld` must expose read-only projections, not mutable entity references. Extend with `TryGetUnit`, `GetVisibleEnemies`, `GetSectors` as immutable DTOs.
- `CommandResult` returned by validation; execution may still fail gracefully if world changed before scheduled tick.
- Navigation and LOS are injected; they must not mutate world state.

## Godot bridge pseudocode
```csharp
// In a Godot Node: call from _PhysicsProcess or an accumulator.
// Use an accumulator if engine physics rate differs from 20 Hz.
// Accumulator should cap catch-up ticks to prevent spiral-of-death.
while (accumulator >= 0.05 && ticksThisFrame < MaxCatchUpTicks) {
    var events = simulation.Tick();
    viewSynchronizer.Apply(simulation.World, events);
    accumulator -= 0.05;
    ticksThisFrame++;
}
// Visual interpolation between previous and current snapshots.
```

## Configuration schema 1

The [default configuration](../src/IronDominion.Infrastructure/Configuration/DefaultGameConfiguration.json)
ships as an embedded resource in the Infrastructure assembly. The Godot scene
loads and validates it at startup. `GameConfigurationLoader.Load(Stream)` can
also validate another JSON document without Godot.

The schema requires a 20 Hz tick rate, one definition per unit and building
kind, positive maximum HP and nonnegative costs, rates, ranges and durations.
Distances and speeds must be finite. The infantry cover multiplier is between
0 and 1 inclusive. Field names use camel case. Missing, unknown and duplicate
properties are rejected.

`EntityId` and `PlayerId` constructors require positive values. Entity
constructors also reject default IDs and owners, since C# value types can be
zero-initialized. A neutral sector has a null owner.
