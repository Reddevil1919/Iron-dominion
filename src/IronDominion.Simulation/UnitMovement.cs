using System.Collections.Immutable;
using System.Numerics;
using IronDominion.Domain;

namespace IronDominion.Simulation;

internal sealed class UnitMovement(
    WorldEntities entities,
    GameConfiguration configuration,
    INavigationService navigation)
{
    private const int GroundMask = 1;
    private const float GroupSpacing = 9f;
    private static readonly CommandResult Accepted = new(true, null);

    private readonly Dictionary<UnitKind, float> _stepSizes = configuration.Units.ToDictionary(
        unit => unit.Kind, unit => unit.SpeedMetersPerSecond / configuration.TickRate);
    private readonly Dictionary<EntityId, Route> _routes = new();

    public ImmutableDictionary<EntityId, SimVector> Positions { get; private set; } =
        entities.Units.ToImmutableDictionary(unit => unit.Id, unit => unit.Position);

    public CommandResult Plan(MoveCommand command)
    {
        var ids = command.Units.Distinct().OrderBy(id => id.Value).ToArray();
        var width = (int)MathF.Ceiling(MathF.Sqrt(ids.Length));
        var routes = new Route[ids.Length];

        for (var i = 0; i < ids.Length; i++)
        {
            var destination = new SimVector(
                command.Destination.X + i % width * GroupSpacing,
                command.Destination.Z + i / width * GroupSpacing);
            var path = navigation.FindPath(Positions[ids[i]], destination, GroundMask);
            if (path.Count == 0 || path[^1] != destination)
                return new CommandResult(false, "unreachable_destination");

            routes[i] = new Route(new Queue<SimVector>(path), command.AttackMove);
        }

        for (var i = 0; i < ids.Length; i++)
            _routes[ids[i]] = routes[i];

        return Accepted;
    }

    public bool IsMoving(EntityId id) => _routes.ContainsKey(id);

    public void Register(Unit unit) => Positions = Positions.Add(unit.Id, unit.Position);

    public void Stop(IEnumerable<EntityId> ids)
    {
        foreach (var id in ids)
            _routes.Remove(id);
    }

    public void Remove(EntityId id)
    {
        _routes.Remove(id);
        Positions = Positions.Remove(id);
    }

    public void Advance(UnitCombat? combat)
    {
        if (_routes.Count == 0)
            return;

        var positions = Positions.ToBuilder();
        var completed = new List<EntityId>();
        foreach (var (id, route) in _routes)
        {
            // All routes check for targets against the positions at the start of the movement phase.
            if (route.AttackMove && combat?.HasTargetInRange(id, Positions) == true)
                continue;

            var unit = (Unit)entities.Find(id)!;
            var position = positions[id];
            var remaining = _stepSizes[unit.Kind];
            while (route.Waypoints.TryPeek(out var waypoint))
            {
                var delta = new Vector2(waypoint.X - position.X, waypoint.Z - position.Z);
                var distance = delta.Length();
                if (distance > remaining)
                {
                    var step = delta * (remaining / distance);
                    position = new SimVector(position.X + step.X, position.Z + step.Y);
                    break;
                }

                position = route.Waypoints.Dequeue();
                remaining -= distance;
            }

            positions[id] = position;
            if (route.Waypoints.Count == 0)
                completed.Add(id);
        }

        foreach (var id in completed)
            _routes.Remove(id);

        Positions = positions.ToImmutable();
    }

    private readonly record struct Route(Queue<SimVector> Waypoints, bool AttackMove);
}
