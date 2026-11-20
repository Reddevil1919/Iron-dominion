using Godot;
using IronDominion.Domain;

namespace IronDominion.Game;

internal sealed class BattlefieldSight(World3D world, uint obstacleLayer,
    IReadOnlyDictionary<EntityId, StaticBody3D> buildings) : ILineOfSightService
{
    public bool HasLineOfSight(SimVector origin, SimVector target, EntityId targetId)
    {
        var start = new Vector3(origin.X, 1.5f, origin.Z);
        var end = new Vector3(target.X, 1.5f, target.Z);
        if (start == end)
            return true;

        using var query = PhysicsRayQueryParameters3D.Create(start, end, obstacleLayer);
        query.HitFromInside = true;
        if (buildings.TryGetValue(targetId, out var building))
            query.Exclude = [building.GetRid()];

        using var hit = world.DirectSpaceState.IntersectRay(query);
        return hit.Count == 0;
    }
}
