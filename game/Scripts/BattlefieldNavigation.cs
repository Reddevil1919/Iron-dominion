using Godot;
using IronDominion.Domain;

namespace IronDominion.Game;

internal sealed class BattlefieldNavigation : INavigationService
{
    private const float UnitClearance = 5f;
    private const float EndpointTolerance = 0.05f;

    private readonly NavigationRegion3D _region;
    private readonly float _height;

    public BattlefieldNavigation(NavigationRegion3D region, CollisionShape3D terrain,
        IReadOnlyList<CollisionShape3D> blockers)
    {
        _region = region;
        var bounds = BoxBounds(terrain);
        _height = bounds.End.Y;
        var floor = Footprint(bounds).Grow(-UnitClearance);
        var holes = blockers.Select(blocker => Footprint(BoxBounds(blocker)).Grow(UnitClearance).Intersection(floor))
            .Where(rect => rect.HasArea())
            .ToArray();
        _region.NavigationMesh = CreateMesh(floor, holes);
    }

    public long PathRequests { get; private set; }

    public IReadOnlyList<SimVector> FindPath(SimVector start, SimVector destination, int movementMask)
    {
        PathRequests++;
        var map = _region.GetNavigationMap();
        if (NavigationServer3D.MapGetIterationId(map) == 0)
            return [];

        var from = new Vector3(start.X, _height, start.Z);
        var to = new Vector3(destination.X, _height, destination.Z);
        if (!Near(NavigationServer3D.MapGetClosestPoint(map, from), from)
            || !Near(NavigationServer3D.MapGetClosestPoint(map, to), to))
            return [];

        if (start == destination)
            return [destination];

        var path = NavigationServer3D.MapGetPath(map, from, to, optimize: true, (uint)movementMask);
        if (path.Length < 2 || !Near(path[0], from) || !Near(path[^1], to))
            return [];

        var waypoints = new SimVector[path.Length - 1];
        for (var i = 1; i < path.Length - 1; i++)
            waypoints[i - 1] = new SimVector(path[i].X, path[i].Z);
        waypoints[^1] = destination;
        return waypoints;
    }

    private NavigationMesh CreateMesh(Rect2 floor, IReadOnlyList<Rect2> holes)
    {
        var xs = new SortedSet<float> { floor.Position.X, floor.End.X };
        var zs = new SortedSet<float> { floor.Position.Y, floor.End.Y };
        foreach (var hole in holes)
        {
            xs.UnionWith([hole.Position.X, hole.End.X]);
            zs.UnionWith([hole.Position.Y, hole.End.Y]);
        }

        var columns = xs.ToArray();
        var rows = zs.ToArray();
        var width = columns.Length;
        var vertices = new Vector3[width * rows.Length];
        for (var row = 0; row < rows.Length; row++)
        for (var column = 0; column < width; column++)
            vertices[row * width + column] = _region.ToLocal(new Vector3(columns[column], _height, rows[row]));

        var mesh = new NavigationMesh();
        mesh.SetVertices(vertices);
        for (var row = 0; row < rows.Length - 1; row++)
        for (var column = 0; column < width - 1; column++)
        {
            var center = new Vector2((columns[column] + columns[column + 1]) / 2f,
                (rows[row] + rows[row + 1]) / 2f);
            if (holes.Any(hole => hole.HasPoint(center)))
                continue;

            var index = row * width + column;
            mesh.AddPolygon([index, index + width, index + width + 1, index + 1]);
        }

        return mesh;
    }

    private static Aabb BoxBounds(CollisionShape3D collider)
    {
        var box = (BoxShape3D)collider.Shape;
        return collider.GlobalTransform * new Aabb(-box.Size / 2f, box.Size);
    }

    private static Rect2 Footprint(Aabb bounds) =>
        new(bounds.Position.X, bounds.Position.Z, bounds.Size.X, bounds.Size.Z);

    private static bool Near(Vector3 first, Vector3 second) =>
        first.DistanceSquaredTo(second) <= EndpointTolerance * EndpointTolerance;
}
