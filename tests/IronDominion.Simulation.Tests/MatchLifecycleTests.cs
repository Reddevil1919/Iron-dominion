using IronDominion.Domain;
using Xunit;

namespace IronDominion.Simulation.Tests;

public sealed class MatchLifecycleTests
{
    private static readonly PlayerId Blue = new(1);
    private static readonly PlayerId Orange = new(2);
    private static readonly EntityId BlueTank = new(1);
    private static readonly EntityId OrangeTank = new(2);
    private static readonly EntityId BlueHeadquarters = new(7);
    private static readonly EntityId OrangeHeadquarters = new(8);

    private static readonly GameConfiguration Configuration = new(
        1, 20, 500, 5, 2, 10, 6, 0.7m,
        [new(UnitKind.InfantrySquad, 400, 100, 160, 4, 12, 10, 20),
            new(UnitKind.Tank, 600, 500, 400, 3, 16, 45, 40)],
        [new(BuildingKind.Headquarters, 1500), new(BuildingKind.Barracks, 700)]);

    [Theory]
    [InlineData(1500, 45, MatchResult.PlayerOneWon)]
    [InlineData(45, 1500, MatchResult.PlayerTwoWon)]
    [InlineData(45, 45, MatchResult.Draw)]
    public void ResolvesTheResultAfterAllSimultaneousDamage(int blueHp, int orangeHp, MatchResult result)
    {
        var match = CreateMatch(blueHp, orangeHp);
        var before = match.World;

        var events = match.Tick();

        Assert.Equal(MatchResult.Running, before.Result);
        Assert.Equal(result, match.World.Result);
        Assert.Equal(1, match.World.Tick);
        Assert.Equal(new MatchEnded(1, result), Assert.IsType<MatchEnded>(events[^1]));
        var destroyed = events.OfType<EntityDestroyed>().ToArray();
        Assert.Equal(blueHp == orangeHp ? 2 : 1, destroyed.Length);
        Assert.Equal(blueHp == 45, destroyed.Any(death => death.Entity == BlueHeadquarters));
        Assert.Equal(orangeHp == 45, destroyed.Any(death => death.Entity == OrangeHeadquarters));
    }

    [Fact]
    public void FinishedMatchIgnoresPendingAndNewCommandsAndPublishesNoFurtherEvents()
    {
        var match = CreateMatch(1500, 45);
        var destination = new SimVector(0, 12);
        match.Schedule(new MoveCommand(Blue, 2, 1, [BlueTank], destination));
        Assert.Single(match.Tick().OfType<MatchEnded>());
        var finished = match.World;

        match.Schedule(new MoveCommand(Blue, 1, 1, [BlueTank], destination));
        match.Schedule(new MoveCommand(Blue, 4, 2, [BlueTank], destination));
        for (var tick = 0; tick < 25; tick++)
            Assert.Empty(match.Tick());

        Assert.Same(finished, match.World);
        Assert.Equal(500, match.World.Credits(Blue));
        Assert.True(match.World.TryGetUnitPosition(BlueTank, out var position));
        Assert.Equal(new SimVector(44, 0), position);
    }

    [Fact]
    public void DestructionOfTheOnlyEnemyBarracksLeavesTheMatchRunning()
    {
        var match = new GameSimulation(new WorldEntities([
                new Building(BlueHeadquarters, Blue, new SimVector(-50, 0), BuildingKind.Headquarters, 1500),
                new Building(OrangeHeadquarters, Orange, new SimVector(50, 0), BuildingKind.Headquarters, 1500),
                new Building(new EntityId(9), Orange, new SimVector(28, 0), BuildingKind.Barracks, 45),
                new Unit(BlueTank, Blue, new SimVector(25, 0), UnitKind.Tank, 600)
            ]), Configuration, new DirectNavigation(), new ClearSight());

        var events = match.Tick();

        Assert.Equal(new EntityId(9), Assert.Single(events.OfType<EntityDestroyed>()).Entity);
        Assert.Empty(events.OfType<MatchEnded>());
        Assert.Equal(MatchResult.Running, match.World.Result);
        match.Tick();
        Assert.Equal(2, match.World.Tick);
    }

    private static GameSimulation CreateMatch(int blueHp, int orangeHp) =>
        new(new WorldEntities([
            new Building(BlueHeadquarters, Blue, new SimVector(-50, 0), BuildingKind.Headquarters, blueHp),
            new Building(OrangeHeadquarters, Orange, new SimVector(50, 0), BuildingKind.Headquarters, orangeHp),
            new Unit(BlueTank, Blue, new SimVector(44, 0), UnitKind.Tank, 600),
            new Unit(OrangeTank, Orange, new SimVector(-44, 0), UnitKind.Tank, 600)
        ]), Configuration, new DirectNavigation(), new ClearSight());

    private sealed class DirectNavigation : INavigationService
    {
        public IReadOnlyList<SimVector> FindPath(SimVector start, SimVector destination, int movementMask) => [destination];
    }

    private sealed class ClearSight : ILineOfSightService
    {
        public bool HasLineOfSight(SimVector origin, SimVector target, EntityId targetId) => true;
    }
}
