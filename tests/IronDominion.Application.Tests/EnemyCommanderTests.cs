using IronDominion.AI;
using IronDominion.Domain;
using Xunit;

namespace IronDominion.Application.Tests;

public sealed class EnemyCommanderTests
{
    private static readonly PlayerId Orange = new(2);
    private static readonly PlayerId Blue = new(1);
    private static readonly EntityId TankId = new(1);
    private static readonly EntityId OrangeHeadquarters = new(7);
    private static readonly EntityId OrangeBarracks = new(8);
    private static readonly EntityId BlueHeadquarters = new(9);

    [Fact]
    public void ProductionUsesTheNormalQueueOnTheTickAfterPlanning()
    {
        var match = new Match(600, [
            new Building(OrangeHeadquarters, Orange, default, BuildingKind.Headquarters, 1500, new SimVector(14, 0)),
            new Building(OrangeBarracks, Orange, new SimVector(30, 0), BuildingKind.Barracks, 700, new SimVector(44, 0))
        ]);

        Assert.Empty(match.Advance(40));
        Assert.Equal(600, match.Session.World.Credits(Orange));
        var orders = match.Advance(1);
        Assert.Collection(orders,
            order => AssertTraining(order, OrangeHeadquarters, UnitKind.Tank),
            order => AssertTraining(order, OrangeBarracks, UnitKind.InfantrySquad));
        Assert.Equal(0, match.Session.World.Credits(Orange));
        Assert.Single(match.Session.World.TrainingQueue(OrangeHeadquarters));
        Assert.Single(match.Session.World.TrainingQueue(OrangeBarracks));
        Assert.Empty(match.Advance(40));
    }

    [Theory]
    [InlineData(90, 0)]
    [InlineData(100, 1)]
    [InlineData(500, 1)]
    public void ProductionNeverOverspendsTheObservedBalance(int credits, int expectedOrders)
    {
        var match = new Match(credits, [
            new Building(OrangeHeadquarters, Orange, default, BuildingKind.Headquarters, 1500, new SimVector(14, 0)),
            new Building(OrangeBarracks, Orange, new SimVector(30, 0), BuildingKind.Barracks, 700, new SimVector(44, 0))
        ]);

        var orders = match.Advance(41);
        Assert.Equal(expectedOrders, orders.Length);
        Assert.All(orders, order => Assert.True(order.Result.Accepted));
        Assert.Equal(expectedOrders == 0 ? credits : 0, match.Session.World.Credits(Orange));
    }

    [Fact]
    public void SendsTheNearestInfantryToCaptureANeutralSector()
    {
        var closest = new EntityId(11);
        var match = new Match(0, [
            Infantry(2, Orange, 0), Infantry(3, Orange, 30), Tank(8),
            new Sector(closest, null, new SimVector(8, 0)),
            new Sector(new EntityId(12), null, new SimVector(-20, 0)),
            new Sector(new EntityId(13), Orange, new SimVector(1, 0))
        ]);

        var order = Assert.Single(match.Advance(41));
        var move = Assert.IsType<MoveCommand>(order.Command);
        Assert.Equal(new EntityId(2), Assert.Single(move.Units));
        Assert.Equal(new SimVector(8, 0), move.Destination);
        Assert.True(order.Result.Accepted);
        match.Advance(40);
        Assert.Equal(Orange, match.Session.World.Sectors.Single(sector => sector.Id == closest).Owner);
    }

    [Fact]
    public void HiddenEnemiesAreAbsentFromTheObservationAndCannotBeOrderedAsTargets()
    {
        var hiddenInfantry = new EntityId(10);
        var known = new SimVector(3, 20);
        var sight = new Sight { Hidden = [BlueHeadquarters, hiddenInfantry] };
        var match = new Match(0, [
            Tank(0), Infantry(2, Orange, 1), Infantry(3, Orange, 2), Infantry(4, Orange, 3),
            Headquarters(BlueHeadquarters, Blue, 40), Infantry(hiddenInfantry.Value, Blue, 36),
            new Sector(new EntityId(11), Orange, known)
        ], sight: sight);
        var initial = match.Session.Observe(Orange);
        Assert.Empty(initial.Contacts);
        Assert.Empty(initial.Buildings);
        Assert.Equal(known, Assert.Single(initial.Sectors).Position);

        match.Session.Commands.ScheduleAttack(Orange, [TankId], BlueHeadquarters, 41);
        var orders = match.Advance(41);
        var guessedOrder = Assert.Single(orders, item => item.Command is AttackCommand);
        Assert.Equal(new CommandResult(false, "target_not_visible"), guessedOrder.Result);
        var scouting = orders.Where(item => item.Command is MoveCommand).ToArray();
        Assert.Equal(4, scouting.Length);
        Assert.All(scouting, order =>
        {
            Assert.True(order.Result.Accepted);
            Assert.Equal(known, Assert.IsType<MoveCommand>(order.Command).Destination);
        });
        Assert.Empty(match.Session.Observe(Orange).Contacts);

        sight.Hidden = [hiddenInfantry];
        var contact = Assert.Single(match.Session.Observe(Orange).Contacts);
        Assert.Equal(BlueHeadquarters, contact.Id);
        Assert.Equal(EnemyContactKind.Headquarters, contact.Kind);
        Assert.Equal(4, contact.Observers.Length);
        Assert.Empty(initial.Contacts);
    }

    [Fact]
    public void DefendsHeadquartersBeforeCapturingOrAssaulting()
    {
        var threat = new EntityId(10);
        var match = new Match(0, [
            Tank(0), Infantry(2, Orange, -1), Infantry(3, Orange, -2), Infantry(4, Orange, -3),
            Headquarters(OrangeHeadquarters, Orange, 0), Headquarters(BlueHeadquarters, Blue, 25),
            Infantry(threat.Value, Blue, 7), new Sector(new EntityId(11), null, new SimVector(-10, 0))
        ]);

        var orders = match.Advance(41);
        Assert.Equal(4, orders.Length);
        Assert.All(orders, order =>
        {
            Assert.True(order.Result.Accepted);
            Assert.Equal(threat, Assert.IsType<AttackCommand>(order.Command).Target);
        });
    }

    [Fact]
    public void AssaultWaitsForReinforcementsThenMovesWithinRangeToAttack()
    {
        var belowThreshold = new Match(0, [
            Tank(0), Infantry(2, Orange, 0), Infantry(3, Orange, 1), Headquarters(BlueHeadquarters, Blue, 24)
        ]);
        Assert.Empty(belowThreshold.Advance(120));

        var match = new Match(0, [
            Tank(0), Infantry(2, Orange, 0), Infantry(3, Orange, 1), Infantry(4, Orange, 2),
            Headquarters(BlueHeadquarters, Blue, 24)
        ]);
        var initialOrders = match.Advance(41);
        Assert.Equal(4, initialOrders.Length);
        Assert.All(initialOrders, order => Assert.IsType<MoveCommand>(order.Command));

        var later = match.Advance(100);
        Assert.Contains(later, order => order.Result.Accepted
            && order.Command is AttackCommand attack && attack.Target == BlueHeadquarters
            && attack.Units.Contains(TankId));
        Assert.True(match.Session.World.TryGetHitPoints(BlueHeadquarters, out var hp) && hp < 1500);
    }

    [Fact]
    public void AttemptsAnotherFiringPositionAfterAnUnreachableMove()
    {
        var match = new Match(0, [
            Tank(0), Infantry(2, Orange, 0), Infantry(3, Orange, 1), Infantry(4, Orange, 2),
            Headquarters(BlueHeadquarters, Blue, 24)
        ], navigation: new Navigation { Blocked = new SimVector(8.5f, 0) });

        var initial = Assert.Single(match.Advance(41), IsTankMove);
        Assert.Equal(new CommandResult(false, "unreachable_destination"), initial.Result);
        var retry = Assert.Single(match.Advance(40), IsTankMove);
        Assert.True(retry.Result.Accepted);
        Assert.NotEqual(((MoveCommand)initial.Command).Destination, ((MoveCommand)retry.Command).Destination);
    }

    private static bool IsTankMove(CommandProcessed order) =>
        order.Command is MoveCommand move && move.Units.Contains(TankId);

    private static void AssertTraining(CommandProcessed order, EntityId id, UnitKind kind)
    {
        var command = Assert.IsType<TrainUnitCommand>(order.Command);
        Assert.Equal(41, command.ExecuteAtTick);
        Assert.Equal(Orange, command.Player);
        Assert.Equal(id, command.Producer);
        Assert.Equal(kind, command.Kind);
        Assert.True(order.Result.Accepted);
    }

    private static Building Headquarters(EntityId id, PlayerId owner, float x) =>
        new(id, owner, new SimVector(x, 0), BuildingKind.Headquarters, 1500);

    private static Unit Tank(float x) => new(TankId, Orange, new SimVector(x, 0), UnitKind.Tank, 600);

    private static Unit Infantry(int id, PlayerId owner, float x) =>
        new(new EntityId(id), owner, new SimVector(x, 0), UnitKind.InfantrySquad, 400);

    private sealed class Match
    {
        private readonly EnemyCommander _commander;
        private readonly PlayerCommands _orders;

        public Match(int credits, WorldEntity[] entities, Sight? sight = null, Navigation? navigation = null)
        {
            var configuration = new GameConfiguration(
                1, 20, credits, 0, 0, 1, 6, 0.7m,
                [new(UnitKind.InfantrySquad, 400, 100, 160, 4, 12, 10, 20),
                    new(UnitKind.Tank, 600, 500, 400, 3, 16, 45, 40)],
                [new(BuildingKind.Headquarters, 1500), new(BuildingKind.Barracks, 700)]);
            Session = new GameSession(new WorldEntities(entities), configuration, navigation ?? new Navigation(),
                sight ?? new Sight());
            _orders = Session.Commands.ForPlayer(Orange);
            _commander = new EnemyCommander(configuration);
        }

        public GameSession Session { get; }

        public CommandProcessed[] Advance(int ticks)
        {
            var orders = new List<CommandProcessed>();
            for (var elapsed = 0; elapsed < ticks; elapsed++)
            {
                orders.AddRange(Session.AdvanceOneTick().OfType<CommandProcessed>());
                if (EnemyCommander.IsDecisionTick(Session.World.Tick))
                    _commander.Plan(Session.Observe(Orange), _orders);
            }
            return orders.ToArray();
        }
    }

    private sealed class Sight : ILineOfSightService
    {
        public HashSet<EntityId> Hidden { get; set; } = [];

        public bool HasLineOfSight(SimVector origin, SimVector target, EntityId targetId) => !Hidden.Contains(targetId);
    }

    private sealed class Navigation : INavigationService
    {
        public SimVector? Blocked { get; set; }

        public IReadOnlyList<SimVector> FindPath(SimVector start, SimVector destination, int movementMask) =>
            destination == Blocked ? [] : [destination];
    }
}
