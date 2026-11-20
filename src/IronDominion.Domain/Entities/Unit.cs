namespace IronDominion.Domain;

public sealed class Unit : WorldEntity
{
    public Unit(EntityId id, PlayerId owner, SimVector position, UnitKind kind, int hitPoints)
        : base(id, owner, position)
    {
        if (!Enum.IsDefined(kind))
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown unit kind.");
        ArgumentOutOfRangeException.ThrowIfNegative(hitPoints);

        Kind = kind;
        HitPoints = hitPoints;
    }

    public UnitKind Kind { get; }
    public int HitPoints { get; }
}
