namespace IronDominion.Domain;

public sealed class Building : WorldEntity
{
    public Building(EntityId id, PlayerId owner, SimVector position, BuildingKind kind, int hitPoints)
        : base(id, owner, position)
    {
        if (!Enum.IsDefined(kind))
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown building kind.");
        ArgumentOutOfRangeException.ThrowIfNegative(hitPoints);

        Kind = kind;
        HitPoints = hitPoints;
    }

    public BuildingKind Kind { get; }
    public int HitPoints { get; }
}
