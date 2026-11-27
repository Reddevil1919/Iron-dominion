using System.Collections.Immutable;
using IronDominion.Domain;

namespace IronDominion.Simulation;

public sealed record SimulationCheckpoint(long Tick, MatchResult Result,
    ImmutableArray<UnitCheckpoint> Units, ImmutableArray<BuildingCheckpoint> Buildings,
    ImmutableArray<SectorState> Sectors, ImmutableArray<PlayerCheckpoint> Players,
    ImmutableArray<CommandCheckpoint> Pending, ImmutableArray<CommandSequence> UsedSequences,
    ImmutableArray<ConstructionSite>? ConstructionSites = null);

public readonly record struct UnitCheckpoint(EntityId Id, PlayerId Owner, UnitKind Kind,
    SimVector SpawnPosition, int StartingHitPoints, int HitPoints, SimVector? Position, RouteCheckpoint? Route,
    EntityId? FocusedTarget, long? NextShot, long? SuppressedThrough = null, EntityId? RepairTarget = null)
{
    public Unit CreateUnit() => new(Id, Owner, SpawnPosition, Kind, StartingHitPoints);
}

public readonly record struct RouteCheckpoint(bool AttackMove, ImmutableArray<SimVector> Waypoints);
public readonly record struct BuildingCheckpoint(EntityId Id, int HitPoints, ImmutableArray<TrainingOrder> Queue,
    BuildingPlacement? Placement = null);
public readonly record struct PlayerCheckpoint(PlayerId Id, int Credits, long IncomeRemainder);
public readonly record struct CommandSequence(PlayerId Player, long Sequence);

public enum SavedOrderKind { Move, AttackMove, HoldPosition, Attack, Train, Build, Repair }

public readonly record struct CommandCheckpoint(PlayerId Player, long Tick, long Sequence, SavedOrderKind Kind,
    ImmutableArray<EntityId> Units, SimVector Destination, EntityId Target, UnitKind TrainingKind,
    BuildingKind BuildingKind = default, int QuarterTurns = 0)
{
    internal static CommandCheckpoint Capture(GameCommand command)
    {
        var order = new CommandCheckpoint(command.Player, command.ExecuteAtTick, command.Sequence, default,
            [], default, default, default);
        return command switch
        {
            MoveCommand move => order with
            {
                Kind = move.AttackMove ? SavedOrderKind.AttackMove : SavedOrderKind.Move,
                Units = move.Units,
                Destination = move.Destination
            },
            HoldPositionCommand hold => order with { Kind = SavedOrderKind.HoldPosition, Units = hold.Units },
            AttackCommand attack => order with { Kind = SavedOrderKind.Attack, Units = attack.Units, Target = attack.Target },
            BuildCommand build => order with
            {
                Kind = SavedOrderKind.Build, Target = build.Headquarters, Destination = build.Position,
                BuildingKind = build.Kind, QuarterTurns = build.QuarterTurns
            },
            RepairCommand repair => order with { Kind = SavedOrderKind.Repair, Units = repair.Units, Target = repair.Target },
            TrainUnitCommand train => order with
            {
                Kind = SavedOrderKind.Train, Target = train.Producer, TrainingKind = train.Kind
            },
            _ => throw new InvalidOperationException("This order cannot be saved.")
        };
    }

    internal GameCommand Restore() => Kind switch
    {
        SavedOrderKind.Move or SavedOrderKind.AttackMove => new MoveCommand(Player, Tick, Sequence, Units,
            Destination, attackMove: Kind == SavedOrderKind.AttackMove),
        SavedOrderKind.HoldPosition => new HoldPositionCommand(Player, Tick, Sequence, Units),
        SavedOrderKind.Attack => new AttackCommand(Player, Tick, Sequence, Units, Target),
        SavedOrderKind.Build => new BuildCommand(Player, Tick, Sequence, Target, BuildingKind, Destination, QuarterTurns),
        SavedOrderKind.Repair => new RepairCommand(Player, Tick, Sequence, Units, Target),
        SavedOrderKind.Train => new TrainUnitCommand(Player, Tick, Sequence, Target, TrainingKind),
        _ => throw new InvalidDataException("Unknown saved order type.")
    };
}
