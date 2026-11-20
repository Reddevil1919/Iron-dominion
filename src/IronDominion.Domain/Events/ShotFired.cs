namespace IronDominion.Domain;

public sealed record ShotFired(long Tick, EntityId Attacker, EntityId Target) : IGameEvent;
