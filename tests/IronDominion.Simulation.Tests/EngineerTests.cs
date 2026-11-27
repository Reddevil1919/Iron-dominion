using IronDominion.Domain;
using Xunit;

namespace IronDominion.Simulation.Tests;

public sealed class EngineerTests
{
    private static readonly PlayerId Blue = new(1);
    private static readonly PlayerId Orange = new(2);
    private static readonly EntityId Engineer = new(1);
    private static readonly EntityId Tank = new(2);
    private static readonly EntityId Opponent = new(3);
    private static readonly EntityId Barracks = new(4);
    private static readonly GameConfiguration Config = new(1, 20, 500, 0, 0, 10, 6, 0.7m,
        [new(UnitKind.InfantrySquad, 400, 100, 2, 4, 12, 10, 20),
         new(UnitKind.Tank, 600, 500, 4, 3, 16, 45, 40),
         new(UnitKind.EngineerSquad, 300, 150, 3, 4, 0, 0, 0)],
        [new(BuildingKind.Headquarters, 1500), new(BuildingKind.Barracks, 700)]);

    [Fact]
    public void ApproachesRepairsAndStopsAtMaximumHealthAfterRestore()
    {
        var terrain = new Terrain { EnemyCanFire = true };
        var map = MakeMap();
        var match = new GameSimulation(map, Config, terrain, terrain);
        match.Tick();
        Assert.Equal(555, Health(match));
        terrain.EnemyCanFire = false;
        match.Schedule(new RepairCommand(Blue, 2, 1, [Engineer], Tank));
        Assert.True(Assert.Single(match.Tick().OfType<CommandProcessed>()).Result.Accepted);
        Assert.Null(match.World.RepairingTank(Engineer));
        Assert.Equal(555, Health(match));
        var approach = match.Capture();
        Assert.Equal(Tank, approach.Units.Single(unit => unit.Id == Engineer).RepairTarget);
        match = GameSimulation.Restore(map, Config, terrain, terrain, approach);

        var pulses = Enumerable.Range(0, 120).SelectMany(_ => match.Tick()).OfType<TankRepaired>().ToArray();
        Assert.Equal(45, pulses.Sum(pulse => pulse.RestoredHitPoints));
        Assert.All(pulses, pulse => Assert.Equal(0L, pulse.Tick % 4));
        Assert.Equal(600, Health(match));
        Assert.Null(match.World.RepairingTank(Engineer));
        Assert.Null(match.Capture().Units.Single(unit => unit.Id == Engineer).RepairTarget);
    }

    [Fact]
    public void HoldsCancelRepairsAndEngineersCannotFireOrRepairEnemies()
    {
        var terrain = new Terrain { EnemyCanFire = true };
        var match = new GameSimulation(MakeMap(new SimVector(-4.65f, 0)), Config, terrain, terrain);
        Assert.DoesNotContain(match.Tick().OfType<ShotFired>(), shot => shot.Attacker == Engineer);
        terrain.EnemyCanFire = false;
        match.Schedule(new AttackCommand(Blue, 2, 1, [Engineer], Opponent));
        match.Schedule(new RepairCommand(Blue, 2, 2, [Engineer], Opponent));
        match.Schedule(new RepairCommand(Blue, 2, 3, [Tank], Tank));
        match.Schedule(new RepairCommand(Blue, 2, 4, [Engineer], Tank));
        Assert.Collection(match.Tick().OfType<CommandProcessed>(),
            item => Assert.Equal("unit_cannot_attack", item.Result.RejectionCode),
            item => Assert.Equal("invalid_repair_target", item.Result.RejectionCode),
            item => Assert.Equal("unit_cannot_repair", item.Result.RejectionCode),
            item => Assert.True(item.Result.Accepted));
        Assert.Equal(Tank, match.World.RepairingTank(Engineer));
        match.Tick();
        Assert.Single(match.Tick().OfType<TankRepaired>());
        var repaired = Health(match);
        match.Schedule(new HoldPositionCommand(Blue, 5, 5, [Engineer]));
        Assert.All(Enumerable.Range(0, 12).SelectMany(_ => match.Tick()), item => Assert.IsNotType<TankRepaired>(item));
        Assert.Equal(repaired, Health(match));
        Assert.Null(match.World.RepairingTank(Engineer));
    }

    [Fact]
    public void BarracksShareOneTrainingQueueForInfantryAndEngineers()
    {
        var terrain = new Terrain();
        var match = new GameSimulation(MakeMap(), Config, terrain, terrain);
        match.Schedule(new TrainUnitCommand(Blue, 1, 1, Barracks, UnitKind.InfantrySquad));
        match.Schedule(new TrainUnitCommand(Blue, 1, 2, Barracks, UnitKind.EngineerSquad));
        Assert.All(match.Tick().OfType<CommandProcessed>(), result => Assert.True(result.Result.Accepted));
        Assert.Equal(250, match.World.Credits(Blue));
        Assert.Collection(match.World.TrainingQueue(Barracks),
            order => Assert.Equal(UnitKind.InfantrySquad, order.Kind),
            order => Assert.Equal(UnitKind.EngineerSquad, order.Kind));
        var infantry = Assert.Single(match.Tick().OfType<UnitSpawned>()).Unit;
        Assert.Equal(UnitKind.InfantrySquad, infantry.Kind);
        match.Schedule(new MoveCommand(Blue, 3, 3, [infantry.Id], new SimVector(-37, 35)));
        var engineer = Assert.Single(Enumerable.Range(0, 60).SelectMany(_ => match.Tick()).OfType<UnitSpawned>()).Unit;
        Assert.Equal(UnitKind.EngineerSquad, engineer.Kind);
        Assert.Equal(300, engineer.HitPoints);
    }

    private static int Health(GameSimulation match)
    {
        Assert.True(match.World.TryGetHitPoints(Tank, out var hp));
        return hp;
    }

    private static WorldEntities MakeMap(SimVector? engineerPosition = null) => new([
        new Unit(Engineer, Blue, engineerPosition ?? new SimVector(-14, 0), UnitKind.EngineerSquad, 300),
        new Unit(Tank, Blue, default, UnitKind.Tank, 600),
        new Unit(Opponent, Orange, new SimVector(12, 0), UnitKind.Tank, 600),
        new Building(Barracks, Blue, new SimVector(-37, 35), BuildingKind.Barracks, 700, new SimVector(-37, 20))
    ]);

    private sealed class Terrain : INavigationService, ILineOfSightService
    {
        public bool EnemyCanFire { get; set; }
        public IReadOnlyList<SimVector> FindPath(SimVector start, SimVector destination, int movementMask) => [destination];
        public bool HasLineOfSight(SimVector origin, SimVector target, EntityId targetId) =>
            origin.X < 0f && targetId == Tank || EnemyCanFire && origin.X > 0f && targetId == Tank;
    }
}
