using System.Collections.Immutable;
using IronDominion.Domain;

namespace IronDominion.Simulation;

internal sealed class UnitRepairs(WorldEntities world, UnitMovement movement, UnitCombat combat, ILineOfSightService sight)
{
    private const float WorkingDistance = 4.65f;
    private const float MinimumDistance = 3.6f;
    private const float MaximumDistance = 5.7f;
    private const int RepathTicks = 10;
    private const int RepairPulseTicks = 4;
    private const int HitPointsPerPulse = 6;
    private readonly Dictionary<EntityId, Unit> _orders = [];

    public ImmutableDictionary<EntityId, EntityId> Active { get; private set; } =
        ImmutableDictionary<EntityId, EntityId>.Empty;

    public EntityId? TargetOf(EntityId engineer) => _orders.GetValueOrDefault(engineer)?.Id;

    public CommandResult Order(RepairCommand command)
    {
        if (world.Find(command.Target) is not Unit { Kind: UnitKind.Tank } tank || tank.Owner != command.Player
            || combat.Health[tank.Id] <= 0)
            return new(false, "invalid_repair_target");
        if (combat.Health[tank.Id] >= tank.HitPoints)
            return new(false, "tank_already_repaired");

        var aim = movement.Positions[tank.Id];
        var accepted = false;
        foreach (var engineer in command.Units.Distinct().OrderBy(id => id.Value))
        {
            var origin = movement.Positions[engineer];
            var path = InWorkingPosition(origin, aim, tank.Id) ? [] : FindApproach(origin, aim, tank.Id);
            if (path is null)
                continue;
            _orders[engineer] = tank;
            movement.SetApproach(engineer, path);
            accepted = true;
        }

        return accepted ? new(true, null) : new(false, "no_repair_position");
    }

    public void Cancel(IEnumerable<EntityId> engineers)
    {
        foreach (var engineer in engineers)
            _orders.Remove(engineer);
    }

    public void UpdateApproaches(long tick)
    {
        foreach (var (engineer, tank) in _orders.OrderBy(order => order.Key.Value))
        {
            var origin = movement.Positions[engineer];
            var aim = movement.Positions[tank.Id];
            if (InWorkingPosition(origin, aim, tank.Id))
                movement.Stop(engineer);
            else if (tick % RepathTicks == 0 && (movement.RouteDestination(engineer) is not { } destination
                || !InWorkingPosition(destination, aim, tank.Id)))
                movement.SetApproach(engineer, FindApproach(origin, aim, tank.Id));
        }
    }

    public void Resolve(long tick, List<IGameEvent> events)
    {
        if (_orders.Count == 0 && Active.IsEmpty)
            return;

        RemoveFinishedOrders();
        if (tick % RepairPulseTicks == 0)
        {
            foreach (var (engineer, tank) in _orders.OrderBy(order => order.Key.Value))
            {
                if (!InWorkingPosition(movement.Positions[engineer], movement.Positions[tank.Id], tank.Id))
                    continue;
                var restored = combat.RestoreTankHealth(tank, HitPointsPerPulse);
                if (restored > 0)
                    events.Add(new TankRepaired(tick, engineer, tank.Id, restored, combat.Health[tank.Id]));
            }
            RemoveFinishedOrders();
        }
        PublishActive();
    }

    public void Restore(ImmutableArray<UnitCheckpoint> units)
    {
        foreach (var unit in units)
            if (unit.RepairTarget is { } tank)
                _orders.Add(unit.Id, (Unit)world.Find(tank)!);
        PublishActive();
    }

    private void RemoveFinishedOrders()
    {
        foreach (var (engineer, tank) in _orders)
        {
            var health = combat.Health[tank.Id];
            if (combat.Health[engineer] > 0 && health > 0 && health < tank.HitPoints)
                continue;
            movement.Stop(engineer);
            _orders.Remove(engineer);
        }
    }

    private void PublishActive() => Active = _orders.Count == 0
        ? ImmutableDictionary<EntityId, EntityId>.Empty
        : _orders.Where(pair => InWorkingPosition(movement.Positions[pair.Key], movement.Positions[pair.Value.Id],
                pair.Value.Id)).ToImmutableDictionary(pair => pair.Key, pair => pair.Value.Id);

    private IReadOnlyList<SimVector>? FindApproach(SimVector origin, SimVector aim, EntityId tank) =>
        movement.FindApproach(origin, aim, WorkingDistance, point => InWorkingPosition(point, aim, tank));

    private bool InWorkingPosition(SimVector origin, SimVector aim, EntityId tank)
    {
        var x = origin.X - aim.X;
        var z = origin.Z - aim.Z;
        var distance = x * x + z * z;
        return distance is >= MinimumDistance * MinimumDistance and <= MaximumDistance * MaximumDistance
            && sight.HasLineOfSight(origin, aim, tank);
    }
}
