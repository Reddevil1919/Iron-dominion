namespace IronDominion.Domain;

public sealed record TankRepaired(long Tick, EntityId Engineer, EntityId Tank, int RestoredHitPoints,
    int RemainingHitPoints) : IGameEvent;
