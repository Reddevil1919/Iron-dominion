namespace IronDominion.Domain;

public sealed class Building : WorldEntity
{
    public Building(EntityId id, PlayerId owner, SimVector position, BuildingKind kind, int hitPoints,
        SimVector? productionExit = null, BuildingPlacement? placement = null)
        : base(id, owner, position)
    {
        if (!Enum.IsDefined(kind))
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown building kind.");
        ArgumentOutOfRangeException.ThrowIfNegative(hitPoints);

        Kind = kind;
        HitPoints = hitPoints;
        ProductionExit = productionExit;
        Placement = placement;
    }

    public BuildingKind Kind { get; }
    public int HitPoints { get; }
    public SimVector? ProductionExit { get; }
    public BuildingPlacement? Placement { get; }
    public UnitKind TrainedUnitKind => Kind is BuildingKind.Headquarters or BuildingKind.TankFactory
        ? UnitKind.Tank : UnitKind.InfantrySquad;
}
