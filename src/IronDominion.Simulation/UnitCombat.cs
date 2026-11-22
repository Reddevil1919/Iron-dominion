using System.Collections.Immutable;
using IronDominion.Domain;

namespace IronDominion.Simulation;

internal sealed class UnitCombat(
    WorldEntities world,
    GameConfiguration configuration,
    ILineOfSightService sight)
{
    private static readonly CommandResult Accepted = new(true, null);

    private readonly List<Unit> _units = world.Units.OrderBy(unit => unit.Id.Value).ToList();
    private readonly List<WorldEntity> _targets = world.Units.Cast<WorldEntity>().Concat(world.Buildings)
        .OrderBy(entity => entity.Id.Value).ToList();
    private readonly Dictionary<UnitKind, UnitDefinition> _definitions = configuration.Units.ToDictionary(unit => unit.Kind);
    private readonly Dictionary<EntityId, EntityId> _orderedTargets = new();
    private readonly Dictionary<EntityId, long> _nextShot = new();

    public ImmutableDictionary<EntityId, int> Health { get; private set; } = InitialHealth(world);

    public static ImmutableDictionary<EntityId, int> InitialHealth(WorldEntities world) =>
        world.Units.Select(unit => (unit.Id, unit.HitPoints))
            .Concat(world.Buildings.Select(building => (building.Id, building.HitPoints)))
            .ToImmutableDictionary(entity => entity.Id, entity => entity.HitPoints);

    public EntityId? FocusedTarget(EntityId id) => _orderedTargets.TryGetValue(id, out var target) ? target : null;

    public long? NextShot(EntityId id) => _nextShot.TryGetValue(id, out var tick) ? tick : null;

    public void Restore(ImmutableArray<UnitCheckpoint> units, ImmutableArray<BuildingCheckpoint> buildings)
    {
        Health = units.Select(unit => (unit.Id, unit.HitPoints)).Concat(buildings.Select(building => (building.Id, building.HitPoints)))
            .ToImmutableDictionary(actor => actor.Id, actor => actor.HitPoints);
        foreach (var unit in units)
        {
            if (unit.FocusedTarget is { } target)
                _orderedTargets.Add(unit.Id, target);
            if (unit.NextShot is { } tick)
                _nextShot.Add(unit.Id, tick);
        }
    }

    public void Register(Unit unit)
    {
        // Spawn IDs are greater than the existing IDs, so appending preserves combat order.
        _units.Add(unit);
        _targets.Add(unit);
        Health = Health.Add(unit.Id, unit.HitPoints);
    }

    public CommandResult OrderAttack(AttackCommand command, IReadOnlyDictionary<EntityId, SimVector> positions)
    {
        if (world.Find(command.Target) is not { Owner: { } owner } target
            || owner == command.Player || !Alive(target.Id))
            return new(false, "invalid_target");

        if (!LivingUnits(command.Player).Any(unit => CanSee(unit, target, positions)))
            return new(false, "target_not_visible");

        foreach (var unit in command.Units)
            _orderedTargets[unit] = target.Id;

        return Accepted;
    }

    public ImmutableArray<EnemyContact> ObserveEnemies(PlayerId player, IReadOnlyDictionary<EntityId, SimVector> positions)
    {
        var ownUnits = LivingUnits(player).ToArray();
        if (ownUnits.Length == 0)
            return [];

        var contacts = ImmutableArray.CreateBuilder<EnemyContact>();
        foreach (var target in _targets)
        {
            if (target.Owner == player || !Alive(target.Id))
                continue;

            var observers = ownUnits.Where(unit => CanSee(unit, target, positions))
                .Select(unit => unit.Id).ToImmutableArray();
            if (observers.IsEmpty)
                continue;

            var kind = target switch
            {
                Building { Kind: BuildingKind.Headquarters } => EnemyContactKind.Headquarters,
                Building => EnemyContactKind.Barracks,
                _ => EnemyContactKind.Unit
            };
            contacts.Add(new EnemyContact(target.Id, kind, PositionOf(target, positions), observers));
        }

        return contacts.ToImmutable();
    }

    private IEnumerable<Unit> LivingUnits(PlayerId player) =>
        _units.Where(unit => unit.Owner == player && Alive(unit.Id));

    private bool CanSee(Unit observer, WorldEntity target, IReadOnlyDictionary<EntityId, SimVector> positions) =>
        sight.HasLineOfSight(positions[observer.Id], PositionOf(target, positions), target.Id);

    public void CancelOrders(IEnumerable<EntityId> units)
    {
        foreach (var id in units)
            _orderedTargets.Remove(id);
    }

    public void Resolve(long tick, IReadOnlyDictionary<EntityId, SimVector> positions,
        List<IGameEvent> events, UnitMovement movement)
    {
        var damage = new Dictionary<EntityId, long>();
        foreach (var attacker in _units)
        {
            if (!Alive(attacker.Id) || _nextShot.GetValueOrDefault(attacker.Id) > tick)
                continue;

            var definition = _definitions[attacker.Kind];
            var target = FindTarget(attacker, definition.AttackRangeMeters, positions);
            if (target is null)
                continue;

            _nextShot[attacker.Id] = tick + definition.CooldownTicks;
            events.Add(new ShotFired(tick, attacker.Id, target.Id));
            damage[target.Id] = damage.GetValueOrDefault(target.Id) + DamagePerShot(definition, target, positions);
        }

        if (damage.Count == 0)
            return;

        var health = Health.ToBuilder();
        foreach (var (id, amount) in damage.OrderBy(hit => hit.Key.Value))
        {
            var applied = (int)Math.Min(health[id], amount);
            if (applied == 0)
                continue;

            health[id] -= applied;
            events.Add(new EntityDamaged(tick, id, applied, health[id]));
            if (health[id] == 0)
            {
                events.Add(new EntityDestroyed(tick, id));
                _orderedTargets.Remove(id);
                _nextShot.Remove(id);
                movement.Remove(id);
            }
        }

        Health = health.ToImmutable();
    }

    private int DamagePerShot(UnitDefinition definition, WorldEntity target,
        IReadOnlyDictionary<EntityId, SimVector> positions)
    {
        if (target is Unit unit && CoverZone.Protects(world.CoverZones, unit, positions[unit.Id]))
        {
            var reduced = definition.DamagePerShot * configuration.InfantryCoverMultiplier;
            return (int)Math.Round(reduced, MidpointRounding.AwayFromZero);
        }

        return definition.DamagePerShot;
    }

    public bool HasTargetInRange(EntityId id, IReadOnlyDictionary<EntityId, SimVector> positions)
    {
        var unit = (Unit)world.Find(id)!;
        return FindTarget(unit, _definitions[unit.Kind].AttackRangeMeters, positions) is not null;
    }

    private WorldEntity? FindTarget(Unit attacker, float range, IReadOnlyDictionary<EntityId, SimVector> positions)
    {
        var origin = positions[attacker.Id];
        var maxDistanceSquared = range * range;
        if (_orderedTargets.TryGetValue(attacker.Id, out var id) && world.Find(id) is { } ordered
            && CanFireAt(attacker, ordered, origin, maxDistanceSquared, positions))
            return ordered;

        WorldEntity? nearest = null;
        var nearestDistance = maxDistanceSquared;
        foreach (var candidate in _targets)
        {
            if (!CanFireAt(attacker, candidate, origin, nearestDistance, positions))
                continue;

            var distance = DistanceSquared(origin, PositionOf(candidate, positions));
            if (nearest is null || distance < nearestDistance)
            {
                nearest = candidate;
                nearestDistance = distance;
            }
        }

        return nearest;
    }

    private bool CanFireAt(Unit attacker, WorldEntity target, SimVector origin,
        float maxDistanceSquared, IReadOnlyDictionary<EntityId, SimVector> positions)
    {
        if (target.Owner is not { } owner || owner == attacker.Owner || !Alive(target.Id))
            return false;

        var destination = PositionOf(target, positions);
        return DistanceSquared(origin, destination) <= maxDistanceSquared
               && sight.HasLineOfSight(origin, destination, target.Id);
    }

    private bool Alive(EntityId id) => Health.GetValueOrDefault(id) > 0;

    private static SimVector PositionOf(WorldEntity entity, IReadOnlyDictionary<EntityId, SimVector> positions) =>
        entity is Unit ? positions[entity.Id] : entity.Position;

    private static float DistanceSquared(SimVector first, SimVector second)
    {
        var x = second.X - first.X;
        var z = second.Z - first.Z;
        return x * x + z * z;
    }
}
