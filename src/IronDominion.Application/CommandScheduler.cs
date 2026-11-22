using System.Collections.Immutable;
using IronDominion.Domain;
using IronDominion.Simulation;

namespace IronDominion.Application;

public sealed class CommandScheduler
{
    private readonly IGameSimulation _simulation;
    private readonly Dictionary<PlayerId, long> _lastSequences = new();

    internal CommandScheduler(IGameSimulation simulation)
    {
        _simulation = simulation;
    }

    public PlayerCommands ForPlayer(PlayerId player) => new(this, player);

    public MoveCommand ScheduleMove(PlayerId player, IReadOnlyList<EntityId> units, SimVector destination,
        long? executeAtTick = null)
    {
        return Schedule(player, executeAtTick, (tick, sequence) => new MoveCommand(player, tick, sequence, units, destination));
    }

    public MoveCommand ScheduleAttackMove(PlayerId player, IReadOnlyList<EntityId> units, SimVector destination,
        long? executeAtTick = null)
    {
        return Schedule(player, executeAtTick,
            (tick, sequence) => new MoveCommand(player, tick, sequence, units, destination, attackMove: true));
    }

    public HoldPositionCommand ScheduleHoldPosition(PlayerId player, IReadOnlyList<EntityId> units,
        long? executeAtTick = null)
    {
        return Schedule(player, executeAtTick, (tick, sequence) => new HoldPositionCommand(player, tick, sequence, units));
    }

    public AttackCommand ScheduleAttack(PlayerId player, IReadOnlyList<EntityId> units, EntityId target,
        long? executeAtTick = null)
    {
        return Schedule(player, executeAtTick, (tick, sequence) => new AttackCommand(player, tick, sequence, units, target));
    }

    public TrainUnitCommand ScheduleTraining(PlayerId player, EntityId producer, UnitKind kind,
        long? executeAtTick = null)
    {
        return Schedule(player, executeAtTick, (tick, sequence) => new TrainUnitCommand(player, tick, sequence, producer, kind));
    }

    internal ImmutableArray<CommandSequence> Capture() => _lastSequences.OrderBy(item => item.Key.Value)
        .Select(item => new CommandSequence(item.Key, item.Value)).ToImmutableArray();

    internal void Restore(ImmutableArray<CommandSequence> sequences)
    {
        if (sequences.IsDefault)
            throw new InvalidDataException("The saved scheduler is missing.");
        foreach (var sequence in sequences)
        {
            if (sequence.Player.Value <= 0 || sequence.Sequence is <= 0 or long.MaxValue
                || !_lastSequences.TryAdd(sequence.Player, sequence.Sequence))
                throw new InvalidDataException("Invalid saved scheduler sequence.");
        }
    }

    private TCommand Schedule<TCommand>(PlayerId player, long? executeAtTick, Func<long, long, TCommand> createCommand)
        where TCommand : GameCommand
    {
        var sequence = checked(_lastSequences.GetValueOrDefault(player) + 1);
        var tick = executeAtTick ?? checked(_simulation.World.Tick + 1);
        var command = createCommand(tick, sequence);

        _simulation.Schedule(command);
        _lastSequences[player] = sequence;
        return command;
    }
}
