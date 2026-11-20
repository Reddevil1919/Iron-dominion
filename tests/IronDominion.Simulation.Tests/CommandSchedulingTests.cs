using IronDominion.Application;
using IronDominion.Domain;
using Xunit;

namespace IronDominion.Simulation.Tests;

public sealed class CommandSchedulingTests
{
    private static readonly PlayerId Blue = new(1);
    private static readonly PlayerId Orange = new(2);
    private static readonly EntityId BlueUnit = new(101);
    private static readonly EntityId OrangeUnit = new(102);
    private static readonly EntityId DestroyedUnit = new(103);
    private static readonly EntityId BlueBarracks = new(201);
    private static readonly EntityId DestroyedHeadquarters = new(202);

    [Fact]
    public void DifferentArrivalOrders_ProduceTheSameTickPlayerSequenceOrder()
    {
        GameCommand[] orders =
        [
            new MoveCommand(Orange, 1, 1, [OrangeUnit], default),
            new MoveCommand(Blue, 2, 1, [BlueUnit], default),
            new AttackCommand(Blue, 1, 3, [BlueUnit], OrangeUnit),
            new TrainUnitCommand(Blue, 1, 2, BlueBarracks, UnitKind.InfantrySquad)
        ];

        var firstRun = Run(orders);
        var secondRun = Run(orders.Reverse());

        Assert.Equal(firstRun, secondRun);
        Assert.Equal(
            [(1L, Blue, 2L), (1L, Blue, 3L), (1L, Orange, 1L), (2L, Blue, 1L)],
            firstRun.Select(result => (result.Tick, result.Command.Player, result.Command.Sequence)));
        Assert.All(firstRun, result => Assert.True(result.Result.Accepted));
    }

    [Fact]
    public void Tick_ProcessesOnlyDueCommandsOnceAndPreservesPreviousSnapshot()
    {
        var simulation = new GameSimulation(CreateEntities());
        var command = new MoveCommand(Blue, 3, 1, [BlueUnit], default);
        simulation.Schedule(command);
        var initial = simulation.World;

        Assert.Empty(simulation.Tick());
        Assert.Empty(simulation.Tick());
        var result = Assert.IsType<CommandProcessed>(Assert.Single(simulation.Tick()));
        Assert.Same(command, result.Command);
        Assert.Equal(3L, result.Tick);
        Assert.Equal(3L, simulation.World.Tick);
        Assert.Equal(0L, initial.Tick);
        Assert.Empty(simulation.Tick());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    public void Schedule_RejectsTicksAlreadyReached(long tick)
    {
        var simulation = new GameSimulation(CreateEntities());
        simulation.Tick();

        Assert.Throws<ArgumentOutOfRangeException>("command",
            () => simulation.Schedule(new MoveCommand(Blue, tick, 1, [BlueUnit], default)));
        simulation.Schedule(new MoveCommand(Blue, 2, 1, [BlueUnit], default));
        Assert.Single(simulation.Tick());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Schedule_RejectsNonpositiveSequences(long sequence)
    {
        var simulation = new GameSimulation(CreateEntities());

        Assert.Throws<ArgumentOutOfRangeException>("command",
            () => simulation.Schedule(new MoveCommand(Blue, 1, sequence, [BlueUnit], default)));
        Assert.Empty(simulation.Tick());
    }

    [Fact]
    public void Schedule_RejectsDefaultPlayer()
    {
        var simulation = new GameSimulation(CreateEntities());

        Assert.Throws<ArgumentOutOfRangeException>("command",
            () => simulation.Schedule(new MoveCommand(default, 1, 1, [BlueUnit], default)));
        Assert.Empty(simulation.Tick());
    }

    [Fact]
    public void Schedule_RejectsDuplicatePlayerSequenceAcrossTicksAndAfterProcessing()
    {
        var simulation = new GameSimulation(CreateEntities());
        simulation.Schedule(new MoveCommand(Blue, 1, 1, [BlueUnit], default));
        Assert.Throws<ArgumentException>("command",
            () => simulation.Schedule(new MoveCommand(Blue, 2, 1, [BlueUnit], default)));

        Assert.Single(simulation.Tick());

        Assert.Throws<ArgumentException>("command",
            () => simulation.Schedule(new MoveCommand(Blue, 2, 1, [BlueUnit], default)));
        simulation.Schedule(new MoveCommand(Orange, 2, 1, [OrangeUnit], default));
        Assert.Equal(Orange, Assert.IsType<CommandProcessed>(Assert.Single(simulation.Tick())).Command.Player);
    }

    [Fact]
    public void Tick_RejectsUnownedOrDestroyedActorsAndContinuesWithLaterCommands()
    {
        var simulation = new GameSimulation(CreateEntities());
        simulation.Schedule(new MoveCommand(Blue, 1, 1, [BlueUnit, OrangeUnit], default));
        simulation.Schedule(new AttackCommand(Blue, 1, 2, [DestroyedUnit], OrangeUnit));
        simulation.Schedule(new TrainUnitCommand(Orange, 1, 1, BlueBarracks, UnitKind.InfantrySquad));
        simulation.Schedule(new TrainUnitCommand(Blue, 1, 3, DestroyedHeadquarters, UnitKind.Tank));
        simulation.Schedule(new TrainUnitCommand(Blue, 1, 4, BlueBarracks, UnitKind.InfantrySquad));

        Assert.Collection(simulation.Tick(),
            result => AssertRejected(result, "not_owner"),
            result => AssertRejected(result, "actor_destroyed"),
            result => AssertRejected(result, "actor_destroyed"),
            result => Assert.True(Assert.IsType<CommandProcessed>(result).Result.Accepted),
            result => AssertRejected(result, "not_owner"));
    }

    [Fact]
    public void Tick_RejectsMalformedOrdersWithoutDroppingValidCommands()
    {
        var simulation = new GameSimulation(CreateEntities());
        simulation.Schedule(new MoveCommand(Blue, 1, 1, [], default));
        simulation.Schedule(new MoveCommand(Blue, 1, 2, [BlueBarracks], default));
        simulation.Schedule(new AttackCommand(Blue, 1, 3, [BlueUnit], default));
        simulation.Schedule(new TrainUnitCommand(Blue, 1, 4, BlueUnit, UnitKind.InfantrySquad));
        simulation.Schedule(new TrainUnitCommand(Blue, 1, 5, BlueBarracks, (UnitKind)99));
        simulation.Schedule(new MoveCommand(Blue, 1, 6, [BlueUnit], default));

        Assert.Collection(simulation.Tick(),
            result => AssertRejected(result, "empty_selection"),
            result => AssertRejected(result, "invalid_actor"),
            result => AssertRejected(result, "invalid_target"),
            result => AssertRejected(result, "invalid_actor"),
            result => AssertRejected(result, "invalid_unit_kind"),
            result => Assert.True(Assert.IsType<CommandProcessed>(result).Result.Accepted));
    }

    [Fact]
    public void Session_AssignsSequencesPerPlayerAndUsesTheNextTickByDefault()
    {
        var session = new GameSession(CreateEntities());
        var future = session.Commands.ScheduleTraining(Blue, BlueBarracks, UnitKind.InfantrySquad, executeAtTick: 3);
        var orangeMove = session.Commands.ScheduleMove(Orange, [OrangeUnit], default);

        Assert.Equal(1L, future.Sequence);
        Assert.Equal(1L, orangeMove.Sequence);
        Assert.Equal(1L, orangeMove.ExecuteAtTick);
        Assert.Same(orangeMove, Assert.IsType<CommandProcessed>(Assert.Single(session.AdvanceOneTick())).Command);
        Assert.Throws<ArgumentOutOfRangeException>("command",
            () => session.Commands.ScheduleMove(Blue, [BlueUnit], default, executeAtTick: 1));

        var attack = session.Commands.ScheduleAttack(Blue, [BlueUnit], OrangeUnit);
        Assert.Equal(2L, attack.Sequence);
        Assert.Equal(2L, attack.ExecuteAtTick);
        Assert.Same(attack, Assert.IsType<CommandProcessed>(Assert.Single(session.AdvanceOneTick())).Command);
        Assert.Same(future, Assert.IsType<CommandProcessed>(Assert.Single(session.AdvanceOneTick())).Command);
        Assert.Equal(3L, session.World.Tick);
    }

    [Fact]
    public void ScheduledCommands_CopyTheSelectionBeforeItCanChange()
    {
        var session = new GameSession(CreateEntities());
        var selection = new List<EntityId> { BlueUnit };
        var move = session.Commands.ScheduleMove(Blue, selection, default);
        var attack = session.Commands.ScheduleAttack(Blue, selection, OrangeUnit);
        selection[0] = OrangeUnit;

        Assert.Equal(BlueUnit, Assert.Single(move.Units));
        Assert.Equal(BlueUnit, Assert.Single(attack.Units));
        var results = session.AdvanceOneTick();
        Assert.Equal(2, results.Count);
        Assert.All(results, result => Assert.True(Assert.IsType<CommandProcessed>(result).Result.Accepted));
    }

    private static CommandProcessed[] Run(IEnumerable<GameCommand> orders)
    {
        var simulation = new GameSimulation(CreateEntities());
        foreach (var order in orders)
            simulation.Schedule(order);

        return simulation.Tick().Concat(simulation.Tick()).Cast<CommandProcessed>().ToArray();
    }

    private static WorldEntities CreateEntities() => new(
    [
        new Unit(BlueUnit, Blue, default, UnitKind.InfantrySquad, 400),
        new Unit(OrangeUnit, Orange, default, UnitKind.Tank, 600),
        new Unit(DestroyedUnit, Blue, default, UnitKind.InfantrySquad, 0),
        new Building(BlueBarracks, Blue, default, BuildingKind.Barracks, 700),
        new Building(DestroyedHeadquarters, Blue, default, BuildingKind.Headquarters, 0)
    ]);

    private static void AssertRejected(IGameEvent result, string reason)
    {
        Assert.Equal(new CommandResult(false, reason), Assert.IsType<CommandProcessed>(result).Result);
    }
}
