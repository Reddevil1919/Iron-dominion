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

    [Theory]
    [InlineData(0f, 7)]
    [InlineData(2f, 7)]
    [InlineData(2.01f, 10)]
    public void InfantryReceivesSevenDamageInsideCoverIncludingItsEdge(float x, int expectedDamage)
    {
        var match = MatchWithCover([CentralCover, CentralCover], Infantry(First, Blue, x), Infantry(Second, Orange, 8));

        var damage = Assert.Single(match.Tick().OfType<EntityDamaged>(), hit => hit.Entity == First);
        Assert.Equal(expectedDamage, damage.Damage);
        Assert.Equal(400 - expectedDamage, HitPoints(match.World, First));
        Assert.Equal(expectedDamage == 7, match.World.HasCover(First));
    }

    [Fact]
    public void TankHitsRoundIndividuallyBeforeSimultaneousDamageIsCombined()
    {
        var match = MatchWithCover([CentralCover],
            Infantry(First, Blue, 0), Tank(Second, Orange, 8), Tank(Third, Orange, 9));

        var damage = Assert.Single(match.Tick().OfType<EntityDamaged>(), hit => hit.Entity == First);
        Assert.Equal(new EntityDamaged(1, First, 64, 336), damage);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TanksAndBuildingsTakeFullDamageInsideCover(bool tank)
    {
        WorldEntity target = tank
            ? Tank(First, Blue, 0)
            : new Building(First, Blue, default, BuildingKind.Barracks, 700);
        var match = MatchWithCover([CentralCover], target, Infantry(Second, Orange, 8));

        var damage = Assert.Single(match.Tick().OfType<EntityDamaged>(), hit => hit.Entity == First);
        Assert.Equal(10, damage.Damage);
        Assert.False(match.World.HasCover(First));
    }

    [Fact]
    public void MovementUpdatesProtectionBeforeFireAndOlderSnapshotsRetainTheirState()
    {
        var match = MatchWithCover([CentralCover], Infantry(First, Blue, 2.1f), Infantry(Second, Orange, 8));
        var before = match.World;
        match.Schedule(new MoveCommand(Blue, 1, 1, [First], new SimVector(2, 0)));

        var inCover = Assert.Single(match.Tick().OfType<EntityDamaged>(), hit => hit.Entity == First);
        Assert.Equal(7, inCover.Damage);
        var covered = match.World;
        Assert.True(covered.HasCover(First));
        Assert.False(before.HasCover(First));

        match.Schedule(new MoveCommand(Blue, 21, 2, [First], new SimVector(2.2f, 0)));
        for (var tick = 0; tick < 19; tick++)
            match.Tick();
        var exposed = Assert.Single(match.Tick().OfType<EntityDamaged>(), hit => hit.Entity == First);
        Assert.Equal(10, exposed.Damage);
        Assert.False(match.World.HasCover(First));
        Assert.True(covered.HasCover(First));
    }

    [Fact]
    public void DestroyedInfantryHasNoCoverInThePublishedSnapshot()
    {
        var match = MatchWithCover([CentralCover], Infantry(First, Blue, 0, 10), Tank(Second, Orange, 8));
        Assert.True(match.World.HasCover(First));

        Assert.Equal(new EntityDestroyed(1, First), Assert.Single(match.Tick().OfType<EntityDestroyed>()));
        Assert.False(match.World.HasCover(First));
    }

    [Fact]
    public void AttackMoveAdvancesToRangeWaitsThroughCooldownThenResumesAfterDestruction()
    {
        var destination = new SimVector(24, 0);
        var match = Match(new Sight(), Tank(First, Blue, 0),
            new Building(Second, Orange, new SimVector(18, 0), BuildingKind.Barracks, 90));
        match.Schedule(new MoveCommand(Blue, 1, 1, [First], destination, attackMove: true));

        for (var i = 0; i < 20 && HitPoints(match.World, Second) == 90; i++)
            match.Tick();

        Assert.Equal(45, HitPoints(match.World, Second));
        var engagement = Position(match, First);
        Assert.InRange(engagement.X, 2f, 2.16f);
        for (var i = 0; i < 39; i++)
        {
            Assert.Empty(match.Tick().OfType<ShotFired>());
            Assert.Equal(engagement, Position(match, First));
        }

        Assert.Single(match.Tick().OfType<EntityDestroyed>());
        Assert.Equal(engagement, Position(match, First));
        match.Tick();
        Assert.True(Position(match, First).X > engagement.X);
        for (var i = 0; i < 160; i++)
            match.Tick();
        Assert.Equal(destination, Position(match, First));
    }

    [Fact]
    public void AttackMovePausesOnlyWhileItHasAnUnobstructedTarget()
    {
        var sight = new Sight { Clear = false };
        var match = Match(sight, Tank(First, Blue, 0),
            new Building(Second, Orange, new SimVector(8, 0), BuildingKind.Barracks, 700));
        match.Schedule(new MoveCommand(Blue, 1, 1, [First], new SimVector(10, 0), attackMove: true));

        Assert.Empty(match.Tick().OfType<ShotFired>());
        var beforeContact = Position(match, First);
        Assert.True(beforeContact.X > 0);
        sight.Clear = true;
        Assert.Single(match.Tick().OfType<ShotFired>());
        Assert.Equal(beforeContact, Position(match, First));

        sight.Clear = false;
        Assert.Empty(match.Tick().OfType<ShotFired>());
        Assert.True(Position(match, First).X > beforeContact.X);
    }

    [Fact]
    public void HoldPositionCancelsAnEngagedAttackMoveAndKeepsAutomaticFire()
    {
        var sight = new Sight { Clear = false };
        var match = Match(sight, Tank(First, Blue, 0),
            new Building(Second, Orange, new SimVector(8, 0), BuildingKind.Barracks, 700));
        match.Schedule(new MoveCommand(Blue, 1, 1, [First], new SimVector(10, 0), attackMove: true));
        match.Tick();
        var held = Position(match, First);
        sight.Clear = true;
        Assert.Single(match.Tick().OfType<ShotFired>());

        match.Schedule(new HoldPositionCommand(Blue, 3, 2, [First]));
        Assert.True(Assert.Single(match.Tick().OfType<CommandProcessed>()).Result.Accepted);
        sight.Clear = false;
        for (var i = 0; i < 38; i++)
        {
            match.Tick();
            Assert.Equal(held, Position(match, First));
        }
        sight.Clear = true;
        Assert.Single(match.Tick().OfType<ShotFired>());
        Assert.Equal(held, Position(match, First));

        match.Schedule(new MoveCommand(Blue, 43, 3, [First], new SimVector(10, 0)));
        match.Tick();
        Assert.True(Position(match, First).X > held.X);
    }

    [Fact]
    public void HoldPositionReleasesFocusAndReturnsToNearestTarget()
    {
        var match = Match(new Sight(), Tank(First, Blue, 0),
            new Building(Second, Orange, new SimVector(2, 0), BuildingKind.Barracks, 700),
            new Building(Third, Orange, new SimVector(8, 0), BuildingKind.Barracks, 700));
        match.Schedule(new AttackCommand(Blue, 1, 1, [First], Third));
        Assert.Equal(Third, Assert.Single(match.Tick().OfType<ShotFired>()).Target);

        match.Schedule(new HoldPositionCommand(Blue, 2, 2, [First]));
        for (var i = 0; i < 39; i++)
            match.Tick();
        Assert.Equal(Second, Assert.Single(match.Tick().OfType<ShotFired>()).Target);
        Assert.Equal(default, Position(match, First));
    }

    [Theory]
    [InlineData(5, 0, 7)]
    [InlineData(5, 5, 7)]
    [InlineData(4, 5, 10)]
    [InlineData(-5, 0, 10)]
    [InlineData(0, 5, 10)]
    [InlineData(0, 0, 10)]
    public void DirectionalCoverProtectsOnlyAgainstItsMarkedArc(float x, float z, int expectedDamage)
    {
        var cover = new CoverZone(new SimVector(-2, -2), new SimVector(2, 2), CoverFacing.East);
        var enemy = new Unit(Second, Orange, new SimVector(x, z), UnitKind.InfantrySquad, 400);
        var match = MatchWithCover([cover], Infantry(First, Blue, 0), enemy);
        var hit = Assert.Single(match.Tick().OfType<EntityDamaged>(), damage => damage.Entity == First);

        Assert.True(match.World.HasCover(First));
        Assert.Equal(expectedDamage, hit.Damage);
    }

    [Fact]
    public void FrontAndFlankTankHitsRoundBeforeCombiningTheirDamage()
    {
        var cover = new CoverZone(new SimVector(-2, -2), new SimVector(2, 2), CoverFacing.East);
        var match = MatchWithCover([cover], Infantry(First, Blue, 0),
            Tank(Second, Orange, 8), Tank(Third, Orange, -8));
        var hit = Assert.Single(match.Tick().OfType<EntityDamaged>(), damage => damage.Entity == First);
        Assert.Equal(77, hit.Damage);
    }

    [Fact]
    public void SuppressionSlowsTheNextTwentyFourTicksAndRecoversWithoutReplanning()
    {
        var sight = new Sight();
        var match = MatchWithSuppression(sight, Infantry(First, Blue, 0), Tank(Second, Orange, 8));
        match.Schedule(new MoveCommand(Blue, 1, 1, [First], new SimVector(0, 20)));
        match.Tick();
        var impacted = match.World;
        Assert.True(impacted.IsSuppressed(First));
        Assert.False(impacted.IsSuppressed(Second));
        Assert.Equal(0.2f, Position(match, First).Z, precision: 4);

        sight.Clear = false;
        for (var tick = 0; tick < 24; tick++)
        {
            var last = Position(match, First).Z;
            match.Tick();
            Assert.True(match.World.IsSuppressed(First));
            Assert.Equal(0.13f, Position(match, First).Z - last, precision: 4);
        }

        var recovery = Position(match, First).Z;
        match.Tick();
        Assert.False(match.World.IsSuppressed(First));
        Assert.Equal(0.2f, Position(match, First).Z - recovery, precision: 4);
        Assert.True(impacted.IsSuppressed(First));
    }

    [Fact]
    public void ASecondHitRefreshesSuppressionWithoutStackingTheMovementPenalty()
    {
        var sight = new Sight { Clear = false };
        var match = MatchWithSuppression(sight, Infantry(First, Blue, 0), Infantry(Second, Orange, 8));
        match.Schedule(new MoveCommand(Blue, 1, 1, [First], new SimVector(0, 14)));
        match.Tick();
        Assert.False(match.World.IsSuppressed(First));
        sight.Clear = true;
        while (match.World.Tick < 22)
            match.Tick();
        Assert.Equal(380, HitPoints(match.World, First));
        Assert.True(match.World.IsSuppressed(First));

        sight.Clear = false;
        while (match.World.Tick < 46)
        {
            var last = Position(match, First).Z;
            match.Tick();
            Assert.True(match.World.IsSuppressed(First));
            Assert.Equal(0.13f, Position(match, First).Z - last, precision: 4);
        }
        match.Tick();
        Assert.False(match.World.IsSuppressed(First));
    }

    private static GameSimulation MatchWithSuppression(ILineOfSightService sight, params WorldEntity[] entities)
    {
        var config = new GameConfiguration(1, 20, 500, 5, 2, 10, 6, 0.7m,
            Configuration.Units, Configuration.Buildings, new SuppressionRules(24, 0.65m));
        return new GameSimulation(new WorldEntities(entities), config, new StraightNavigation(), sight);
    }

    private static SimVector Position(GameSimulation match, EntityId id)
    {
        Assert.True(match.World.TryGetUnitPosition(id, out var position));
        return position;
    }

    private static readonly CoverZone CentralCover = new(new SimVector(-2, -2), new SimVector(2, 2));

    private static Unit Infantry(EntityId id, PlayerId player, float x, int hp = 400) =>
        new(id, player, new SimVector(x, 0), UnitKind.InfantrySquad, hp);

    private static GameSimulation MatchWithCover(CoverZone[] coverZones, params WorldEntity[] entities) =>
        new(new WorldEntities(entities, coverZones), Configuration, new StraightNavigation(), new Sight());

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
