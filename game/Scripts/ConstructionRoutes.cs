using Godot;
using IronDominion.Domain;

namespace IronDominion.Game;

internal sealed class ConstructionRoutes
{
    private const float BoundaryTolerance = 0.001f;
    private readonly Rect2 _floor;
    private readonly Rect2[] _interiors;
    private readonly Vector2[] _corners;
    private readonly List<(int Index, float Length)>[] _edges;

    public ConstructionRoutes(Rect2 floor, Rect2[] holes)
    {
        _floor = floor;
        _interiors = holes.Select(Interior).Where(hole => hole.HasArea()).ToArray();
        _corners = holes.SelectMany(Corners).Concat(Corners(floor)).Distinct().Where(IsWalkable).ToArray();
        _edges = new List<(int, float)>[_corners.Length];
        for (var i = 0; i < _corners.Length; i++)
        {
            _edges[i] = [];
            for (var j = 0; j < i; j++)
            {
                if (!Clear(_corners[i], _corners[j]))
                    continue;
                var length = _corners[i].DistanceTo(_corners[j]);
                _edges[i].Add((j, length));
                _edges[j].Add((i, length));
            }
        }
    }

    public IReadOnlyList<SimVector> Find(SimVector origin, SimVector destination)
    {
        var start = new Vector2(origin.X, origin.Z);
        var finish = new Vector2(destination.X, destination.Z);
        if (!IsWalkable(start) || !IsWalkable(finish))
            return [];
        if (Clear(start, finish))
            return [destination];

        var count = _corners.Length;
        var costs = Enumerable.Repeat(float.PositiveInfinity, count).ToArray();
        var previous = Enumerable.Repeat(-1, count).ToArray();
        var queue = new PriorityQueue<int, (float Cost, int Index)>();
        for (var i = 0; i < count; i++)
        {
            if (!Clear(start, _corners[i]))
                continue;
            costs[i] = start.DistanceTo(_corners[i]);
            queue.Enqueue(i, (costs[i], i));
        }

        var cheapest = float.PositiveInfinity;
        var end = -1;
        while (queue.TryDequeue(out var index, out var priority))
        {
            if (priority.Cost >= cheapest)
                break;
            if (priority.Cost != costs[index])
                continue;
            var point = _corners[index];
            var total = priority.Cost + point.DistanceTo(finish);
            if (total < cheapest && Clear(point, finish))
            {
                cheapest = total;
                end = index;
            }
            foreach (var (next, length) in _edges[index])
            {
                var cost = priority.Cost + length;
                if (cost >= costs[next])
                    continue;
                costs[next] = cost;
                previous[next] = index;
                queue.Enqueue(next, (cost, next));
            }
        }

        if (end < 0)
            return [];
        var route = new List<SimVector> { destination };
        for (var i = end; i >= 0; i = previous[i])
            if (_corners[i] != start)
                route.Add(new SimVector(_corners[i].X, _corners[i].Y));
        route.Reverse();
        return route;
    }

    private Rect2 Interior(Rect2 hole)
    {
        // Shrink navigable faces slightly so a segment on the boundary stays legal.
        // A face clipped by the edge of the map extends past the floor instead.
        var start = hole.Position + new Vector2(
            hole.Position.X == _floor.Position.X ? -BoundaryTolerance : BoundaryTolerance,
            hole.Position.Y == _floor.Position.Y ? -BoundaryTolerance : BoundaryTolerance);
        var end = hole.End - new Vector2(
            hole.End.X == _floor.End.X ? -BoundaryTolerance : BoundaryTolerance,
            hole.End.Y == _floor.End.Y ? -BoundaryTolerance : BoundaryTolerance);
        return new Rect2(start, end - start);
    }

    private bool IsWalkable(Vector2 point) => _floor.Grow(BoundaryTolerance).HasPoint(point)
        && !_interiors.Any(hole => hole.HasPoint(point));

    private bool Clear(Vector2 from, Vector2 to)
    {
        var delta = to - from;
        foreach (var hole in _interiors)
        {
            var entry = 0f;
            var exit = 1f;
            if (Clip(from.X, delta.X, hole.Position.X, hole.End.X, ref entry, ref exit)
                && Clip(from.Y, delta.Y, hole.Position.Y, hole.End.Y, ref entry, ref exit))
                return false;
        }
        return true;
    }

    private static bool Clip(float origin, float direction, float minimum, float maximum, ref float entry, ref float exit)
    {
        if (Mathf.IsZeroApprox(direction))
            return origin > minimum && origin < maximum;
        var first = (minimum - origin) / direction;
        var last = (maximum - origin) / direction;
        entry = Mathf.Max(entry, Mathf.Min(first, last));
        exit = Mathf.Min(exit, Mathf.Max(first, last));
        return entry < exit;
    }

    private static Vector2[] Corners(Rect2 rectangle) =>
        [rectangle.Position, new(rectangle.Position.X, rectangle.End.Y), new(rectangle.End.X, rectangle.Position.Y), rectangle.End];
}
