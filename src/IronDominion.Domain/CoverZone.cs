using System.Collections.Immutable;

namespace IronDominion.Domain;

public enum CoverFacing { AllDirections, East, West, North, South }

public readonly record struct CoverZone
{
    public CoverZone(SimVector minimum, SimVector maximum, CoverFacing facing = CoverFacing.AllDirections)
    {
        if (minimum.X > maximum.X || minimum.Z > maximum.Z)
            throw new ArgumentException("The maximum must be at or beyond the minimum on both axes.", nameof(maximum));

        if (!Enum.IsDefined(facing))
            throw new ArgumentOutOfRangeException(nameof(facing));

        Minimum = minimum;
        Maximum = maximum;
        Facing = facing;
    }

    public SimVector Minimum { get; }
    public SimVector Maximum { get; }
    public CoverFacing Facing { get; }

    public bool Contains(SimVector position) =>
        position.X >= Minimum.X && position.X <= Maximum.X
        && position.Z >= Minimum.Z && position.Z <= Maximum.Z;

    public static bool Protects(ImmutableArray<CoverZone> zones, Unit unit, SimVector position, SimVector? incomingFrom = null)
    {
        if (unit.Kind != UnitKind.InfantrySquad || zones.IsDefaultOrEmpty)
            return false;

        foreach (var zone in zones)
        {
            if (zone.Contains(position) && (incomingFrom is not { } source || zone.Faces(position, source)))
                return true;
        }

        return false;
    }
    private bool Faces(SimVector position, SimVector source)
    {
        var x = (double)source.X - position.X;
        var z = (double)source.Z - position.Z;
        return Facing switch
        {
            CoverFacing.East => x > 0 && x >= Math.Abs(z),
            CoverFacing.West => x < 0 && -x >= Math.Abs(z),
            CoverFacing.North => z < 0 && -z >= Math.Abs(x),
            CoverFacing.South => z > 0 && z >= Math.Abs(x),
            _ => true
        };
    }
}
