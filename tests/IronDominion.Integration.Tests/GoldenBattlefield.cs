using IronDominion.Domain;

namespace IronDominion.Integration.Tests;

internal sealed class GoldenBattlefield(int seed) : INavigationService, ILineOfSightService
{
    public static readonly PlayerId Blue = new(1);
    public static readonly PlayerId Orange = new(2);
    public static readonly EntityId BlueInfantry = new(1);
    public static readonly EntityId BlueTank = new(3);
    public static readonly EntityId OrangeTank = new(6);
    public static readonly EntityId BlueHq = new(7);
    public static readonly EntityId OrangeHq = new(8);
    public static readonly EntityId BlueBarracks = new(9);
    public static readonly EntityId Alpha = new(11);
    public static readonly EntityId Bravo = new(12);
    public static readonly SimVector BlueExit = new(-44, 36);
    public static readonly SimVector BlockedGoal = default;
    public static readonly SimVector BlueTankStart = new(-15, 0);
    public static readonly SimVector AcrossWall = new(4, 0);
    public static readonly SimVector StagingPoint = new(28, -36);
    public static readonly SimVector FiringPosition = new(44, -14);
    public static readonly SimVector AlphaPoint = new(-22, -10);
    public SimVector Waypoint { get; } = new(0, seed % 2 == 0 ? -9 : 9);

    public static WorldEntities CreateWorld() => new([
        new Unit(BlueInfantry, Blue, new SimVector(-26, -4), UnitKind.InfantrySquad, 400),
        new Unit(new EntityId(2), Blue, new SimVector(-15, -14), UnitKind.InfantrySquad, 400),
        new Unit(BlueTank, Blue, BlueTankStart, UnitKind.Tank, 600),
        new Unit(new EntityId(4), Orange, new SimVector(24, 0), UnitKind.InfantrySquad, 400),
        new Unit(new EntityId(5), Orange, new SimVector(24, 16), UnitKind.InfantrySquad, 400),
        new Unit(OrangeTank, Orange, new SimVector(30, -14), UnitKind.Tank, 600),
        new Building(BlueHq, Blue, new SimVector(-44, 0), BuildingKind.Headquarters, 1500, new SimVector(-57, 0)),
        new Building(OrangeHq, Orange, new SimVector(44, 0), BuildingKind.Headquarters, 180, new SimVector(57, 0)),
        new Building(BlueBarracks, Blue, new SimVector(-44, 24), BuildingKind.Barracks, 700, BlueExit),
        new Building(new EntityId(10), Orange, new SimVector(44, 24), BuildingKind.Barracks, 700, new SimVector(44, 36)),
        new Sector(Alpha, null, AlphaPoint),
        new Sector(Bravo, null, new SimVector(24, 24)),
        new Sector(new EntityId(13), null, new SimVector(-4, 20))
    ], [new CoverZone(new SimVector(-25, -13), new SimVector(-19, -7))]);

    public IReadOnlyList<SimVector> FindPath(SimVector start, SimVector destination, int movementMask)
    {
        if (Math.Abs(destination.X) > 60 || Math.Abs(destination.Z) > 60
            || destination.X == 0 && Math.Abs(destination.Z) <= 4)
            return [];

        return CrossesWall(start, destination) ? [Waypoint, destination] : [destination];
    }

    public bool HasLineOfSight(SimVector origin, SimVector target, EntityId targetId) => !CrossesWall(origin, target);

    private static bool CrossesWall(SimVector start, SimVector end)
    {
        if (start.X == end.X)
            return start.X == 0 && Math.Min(start.Z, end.Z) <= 4 && Math.Max(start.Z, end.Z) >= -4;

        var crossing = -(double)start.X / (end.X - start.X);
        return crossing is >= 0 and <= 1 && Math.Abs(start.Z + crossing * (end.Z - start.Z)) <= 4;
    }
}
