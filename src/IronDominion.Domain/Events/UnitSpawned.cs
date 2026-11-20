namespace IronDominion.Domain;

public sealed record UnitSpawned(long Tick, Unit Unit) : IGameEvent;
