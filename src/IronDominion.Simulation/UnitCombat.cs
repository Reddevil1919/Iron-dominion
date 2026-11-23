using System.Collections.Immutable;
using System.Numerics;
using IronDominion.Domain;

namespace IronDominion.Simulation;

internal sealed class UnitCombat(
    WorldEntities world,
    GameConfiguration configuration,
    ILineOfSightService sight)
{
    private static readonly CommandResult Accepted = new(true, null);
    private static readonly Vector2[] ApproachDirections =
    [
        Vector2.UnitX, -Vector2.UnitX, Vector2.UnitY, -Vector2.UnitY,
        Vector2.Normalize(new Vector2(1, 1)), Vector2.Normalize(new Vector2(1, -1)),
        Vector2.Normalize(new Vector2(-1, 1)), Vector2.Normalize(new Vector2(-1, -1))
    ];

    private readonly List<Unit> _units = world.Units.OrderBy(unit => unit.Id.Value).ToList();
    private readonly List<WorldEntity> _targets = world.Units.Cast<WorldEntity>().Concat(world.Buildings)
        .OrderBy(entity => entity.Id.Value).ToList();
    private readonly Dictionary<UnitKind, UnitDefinition> _definitions = configuration.Units.ToDictionary(unit => unit.Kind);
    private readonly Dictionary<EntityId, EntityId> _orderedTargets = new();
    private readonly Dictionary<EntityId, long> _nextShot = new();

    public ImmutableDictionary<EntityId, int> Health { get; private set; } = InitialHealth(world);

    public ImmutableDictionary<EntityId, long> Suppression { get; private set; } = ImmutableDictionary<EntityId, long>.Empty;

    public bool IsSuppressed(EntityId id, long tick) =>
        Suppression.TryGetValue(id, out var through) && tick <= through;

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
        Suppression = units.Where(unit => unit.SuppressedThrough.HasValue)
            .ToImmutableDictionary(unit => unit.Id, unit => unit.SuppressedThrough!.Value);
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

    public CommandResult OrderAttack(AttackCommand command, UnitMovement movement)
    {
        var positions = movement.Positions;
        if (world.Find(command.Target) is not { Owner: { } owner } target
            || owner == command.Player || !Alive(target.Id))
            return new(false, "invalid_target");

        if (!CanSeeTarget(command.Player, target, positions))
            return new(false, "target_not_visible");

        var approaches = command.Units.Distinct().OrderBy(id => id.Value)
            .Select(id => (Id: id, Path: FindApproach((Unit)world.Find(id)!, target, movement)))
            .ToArray();
        if (approaches.All(approach => approach.Path is null))
            return new(false, "no_firing_position");

        foreach (var (id, path) in approaches)
        {
            _orderedTargets[id] = target.Id;
            movement.SetApproach(id, path);
        }

        return Accepted;
    }

    public void UpdateApproaches(UnitMovement movement, long tick)
    {
        if (_orderedTargets.Count == 0)
            return;

        var positions = movement.Positions;
        foreach (var unit in _units)
        {
            if (!_orderedTargets.TryGetValue(unit.Id, out var id))
                continue;

            var target = world.Find(id)!;
            var range = _definitions[unit.Kind].AttackRangeMeters;
            if (CanFireAt(unit, target, positions[unit.Id], range * range, positions))
            {
                movement.Stop(unit.Id);
                continue;
            }

            if (!Alive(id) || !CanSeeTarget(unit.Owner!.Value, target, positions))
            {
                _orderedTargets.Remove(unit.Id);
                movement.Stop(unit.Id);
                continue;
            }

            // Retain valid routes and limit path requests for moving or unreachable targets.
            if (tick % configuration.TickRate == 0
                && (movement.RouteDestination(unit.Id) is not { } destination
                    || !CanFireAt(unit, target, destination, range * range, positions)))
                movement.SetApproach(unit.Id, FindApproach(unit, target, movement));
        }
    }

    private IReadOnlyList<SimVector>? FindApproach(Unit attacker, WorldEntity target, UnitMovement movement)
    {
        var positions = movement.Positions;
        var origin = positions[attacker.Id];
        var range = _definitions[attacker.Kind].AttackRangeMeters;
        if (CanFireAt(attacker, target, origin, range * range, positions))
            return [];

        var aim = PositionOf(target, positions);
        var offset = new Vector2(origin.X - aim.X, origin.Z - aim.Z);
        var nearSide = offset == Vector2.Zero ? Vector2.UnitX : Vector2.Normalize(offset);
        var radius = MathF.Max(0f, range - 0.5f);
        var directions = ApproachDirections.OrderByDescending(direction => Vector2.Dot(direction, nearSide))
            .Prepend(nearSide);
        foreach (var direction in directions.Distinct())
        {
            var destination = new SimVector(aim.X + direction.X * radius, aim.Z + direction.Y * radius);
            if (CanFireAt(attacker, target, destination, range * range, positions)
                && movement.FindRoute(origin, destination) is { } route)
                return route;
        }

        return null;
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

    private bool CanSeeTarget(PlayerId player, WorldEntity target, IReadOnlyDictionary<EntityId, SimVector> positions) =>
        LivingUnits(player).Any(unit => CanSee(unit, target, positions));

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
            damage[target.Id] = damage.GetValueOrDefault(target.Id) + DamagePerShot(definition, target, positions[attacker.Id], positions);
        }

        if (damage.Count == 0)
            return;

        var health = Health.ToBuilder();
        var suppression = Suppression.ToBuilder();
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
                suppression.Remove(id);
                movement.Remove(id);
            }
            else if (configuration.Suppression is { } rules && world.Find(id) is Unit { Kind: UnitKind.InfantrySquad })
            {
                suppression[id] = checked(tick + rules.DurationTicks);
            }
        }

        Health = health.ToImmutable();
        Suppression = suppression.ToImmutable();
    }

    private int DamagePerShot(UnitDefinition definition, WorldEntity target, SimVector origin,
        IReadOnlyDictionary<EntityId, SimVector> positions)
    {
        if (target is Unit unit && CoverZone.Protects(world.CoverZones, unit, positions[unit.Id], origin))
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
        if (_orderedTargets.TryGetValue(attacker.Id, out var id) && world.Find(id) is { } ordered)
            return CanFireAt(attacker, ordered, origin, maxDistanceSquared, positions) ? ordered : null;

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
