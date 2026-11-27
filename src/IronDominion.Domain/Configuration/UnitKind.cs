namespace IronDominion.Domain;

public enum UnitKind
{
    InfantrySquad,
    Tank,
    EngineerSquad
}

public static class UnitCapabilities
{
    public static bool CanAttack(this UnitKind kind) => kind is UnitKind.InfantrySquad or UnitKind.Tank;
    public static bool UsesCover(this UnitKind kind) => kind is UnitKind.InfantrySquad or UnitKind.EngineerSquad;
}
