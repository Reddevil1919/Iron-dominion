using IronDominion.Domain;
using Xunit;

namespace IronDominion.Application.Tests;

public sealed class UnitSelectionTests
{
    private static readonly PlayerId Blue = new(1);
    private static readonly PlayerId Orange = new(2);
    private static readonly EntityId BlueInfantry = new(1);
    private static readonly EntityId BlueTank = new(2);
    private static readonly EntityId Enemy = new(3);
    private static readonly EntityId Dead = new(4);
    private static readonly EntityId Headquarters = new(5);

    [Fact]
    public void Select_OnlyKeepsLiveOwnedUnitsInIdOrder()
    {
        var selection = CreateSelection();

        selection.Select([BlueTank, Enemy, Headquarters, BlueInfantry, BlueTank, Dead, new EntityId(8)]);

        Assert.Equal<EntityId>([BlueInfantry, BlueTank], selection.Selected);
    }

    [Fact]
    public void Select_WithoutShiftReplacesAndCanClear()
    {
        var selection = CreateSelection();
        selection.Select([BlueInfantry, BlueTank]);
        selection.Select([BlueTank]);

        Assert.Equal<EntityId>([BlueTank], selection.Selected);

        selection.Select([Enemy]);

        Assert.Empty(selection.Selected);
    }

    [Fact]
    public void Select_WithShiftTogglesOwnedUnitsOnce()
    {
        var selection = CreateSelection();
        selection.Select([BlueInfantry]);
        selection.Select([BlueInfantry, BlueInfantry, BlueTank, Enemy, Dead], toggle: true);

        Assert.Equal<EntityId>([BlueTank], selection.Selected);

        selection.Select([], toggle: true);

        Assert.Equal<EntityId>([BlueTank], selection.Selected);
    }

    [Fact]
    public void ControlGroups_StoreIndependentSnapshots()
    {
        var selection = CreateSelection();
        selection.Select([BlueInfantry, BlueTank]);
        selection.AssignGroup(1);
        selection.Select([BlueTank]);
        selection.AssignGroup(9);

        selection.RecallGroup(1);
        Assert.Equal<EntityId>([BlueInfantry, BlueTank], selection.Selected);
        selection.RecallGroup(9);
        Assert.Equal<EntityId>([BlueTank], selection.Selected);
    }

    [Fact]
    public void AssignGroup_ReplacesTheStoredSelectionAndEmptyAssignmentClearsIt()
    {
        var selection = CreateSelection();
        selection.Select([BlueInfantry]);
        selection.AssignGroup(2);
        selection.Select([BlueTank]);
        selection.AssignGroup(2);
        selection.Select([]);
        selection.RecallGroup(2);

        Assert.Equal<EntityId>([BlueTank], selection.Selected);

        selection.Select([]);
        selection.AssignGroup(2);
        selection.Select([BlueInfantry]);
        selection.RecallGroup(2);

        Assert.Equal<EntityId>([BlueInfantry], selection.Selected);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    public void ControlGroups_RejectNumbersOutsideOneThroughNine(int number)
    {
        var selection = CreateSelection();

        Assert.Throws<ArgumentOutOfRangeException>("number", () => selection.AssignGroup(number));
        Assert.Throws<ArgumentOutOfRangeException>("number", () => selection.RecallGroup(number));
    }

    private static UnitSelection CreateSelection() => new(Blue, new WorldEntities(
    [
        new Unit(BlueInfantry, Blue, default, UnitKind.InfantrySquad, 400),
        new Unit(BlueTank, Blue, default, UnitKind.Tank, 600),
        new Unit(Enemy, Orange, default, UnitKind.InfantrySquad, 400),
        new Unit(Dead, Blue, default, UnitKind.InfantrySquad, 0),
        new Building(Headquarters, Blue, default, BuildingKind.Headquarters, 1500)
    ]));
}
