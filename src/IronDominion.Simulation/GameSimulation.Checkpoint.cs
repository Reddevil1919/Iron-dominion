using System.Collections.Immutable;
using IronDominion.Domain;

namespace IronDominion.Simulation;

public sealed partial class GameSimulation
{
    public SimulationCheckpoint Capture()
    {
        if (_movement is not { } movement || _combat is not { } combat || _economy is not { } economy)
            throw new InvalidOperationException("Saving requires a full match simulation.");

        var units = _entities.Units.OrderBy(unit => unit.Id.Value).Select(unit => new UnitCheckpoint(
            unit.Id, unit.Owner!.Value, unit.Kind, unit.Position, unit.HitPoints, combat.Health[unit.Id],
            movement.Positions.TryGetValue(unit.Id, out var position) ? position : null,
            movement.CaptureRoute(unit.Id), combat.FocusedTarget(unit.Id), combat.NextShot(unit.Id),
            combat.IsSuppressed(unit.Id, World.Tick) ? combat.Suppression[unit.Id] : null,
            _repairs?.TargetOf(unit.Id))).ToImmutableArray();
        var buildings = _entities.Buildings.OrderBy(building => building.Id.Value).Select(building =>
            new BuildingCheckpoint(building.Id, combat.Health[building.Id],
                World.TrainingQueue(building.Id).ToImmutableArray())).ToImmutableArray();
        var pending = _pending.UnorderedItems.OrderBy(item => item.Priority)
            .Select(item => CommandCheckpoint.Capture(item.Element)).ToImmutableArray();
        var used = _scheduledSequences.OrderBy(item => item.Player.Value).ThenBy(item => item.Sequence)
            .Select(item => new CommandSequence(item.Player, item.Sequence)).ToImmutableArray();
        return new SimulationCheckpoint(World.Tick, World.Result, units, buildings, World.Sectors.ToImmutableArray(),
            economy.Capture(), pending, used);
    }

    public static GameSimulation Restore(WorldEntities map, GameConfiguration configuration,
        INavigationService navigation, ILineOfSightService sight, SimulationCheckpoint saved)
    {
        ValidateHeader(saved);
        var players = map.Units.Cast<WorldEntity>().Concat(map.Buildings).Concat(map.Sectors)
            .Select(entity => entity.Owner).OfType<PlayerId>().ToHashSet();
        var world = RestoreEntities(map, players, configuration, saved);
        ValidateMapState(map, players, configuration, saved);

        var simulation = new GameSimulation(world, configuration, navigation, sight);
        simulation._movement!.Restore(saved.Units);
        simulation._combat!.Restore(saved.Units, saved.Buildings);
        simulation._repairs!.Restore(saved.Units);
        var sectors = saved.Sectors.OrderBy(sector => sector.Id.Value).ToImmutableArray();
        simulation._sectorControl!.Restore(sectors);
        simulation._economy!.Restore(saved.Players, simulation._combat.Health, sectors);
        simulation._production!.Restore(saved.Buildings);
        Require((saved.Tick == 0 ? MatchResult.Running : simulation.ResolveMatchResult()) == saved.Result, "Saved match result disagrees with headquarters health.");
        simulation.World = simulation.Snapshot(saved.Tick, saved.Result);
        simulation.RestoreOrders(saved);
        return simulation;
    }

    private static void ValidateHeader(SimulationCheckpoint saved)
    {
        Require(saved.Tick >= 0 && saved.Tick < long.MaxValue - int.MaxValue && Enum.IsDefined(saved.Result),
            "Invalid saved tick or result.");
        Require(!saved.Units.IsDefault && !saved.Buildings.IsDefault && !saved.Sectors.IsDefault &&
            !saved.Players.IsDefault && !saved.Pending.IsDefault && !saved.UsedSequences.IsDefault,
            "Match state is incomplete.");
        Require(saved.Result == MatchResult.Running || (saved.Pending.IsEmpty && saved.UsedSequences.IsEmpty),
            "A finished match has pending command state.");
    }

    private static WorldEntities RestoreEntities(WorldEntities map, HashSet<PlayerId> players,
        GameConfiguration configuration, SimulationCheckpoint saved)
    {
        var staticActors = map.Buildings.Cast<WorldEntity>().Concat(map.Sectors).ToArray();
        var initialUnits = map.Units.ToDictionary(unit => unit.Id);
        var initialIdLimit = staticActors.Select(entity => entity.Id).Concat(initialUnits.Keys)
            .Select(id => id.Value).DefaultIfEmpty().Max();
        var lastSpawnId = (long)initialIdLimit + saved.Units.Length - initialUnits.Count;
        var ids = staticActors.Select(entity => entity.Id).ToHashSet();
        var kinds = configuration.Units.Select(definition => definition.Kind).ToHashSet();
        foreach (var unit in saved.Units)
        {
            Require(unit.Id.Value > 0 && ids.Add(unit.Id) && players.Contains(unit.Owner) && kinds.Contains(unit.Kind)
                && IsFinite(unit.SpawnPosition) && unit.HitPoints >= 0 && unit.HitPoints <= unit.StartingHitPoints,
                "Invalid or duplicate saved unit.");
            if (initialUnits.TryGetValue(unit.Id, out var initial))
            {
                Require(initial.Owner == unit.Owner && initial.Kind == unit.Kind && initial.Position == unit.SpawnPosition
                    && initial.HitPoints == unit.StartingHitPoints, "An initial unit does not match this battlefield.");
                initialUnits.Remove(unit.Id);
            }
            else
            {
                Require(unit.Id.Value > initialIdLimit && unit.Id.Value <= lastSpawnId,
                    "A saved unit has an invalid spawn ID.");
            }

            var alive = unit.HitPoints > 0;
            Require(unit.Position is { } position ? IsFinite(position) && (alive || unit.StartingHitPoints == 0) : !alive,
                "Invalid saved unit position.");
            Require(unit.NextShot is null || (alive && unit.Kind.CanAttack()
                && unit.NextShot >= 1 && unit.NextShot <= saved.Tick + int.MaxValue),
                "Invalid saved weapon cooldown.");
            if (unit.SuppressedThrough is { } through)
            {
                Require(alive && unit.Kind.UsesCover() && configuration.Suppression is { } rules
                    && saved.Tick > 0 && through >= saved.Tick && through <= saved.Tick + rules.DurationTicks,
                    "Invalid saved suppression timer.");
            }
            if (unit.Route is { } route)
            {
                Require(alive && !route.Waypoints.IsDefaultOrEmpty && route.Waypoints.All(IsFinite),
                    "Invalid saved movement route.");
            }
        }
        Require(initialUnits.Count == 0, "A saved match is missing initial units.");

        var world = new WorldEntities(saved.Units.OrderBy(unit => unit.Id.Value)
            .Select(unit => (WorldEntity)unit.CreateUnit()).Concat(staticActors), map.CoverZones);
        var unitStates = saved.Units.ToDictionary(unit => unit.Id);
        foreach (var unit in saved.Units)
        {
            if (unit.FocusedTarget is { } target)
                Require(unit.HitPoints > 0 && unit.Kind.CanAttack() && world.Find(target) is Unit or Building
                    && world.Find(target)!.Owner != unit.Owner, "Invalid saved focused target.");
            if (unit.RepairTarget is { } tank)
                Require(unit.HitPoints > 0 && unit.Kind == UnitKind.EngineerSquad
                    && unit.Route is not { AttackMove: true } && unitStates.TryGetValue(tank, out var targetState)
                    && targetState.Kind == UnitKind.Tank && targetState.Owner == unit.Owner
                    && targetState.HitPoints > 0 && targetState.HitPoints < targetState.StartingHitPoints,
                    "Invalid saved repair target.");
        }
        return world;
    }

    private static void ValidateMapState(WorldEntities map, HashSet<PlayerId> players,
        GameConfiguration configuration, SimulationCheckpoint saved)
    {
        var buildingIds = map.Buildings.Select(building => building.Id).ToHashSet();
        var unitDefinitions = configuration.Units.ToDictionary(unit => unit.Kind);
        foreach (var building in saved.Buildings)
        {
            Require(buildingIds.Remove(building.Id) && map.Find(building.Id) is Building original
                && building.HitPoints >= 0 && building.HitPoints <= original.HitPoints && !building.Queue.IsDefault,
                "Invalid saved building state.");
            var producer = (Building)map.Find(building.Id)!;
            Require(building.Queue.IsEmpty || (building.HitPoints > 0 && producer.ProductionExit.HasValue
                && building.Queue.All(order => order is not null && producer.CanTrain(order.Kind)
                    && unitDefinitions.TryGetValue(order.Kind, out var definition)
                    && order.RemainingTicks >= 0 && order.RemainingTicks <= definition.BuildTicks)),
                "Invalid saved production queue.");
        }
        Require(buildingIds.Count == 0, "A saved match is missing buildings.");

        var sectorIds = map.Sectors.Select(sector => sector.Id).ToHashSet();
        var captureLimit = Math.Max(1L, (long)configuration.CaptureSeconds * configuration.TickRate);
        foreach (var sector in saved.Sectors)
        {
            Require(sectorIds.Remove(sector.Id) && map.Find(sector.Id)?.Position == sector.Position
                && (sector.Owner is null || players.Contains(sector.Owner.Value))
                && (sector.CapturingPlayer is null || players.Contains(sector.CapturingPlayer.Value))
                && sector.CaptureTicks >= 0 && sector.CaptureTicks < captureLimit
                && (sector.CapturingPlayer is null) == (sector.CaptureTicks == 0), "Invalid saved sector state.");
        }
        Require(sectorIds.Count == 0, "A saved match is missing sectors.");

        foreach (var player in saved.Players)
            Require(players.Remove(player.Id) && player.Credits >= 0
                && player.IncomeRemainder >= 0 && player.IncomeRemainder < configuration.TickRate, "Invalid saved economy.");
        Require(players.Count == 0, "A saved match is missing player balances.");
    }

    private void RestoreOrders(SimulationCheckpoint saved)
    {
        foreach (var order in saved.Pending)
        {
            Require(order.Tick > saved.Tick && order.Player.Value > 0 && order.Sequence > 0
                && !order.Units.IsDefault && IsFinite(order.Destination)
                && _scheduledSequences.Add((order.Player, order.Sequence)), "Invalid or duplicate pending order.");
            _pending.Enqueue(order.Restore(), (order.Tick, order.Player.Value, order.Sequence));
        }

        var used = new HashSet<(PlayerId, long)>();
        foreach (var sequence in saved.UsedSequences)
            Require(sequence.Player.Value > 0 && sequence.Sequence > 0 && used.Add((sequence.Player, sequence.Sequence)),
                "Invalid saved command sequence.");
        Require(used.IsSupersetOf(_scheduledSequences), "Pending order is missing its sequence.");
        _scheduledSequences.UnionWith(used);
    }

    private static bool IsFinite(SimVector position) => float.IsFinite(position.X) && float.IsFinite(position.Z);

    private static void Require(bool condition, string reason)
    {
        if (!condition)
            throw new InvalidDataException(reason);
    }
}
