using IronDominion.Domain;
using Xunit;

namespace IronDominion.Simulation.Tests;

public sealed class EconomyTests
{
    private static readonly PlayerId Blue = new(1);
    private static readonly PlayerId Orange = new(2);
    private static readonly EntityId Point = new(11);
    private static readonly EntityId Headquarters = new(7);
    private static readonly EntityId BlueInfantry = new(1);
    private static readonly EntityId OrangeInfantry = new(2);

    private static readonly GameConfiguration Configuration = new(
        1, 20, 0, 5, 2, 10, 6, 0.7m,
        [new(UnitKind.InfantrySquad, 400, 100, 160, 4, 12, 10, 20),
            new(UnitKind.Tank, 600, 500, 400, 3, 16, 45, 40)],
        [new(BuildingKind.Headquarters, 1500), new(BuildingKind.Barracks, 700)]);

    [Fact]
    public void HeadquartersAccruesFiveCreditsInTwentyTicks()
    {
        var match = Match(new Building(Headquarters, Blue, default, BuildingKind.Headquarters, 1500),
            new Building(new EntityId(8), Orange, default, BuildingKind.Barracks, 700));
        var initial = match.World;
        Assert.Equal(5, initial.IncomePerSecond(Blue));

        Assert.Empty(Advance(match, 3));
        Assert.Equal(0, match.World.Credits(Blue));
        Assert.Equal(new CreditsChanged(4, Blue, 1, 1), Assert.Single(match.Tick()));
        Advance(match, 16);
        Assert.Equal(5, match.World.Credits(Blue));
        Assert.Equal(0, match.World.Credits(Orange));
        Assert.Equal(0, initial.Credits(Blue));
    }

    [Fact]
    public void DestroyedHeadquartersStopsEarningOnTheDamageTick()
    {
        var sight = new Sight();
        var match = new GameSimulation(new WorldEntities([
            new Building(Headquarters, Blue, default, BuildingKind.Headquarters, 45),
            new Unit(OrangeInfantry, Orange, new SimVector(5, 0), UnitKind.Tank, 600)
        ]), Configuration, new Navigation(), sight);
        Advance(match, 7);
        Assert.Equal(1, match.World.Credits(Blue));

        sight.Clear = true;
        Assert.Contains(new EntityDestroyed(8, Headquarters), match.Tick());
        Advance(match, 20);
        Assert.Equal(0, match.World.IncomePerSecond(Blue));
        Assert.Equal(1, match.World.Credits(Blue));
    }

    [Fact]
    public void UncontestedInfantryCapturesAtTickTwoHundredAtTheInclusiveRadius()
    {
        var match = Match(Infantry(BlueInfantry, Blue, 6),
            Infantry(OrangeInfantry, Orange, 0, hp: 0),
            new Unit(new EntityId(3), Orange, default, UnitKind.Tank, 600),
            new Sector(Point, null, default));
        Advance(match, 199);
        var before = match.World;
        var pending = Assert.Single(before.Sectors);
        Assert.Equal(199, pending.CaptureTicks);
        Assert.Null(pending.Owner);
        Assert.Equal(0, before.Credits(Blue));

        Assert.Equal(new SectorCaptured(200, Point, null, Blue), Assert.Single(match.Tick()));
        Assert.Equal(Blue, Assert.Single(match.World.Sectors).Owner);
        Assert.Equal(2, match.World.IncomePerSecond(Blue));
        Advance(match, 20);
        Assert.Equal(2, match.World.Credits(Blue));
        Assert.Equal(0, match.World.Credits(Orange));
        Assert.Equal(pending, Assert.Single(before.Sectors));
    }

    [Fact]
    public void OpposingInfantryKeepsAnUnclaimedSectorContested()
    {
        var match = Match(Infantry(BlueInfantry, Blue, 6), Infantry(OrangeInfantry, Orange, -6),
            new Sector(Point, null, default));

        Assert.Empty(Advance(match, 200));
        var sector = Assert.Single(match.World.Sectors);
        Assert.True(sector.Contested);
        Assert.Null(sector.Owner);
        Assert.Null(sector.CapturingPlayer);
        Assert.Equal(0, sector.CaptureTicks);
    }

    [Fact]
    public void ContestOnTheFinalTickPausesThenTheSoleRivalReversesProgress()
    {
        var match = Match(Infantry(BlueInfantry, Blue, 6), Infantry(OrangeInfantry, Orange, -6.2f),
            new Sector(Point, null, default));
        Advance(match, 199);

        match.Schedule(new MoveCommand(Orange, 200, 1, [OrangeInfantry], new SimVector(-6, 0)));
        Assert.Empty(match.Tick().OfType<SectorCaptured>());
        var contested = Assert.Single(match.World.Sectors);
        Assert.True(contested.Contested);
        Assert.Equal(199, contested.CaptureTicks);
        Assert.Null(contested.Owner);

        match.Schedule(new MoveCommand(Blue, 201, 1, [BlueInfantry], new SimVector(6.2f, 0)));
        match.Tick();
        var reversing = Assert.Single(match.World.Sectors);
        Assert.False(reversing.Contested);
        Assert.Equal(198, reversing.CaptureTicks);
        Assert.Equal(Blue, reversing.CapturingPlayer);
        Assert.Empty(Advance(match, 198));
        Assert.Equal(0, Assert.Single(match.World.Sectors).CaptureTicks);
        Assert.Empty(Advance(match, 199));
        Assert.Equal(new SectorCaptured(599, Point, null, Orange), Assert.Single(match.Tick()));
    }

    [Fact]
    public void LeavingTheSectorResetsIncompleteCapture()
    {
        var match = Match(Infantry(BlueInfantry, Blue, 6), new Sector(Point, null, default));
        Advance(match, 50);
        match.Schedule(new MoveCommand(Blue, 51, 1, [BlueInfantry], new SimVector(6.2f, 0)));
        match.Tick();

        var empty = Assert.Single(match.World.Sectors);
        Assert.Equal(0, empty.CaptureTicks);
        Assert.Null(empty.CapturingPlayer);
    }

    [Fact]
    public void IncomeTransfersOnlyWhenRecaptureCompletes()
    {
        var match = Match(Infantry(OrangeInfantry, Orange, 6), new Sector(Point, Blue, default));
        Advance(match, 199);

        Assert.Equal(Blue, Assert.Single(match.World.Sectors).Owner);
        Assert.Equal(19, match.World.Credits(Blue));
        Assert.Equal(0, match.World.Credits(Orange));
        Assert.Equal(new SectorCaptured(200, Point, Blue, Orange), Assert.Single(match.Tick()));
        Assert.Equal(0, match.World.IncomePerSecond(Blue));
        Assert.Equal(2, match.World.IncomePerSecond(Orange));
        Advance(match, 20);
        Assert.Equal(19, match.World.Credits(Blue));
        Assert.Equal(2, match.World.Credits(Orange));
    }

    private static Unit Infantry(EntityId id, PlayerId player, float x, int hp = 400) =>
        new(id, player, new SimVector(x, 0), UnitKind.InfantrySquad, hp);

    private static GameSimulation Match(params WorldEntity[] entities) =>
        new(new WorldEntities(entities), Configuration, new Navigation(), new Sight());

    private static IGameEvent[] Advance(GameSimulation simulation, int ticks) =>
        Enumerable.Range(0, ticks).SelectMany(_ => simulation.Tick()).ToArray();

    private sealed class Navigation : INavigationService
    {
        public IReadOnlyList<SimVector> FindPath(SimVector start, SimVector destination, int movementMask) => [destination];
    }

    private sealed class Sight : ILineOfSightService
    {
        public bool Clear { get; set; }

        public bool HasLineOfSight(SimVector origin, SimVector target, EntityId targetId) => Clear;
    }
}
