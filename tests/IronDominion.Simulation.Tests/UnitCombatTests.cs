using IronDominion.Domain;
using Xunit;

namespace IronDominion.Simulation.Tests;

public sealed class UnitCombatTests
{
    private static readonly PlayerId Blue = new(1);
    private static readonly PlayerId Orange = new(2);
    private static readonly EntityId First = new(1);
    private static readonly EntityId Second = new(2);
    private static readonly EntityId Third = new(3);

    private static readonly GameConfiguration Configuration = new(
        1, 20, 500, 5, 2, 10, 6, 0.7m,
        [new(UnitKind.InfantrySquad, 400, 100, 160, 4, 12, 10, 20),
            new(UnitKind.Tank, 600, 500, 400, 3, 16, 45, 40)],
        [new(BuildingKind.Headquarters, 1500), new(BuildingKind.Barracks, 700)]);

    [Fact]
    public void TankFiresOncePerCooldownAndOldSnapshotsKeepTheirHealth()
    {
        var match = Match(new Sight(), Tank(First, Blue, 0), Tank(Second, Orange, 16));
        var before = match.World;
        var first = match.Tick().OfType<ShotFired>().ToArray();
        Assert.Equal(2, first.Length);
        Assert.All(first, shot => Assert.Equal(1, shot.Tick));

        for (var i = 0; i < 39; i++)
            Assert.Empty(match.Tick());
        Assert.All(match.Tick().OfType<ShotFired>(), shot => Assert.Equal(41, shot.Tick));
        Assert.Equal(510, HitPoints(match.World, Second));
        Assert.Equal(600, HitPoints(before, Second));
    }

    [Fact]
    public void OutOfRangeTargetTakesNoDamage()
    {
        var match = Match(new Sight(), Tank(First, Blue, 0), Tank(Second, Orange, 17));
        match.Schedule(new AttackCommand(Blue, 1, 1, [First], Second));
        var result = Assert.IsType<CommandProcessed>(Assert.Single(match.Tick()));
        Assert.True(result.Result.Accepted);
        Assert.Equal(600, HitPoints(match.World, Second));
    }

    [Fact]
    public void ObstacleRejectsAnUnseenTargetAndPreventsDamage()
    {
        var match = Match(new Sight { Clear = false }, Tank(First, Blue, 0), Tank(Second, Orange, 8));
        match.Schedule(new AttackCommand(Blue, 1, 1, [First], Second));

        var result = Assert.IsType<CommandProcessed>(Assert.Single(match.Tick()));
        Assert.Equal(new CommandResult(false, "target_not_visible"), result.Result);
        Assert.Equal(600, HitPoints(match.World, Second));
    }

    [Fact]
    public void AnAcceptedOrderChecksLineOfSightAgainBeforeFiring()
    {
        var sight = new Sight();
        var match = Match(sight, Tank(First, Blue, 0), Tank(Second, Orange, 8));
        match.Schedule(new AttackCommand(Blue, 1, 1, [First], Second));
        Assert.Contains(match.Tick(), result => result is ShotFired { Attacker: var attacker } && attacker == First);

        sight.Clear = false;
        for (var tick = 0; tick < 42; tick++)
            Assert.Empty(match.Tick());
        Assert.Equal(555, HitPoints(match.World, Second));
    }

    [Fact]
    public void AnOrderedTargetTakesPriorityOverANearerEnemy()
    {
        var match = Match(new Sight(), Tank(First, Blue, 0), Tank(Second, Orange, 2), Tank(Third, Orange, 8));
        match.Schedule(new AttackCommand(Blue, 1, 1, [First], Third));

        var firstShot = Assert.Single(match.Tick().OfType<ShotFired>(), shot => shot.Attacker == First);
        Assert.Equal(Third, firstShot.Target);
        Assert.Equal(600, HitPoints(match.World, Second));
        Assert.Equal(555, HitPoints(match.World, Third));
    }

    [Fact]
    public void EquidistantTargetsAreSelectedByEntityId()
    {
        var match = Match(new Sight(), Tank(Third, Orange, 8), Tank(Second, Orange, -8), Tank(First, Blue, 0));
        var firstShot = Assert.Single(match.Tick().OfType<ShotFired>(), shot => shot.Attacker == First);
        Assert.Equal(Second, firstShot.Target);
    }

    [Fact]
    public void LethalHitsOnTheSameTickDestroyBothUnitsAndBlockLaterOrders()
    {
        var match = Match(new Sight(), Tank(First, Blue, 0, 40), Tank(Second, Orange, 8, 40));
        var events = match.Tick();
        Assert.Equal<EntityId>([First, Second], events.OfType<EntityDestroyed>().Select(eventData => eventData.Entity));
        Assert.All(events.OfType<EntityDamaged>(), damage =>
        {
            Assert.Equal(40, damage.Damage);
            Assert.Equal(0, damage.RemainingHitPoints);
        });
        Assert.False(match.World.TryGetUnitPosition(First, out _));
        Assert.Equal(0, HitPoints(match.World, Second));

        match.Schedule(new MoveCommand(Blue, 2, 1, [First], default));
        var result = Assert.IsType<CommandProcessed>(Assert.Single(match.Tick()));
        Assert.Equal(new CommandResult(false, "actor_destroyed"), result.Result);
        Assert.Empty(match.Tick());
    }

    [Fact]
    public void TwoShotsOnOneHeadquartersClampDamageAndDestroyItOnce()
    {
        var headquarters = new Building(Third, Orange, new SimVector(8, 0), BuildingKind.Headquarters, 70);
        var match = Match(new Sight(), Tank(First, Blue, 0), Tank(Second, Blue, 1), headquarters);
        var events = match.Tick();

        Assert.Equal(2, events.OfType<ShotFired>().Count());
        Assert.Equal(new EntityDamaged(1, Third, 70, 0), Assert.Single(events.OfType<EntityDamaged>()));
        Assert.Equal(new EntityDestroyed(1, Third), Assert.Single(events.OfType<EntityDestroyed>()));
        Assert.Empty(match.Tick());
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(99)]
    public void FriendlyDestroyedAndUnknownTargetsAreRejected(int targetId)
    {
        var match = Match(new Sight(), Tank(First, Blue, 0), Tank(Second, Blue, 2), Tank(Third, Orange, 8, 0));
        match.Schedule(new AttackCommand(Blue, 1, 1, [First], new EntityId(targetId)));
        var result = Assert.IsType<CommandProcessed>(Assert.Single(match.Tick()));
        Assert.Equal(new CommandResult(false, "invalid_target"), result.Result);
    }

    private static Unit Tank(EntityId id, PlayerId player, float x, int hp = 600) =>
        new(id, player, new SimVector(x, 0), UnitKind.Tank, hp);

    private static GameSimulation Match(ILineOfSightService sight, params WorldEntity[] entities) =>
        new(new WorldEntities(entities), Configuration, new StraightNavigation(), sight);

    private static int HitPoints(IReadOnlyWorld world, EntityId id)
    {
        Assert.True(world.TryGetHitPoints(id, out var hp));
        return hp;
    }

    private sealed class Sight : ILineOfSightService
    {
        public bool Clear { get; set; } = true;

        public bool HasLineOfSight(SimVector origin, SimVector target, EntityId targetId) => Clear;
    }

    private sealed class StraightNavigation : INavigationService
    {
        public IReadOnlyList<SimVector> FindPath(SimVector start, SimVector destination, int movementMask) => [destination];
    }
}
