using System.Collections.Immutable;
using IronDominion.Domain;

namespace IronDominion.Simulation;

internal sealed class BuildingConstruction(WorldEntities world, GameConfiguration configuration,
    UnitMovement movement, UnitCombat combat, PlayerEconomy economy)
{
    private readonly Dictionary<BuildingKind, BuildingDefinition> _definitions =
        configuration.Buildings.ToDictionary(definition => definition.Kind);

    public ImmutableArray<ConstructionSite> Sites { get; private set; } = [];

    public CommandResult Availability(PlayerId player, EntityId? headquartersId, BuildingKind kind)
    {
        if (world.Construction is not { } layout || !layout.Zones.ContainsKey(player))
            return new(false, "construction_unavailable");
        if (headquartersId is not { } id || world.Find(id) is not Building { Kind: BuildingKind.Headquarters } headquarters)
            return new(false, "invalid_actor");
        if (headquarters.Owner != player)
            return new(false, "not_owner");
        if (combat.Health[headquarters.Id] <= 0)
            return new(false, "actor_destroyed");
        if (!_definitions.TryGetValue(kind, out var definition) || definition.Construction is not { } spec)
            return new(false, "invalid_building_kind");

        if (kind == BuildingKind.Headquarters && (world.Buildings.Any(building => building.Owner == player
                && building.Kind == BuildingKind.Headquarters && building.Placement is not null && combat.Health[building.Id] > 0)
            || Sites.Any(site => site.Placement.Owner == player && site.Placement.Kind == BuildingKind.Headquarters)))
            return new(false, "reserve_headquarters_limit");

        return economy.Credits[player] < spec.Cost ? new(false, "insufficient_credits") : new(true, null);
    }

    public CommandResult Check(BuildCommand order)
    {
        var availability = Availability(order.Player, order.Headquarters, order.Kind);
        if (!availability.Accepted)
            return availability;

        var layout = world.Construction!;
        var definition = _definitions[order.Kind];
        var spec = definition.Construction!;
        var placement = new BuildingPlacement(order.Player, order.Kind, order.Position, order.QuarterTurns);
        if (!TryFootprint(placement, spec, out var area) || !layout.Bounds.Contains(area))
            return new(false, "invalid_building_location");
        if (!layout.Zones[order.Player].Contains(area))
            return new(false, "outside_construction_zone");

        var buildings = world.Buildings.Where(building => combat.Health[building.Id] > 0);
        var newExit = ProductionExit(placement, definition);
        if (newExit is { } egress && !layout.Bounds.Contains(egress))
            return new(false, "invalid_building_location");

        bool Blocks(ConstructionBounds occupied) => area.Overlaps(occupied)
            || newExit is { } exit && occupied.Contains(exit);
        if (layout.Obstacles.Any(Blocks)
            || buildings.Any(building => Footprint(building) is { } occupied && Blocks(occupied)
                || building.ProductionExit is { } exit && area.Contains(exit))
            || Sites.Any(site => TryFootprint(site.Placement, _definitions[site.Placement.Kind].Construction!, out var reserved)
                && Blocks(reserved) || ProductionExit(site.Placement, _definitions[site.Placement.Kind]) is { } exit
                && area.Contains(exit))
            || movement.Positions.Values.Any(point => area.Contains(point, layout.UnitClearance)))
            return new(false, "building_location_occupied");

        return new(true, null);
    }

    public CommandResult Order(BuildCommand order)
    {
        var result = Check(order);
        if (!result.Accepted)
            return result;

        var spec = _definitions[order.Kind].Construction!;
        if (!economy.TrySpend(order.Player, spec.Cost))
            return new(false, "insufficient_credits");

        var placement = new BuildingPlacement(order.Player, order.Kind, order.Position, order.QuarterTurns);
        Sites = Sites.Add(new ConstructionSite(placement, spec.BuildTicks));
        return result;
    }

    public IEnumerable<ConstructionBounds> NavigationFootprints()
    {
        foreach (var building in world.Buildings)
            if (building.Placement is not null && combat.Health[building.Id] > 0 && Footprint(building) is { } area)
                yield return area;
        foreach (var site in Sites)
            if (TryFootprint(site.Placement, _definitions[site.Placement.Kind].Construction!, out var area))
                yield return area;
    }

    public void Advance(long tick, List<IGameEvent> events)
    {
        if (Sites.IsEmpty)
            return;

        var ongoing = ImmutableArray.CreateBuilder<ConstructionSite>();
        foreach (var site in Sites)
        {
            if (site.RemainingTicks > 1)
            {
                ongoing.Add(site with { RemainingTicks = site.RemainingTicks - 1 });
                continue;
            }

            var definition = _definitions[site.Placement.Kind];
            var building = world.SpawnBuilding(site.Placement, definition.MaxHp, ProductionExit(site.Placement, definition));
            combat.Register(building);
            events.Add(new BuildingCompleted(tick, building));
        }
        Sites = ongoing.ToImmutable();
    }

    public void Restore(ImmutableArray<ConstructionSite> sites)
    {
        var constructed = world.Buildings.Where(building => building.Placement is not null).ToArray();
        if (sites.IsDefault || (world.Construction is null && (!sites.IsEmpty || constructed.Length > 0)))
            throw new InvalidDataException("Invalid saved construction layout.");

        var occupied = world.Buildings.Where(building => building.Placement is null && combat.Health[building.Id] > 0)
            .Select(Footprint).OfType<ConstructionBounds>().ToList();
        var reserves = new HashSet<PlayerId>();
        foreach (var building in constructed)
            CheckReservation(building.Placement!, combat.Health[building.Id] > 0);
        foreach (var site in sites)
        {
            if (site?.Placement is not { } placement || !economy.Credits.ContainsKey(placement.Owner)
                || _definitions.GetValueOrDefault(placement.Kind)?.Construction is not { } plan
                || site.RemainingTicks <= 0 || site.RemainingTicks > plan.BuildTicks)
                throw new InvalidDataException("Invalid saved construction progress.");
            CheckReservation(placement, true);
        }
        Sites = sites;

        void CheckReservation(BuildingPlacement placement, bool live)
        {
            var layout = world.Construction!;
            var plan = _definitions[placement.Kind].Construction!;
            if (!TryFootprint(placement, plan, out var area) || !layout.Bounds.Contains(area)
                || !layout.Zones.TryGetValue(placement.Owner, out var zone) || !zone.Contains(area)
                || layout.Obstacles.Any(area.Overlaps)
                || live && (occupied.Any(area.Overlaps)
                    || placement.Kind == BuildingKind.Headquarters && !reserves.Add(placement.Owner)))
                throw new InvalidDataException("Invalid saved construction footprint or reserve headquarters.");
            if (live)
                occupied.Add(area);
        }
    }

    private ConstructionBounds? Footprint(Building building)
    {
        var placement = building.Placement ?? new BuildingPlacement(building.Owner!.Value, building.Kind, building.Position, 0);
        return _definitions.GetValueOrDefault(building.Kind)?.Construction is { } spec
            && TryFootprint(placement, spec, out var area) ? area : null;
    }

    public static bool TryFootprint(BuildingPlacement placement, ConstructionDefinition spec, out ConstructionBounds area)
    {
        area = default;
        if (placement.QuarterTurns is < 0 or > 3)
            return false;
        var rotated = placement.QuarterTurns % 2 != 0;
        var x = (rotated ? spec.Depth : spec.Width) / 2f;
        var z = (rotated ? spec.Width : spec.Depth) / 2f;
        var minX = placement.Position.X - x;
        var maxX = placement.Position.X + x;
        var minZ = placement.Position.Z - z;
        var maxZ = placement.Position.Z + z;
        if (!float.IsFinite(minX) || !float.IsFinite(maxX) || !float.IsFinite(minZ) || !float.IsFinite(maxZ)
            || minX >= maxX || minZ >= maxZ)
            return false;
        area = new ConstructionBounds(new SimVector(minX, minZ), new SimVector(maxX, maxZ));
        return true;
    }

    public static SimVector? ProductionExit(BuildingPlacement placement, BuildingDefinition definition)
    {
        if (placement.Kind is not (BuildingKind.Barracks or BuildingKind.TankFactory))
            return null;
        var distance = definition.Construction!.Depth / 2f + 5f;
        return placement.QuarterTurns switch
        {
            0 => new SimVector(placement.Position.X, placement.Position.Z + distance),
            1 => new SimVector(placement.Position.X + distance, placement.Position.Z),
            2 => new SimVector(placement.Position.X, placement.Position.Z - distance),
            _ => new SimVector(placement.Position.X - distance, placement.Position.Z)
        };
    }
}
