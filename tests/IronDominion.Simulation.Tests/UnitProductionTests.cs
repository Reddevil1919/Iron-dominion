using IronDominion.Domain;
using Xunit;

namespace IronDominion.Simulation.Tests;

public sealed class UnitProductionTests
{
    private static readonly PlayerId Blue = new(1);
    private static readonly PlayerId Orange = new(2);
    private static readonly EntityId Headquarters = new(7);
    private static readonly EntityId Barracks = new(9);
    private static readonly SimVector Exit = new(20, 0);

    private static readonly GameConfiguration Configuration = new(
        1, 20, 500, 5, 2, 10, 6, 0.7m,
        [new(UnitKind.InfantrySquad, 400, 100, 2, 40, 12, 10, 20),
            new(UnitKind.Tank, 600, 500, 3, 30, 16, 45, 40)],
        [new(BuildingKind.Headquarters, 1500), new(BuildingKind.Barracks, 700)]);

    [Fact]
    public void ReservesFullCostInCommandOrderAndKeepsBalancesIndependent()
    {
        var match = Match(new Navigation(),
            new Building(Headquarters, Blue, default, BuildingKind.Headquarters, 1500, Exit),
            new Building(Barracks, Orange, default, BuildingKind.Barracks, 700, new SimVector(-20, 0)));
        var before = match.World;
        match.Schedule(new TrainUnitCommand(Blue, 1, 2, Headquarters, UnitKind.Tank));
        match.Schedule(new TrainUnitCommand(Orange, 1, 1, Barracks, UnitKind.InfantrySquad));
        match.Schedule(new TrainUnitCommand(Blue, 1, 1, Headquarters, UnitKind.Tank));

        Assert.Collection(match.Tick().OfType<CommandProcessed>(),
            result => Assert.True(result.Result.Accepted),
            result => Assert.Equal("insufficient_credits", result.Result.RejectionCode),
            result => Assert.True(result.Result.Accepted));
        Assert.Equal(0, match.World.Credits(Blue));
        Assert.Equal(400, match.World.Credits(Orange));
        Assert.Single(match.World.TrainingQueue(Headquarters));
        Assert.Equal(500, before.Credits(Blue));
        Assert.Empty(before.TrainingQueue(Headquarters));
        Assert.Equal(UnitKind.InfantrySquad, Assert.IsType<UnitSpawned>(Assert.Single(match.Tick())).Unit.Kind);
        var tank = Assert.IsType<UnitSpawned>(Assert.Single(match.Tick()));
        Assert.Equal(UnitKind.Tank, tank.Unit.Kind);
        Assert.Equal(Exit, tank.Unit.Position);
        Assert.Equal(0, match.World.Credits(Blue));
    }

    [Theory]
    [InlineData(1, 9, UnitKind.Tank, "invalid_production_type")]
    [InlineData(2, 9, UnitKind.InfantrySquad, "not_owner")]
    [InlineData(1, 7, UnitKind.Tank, "actor_destroyed")]
    [InlineData(1, 99, UnitKind.Tank, "invalid_actor")]
    public void RejectsInvalidProducersAndKindsWithoutCharging(int player, int producer, UnitKind kind, string error)
    {
        var match = Match(new Navigation(),
            new Building(Headquarters, Blue, default, BuildingKind.Headquarters, 0, Exit),
            new Building(Barracks, Blue, default, BuildingKind.Barracks, 700, Exit));
        match.Schedule(new TrainUnitCommand(new PlayerId(player), 1, 1, new EntityId(producer), kind));

        var result = Assert.IsType<CommandProcessed>(Assert.Single(match.Tick()));
        Assert.Equal(new CommandResult(false, error), result.Result);
        Assert.Equal(500, match.World.Credits(Blue));
        Assert.Empty(match.World.TrainingQueue(Barracks));
    }

    [Fact]
    public void WaitsAtAnOccupiedExitAndTrainsInOrder()
    {
        var blocker = new EntityId(1);
        var match = Match(new Navigation(),
            new Unit(blocker, Blue, Exit, UnitKind.InfantrySquad, 400),
            new Building(Barracks, Blue, default, BuildingKind.Barracks, 700, Exit),
            new Sector(new EntityId(20), null, default));
        match.Schedule(new TrainUnitCommand(Blue, 1, 1, Barracks, UnitKind.InfantrySquad));
        match.Schedule(new TrainUnitCommand(Blue, 1, 2, Barracks, UnitKind.InfantrySquad));

        Assert.Empty(match.Tick().OfType<UnitSpawned>());
        Assert.Empty(match.Tick());
        var blocked = match.World;
        Assert.Collection(blocked.TrainingQueue(Barracks),
            order => Assert.Equal(0, order.RemainingTicks),
            order => Assert.Equal(2, order.RemainingTicks));
        Assert.Empty(match.Tick());
        match.Schedule(new MoveCommand(Blue, 4, 3, [blocker], new SimVector(40, 0)));
        var first = Assert.Single(Enumerable.Range(0, 5).SelectMany(_ => match.Tick()).OfType<UnitSpawned>());
        Assert.Equal(new EntityId(21), first.Unit.Id);
        Assert.Equal(Blue, match.World.LivingUnitOwner(first.Unit.Id));
        Assert.True(match.World.TryGetHitPoints(first.Unit.Id, out var hp));
        Assert.Equal(400, hp);
        Assert.Equal(2, Assert.Single(match.World.TrainingQueue(Barracks)).RemainingTicks);
        Assert.Equal(2, blocked.TrainingQueue(Barracks).Count);
        Assert.Empty(match.Tick());
        Assert.Empty(match.Tick());
        Assert.Equal(0, Assert.Single(match.World.TrainingQueue(Barracks)).RemainingTicks);

        match.Schedule(new MoveCommand(Blue, 11, 4, [first.Unit.Id], new SimVector(20, 20)));
        Assert.True(Assert.Single(match.Tick().OfType<CommandProcessed>()).Result.Accepted);
        var second = Assert.Single(Enumerable.Range(0, 4).SelectMany(_ => match.Tick()).OfType<UnitSpawned>());
        Assert.Equal(new EntityId(22), second.Unit.Id);
        Assert.True(match.World.TryGetUnitPosition(second.Unit.Id, out var position));
        Assert.Equal(Exit, position);
        Assert.Equal(300, match.World.Credits(Blue));
        Assert.Empty(match.World.TrainingQueue(Barracks));
        Assert.Empty(match.Tick());
    }

    [Fact]
    public void ProducersProgressIndependentlyAndWaitForWalkableTerrain()
    {
        var otherBarracks = new EntityId(10);
        var otherExit = new SimVector(-20, 0);
        var navigation = new Navigation { BlockedExit = Exit };
        var match = Match(navigation,
            new Unit(new EntityId(1), Blue, Exit, UnitKind.InfantrySquad, 0),
            new Building(Barracks, Blue, default, BuildingKind.Barracks, 700, Exit),
            new Building(otherBarracks, Orange, default, BuildingKind.Barracks, 700, otherExit));
        match.Schedule(new TrainUnitCommand(Blue, 1, 1, Barracks, UnitKind.InfantrySquad));
        match.Schedule(new TrainUnitCommand(Orange, 1, 1, otherBarracks, UnitKind.InfantrySquad));

        Assert.Empty(match.Tick().OfType<UnitSpawned>());
        var orange = Assert.IsType<UnitSpawned>(Assert.Single(match.Tick()));
        Assert.Equal(Orange, orange.Unit.Owner);
        Assert.Equal(otherExit, orange.Unit.Position);
        Assert.Equal(0, Assert.Single(match.World.TrainingQueue(Barracks)).RemainingTicks);
        Assert.Empty(match.Tick());

        navigation.BlockedExit = null;
        var blue = Assert.IsType<UnitSpawned>(Assert.Single(match.Tick()));
        Assert.Equal(Blue, blue.Unit.Owner);
        Assert.Equal(Exit, blue.Unit.Position);
        Assert.Equal(400, match.World.Credits(Blue));
        Assert.Empty(match.Tick());
    }

    [Fact]
    public void DestroyingAProducerDiscardsItsPaidQueue()
    {
        var enemy = new Unit(new EntityId(1), Orange, new SimVector(5, 0), UnitKind.Tank, 600);
        var producer = new Building(Barracks, Blue, default, BuildingKind.Barracks, 40, Exit);
        var match = new GameSimulation(new WorldEntities([enemy, producer]), Configuration,
            new Navigation(), new Sight(true));
        match.Schedule(new TrainUnitCommand(Blue, 1, 1, Barracks, UnitKind.InfantrySquad));

        var events = match.Tick();
        Assert.True(Assert.Single(events.OfType<CommandProcessed>()).Result.Accepted);
        Assert.Equal(Barracks, Assert.Single(events.OfType<EntityDestroyed>()).Entity);
        Assert.Equal(400, match.World.Credits(Blue));
        Assert.Empty(match.World.TrainingQueue(Barracks));
        Assert.Empty(match.Tick());
    }

    private static GameSimulation Match(INavigationService navigation, params WorldEntity[] entities) =>
        new(new WorldEntities(entities), Configuration, navigation, new Sight(false));

    private sealed class Navigation : INavigationService
    {
        public SimVector? BlockedExit { get; set; }

        public IReadOnlyList<SimVector> FindPath(SimVector start, SimVector destination, int movementMask) =>
            start == destination && destination == BlockedExit ? [] : [destination];
    }

    private sealed class Sight(bool clear) : ILineOfSightService
    {
        public bool HasLineOfSight(SimVector origin, SimVector target, EntityId targetId) => clear;
    }
}
