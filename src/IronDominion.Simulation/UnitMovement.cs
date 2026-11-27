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
    // A circle enclosing the tracks and hull at any heading.
    private const float TankSeparation = 6.6f;
    private const float TankSeparationSquared = TankSeparation * TankSeparation;
    private const int DetourIntervalTicks = 8;
    private static readonly CommandResult Accepted = new(true, null);
    private static readonly float[] DetourExitFractions = [0.6f, 1f];
    private static readonly Vector2[] ApproachDirections =
    [
        Vector2.UnitX, -Vector2.UnitX, Vector2.UnitY, -Vector2.UnitY,
        Vector2.Normalize(new Vector2(1, 1)), Vector2.Normalize(new Vector2(1, -1)),
        Vector2.Normalize(new Vector2(-1, 1)), Vector2.Normalize(new Vector2(-1, -1))
    ];

    private readonly Dictionary<UnitKind, float> _stepSizes = configuration.Units.ToDictionary(
        unit => unit.Kind, unit => unit.SpeedMetersPerSecond / configuration.TickRate);
    private readonly Dictionary<EntityId, Route> _routes = new();
    private readonly SortedSet<EntityId> _tanks = new(entities.Units
        .Where(unit => unit.Kind == UnitKind.Tank && unit.HitPoints > 0).Select(unit => unit.Id),
        Comparer<EntityId>.Create((first, second) => first.Value.CompareTo(second.Value)));
    private readonly float _suppressedMovement = (float)(configuration.Suppression?.MovementMultiplier ?? 1);

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
            if (FindRoute(Positions[ids[i]], destination) is not { } path)
                return new CommandResult(false, "unreachable_destination");

            routes[i] = new Route(new Queue<SimVector>(path), command.AttackMove);
        }

        for (var i = 0; i < ids.Length; i++)
            _routes[ids[i]] = routes[i];

        return Accepted;
    }

    public IReadOnlyList<SimVector>? FindRoute(SimVector origin, SimVector destination)
    {
        var path = navigation.FindPath(origin, destination, GroundMask);
        return path.Count > 0 && path[^1] == destination ? path : null;
    }

    public IReadOnlyList<SimVector>? FindApproach(SimVector origin, SimVector target, float radius,
        Func<SimVector, bool> canWorkAt)
    {
        var offset = new Vector2(origin.X - target.X, origin.Z - target.Z);
        var nearSide = offset == Vector2.Zero ? Vector2.UnitX : Vector2.Normalize(offset);
        var directions = ApproachDirections.OrderByDescending(direction => Vector2.Dot(direction, nearSide))
            .Prepend(nearSide);
        foreach (var direction in directions.Distinct())
        {
            var destination = new SimVector(target.X + direction.X * radius, target.Z + direction.Y * radius);
            if (canWorkAt(destination) && FindRoute(origin, destination) is { } path)
                return path;
        }

        return null;
    }

    public SimVector? RouteDestination(EntityId id) => _routes.TryGetValue(id, out var route)
        ? route.Waypoints.Last() : null;

    public bool CanOccupy(Unit unit, SimVector point) => unit.Kind != UnitKind.Tank
        || !_tanks.Any(other => other != unit.Id && Positions.TryGetValue(other, out var center)
            && Vector2.DistanceSquared(Point(point), Point(center)) < TankSeparationSquared);

    public void SetApproach(EntityId id, IReadOnlyList<SimVector>? path)
    {
        if (path is { Count: > 0 })
            _routes[id] = new Route(new Queue<SimVector>(path), AttackMove: false);
        else
            Stop(id);
    }

    public RouteCheckpoint? CaptureRoute(EntityId id) => _routes.TryGetValue(id, out var route)
        ? new RouteCheckpoint(route.AttackMove, route.Waypoints.ToImmutableArray()) : null;

    public void Restore(ImmutableArray<UnitCheckpoint> units)
    {
        Positions = units.Where(unit => unit.Position.HasValue)
            .ToImmutableDictionary(unit => unit.Id, unit => unit.Position!.Value);
        foreach (var unit in units)
        {
            if (unit.Route is { } route)
                _routes.Add(unit.Id, new Route(new Queue<SimVector>(route.Waypoints), route.AttackMove));
        }
    }

    public bool IsMoving(EntityId id) => _routes.ContainsKey(id);

    public void Register(Unit unit)
    {
        Positions = Positions.Add(unit.Id, unit.Position);
        if (unit.Kind == UnitKind.Tank)
            _tanks.Add(unit.Id);
    }

    public void Stop(IEnumerable<EntityId> ids)
    {
        foreach (var id in ids)
            Stop(id);
    }

    public void Stop(EntityId id) => _routes.Remove(id);

    public void Remove(EntityId id)
    {
        Stop(id);
        _tanks.Remove(id);
        Positions = Positions.Remove(id);
    }

    public void Advance(UnitCombat? combat, long tick)
    {
        if (_routes.Count == 0)
            return;

        var positions = Positions.ToBuilder();
        var completed = new List<EntityId>();
        // Resolve right of way in entity order, including after restoring a checkpoint.
        foreach (var id in _routes.Keys.OrderBy(id => id.Value))
        {
            var route = _routes[id];
            // All routes check for targets against the positions at the start of the movement phase.
            if (route.AttackMove && combat?.HasTargetInRange(id, Positions) == true)
                continue;

            var unit = (Unit)entities.Find(id)!;
            var position = positions[id];
            var remaining = _stepSizes[unit.Kind];
            if (combat?.IsSuppressed(id, tick) == true)
                remaining *= _suppressedMovement;
            while (route.Waypoints.TryPeek(out var waypoint))
            {
                var delta = new Vector2(waypoint.X - position.X, waypoint.Z - position.Z);
                var distance = delta.Length();
                var step = distance > remaining ? Position(Point(position) + delta * (remaining / distance)) : waypoint;
                if (unit.Kind == UnitKind.Tank && FindTankBlocker(id, position, step, positions) is { } blocker)
                {
                    if ((tick + id.Value) % DetourIntervalTicks == 0)
                        TryDetour(position, route.Waypoints, blocker);
                    break;
                }

                position = step;
                if (distance > remaining)
                    break;
                route.Waypoints.Dequeue();
                remaining -= distance;
            }

            positions[id] = position;
            if (route.Waypoints.Count == 0)
                completed.Add(id);
        }

        foreach (var id in completed)
            Stop(id);

        Positions = positions.ToImmutable();
    }

    private SimVector? FindTankBlocker(EntityId id, SimVector from, SimVector to,
        IReadOnlyDictionary<EntityId, SimVector> positions)
    {
        foreach (var other in _tanks)
            if (other != id && positions.TryGetValue(other, out var center) && CrossesTank(from, to, center))
                return center;
        return null;
    }

    private void TryDetour(SimVector from, Queue<SimVector> waypoints, SimVector blocker)
    {
        var destination = waypoints.Last();
        var center = Point(blocker);
        var startOffset = Point(from) - center;
        var endOffset = Point(destination) - center;
        if (endOffset.LengthSquared() < TankSeparationSquared || from == destination)
            return; // Wait for an occupied destination to clear.

        var forward = Vector2.Normalize(endOffset - startOffset);
        var side = new Vector2(-forward.Y, forward.X);
        if (Vector2.Dot(startOffset, side) < -0.05f)
            side = -side;

        const float clearance = TankSeparation + 0.6f;
        for (var turn = 0; turn < 2; turn++, side = -side)
        {
            var outside = center + side * clearance;
            var near = Position(outside + forward * Math.Clamp(Vector2.Dot(startOffset, forward), -clearance, clearance));
            if (FindRoute(from, near) is not { } first || CrossesTank(from, first, blocker))
                continue;

            // Rejoin early when a building or the edge of the navmesh blocks the wider turn.
            var exit = Math.Clamp(Vector2.Dot(endOffset, forward), -clearance, clearance);
            foreach (var fraction in DetourExitFractions)
            {
                var far = Position(outside + forward * (exit * fraction));
                if (FindRoute(near, far) is not { } middle || CrossesTank(near, middle, blocker)
                    || FindRoute(far, destination) is not { } last || CrossesTank(far, last, blocker))
                    continue;

                waypoints.Clear();
                foreach (var point in first.Concat(middle).Concat(last))
                    waypoints.Enqueue(point);
                return;
            }
        }
    }

    private static bool CrossesTank(SimVector from, IReadOnlyList<SimVector> path, SimVector center)
    {
        foreach (var to in path)
        {
            if (CrossesTank(from, to, center))
                return true;
            from = to;
        }
        return false;
    }

    private static bool CrossesTank(SimVector from, SimVector to, SimVector center)
    {
        var offset = Point(from) - Point(center);
        var step = Point(to) - Point(from);
        var lengthSquared = step.LengthSquared();
        if (lengthSquared == 0f)
            return false;

        var dot = Vector2.Dot(offset, step);
        // Legacy saves may already overlap. Allow separation without pushing a tank deeper inside.
        if (offset.LengthSquared() < TankSeparationSquared)
            return dot < 0f;

        var fraction = Math.Clamp(-dot / lengthSquared, 0f, 1f);
        return (offset + step * fraction).LengthSquared() < TankSeparationSquared;
    }

    private static Vector2 Point(SimVector position) => new(position.X, position.Z);

    private static SimVector Position(Vector2 point) => new(point.X, point.Y);

    private readonly record struct Route(Queue<SimVector> Waypoints, bool AttackMove);
}
