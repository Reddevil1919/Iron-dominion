using Godot;
using IronDominion.AI;
using IronDominion.Application;
using IronDominion.Domain;
using IronDominion.Infrastructure.Persistence;

namespace IronDominion.Game;

public partial class Battlefield
{
    private void SaveMatch(MatchSaveFile file, BattlefieldFog fog) =>
        file.Write(new BattlefieldCheckpoint(_session!.Capture(), _enemyCommander?.Capture(), fog.CaptureExplored()));

    private GameSession LoadMatch(MatchSaveFile file, WorldEntities map, GameConfiguration configuration,
        BattlefieldNavigation navigation, BattlefieldSight sight, BattlefieldFog fog)
    {
        var saved = file.Read<BattlefieldCheckpoint>();
        var session = GameSession.Restore(map, configuration, navigation, sight, saved.Session);
        var simulation = saved.Session.Simulation;
        var floor = BattlefieldNavigation.BoxBounds(GetNode<CollisionShape3D>("Terrain/GroundCollision"));
        var bounds = new Rect2(floor.Position.X, floor.Position.Z, floor.Size.X, floor.Size.Z);
        bool IsOnMap(SimVector point) => bounds.HasPoint(new Vector2(point.X, point.Z));
        if (simulation.Units.Any(unit => !IsOnMap(unit.SpawnPosition)
            || (unit.Position is { } position && !IsOnMap(position))
            || (unit.Route is { } route && !route.Waypoints.All(IsOnMap))))
            throw new InvalidDataException("Saved unit coordinates are outside this battlefield.");
        var enemy = saved.Enemy is { } state
            ? EnemyCommander.Restore(configuration, state,
                simulation.Units.Where(unit => unit.Owner == Opponent).Select(unit => unit.Id).ToHashSet(),
                simulation.Units.Select(unit => unit.Id).Concat(simulation.Buildings.Select(building => building.Id)).ToHashSet(),
                simulation.Sectors.Select(sector => sector.Id).ToHashSet())
            : null;

        // Stage the simulation, AI and map data before replacing the active match.
        var observation = session.Observe(LocalPlayer);
        fog.RestoreExplored(saved.Explored);
        fog.Update(observation);
        _session = session;
        _enemyCommander = enemy;
        _enemyCommands = enemy is null ? null : session.Commands.ForPlayer(Opponent);
        _unitSelection.ResetForLoad();
        _feedback?.Reset();
        _casualties?.Reset();

        foreach (var view in _views)
        {
            _unitRoot.RemoveChild(view);
            view.QueueFree();
        }
        _views.Clear();
        _healthBars.Clear();

        foreach (var unit in simulation.Units.Where(unit => unit.HitPoints > 0))
        {
            AddUnitView(unit.CreateUnit());
            _healthBars[unit.Id].SetHitPoints(unit.HitPoints);
        }

        foreach (var building in simulation.Buildings)
        {
            var body = _buildings[building.Id];
            RemoveBuildingBar(body);
            if (building.HitPoints == 0)
            {
                ShowBuildingRuin(body);
                continue;
            }

            body.GetNode<MeshInstance3D>("Base").MaterialOverride = null;
            var maxHp = ((Building)map.Find(building.Id)!).HitPoints;
            var bar = new HealthBar(body, maxHp, 7.3f, 0.14f);
            _healthBars.Add(building.Id, bar);
            bar.SetHitPoints(building.HitPoints);
        }

        UpdateActorVisibility(fog.VisibleActors);
        UpdateUnitViews(session.World);
        foreach (var view in _views)
            view.ResetPhysicsInterpolation();
        _contactShadows?.Render(_views);
        _productionPanel?.Update(session.World);
        _sectorBoard?.Update(session.World, fog.CanSeeGround);
        _selectionPanel?.Update(session.World, _views);
        return session;
    }

    private static void RemoveBuildingBar(StaticBody3D building)
    {
        if (building.GetNodeOrNull<Sprite3D>("HealthBar") is not { } bar)
            return;

        building.RemoveChild(bar);
        bar.QueueFree();
    }

    private static void ShowBuildingRuin(StaticBody3D building)
    {
        var ruin = new StandardMaterial3D { AlbedoColor = new Color("505862") };
        building.GetNode<MeshInstance3D>("Base").MaterialOverride = ruin;
    }

    private sealed record BattlefieldCheckpoint(SessionCheckpoint Session, CommanderCheckpoint? Enemy, byte[] Explored);
}
