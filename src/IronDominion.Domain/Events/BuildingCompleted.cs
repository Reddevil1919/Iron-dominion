namespace IronDominion.Domain;

public sealed record BuildingCompleted(long Tick, Building Building) : IGameEvent;
