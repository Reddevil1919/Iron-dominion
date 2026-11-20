using Xunit;

namespace IronDominion.Domain.Tests;

public sealed class WorldEntitiesTests
{
    [Fact]
    public void Index_LooksUpEntitiesAndPreservesNeutralSector()
    {
        var owner = new PlayerId(1);
        var unit = new Unit(new EntityId(1), owner, new SimVector(8, -6), UnitKind.Tank, 600);
        var headquarters = new Building(new EntityId(2), owner, default, BuildingKind.Headquarters, 1500);
        var sector = new Sector(new EntityId(3), null, default);
        var entities = new WorldEntities([unit, headquarters, sector]);

        Assert.Same(unit, entities.Find(unit.Id));
        Assert.Equal(owner, unit.Owner);
        Assert.Equal(600, unit.HitPoints);
        Assert.Equal(new SimVector(8, -6), unit.Position);
        Assert.Same(headquarters, entities.Find(headquarters.Id));
        Assert.Equal(owner, headquarters.Owner);
        Assert.Same(sector, entities.Find(sector.Id));
        Assert.Null(sector.Owner);
        Assert.Null(entities.Find(new EntityId(4)));
    }

    [Fact]
    public void Index_RejectsDuplicateIdsAcrossEntityKinds()
    {
        var id = new EntityId(1);
        var unit = new Unit(id, new PlayerId(1), default, UnitKind.InfantrySquad, 400);
        var sector = new Sector(id, null, default);

        Assert.Throws<ArgumentException>("entities", () => new WorldEntities([unit, sector]));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Ids_RejectNonpositiveValues(int value)
    {
        Assert.Throws<ArgumentOutOfRangeException>("value", () => new EntityId(value));
        Assert.Throws<ArgumentOutOfRangeException>("value", () => new PlayerId(value));
    }

    [Fact]
    public void Entities_RejectDefaultIdsAndOwners()
    {
        Assert.Throws<ArgumentOutOfRangeException>("id", () => new Sector(default, null, default));
        Assert.Throws<ArgumentOutOfRangeException>("owner",
            () => new Unit(new EntityId(1), default, default, UnitKind.Tank, 600));
        Assert.Throws<ArgumentOutOfRangeException>("owner",
            () => new Building(new EntityId(1), default, default, BuildingKind.Barracks, 700));
        Assert.Throws<ArgumentOutOfRangeException>("id", () => new Player(default, 500));
    }

    [Fact]
    public void PlayerAndEntities_AllowZeroAndRejectNegativeCreditsOrHitPoints()
    {
        var owner = new PlayerId(1);
        var id = new EntityId(1);

        Assert.Equal(0, new Player(owner, 0).Credits);
        Assert.Equal(0, new Unit(id, owner, default, UnitKind.Tank, 0).HitPoints);
        Assert.Equal(0, new Building(id, owner, default, BuildingKind.Headquarters, 0).HitPoints);
        Assert.Throws<ArgumentOutOfRangeException>("credits", () => new Player(owner, -1));
        Assert.Throws<ArgumentOutOfRangeException>("hitPoints",
            () => new Unit(id, owner, default, UnitKind.Tank, -1));
        Assert.Throws<ArgumentOutOfRangeException>("hitPoints",
            () => new Building(id, owner, default, BuildingKind.Headquarters, -1));
    }

    [Fact]
    public void Position_RejectsNonfiniteCoordinates()
    {
        Assert.Throws<ArgumentOutOfRangeException>("x", () => new SimVector(float.NaN, 0));
        Assert.Throws<ArgumentOutOfRangeException>("z", () => new SimVector(0, float.PositiveInfinity));
    }
}
