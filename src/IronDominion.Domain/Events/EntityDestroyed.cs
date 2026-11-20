namespace IronDominion.Domain;

public sealed record EntityDestroyed(long Tick, EntityId Entity) : IGameEvent;
