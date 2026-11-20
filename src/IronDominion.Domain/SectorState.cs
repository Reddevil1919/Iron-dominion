namespace IronDominion.Domain;

public readonly record struct SectorState(EntityId Id, SimVector Position, PlayerId? Owner,
    PlayerId? CapturingPlayer, long CaptureTicks, bool Contested);
