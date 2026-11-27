namespace IronDominion.Domain;

public sealed record BuildingDefinition(BuildingKind Kind, int MaxHp, ConstructionDefinition? Construction = null);

public sealed record ConstructionDefinition(int Cost, int BuildTicks, float Width, float Depth);
