using System.Collections.Immutable;
using IronDominion.Domain;
using Xunit;

namespace IronDominion.Simulation.Tests;

public sealed class ConstructionTests
{
    private static readonly PlayerId Blue = new(1);
    private static readonly PlayerId Orange = new(2);
    private static readonly EntityId BlueHq = new(1);
    private static readonly EntityId OrangeHq = new(2);
    private static readonly SimVector Site = new(-12, 0);

    [Fact]
    public void SameTickOrdersReserveOnceInSequenceOrderAndFactoryCanTrain()
    {
        var match = Match(new Terrain());
        var later = new BuildCommand(Blue, 1, 2, BlueHq, BuildingKind.Airport, Site);
        var first = new BuildCommand(Blue, 1, 1, BlueHq, BuildingKind.TankFactory, Site, 1);
        match.Schedule(later);
        match.Schedule(first);

        var results = match.Tick().OfType<CommandProcessed>().ToArray();
        Assert.Collection(results,
            accepted => { Assert.Equal(first, accepted.Command); Assert.True(accepted.Result.Accepted); },
            rejected => Assert.Equal("building_location_occupied", rejected.Result.RejectionCode));
        Assert.Equal(400, match.World.Credits(Blue));
        Assert.Equal(2, Assert.Single(match.World.ConstructionSites).RemainingTicks);
        Assert.Single(match.Observe(Blue).Buildings);
        Assert.Empty(match.Tick().OfType<BuildingCompleted>());
        var factory = Assert.Single(match.Tick().OfType<BuildingCompleted>()).Building;
        Assert.Equal(new SimVector(-4, 0), factory.ProductionExit);
        Assert.Equal(1000, factory.HitPoints);
        Assert.Empty(match.World.ConstructionSites);

        match.Schedule(new TrainUnitCommand(Blue, 4, 3, factory.Id, UnitKind.Tank));
        Assert.True(Assert.Single(match.Tick().OfType<CommandProcessed>()).Result.Accepted);
        Assert.Equal(UnitKind.Tank, Assert.Single(match.Tick().OfType<UnitSpawned>()).Unit.Kind);
    }

    [Theory]
    [InlineData(2, -12, 0, BuildingKind.Barracks, 0, 500, "not_owner")]
    [InlineData(1, -30, 0, BuildingKind.Barracks, 0, 500, "building_location_occupied")]
    [InlineData(1, -20, 12, BuildingKind.Barracks, 0, 500, "building_location_occupied")]
    [InlineData(1, -39, 0, BuildingKind.Barracks, 0, 500, "invalid_building_location")]
    [InlineData(1, 10, 0, BuildingKind.Barracks, 0, 500, "outside_construction_zone")]
    [InlineData(1, -12, 0, BuildingKind.Barracks, 4, 500, "invalid_building_location")]
    [InlineData(1, -12, 0, (BuildingKind)99, 0, 500, "invalid_building_kind")]
    [InlineData(1, -12, 0, BuildingKind.Barracks, 0, 99, "insufficient_credits")]
    public void InvalidOrdersDoNotChangeCreditsOrReserveSpace(int builder, int x, int z, BuildingKind kind,
        int rotation, int budget, string error)
    {
        var match = Match(new Terrain(), budget);
        match.Schedule(new BuildCommand(Blue, 1, 1, new EntityId(builder), kind, new SimVector(x, z), rotation));
        Assert.Equal(error, Assert.Single(match.Tick().OfType<CommandProcessed>()).Result.RejectionCode);
        Assert.Equal(budget, match.World.Credits(Blue));
        Assert.Empty(match.World.ConstructionSites);
    }

    [Fact]
    public void CompletedReserveMaintainsVictoryConditionAndDoesNotDoubleIncome()
    {
        var terrain = new Terrain();
        var match = Match(terrain);
        match.Schedule(new BuildCommand(Blue, 1, 1, BlueHq, BuildingKind.Headquarters, Site));
        match.Schedule(new BuildCommand(Blue, 2, 2, BlueHq, BuildingKind.Headquarters, new SimVector(-12, 16)));
        match.Tick();
        Assert.Equal("reserve_headquarters_limit", Assert.Single(match.Tick().OfType<CommandProcessed>()).Result.RejectionCode);
        var reserve = Assert.Single(match.Tick().OfType<BuildingCompleted>()).Building;
        Assert.Equal(5, match.World.IncomePerSecond(Blue));
        terrain.Target = BlueHq;
        match.Tick();
        Assert.Equal(MatchResult.Running, match.World.Result);
        Assert.Equal(5, match.World.IncomePerSecond(Blue));
        terrain.Target = reserve.Id;
        while (match.World.Tick < 12 && match.World.Result == MatchResult.Running)
            match.Tick();
        Assert.Equal(MatchResult.PlayerTwoWon, match.World.Result);
    }

    [Fact]
    public void AReserveDueOnTheTickOfDefeatDoesNotSaveThePlayer()
    {
        var terrain = new Terrain();
        var match = Match(terrain);
        match.Schedule(new BuildCommand(Blue, 1, 1, BlueHq, BuildingKind.Headquarters, Site));
        match.Tick();
        match.Tick();
        terrain.Target = BlueHq;
        Assert.Empty(match.Tick().OfType<BuildingCompleted>());
        Assert.Equal(MatchResult.PlayerTwoWon, match.World.Result);
    }

    private static GameSimulation Match(Terrain terrain, int budget = 500) => new(
        new WorldEntities([
            new Building(BlueHq, Blue, new SimVector(-30, 0), BuildingKind.Headquarters, 20),
            new Building(OrangeHq, Orange, new SimVector(30, 0), BuildingKind.Headquarters, 20),
            new Unit(new EntityId(3), Orange, new SimVector(0, 20), UnitKind.Tank, 600)
        ], construction: new ConstructionLayout(Rect(-40, -25, 40, 25),
            ImmutableDictionary<PlayerId, ConstructionBounds>.Empty.Add(Blue, Rect(-40, -25, 0, 25))
                .Add(Orange, Rect(0, -25, 40, 25)), [Rect(-22, 10, -18, 14)])),
        new GameConfiguration(1, 20, budget, 5, 0, 10, 6, 0.7m,
            [new(UnitKind.InfantrySquad, 400, 100, 2, 4, 12, 10, 20), new(UnitKind.Tank, 600, 100, 2, 3, 80, 45, 1)],
            [new(BuildingKind.Headquarters, 100, new(100, 3, 4, 4)), new(BuildingKind.Barracks, 700, new(100, 3, 4, 6)),
                new(BuildingKind.TankFactory, 1000, new(100, 3, 4, 6)), new(BuildingKind.Airport, 900, new(100, 3, 6, 6))]),
        terrain, terrain);

    private static ConstructionBounds Rect(float left, float near, float right, float far) =>
        new(new SimVector(left, near), new SimVector(right, far));

    private sealed class Terrain : INavigationService, ILineOfSightService
    {
        public EntityId? Target { get; set; }
        public IReadOnlyList<SimVector> FindPath(SimVector start, SimVector destination, int movementMask) => [destination];
        public bool HasLineOfSight(SimVector origin, SimVector target, EntityId targetId) => targetId == Target;
    }
}
