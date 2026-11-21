using IronDominion.Application;
using IronDominion.Domain;

namespace IronDominion.AI;

public sealed class EnemyCommander(GameConfiguration configuration)
{
    private const int DecisionInterval = 40;
    private const int AssaultForceSize = 4;
    private const float DefenseRadius = 18f;
    private readonly Dictionary<UnitKind, UnitDefinition> _definitions =
        configuration.Units.ToDictionary(unit => unit.Kind);
    private readonly Dictionary<EntityId, Approach> _approaches = new();
    private (EntityId Unit, EntityId Sector)? _captureOrder;
    private EntityId? _scoutingSector;

    public static bool IsDecisionTick(long tick) => tick > 0 && tick % DecisionInterval == 0;

    public void Plan(PlayerObservation world, PlayerCommands commands)
    {
        if (!IsDecisionTick(world.Tick) || world.Player != commands.Player)
            return;

        Produce(world, commands);
        var living = world.Units.Select(unit => unit.Id).ToHashSet();
        foreach (var unit in _approaches.Keys.Where(id => !living.Contains(id)).ToArray())
            _approaches.Remove(unit);

        var headquarters = world.Buildings.FirstOrDefault(building => building.Kind == BuildingKind.Headquarters);
        if (headquarters is not null && FindThreat(world, headquarters.Position) is { } threat)
        {
            _captureOrder = null;
            foreach (var unit in world.Units)
                Engage(unit, threat, commands);
            return;
        }

        var capturing = CaptureNearest(world, commands);
        var available = world.Units.Where(unit => unit.Id != capturing).ToArray();
        if (world.Units.Length >= AssaultForceSize && available.Any(unit => unit.Kind == UnitKind.Tank))
        {
            var target = world.Contacts.FirstOrDefault(contact => contact.Kind == EnemyContactKind.Headquarters);
            if (target is not null)
            {
                foreach (var unit in available)
                    Engage(unit, target, commands);
            }
            else
            {
                Scout(world, headquarters?.Position, available, commands);
            }
            return;
        }

        RallyAtSectors(world, available, commands);
    }

    private void Produce(PlayerObservation world, PlayerCommands commands)
    {
        var budget = world.Credits;
        foreach (var kind in new[] { UnitKind.Tank, UnitKind.InfantrySquad })
        {
            var cap = kind == UnitKind.Tank ? 2 : 4;
            var cost = _definitions[kind].Cost;
            if (budget < cost || world.Units.Count(unit => unit.Kind == kind) >= cap)
                continue;

            var producer = world.Buildings.FirstOrDefault(building =>
                building.Trains == kind && building.Exit.HasValue && !building.IsTraining);
            if (producer is null)
                continue;

            commands.Train(producer.Id, kind);
            budget -= cost;
        }
    }

    private static EnemyContact? FindThreat(PlayerObservation world, SimVector position) =>
        world.Contacts.Where(contact => contact.Kind == EnemyContactKind.Unit
            && DistanceSquared(position, contact.Position) <= DefenseRadius * DefenseRadius)
            .MinBy(contact => DistanceSquared(position, contact.Position));

    private EntityId? CaptureNearest(PlayerObservation world, PlayerCommands commands)
    {
        var objective = (from unit in world.Units
                         where unit.Kind == UnitKind.InfantrySquad
                         from sector in world.Sectors
                         where sector.Owner is null
                         orderby DistanceSquared(unit.Position, sector.Position), unit.Id.Value, sector.Id.Value
                         select ((FriendlyUnit Unit, SectorSite Sector)?)(unit, sector)).FirstOrDefault();
        if (objective is not { } capture)
        {
            _captureOrder = null;
            return null;
        }

        var (squad, site) = capture;
        var radius = configuration.CaptureRadiusMeters;
        var order = (squad.Id, site.Id);
        if (_captureOrder != order || (!squad.IsMoving && DistanceSquared(squad.Position, site.Position) > radius * radius))
            commands.Move(squad.Id, site.Position);

        _captureOrder = order;
        _approaches.Remove(squad.Id);
        return squad.Id;
    }

    private void Engage(FriendlyUnit unit, EnemyContact target, PlayerCommands commands)
    {
        var range = _definitions[unit.Kind].AttackRangeMeters;
        if (DistanceSquared(unit.Position, target.Position) <= range * range && target.Observers.Contains(unit.Id))
        {
            _approaches.Remove(unit.Id);
            commands.Attack(unit.Id, target.Id);
            return;
        }

        var hasApproach = _approaches.TryGetValue(unit.Id, out var prior)
            && prior.Target == target.Id && prior.Position == target.Position;
        if (hasApproach && unit.IsMoving)
            return;

        var offset = Math.Max(0f, range - 0.5f);
        SimVector[] candidates =
        [
            new(target.Position.X - offset, target.Position.Z),
            new(target.Position.X + offset, target.Position.Z),
            new(target.Position.X, target.Position.Z - offset),
            new(target.Position.X, target.Position.Z + offset)
        ];
        var endpoints = candidates.OrderBy(point => DistanceSquared(unit.Position, point)).ToArray();
        var previous = hasApproach ? Array.IndexOf(endpoints, prior.Destination) : -1;
        var destination = endpoints[(previous + 1) % endpoints.Length];
        commands.Move(unit.Id, destination);
        _approaches[unit.Id] = new Approach(target.Id, target.Position, destination);
    }

    private void Scout(PlayerObservation world, SimVector? basePosition, FriendlyUnit[] units, PlayerCommands commands)
    {
        if (world.Sectors.IsEmpty)
            return;

        var sites = world.Sectors.OrderByDescending(site => DistanceSquared(basePosition ?? default, site.Position))
            .ThenBy(site => site.Id.Value).ToArray();
        var current = Array.FindIndex(sites, site => site.Id == _scoutingSector);
        if (current < 0 || units.All(unit => DistanceSquared(unit.Position, sites[current].Position) <= 1f))
            current = (current + 1) % sites.Length;
        var objective = sites[current];
        var changed = _scoutingSector != objective.Id;
        _scoutingSector = objective.Id;
        foreach (var unit in units)
        {
            var wasApproaching = _approaches.Remove(unit.Id);
            if ((changed || wasApproaching || !unit.IsMoving) && DistanceSquared(unit.Position, objective.Position) > 1f)
                commands.Move(unit.Id, objective.Position);
        }
    }

    private static void RallyAtSectors(PlayerObservation world, FriendlyUnit[] units, PlayerCommands commands)
    {
        if (world.Sectors.IsEmpty)
            return;

        foreach (var unit in units)
        {
            if (unit.IsMoving || !world.Buildings.Any(building =>
                    building.Exit is { } exit && DistanceSquared(exit, unit.Position) <= 1f))
                continue;

            var site = world.Sectors.MinBy(sector => DistanceSquared(sector.Position, unit.Position));
            commands.Move(unit.Id, site.Position);
        }
    }

    private static float DistanceSquared(SimVector first, SimVector second)
    {
        var x = first.X - second.X;
        var z = first.Z - second.Z;
        return x * x + z * z;
    }

    private readonly record struct Approach(EntityId Target, SimVector Position, SimVector Destination);
}
