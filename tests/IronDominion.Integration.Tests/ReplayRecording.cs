using IronDominion.Domain;

namespace IronDominion.Integration.Tests;

internal sealed record ReplayRecording(int ConfigurationVersion, int Seed, RecordedOrder[] Orders);

internal enum OrderKind { Move, Attack, Train }

internal sealed record RecordedOrder(long Tick, int Player, long Sequence, OrderKind Kind,
    int[] Units, int Target, float X, float Z, UnitKind TrainingKind)
{
    public static RecordedOrder Capture(GameCommand command) => command switch
    {
        MoveCommand move => new(move.ExecuteAtTick, move.Player.Value, move.Sequence, OrderKind.Move,
            move.Units.Select(unit => unit.Value).ToArray(), 0, move.Destination.X, move.Destination.Z, default),
        AttackCommand attack => new(attack.ExecuteAtTick, attack.Player.Value, attack.Sequence, OrderKind.Attack,
            attack.Units.Select(unit => unit.Value).ToArray(), attack.Target.Value, 0, 0, default),
        TrainUnitCommand train => new(train.ExecuteAtTick, train.Player.Value, train.Sequence, OrderKind.Train,
            [], train.Producer.Value, 0, 0, train.Kind),
        _ => throw new ArgumentException($"Unsupported replay order {command.GetType().Name}.", nameof(command))
    };

    public GameCommand Restore()
    {
        var player = new PlayerId(Player);
        return Kind switch
        {
            OrderKind.Move => new MoveCommand(player, Tick, Sequence, RestoreUnits(), new SimVector(X, Z)),
            OrderKind.Attack => new AttackCommand(player, Tick, Sequence, RestoreUnits(), new EntityId(Target)),
            OrderKind.Train => new TrainUnitCommand(player, Tick, Sequence, new EntityId(Target), TrainingKind),
            _ => throw new InvalidDataException($"Unknown replay order {Kind}.")
        };
    }

    private EntityId[] RestoreUnits() => Units.Select(value => new EntityId(value)).ToArray();
}
