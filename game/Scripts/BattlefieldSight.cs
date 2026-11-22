using Godot;
using IronDominion.Domain;

namespace IronDominion.Game;

internal sealed class BattlefieldSight(World3D world,
    IReadOnlyDictionary<EntityId, StaticBody3D> buildings) : ILineOfSightService
{
    private const uint ObstacleLayer = 1u << 2;
    private const float VisionRadius = 42f;

    public bool HasLineOfSight(SimVector origin, SimVector target, EntityId targetId) =>
        CanSee(origin, target, buildings.GetValueOrDefault(targetId));

    public bool CanSeeGround(SimVector origin, SimVector target) => CanSee(origin, target, null);

    private bool CanSee(SimVector origin, SimVector target, StaticBody3D? targetBuilding)
    {
        var offset = new Vector2(target.X - origin.X, target.Z - origin.Z);
        if (offset.LengthSquared() > VisionRadius * VisionRadius)
            return false;

        var start = new Vector3(origin.X, 1.5f, origin.Z);
        var end = new Vector3(target.X, 1.5f, target.Z);
        if (start == end)
            return true;

        using var query = PhysicsRayQueryParameters3D.Create(start, end, ObstacleLayer);
        query.HitFromInside = true;
        if (targetBuilding is not null)
            query.Exclude = [targetBuilding.GetRid()];

        using var hit = world.DirectSpaceState.IntersectRay(query);
        return hit.Count == 0;
    }
}
