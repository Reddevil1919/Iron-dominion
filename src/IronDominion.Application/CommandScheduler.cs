using IronDominion.Domain;

namespace IronDominion.Application;

public sealed class CommandScheduler
{
    private readonly IGameSimulation _simulation;
    private readonly Dictionary<PlayerId, long> _lastSequences = new();

    internal CommandScheduler(IGameSimulation simulation)
    {
        _simulation = simulation;
    }

    public MoveCommand ScheduleMove(PlayerId player, IReadOnlyList<EntityId> units, SimVector destination,
        long? executeAtTick = null)
    {
        return Schedule(player, executeAtTick, (tick, sequence) => new MoveCommand(player, tick, sequence, units, destination));
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
