using System.Collections.Immutable;

namespace IronDominion.Domain;

public readonly record struct CoverZone
{
    public CoverZone(SimVector minimum, SimVector maximum)
    {
        if (minimum.X > maximum.X || minimum.Z > maximum.Z)
            throw new ArgumentException("The maximum must be at or beyond the minimum on both axes.", nameof(maximum));

        Minimum = minimum;
        Maximum = maximum;
    }

    public SimVector Minimum { get; }
    public SimVector Maximum { get; }

    public bool Contains(SimVector position) =>
        position.X >= Minimum.X && position.X <= Maximum.X
        && position.Z >= Minimum.Z && position.Z <= Maximum.Z;

    public static bool Protects(ImmutableArray<CoverZone> zones, Unit unit, SimVector position)
    {
        if (unit.Kind != UnitKind.InfantrySquad || zones.IsDefaultOrEmpty)
            return false;

        foreach (var zone in zones)
        {
            if (zone.Contains(position))
                return true;
        }

        return false;
    }
}
