namespace IronDominion.Domain;

public sealed record UnitDefinition(
    UnitKind Kind,
    int MaxHp,
    int Cost,
    int BuildTicks,
    float SpeedMetersPerSecond,
    float AttackRangeMeters,
    int DamagePerShot,
    int CooldownTicks);
