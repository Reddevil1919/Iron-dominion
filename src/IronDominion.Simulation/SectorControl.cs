using System.Collections.Immutable;
using IronDominion.Domain;

namespace IronDominion.Simulation;

internal sealed class SectorControl(ImmutableArray<SectorState> sectors, WorldEntities world,
    GameConfiguration configuration)
{
    private readonly long _requiredTicks = (long)configuration.CaptureSeconds * configuration.TickRate;
    private readonly double _radiusSquared = (double)configuration.CaptureRadiusMeters * configuration.CaptureRadiusMeters;

    public ImmutableArray<SectorState> Sectors { get; private set; } = sectors;

    public void Advance(long tick, IReadOnlyDictionary<EntityId, SimVector> positions,
        IReadOnlyDictionary<EntityId, int> health, List<IGameEvent> events)
    {
        for (var i = 0; i < Sectors.Length; i++)
        {
            var sector = Sectors[i];
            var (occupant, contested) = InfantryPresence(sector, positions, health);
            var next = UpdateProgress(sector, occupant, contested);
            if (next == sector)
                continue;

            Sectors = Sectors.SetItem(i, next);
            if (next.Owner is { } owner && owner != sector.Owner)
                events.Add(new SectorCaptured(tick, sector.Id, sector.Owner, owner));
        }
    }

    private SectorState UpdateProgress(SectorState sector, PlayerId? occupant, bool contested)
    {
        if (contested)
            return sector with { Contested = true };

        sector = sector with { Contested = false };
        if (occupant is null)
            return sector with { CapturingPlayer = null, CaptureTicks = 0 };

        if (sector.CapturingPlayer is { } challenger && challenger != occupant)
        {
            var remaining = sector.CaptureTicks - 1;
            return sector with { CaptureTicks = remaining, CapturingPlayer = remaining == 0 ? null : challenger };
        }

        if (occupant == sector.Owner)
            return sector;

        var progress = sector.CaptureTicks + 1;
        return progress >= _requiredTicks
            ? sector with { Owner = occupant, CapturingPlayer = null, CaptureTicks = 0 }
            : sector with { CapturingPlayer = occupant, CaptureTicks = progress };
    }

    private (PlayerId? Occupant, bool Contested) InfantryPresence(SectorState sector,
        IReadOnlyDictionary<EntityId, SimVector> positions, IReadOnlyDictionary<EntityId, int> health)
    {
        PlayerId? occupant = null;
        foreach (var (id, position) in positions)
        {
            if (world.Find(id) is not Unit { Kind: UnitKind.InfantrySquad, Owner: { } owner }
                || health[id] == 0)
                continue;

            var x = (double)position.X - sector.Position.X;
            var z = (double)position.Z - sector.Position.Z;
            if (x * x + z * z > _radiusSquared)
                continue;
            if (occupant is { } player && player != owner)
                return (null, true);

            occupant = owner;
        }

        return (occupant, false);
    }
}
