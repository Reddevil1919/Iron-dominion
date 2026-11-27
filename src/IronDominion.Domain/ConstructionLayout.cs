using System.Collections.Immutable;

namespace IronDominion.Domain;

public readonly record struct ConstructionBounds
{
    public ConstructionBounds(SimVector minimum, SimVector maximum)
    {
        if (!float.IsFinite(minimum.X) || !float.IsFinite(minimum.Z) || !float.IsFinite(maximum.X)
            || !float.IsFinite(maximum.Z) || minimum.X >= maximum.X || minimum.Z >= maximum.Z)
            throw new ArgumentException("A construction rectangle requires finite, ordered bounds.");
        Minimum = minimum;
        Maximum = maximum;
    }

    public SimVector Minimum { get; }
    public SimVector Maximum { get; }

    public bool Contains(SimVector point) =>
        point.X >= Minimum.X && point.X <= Maximum.X && point.Z >= Minimum.Z && point.Z <= Maximum.Z;

    public bool Contains(ConstructionBounds area) => Contains(area.Minimum) && Contains(area.Maximum);

    public bool Overlaps(ConstructionBounds area) => Minimum.X < area.Maximum.X && Maximum.X > area.Minimum.X
        && Minimum.Z < area.Maximum.Z && Maximum.Z > area.Minimum.Z;
}

public sealed record ConstructionLayout(ConstructionBounds Bounds,
    ImmutableDictionary<PlayerId, ConstructionBounds> Zones, ImmutableArray<ConstructionBounds> Obstacles);

public sealed record BuildingPlacement(PlayerId Owner, BuildingKind Kind, SimVector Position, int QuarterTurns);
public sealed record ConstructionSite(BuildingPlacement Placement, int RemainingTicks);
