using IronDominion.Domain;
using Xunit;

namespace IronDominion.Simulation.Tests;

public sealed class UnitMovementTests
{
    private static readonly PlayerId Blue = new(1);
    private static readonly PlayerId Orange = new(2);
    private static readonly EntityId First = new(1);
    private static readonly EntityId Second = new(2);

    private static readonly GameConfiguration Configuration = new(
        schemaVersion: 1,
        tickRate: 20,
        startingCredits: 500,
        headquartersIncomePerSecond: 5,
        sectorIncomePerSecond: 2,
        captureSeconds: 10,
        captureRadiusMeters: 6,
        infantryCoverMultiplier: 0.7m,
        units: [
            new UnitDefinition(UnitKind.InfantrySquad, 400, 100, 160, 4, 12, 10, 20),
            new UnitDefinition(UnitKind.Tank, 600, 500, 400, 3, 16, 45, 40)
        ],
        buildings: [
            new BuildingDefinition(BuildingKind.Headquarters, 1500),
            new BuildingDefinition(BuildingKind.Barracks, 700)
        ]);

    [Fact]
    public void MovementStartsOnItsScheduledTickAndPreservesOldSnapshots()
    {
        var navigation = StraightRoutes();
        var simulation = CreateSimulation(navigation,
            new Unit(First, Blue, default, UnitKind.InfantrySquad, 400),
            new Unit(Second, Blue, default, UnitKind.Tank, 600));
        var initial = simulation.World;
        simulation.Schedule(new MoveCommand(Blue, 2, 1, [First], new SimVector(1, 0)));
        simulation.Schedule(new MoveCommand(Blue, 2, 2, [Second], new SimVector(1, 0)));

        Assert.Empty(simulation.Tick());
        Assert.Equal(default, Position(simulation, First));
        Assert.All(simulation.Tick(), result => Assert.True(Assert.IsType<CommandProcessed>(result).Result.Accepted));
        Assert.Equal(0.20f, Position(simulation, First).X, precision: 4);
        Assert.Equal(0.15f, Position(simulation, Second).X, precision: 4);

        Advance(simulation, 12);
        Assert.Equal(new SimVector(1, 0), Position(simulation, First));
        Assert.Equal(new SimVector(1, 0), Position(simulation, Second));
        Assert.True(initial.TryGetUnitPosition(First, out var original));
        Assert.Equal(default, original);
        Assert.Equal(2, navigation.Requests.Count);
    }

    [Fact]
    public void FollowsCachedWaypointsAroundAnObstacleWithoutTeleporting()
    {
        var start = new SimVector(-6, 0);
        var target = new SimVector(6, 0);
        var navigation = new TestNavigation((_, destination) =>
            [new SimVector(-4, 5), new SimVector(4, 5), destination]);
        var simulation = CreateSimulation(navigation, Squad(First, Blue, start));
        simulation.Schedule(new MoveCommand(Blue, 1, 1, [First], target));

        var previous = start;
        for (var tick = 0; tick < 120; tick++)
        {
            simulation.Tick();
            var current = Position(simulation, First);
            var dx = current.X - previous.X;
            var dz = current.Z - previous.Z;
            Assert.InRange(MathF.Sqrt(dx * dx + dz * dz), 0f, 0.2001f);
            if (MathF.Abs(current.X) < 2f)
                Assert.InRange(current.Z, 4f, 5.001f);
            previous = current;
        }

        Assert.Equal(target, Position(simulation, First));
        Assert.Single(navigation.Requests);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TanksDetourAroundStationaryAndMovingTanks(bool secondMoves)
    {
        var west = new SimVector(-12, 0);
        var east = new SimVector(12, 0);
        var blocker = secondMoves ? east : default;
        var simulation = CreateSimulation(StraightRoutes(), Tank(First, west), Tank(Second, blocker));
        simulation.Schedule(new MoveCommand(Blue, 1, 1, [First], east));
        if (secondMoves)
            simulation.Schedule(new MoveCommand(Blue, 1, 2, [Second], west));

        var turned = false;
        for (var tick = 0; tick < 340; tick++)
        {
            var previous = Position(simulation, First);
            simulation.Tick();
            var first = Position(simulation, First);
            var second = Position(simulation, Second);
            Assert.True(Distance(first, second) >= 6.599f);
            Assert.InRange(Distance(previous, first), 0f, 0.1501f);
            turned |= MathF.Abs(first.Z) > 0.5f;
        }

        Assert.True(turned);
        Assert.Equal(east, Position(simulation, First));
        Assert.Equal(secondMoves ? west : blocker, Position(simulation, Second));
    }

    [Fact]
    public void ABlockedTankDestinationWaitsThenResumesWhenTheOtherTankMoves()
    {
        var parked = new SimVector(10, 0);
        var simulation = CreateSimulation(StraightRoutes(), Tank(First, default), Tank(Second, parked));
        simulation.Schedule(new MoveCommand(Blue, 1, 1, [First], parked));
        Advance(simulation, 65);
        var waiting = Position(simulation, First);
        Assert.InRange(Distance(waiting, parked), 6.599f, 6.76f);
        Advance(simulation, 15);
        Assert.Equal(waiting, Position(simulation, First));

        simulation.Schedule(new MoveCommand(Blue, 81, 2, [Second], new SimVector(10, 12)));
        Advance(simulation, 120);
        Assert.Equal(parked, Position(simulation, First));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnreachableOrderDoesNotReplaceAnExistingRoute(bool attackMove)
    {
        var navigation = new TestNavigation((_, destination) => destination.X <= 59 ? [destination] : []);
        var simulation = CreateSimulation(navigation, Squad(First, Blue, default));
        var valid = new SimVector(2, 0);
        simulation.Schedule(new MoveCommand(Blue, 1, 1, [First], valid));
        simulation.Schedule(new MoveCommand(Blue, 2, 2, [First], new SimVector(64, 0), attackMove));

        Assert.True(Processed(simulation).Result.Accepted);
        Assert.Equal(new CommandResult(false, "unreachable_destination"), Processed(simulation).Result);
        Assert.Equal(0.4f, Position(simulation, First).X, precision: 4);

        Advance(simulation, 15);
        Assert.Equal(valid, Position(simulation, First));
        Assert.Equal(2, navigation.Requests.Count);
    }

    [Fact]
    public void ABlockedGroupSlotRejectsTheEntireOrder()
    {
        var navigation = new TestNavigation((_, destination) => destination.X < 9 ? [destination] : []);
        var simulation = CreateSimulation(navigation, Squad(First, Blue, new SimVector(-2, 0)),
            Squad(Second, Blue, new SimVector(-4, 0)));
        simulation.Schedule(new MoveCommand(Blue, 1, 1, [First, Second], default));

        Assert.Equal(new CommandResult(false, "unreachable_destination"), Processed(simulation).Result);
        Advance(simulation, 20);

        Assert.Equal(new SimVector(-2, 0), Position(simulation, First));
        Assert.Equal(new SimVector(-4, 0), Position(simulation, Second));
    }

    [Fact]
    public void UnauthorizedOrdersNeverReachTheNavigationAdapter()
    {
        var navigation = StraightRoutes();
        var simulation = CreateSimulation(navigation, Squad(First, Orange, default));
        simulation.Schedule(new MoveCommand(Blue, 1, 1, [First], new SimVector(3, 0)));

        Assert.Equal(new CommandResult(false, "not_owner"), Processed(simulation).Result);
        Assert.Empty(navigation.Requests);
        Assert.Equal(default, Position(simulation, First));
    }

    [Fact]
    public void TenUnitsReceiveDistinctSlotsInEntityIdOrder()
    {
        var navigation = StraightRoutes();
        var units = Enumerable.Range(1, 10)
            .Select(id => Squad(new EntityId(id), Blue, new SimVector(-10, id)))
            .ToArray();
        var simulation = CreateSimulation(navigation, units);
        var selection = units.Select(unit => unit.Id).Reverse().Append(First).ToArray();
        var anchor = new SimVector(5, 5);
        simulation.Schedule(new MoveCommand(Blue, 1, 1, selection, anchor));

        Assert.True(Processed(simulation).Result.Accepted);
        Advance(simulation, 300);

        var positions = units.Select(unit => Position(simulation, unit.Id)).ToArray();
        Assert.Equal(anchor, positions[0]);
        Assert.Equal(10, positions.Distinct().Count());
        Assert.Equal(units.Select(unit => unit.Position), navigation.Requests.Select(request => request.Start));
    }

    [Fact]
    public void HoldPositionRejectsMixedOwnershipWithoutStoppingTheFriendlyUnit()
    {
        var simulation = CreateSimulation(StraightRoutes(), Squad(First, Blue, default), Squad(Second, Orange, default));
        simulation.Schedule(new MoveCommand(Blue, 1, 1, [First], new SimVector(3, 0)));
        simulation.Schedule(new HoldPositionCommand(Blue, 2, 2, [First, Second]));

        Assert.True(Processed(simulation).Result.Accepted);
        Assert.Equal(new CommandResult(false, "not_owner"), Processed(simulation).Result);
        Assert.Equal(0.4f, Position(simulation, First).X, precision: 4);
        Assert.Equal(default, Position(simulation, Second));
    }

    private static Unit Squad(EntityId id, PlayerId owner, SimVector position) =>
        new(id, owner, position, UnitKind.InfantrySquad, 400);

    private static Unit Tank(EntityId id, SimVector position) =>
        new(id, Blue, position, UnitKind.Tank, 600);

    private static float Distance(SimVector first, SimVector second)
    {
        var x = first.X - second.X;
        var z = first.Z - second.Z;
        return MathF.Sqrt(x * x + z * z);
    }

    private static GameSimulation CreateSimulation(INavigationService navigation, params Unit[] units) =>
        new(new WorldEntities(units), Configuration, navigation);

    private static SimVector Position(GameSimulation simulation, EntityId id)
    {
        Assert.True(simulation.World.TryGetUnitPosition(id, out var position));
        return position;
    }

    private static CommandProcessed Processed(GameSimulation simulation) =>
        Assert.IsType<CommandProcessed>(Assert.Single(simulation.Tick()));

    private static void Advance(GameSimulation simulation, int ticks)
    {
        for (var tick = 0; tick < ticks; tick++)
            simulation.Tick();
    }

    private static TestNavigation StraightRoutes() => new((_, destination) => [destination]);

    private sealed class TestNavigation(Func<SimVector, SimVector, IReadOnlyList<SimVector>> route) : INavigationService
    {
        public List<(SimVector Start, SimVector Destination)> Requests { get; } = new();

        public IReadOnlyList<SimVector> FindPath(SimVector start, SimVector destination, int movementMask)
        {
            Requests.Add((start, destination));
            return route(start, destination);
        }
    }
}
