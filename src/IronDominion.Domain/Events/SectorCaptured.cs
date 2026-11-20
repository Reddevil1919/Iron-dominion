namespace IronDominion.Domain;

public sealed record SectorCaptured(long Tick, EntityId Sector, PlayerId? PreviousOwner, PlayerId Owner) : IGameEvent;
