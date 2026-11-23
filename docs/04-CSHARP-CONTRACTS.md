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
public interface ILineOfSightService { bool HasLineOfSight(SimVector origin, SimVector target, EntityId targetId); }
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

## RTS-003 scheduling contract

Call `GameSession.Commands` on the simulation thread to schedule move, attack
and training orders. The scheduler assigns each player a sequence starting at
1 and defaults to the next tick. Commands carry an immutable copy of the unit
selection. `GameSimulation.Schedule` also accepts pre-sequenced commands for
deterministic replay in any arrival order.

Scheduling rejects past or current ticks, nonpositive player IDs and sequence
numbers, and any sequence already used by that player in the same match.
`AdvanceOneTick()` returns the tick's `CommandProcessed` events in order of
scheduled tick, player ID and sequence. Events report whether each order
passed the initial checks, including ownership and whether its actors are
alive. In a session with navigation, move orders also validate that every
requested destination is reachable. The simulation caches successful routes
and publishes positions through `IReadOnlyWorld.TryGetUnitPosition` after
each 50 ms step. The combat session resolves fire after movement. It checks
range, line of sight and the configured cooldown before firing. An order can
focus a hostile target that at least one living allied unit can see. In the
Godot battlefield, a living allied unit sees a target within 42 m when no
solid obstacle blocks its view.

Tank movement checks each swept step against a hull clearance of 6.6 m
between tank centers. Updates use entity ID order. A detour uses the static
navigation adapter to bypass a blocking tank and rejoin the original
destination. An occupied destination can wait and resume once clear.
Detours use the existing route checkpoints.

An accepted focused attack replaces each selected unit's route with an
approach to a reachable firing position when needed. The simulation stops
the approach on engagement, loss of team visibility or destruction. It
revalidates routes against moving targets once per second. Units only fire
at the assigned target while the order is active. If no selected unit can
fire or reach a firing position, the order is rejected with
`no_firing_position` and the previous orders stay active.

`ShotFired` events are ordered by attacker ID. Damage from that tick is
applied together in target ID order and produces `EntityDamaged` and
`EntityDestroyed` events. `IReadOnlyWorld.TryGetHitPoints` reports the
remaining health, including zero after destruction.

## RTS-008 production

Training reserves the full unit cost when the scheduled order succeeds.
Each living barracks trains infantry and each living HQ trains tanks, one
order at a time. Only the first order in each queue advances on a tick,
starting on the tick it was accepted. After movement and combat, ready orders
are checked in producer ID order. A blocked exit holds the order at zero
remaining ticks until the exit is clear and walkable. Destroying a producer
discards its queue and leaves the reserved credits spent.

`IReadOnlyWorld.Credits` and `TrainingQueue` expose the same tick's balance
and remaining training time. A `UnitSpawned` event carries the new unit's
monotonic ID and starting position. On that tick the snapshot includes its
position, health and owner, and it can receive orders on subsequent ticks.

## RTS-009 economy and sector capture

After movement and damage, each sector checks for living infantry within
its configured radius, including the boundary. Opposing infantry freeze
progress while they are both present. If only the rival remains, previous
progress decreases by one tick at a time before that player can start
capturing. A sector without infantry resets unfinished progress. Ownership
and income stay with the current owner until the full capture completes.

Each living HQ and owned sector contributes its configured income rate on
every tick, including a sector's capture tick. Dividing accumulated income
by the tick rate credits whole units and carries the remainder forward per
player. Orders use the balance at command processing time, before that
tick's income. The wallet caps at the maximum C# `int` value to keep
credits nonnegative for large configurations.

`IReadOnlyWorld.Sectors` exposes immutable sector states in entity ID order.
`IncomePerSecond` reports the current rate for a player. `SectorCaptured`
reports the previous and new owners. `CreditsChanged` reports each changed
player's net balance change for the tick in player ID order.

## RTS-010 static infantry cover

The scene supplies axis-aligned cover rectangles in world coordinates. The
simulation copies the rectangles at startup. Their boundary counts as
inside. Cover leaves movement and line of sight available.

After movement, a hit on infantry inside one or more cover rectangles
uses the configured multiplier once. Round each hit to the nearest integer,
with halves rounded away from zero, before adding simultaneous damage.
The attacker's unit type does not affect the reduction. Tanks and
buildings take full damage even inside a cover rectangle.

`IReadOnlyWorld.HasCover` lets the game show whether a living infantry
squad is protected in the published snapshot.

## RTS-011 enemy commander

Every 40 ticks, the game asks `GameSession.Observe` for the enemy
commander's `PlayerObservation`. It contains living friendly units and
buildings, the player's credits, public sector locations and ownership,
and hostile contacts that at least one living ally can see. The same
line-of-sight rule validates player and AI attack orders. Each contact
lists its observing allies, which lets the commander check individual
firing positions. RTS-016 uses this projection for battlefield visibility as well.

The commander uses `CommandScheduler.ForPlayer` to submit orders for its
side on the next tick. It trains up to two tanks and four infantry
squads through the ordinary queues, spending on a tank first when
affordable. It prioritizes hostile units within 18 m of a friendly HQ
and otherwise assigns the nearest infantry squad to a neutral sector.
With at least four units including a tank, it sends the remaining
units toward a visible hostile HQ. If the HQ is hidden, it scouts
public sector locations until a contact is available. An assault unit
moves to firing range before taking aim and tries another position
if its path was rejected.

## RTS-012 battlefield HUD

The production, sector and selection panels read the published world
snapshot. Build bars use elapsed ticks for the first order, and capture
bars use the current sector's capture ticks. The selection panel sums
the current and maximum hit points of living selected units and reports
cover from `IReadOnlyWorld.HasCover`.

The Godot scene turns `ShotFired` events into rifle bursts, cannon effects
and local impacts. It uses `CommandProcessed` rejection codes to
explain failed local orders.
Blocked clicks receive feedback at the input boundary. These effects
read simulation state and leave gameplay rules in the simulation.

## RTS-013 match lifecycle

Competitive sessions start with a headquarters for both players. After
movement, combat and the remaining systems for the tick, the simulation
checks both headquarters. The last HQ falling for a side ends the
match. If both sides lose their last HQ on the same tick, the result is
a draw. The terminal snapshot and one `MatchEnded` event share that tick.
After completion, the snapshot stays frozen and scheduled commands are
ignored.

`GameSession.SetPaused` holds simulation advancement while a running
match is paused. Previously scheduled commands remain queued for the
same simulation tick. The Godot pause menu also stops battlefield input
and camera processing. Restart reloads the battlefield, which constructs
a new session, scheduler, AI and HUD.

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


## RTS-016 battlefield fog

`GameSession.Observe` provides the contacts used for rendering and picking.
The Godot line-of-sight adapter limits vision to 42 m from each living
unit and checks the existing roadblock and building collision shapes.
The same adapter applies to both sides. All current weapon ranges fit
inside the sight radius.

At each simulation tick, Godot hides hostile actors without a contact,
including their health bars, firing effects and picking shapes. Hidden
buildings keep their solid collision for navigation and line of sight. Ranges and damage
are resolved by the existing combat rules. The local map retains explored
terrain and draws fog from samples on a 48 × 48 ground grid. The map mask
refreshes up to five times per second. Actor visibility uses the contact
query at the current tick. Sector ownership is public. Live capture and
contest indicators require ground visibility at the sector center.

## RTS-017 tactical orders

`CommandScheduler.ScheduleAttackMove` submits a `MoveCommand` with attack-move
mode. It plans and validates each group slot using the normal movement rules.
Before advancing an attack-move route, the simulation checks for hostiles
within that unit's weapon range and line of sight. The route waits while a
target remains, including during cooldown, and resumes once there is no
eligible target. All routes check the same start-of-movement positions.
Combat still resolves against the positions after movement.

`CommandScheduler.ScheduleHoldPosition` submits a `HoldPositionCommand`.
An accepted hold clears the selected units' routes and focused targets.
Automatic fire and normal cooldowns apply from the held position. Both
orders use the existing tick, player and sequence ordering and validate
all selected units before applying an effect. The replay test format records
both modes along with the existing move, attack and training orders.

## RTS-018 versioned match saves

`GameSession.Capture` and `GameSession.Restore` operate on a checkpoint of the
current tick and the state required to advance the next one. The checkpoint
includes living and destroyed units, health, cached waypoints, focused targets,
cooldowns, production queues, fractional income, sector progress, pending
orders and sequence state. Restore uses the current battlefield's static
geometry and recreates the simulation before exposing it to callers. The Godot
adapter also saves the enemy commander's tactical state and explored terrain.

The JSON format includes its schema version, the battlefield ID, and
the game configuration version and fingerprint. The game validates the file
and creates a replacement session before applying it to the scene. Disk
writes use a temporary file in the same directory and replace the slot only
after a complete write.

## RTS-019 directional cover and suppression

A directional `CoverZone` has a marked edge. Damage uses the 0.7 cover
multiplier when an infantry target is inside the zone and the attacker is
within 45° of that edge's outward direction. Diagonal boundary shots count
and each shot rounds before same-tick damage is summed. A coincident
attacker has no direction. Zones without a direction retain the original
all-directions behavior. `HasCover` reports a unit's presence in cover.

Each damaging hit on surviving infantry refreshes suppression through
`hit tick + durationTicks`. The default duration is 24 ticks. Movement
runs at `movementMultiplier` during the following 24 ticks, so the first
hit leaves that tick's completed movement intact. The route continues
at ordinary speed when the timer expires. `IsSuppressed` reports the
published state, including the tick on which the hit lands.

Save format 2 records the active expiry tick. Format 1 saves with the
matching previous configuration load with no active suppression.
Once resumed, they use the current battlefield rules.
