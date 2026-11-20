namespace IronDominion.Domain;

public sealed record EntityDamaged(long Tick, EntityId Entity, int Damage, int RemainingHitPoints) : IGameEvent;
