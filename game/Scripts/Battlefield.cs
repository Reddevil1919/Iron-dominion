using System.Diagnostics;
using Godot;
using IronDominion.AI;
using IronDominion.Application;
using IronDominion.Domain;
using IronDominion.Infrastructure.Configuration;
using IronDominion.Infrastructure.Persistence;

namespace IronDominion.Game;

public partial class Battlefield : Node3D
{
    private const int EngineSmokeIntervalTicks = 5;
    private static readonly PlayerId LocalPlayer = new(1);
    private static readonly PlayerId Opponent = new(2);

    [Export] private UnitSelectionController _unitSelection = null!;
    [Export] private Node3D _unitRoot = null!;

    [Export] private NavigationRegion3D _navigationRegion = null!;

    private readonly List<UnitView> _views = new();
    private readonly Dictionary<EntityId, HealthBar> _healthBars = new();
    private readonly Dictionary<EntityId, StaticBody3D> _buildings = new();
    private readonly Dictionary<StaticBody3D, EntityId> _enemyBuildings = new();
    private readonly Dictionary<PlayerId, StandardMaterial3D> _teamMaterials = new();
    private readonly Dictionary<PlayerId, UnitArt> _unitArt = new();
    private readonly HashSet<EntityId> _visibleLosses = new();
    private ProductionPanel? _productionPanel;
    private SectorBoard? _sectorBoard;
    private SelectionPanel? _selectionPanel;
    private BattlefieldFeedback? _feedback;
    private BattlefieldContactShadows? _contactShadows;
    private BattlefieldFog? _fog;
    private BattlefieldCasualties? _casualties;
    private MatchMenu? _matchMenu;
    private GameSession? _session;
    private BattlefieldBenchmark? _benchmark;
    private BattlefieldNavigation? _navigation;
    private EnemyCommander? _enemyCommander;
    private PlayerCommands? _enemyCommands;

    public override void _Ready()
    {
        try
        {
            var configuration = GameConfigurationLoader.LoadDefault();
            Engine.PhysicsTicksPerSecond = configuration.TickRate;
            var benchmark = BenchmarkOptions.Parse(OS.GetCmdlineUserArgs());
            SetUpUnits(configuration, benchmark);
        }
        catch (InvalidDataException exception)
        {
            GD.PushError(exception.Message);
            SetPhysicsProcess(false);
            SetProcess(false);
            GetTree().Quit(1);
        }
    }

    private void SetUpUnits(GameConfiguration configuration, BenchmarkOptions? benchmark)
    {
        var infantry = configuration.Units.Single(unit => unit.Kind == UnitKind.InfantrySquad);
        var tank = configuration.Units.Single(unit => unit.Kind == UnitKind.Tank);
        Unit[] units = benchmark is null
            ? [
                CreateUnit(1, LocalPlayer, infantry, -20, 18),
                CreateUnit(2, LocalPlayer, infantry, -15, 29),
                CreateUnit(3, LocalPlayer, tank, -17, 7),
                CreateUnit(4, Opponent, infantry, 20, -18),
                CreateUnit(5, Opponent, infantry, 15, -29),
                CreateUnit(6, Opponent, tank, 17, -7)
            ]
            : BattlefieldBenchmark.CreateRoster(benchmark, LocalPlayer, Opponent);
        if (benchmark is not null)
            _benchmark = new BattlefieldBenchmark(benchmark, units);
        _teamMaterials.Add(LocalPlayer, new StandardMaterial3D { AlbedoColor = new Color("3da9fc") });
        _teamMaterials.Add(Opponent, new StandardMaterial3D { AlbedoColor = new Color("f7793c") });
        foreach (var (player, material) in _teamMaterials)
            _unitArt.Add(player, new UnitArt(material.AlbedoColor, player == LocalPlayer));
        foreach (var unit in units)
            AddUnitView(unit);

        var headquarters = configuration.Buildings.Single(building => building.Kind == BuildingKind.Headquarters);
        var barracks = configuration.Buildings.Single(building => building.Kind == BuildingKind.Barracks);
        var blueHq = AddBuilding(new EntityId(7), LocalPlayer, headquarters, "BlueHQ");
        var orangeHq = AddBuilding(new EntityId(8), Opponent, headquarters, "OrangeHQ");
        var blueBarracks = AddBuilding(new EntityId(9), LocalPlayer, barracks, "BlueBarracks");
        var orangeBarracks = AddBuilding(new EntityId(10), Opponent, barracks, "OrangeBarracks");
        var canvas = GetNode("SelectionLayer");
        var sectorBoard = new SectorBoard(GetNode<Node3D>("Sectors"), canvas, configuration, _teamMaterials);
        _sectorBoard = sectorBoard;
        var parcels = GetNode<Node3D>("CoverZones").GetChildren().OfType<MeshInstance3D>().ToArray();
        var cover = parcels.Select(ReadCoverZone);
        var ground = GetNode<StaticBody3D>("Terrain");
        var groundCollision = ground.GetNode<CollisionShape3D>("GroundCollision");
        var roadblock = GetNode<CollisionShape3D>("Roadblock/Collision");
        _buildingDefinitions = configuration.Buildings.ToDictionary(definition => definition.Kind);
        var layout = CreateConstructionLayout(BattlefieldNavigation.BoxBounds(groundCollision), roadblock,
            sectorBoard.Sectors, configuration.CaptureRadiusMeters);
        var world = new WorldEntities([.. units, blueHq, orangeHq, blueBarracks, orangeBarracks, .. sectorBoard.Sectors],
            cover, layout);
        var navigation = new BattlefieldNavigation(_navigationRegion, groundCollision,
            _buildings.Values.Select(body => body.GetNode<CollisionShape3D>("Collision")).Append(roadblock).ToArray());
        _navigation = navigation;
        var sight = new BattlefieldSight(GetWorld3D(), _buildings);
        var fog = new BattlefieldFog(this, BattlefieldNavigation.BoxBounds(groundCollision), sight);
        _fog = fog;
        BattlefieldArt.Decorate(this, fog.SceneryMaterial);
        DepotLighting.Configure(this, GetNode<WorldEnvironment>("WorldEnvironment"));
        _contactShadows = new BattlefieldContactShadows(this, BattlefieldNavigation.BoxBounds(groundCollision).End.Y);
        var session = new GameSession(world, configuration, navigation, sight);
        _session = session;
        var menuLayer = new CanvasLayer { Name = "MatchMenuLayer", Layer = 2, ProcessMode = ProcessModeEnum.Always };
        var menu = new MatchMenu { Name = "MatchMenu" };
        menu.Initialize(session, LocalPlayer);
        if (benchmark is null)
        {
            var save = new MatchSaveFile(ProjectSettings.GlobalizePath("user://match-save.json"), "battlefield-1", configuration);
            menu.EnablePersistence(() => SaveMatch(save, fog),
                () => LoadMatch(save, world, configuration, navigation, sight, fog), () => save.Exists);
        }
        _matchMenu = menu;
        AddChild(menuLayer);
        menuLayer.AddChild(menu);

        if (benchmark is null && (!OS.IsDebugBuild() || !OS.GetCmdlineUserArgs().Contains("--passive-opponent")))
        {
            _enemyCommander = new EnemyCommander(configuration);
            _enemyCommands = session.Commands.ForPlayer(Opponent);
        }
        var selection = new UnitSelection(LocalPlayer, id => _session!.World.LivingUnitOwner(id));
        var surfaces = new DepotImpactSurfaces(ground.GetNode<MeshInstance3D>("GroundMesh"), parcels);
        var feedback = new BattlefieldFeedback(this, canvas, surfaces);
        _feedback = feedback;
        _selectionPanel = new SelectionPanel(canvas, selection, configuration, _unitSelection);
        _unitSelection.Initialize(selection, _views, ground,
            (selected, destination, attackMove) =>
            {
                if (attackMove)
                    _session!.Commands.ScheduleAttackMove(LocalPlayer, selected, destination);
                else
                    _session!.Commands.ScheduleMove(LocalPlayer, selected, destination);
            },
            selected => _session!.Commands.ScheduleHoldPosition(LocalPlayer, selected),
            actor => actor switch
            {
                UnitView view when view.Player == Opponent && fog.VisibleActors.Contains(view.Id) => view.Id,
                StaticBody3D body when _enemyBuildings.TryGetValue(body, out var id)
                    && fog.VisibleActors.Contains(id) => id,
                _ => null
            },
            (selected, target) => _session!.Commands.ScheduleAttack(LocalPlayer, selected, target),
            (selected, target) => _session!.Commands.ScheduleRepair(LocalPlayer, selected, target),
            reason => feedback.Reject(reason, _session!.World.Tick));
        _productionPanel = new ProductionPanel(canvas, LocalPlayer, configuration,
            [blueBarracks, blueHq], (producer, kind) => _session!.Commands.ScheduleTraining(LocalPlayer, producer, kind));
        if (benchmark is null)
        {
            var constructionMenu = new ConstructionMenu { Name = "ConstructionMenu" };
            constructionMenu.Initialize(canvas, this, configuration, LocalPlayer, _constructionGround,
                () => _session!, _unitSelection.CancelConstructionInput);
            AddChild(constructionMenu);
            _constructionMenu = constructionMenu;
            menu.CloseConstruction = constructionMenu.Cancel;
        }
        _productionPanel.Update(session.World);
        sectorBoard.Update(session.World, fog.CanSeeGround);
        UpdateActorVisibility(fog.VisibleActors);
        UpdateUnitViews(session.World);
    }

    private void AddUnitView(Unit unit)
    {
        var view = UnitView.Create(unit, _unitArt[unit.Owner!.Value]);
        _unitRoot.AddChild(view);
        view.ResetPhysicsInterpolation();
        _views.Add(view);
        _healthBars.Add(unit.Id, new HealthBar(view, unit.HitPoints, 3.35f, 0.085f));
    }

    private Building AddBuilding(EntityId id, PlayerId owner, BuildingDefinition definition, string path)
    {
        var body = GetNode<StaticBody3D>(path);
        var exit = body.GetNode<Marker3D>("ProductionExit").GlobalPosition;
        _buildings.Add(id, body);
        body.GetNode<MeshInstance3D>("Base").Mesh = BattlefieldArt.Building(
            definition.Kind == BuildingKind.Headquarters, _teamMaterials[owner].AlbedoColor, owner == LocalPlayer,
            body.GetMeta("relay_roof", false).AsBool());
        if (owner == Opponent)
            _enemyBuildings.Add(body, id);
        _healthBars.Add(id, new HealthBar(body, definition.MaxHp, 7.3f, 0.115f));
        return new Building(id, owner, new SimVector(body.GlobalPosition.X, body.GlobalPosition.Z),
            definition.Kind, definition.MaxHp, new SimVector(exit.X, exit.Z));
    }

    private static CoverZone ReadCoverZone(MeshInstance3D mesh)
    {
        var bounds = mesh.GlobalTransform * mesh.GetAabb();
        if (!Enum.TryParse<CoverFacing>(mesh.GetMeta("cover_facing", "AllDirections").AsString(), out var facing)
            || !Enum.IsDefined(facing))
            throw new InvalidDataException($"Unknown cover direction for {mesh.Name}.");

        return new CoverZone(new SimVector(bounds.Position.X, bounds.Position.Z),
            new SimVector(bounds.End.X, bounds.End.Z), facing);
    }

    private static Unit CreateUnit(int id, PlayerId player, UnitDefinition definition, float x, float z) =>
        new(new EntityId(id), player, new SimVector(x, z), definition.Kind, definition.MaxHp);

    public override void _PhysicsProcess(double delta)
    {
        if (_session is not { } session || _fog is not { } fog)
            return;

        _benchmark?.ScheduleOrders(session);
        var startedAt = _benchmark is null ? 0 : Stopwatch.GetTimestamp();
        var events = session.AdvanceOneTick();
        _benchmark?.RecordTick(startedAt, events);
        if (session.World.Result == MatchResult.Running
            && _enemyCommander is { } commander && _enemyCommands is { } commands
            && EnemyCommander.IsDecisionTick(session.World.Tick))
        {
            commander.Plan(session.Observe(Opponent), commands);
        }

        _visibleLosses.Clear();
        foreach (var gameEvent in events)
            if (gameEvent is EntityDestroyed loss && fog.VisibleActors.Contains(loss.Entity))
                _visibleLosses.Add(loss.Entity);

        fog.Update(session.Observe(LocalPlayer));
        var visibleActors = fog.VisibleActors;
        var destroyed = false;
        foreach (var gameEvent in events)
        {
            if (gameEvent is CommandProcessed order && order.Command.Player == LocalPlayer && !order.Result.Accepted)
                _feedback?.Reject(order.Result.RejectionCode, order.Tick);
            if (gameEvent is ShotFired shot
                && (visibleActors.Contains(shot.Attacker) || _visibleLosses.Contains(shot.Attacker))
                && (visibleActors.Contains(shot.Target) || _visibleLosses.Contains(shot.Target))
                && FindActorPosition(shot.Attacker, session.World) is { } origin
                && FindActorPosition(shot.Target, session.World) is { } target
                && _views.Find(view => view.Id == shot.Attacker) is { } attacker)
            {
                attacker.PlayShot(origin, target);
                var impact = ResolveShotSurface(shot.Target, target);
                _feedback?.Fire(shot, attacker, impact,
                    _visibleLosses.Contains(shot.Attacker), _visibleLosses.Contains(shot.Target));
            }
            if (gameEvent is EntityDamaged damage && _healthBars.TryGetValue(damage.Entity, out var bar))
                bar.SetHitPoints(damage.RemainingHitPoints);
            if (gameEvent is TankRepaired repair)
            {
                if (_healthBars.TryGetValue(repair.Tank, out var restoredBar))
                    restoredBar.SetHitPoints(repair.RemainingHitPoints);
                if (visibleActors.Contains(repair.Engineer) && visibleActors.Contains(repair.Tank)
                    && _views.Find(view => view.Id == repair.Engineer) is { } engineer
                    && _views.Find(view => view.Id == repair.Tank)?.ArmorMesh is { } armor)
                    _feedback?.Weld(repair, engineer.WeldContact(armor, repair.Tick));
            }
            if (gameEvent is UnitSpawned spawn)
                AddUnitView(spawn.Unit);
            if (gameEvent is BuildingCompleted complete)
                AddConstructedBuilding(complete.Building.Id, complete.Building.Placement!, complete.Building.HitPoints);
            if (gameEvent is EntityDestroyed death)
            {
                DestroyView(death.Entity, death.Tick);
                destroyed = true;
            }
        }

        if (destroyed)
            _unitSelection.RefreshSelection();

        UpdateConstructionSites(session.World, fog);
        _constructionMenu?.Update(events);
        _productionPanel?.Update(session.World);
        _sectorBoard?.Update(session.World, fog.CanSeeGround);
        _selectionPanel?.Update(session.World, _views);
        UpdateActorVisibility(visibleActors);
        UpdateUnitViews(session.World, emitEffects: true);
        _feedback?.Update(session.World.Tick, fog);
        _casualties?.Advance(session.World.Tick, fog);
        if (session.World.Result != MatchResult.Running)
            _matchMenu?.ShowOutcome(session.World.Result);
    }

    public override void _Process(double delta)
    {
        _contactShadows?.Render(_views);
        var tickFraction = Engine.GetPhysicsInterpolationFraction();
        _feedback?.Render(tickFraction);
        _casualties?.Render(tickFraction);
        if (_benchmark is null || _navigation is null)
            return;

        try
        {
            if (_benchmark.RecordFrame(_views.Count, _navigation.PathRequests))
                GetTree().Quit();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            GD.PushError(exception.Message);
            GetTree().Quit(1);
        }
    }

    public override void _ExitTree()
    {
        _benchmark?.Dispose();
        _casualties?.Dispose();
        _fog?.Dispose();
        // Drain managed input and resource wrappers while the Godot runtime still owns their native objects.
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
        GC.WaitForPendingFinalizers();
    }

    private Vector3? FindActorPosition(EntityId id, IReadOnlyWorld world)
    {
        var height = Vector3.Up * 1.75f;
        if (world.TryGetUnitPosition(id, out var position))
            return new Vector3(position.X, height.Y, position.Z);
        if (_buildings.TryGetValue(id, out var building))
            return building.GlobalPosition + height;
        if (_views.Find(view => view.Id == id) is { } lastView)
            return lastView.GlobalPosition + height;
        return null;
    }

    private ShotSurface ResolveShotSurface(EntityId id, Vector3 center)
    {
        if (_views.Find(view => view.Id == id)?.ArmorMesh is { } armor)
            return new ShotSurface(center, armor, ImpactMaterial.Armor);
        if (_buildings.TryGetValue(id, out var building))
            return new ShotSurface(center, building.GetNode<MeshInstance3D>("Base"), ImpactMaterial.Concrete);
        return new ShotSurface(center);
    }

    private void UpdateActorVisibility(IReadOnlySet<EntityId> visibleActors)
    {
        foreach (var view in _views)
            SetActorVisible(view, view.Player == LocalPlayer || visibleActors.Contains(view.Id));
        foreach (var (building, id) in _enemyBuildings)
            SetActorVisible(building, visibleActors.Contains(id));
    }

    private static void SetActorVisible(StaticBody3D actor, bool visible)
    {
        if (actor.Visible == visible)
            return;

        actor.Visible = visible;
        actor.SetCollisionLayerValue(UnitView.PickingLayerNumber, visible);
    }

    private void UpdateUnitViews(IReadOnlyWorld world, bool emitEffects = false)
    {
        foreach (var view in _views)
        {
            if (!world.TryGetUnitPosition(view.Id, out var position))
                continue;

            var travelled = view.ApplyPosition(new Vector3(position.X, 0f, position.Z), Engine.PhysicsTicksPerSecond);
            if (view.Kind.UsesCover())
            {
                if (view.Kind == UnitKind.EngineerSquad)
                    view.ShowRepair(world.RepairingTank(view.Id) is { } tank
                        ? _views.Find(candidate => candidate.Id == tank)?.ArmorMesh : null, world.Tick);
                view.ShowInfantryStatus(world.HasCover(view.Id), world.IsSuppressed(view.Id), snapPose: !emitEffects);
                if (view.TakeFootfall() is { } footfall && emitEffects && view.Visible)
                    _feedback?.FootstepDust(view.Id, world.Tick, footfall);
                continue;
            }

            if (world.TryGetHitPoints(view.Id, out var hitPoints))
                view.ShowTankDamage(hitPoints);
            if (!emitEffects || !view.Visible)
                continue;

            if (travelled > 0f && view.AdvanceTreadMarks(travelled) is { } overshoot)
                _feedback?.TrackTerrain(view, world.Tick, overshoot, view.Player == LocalPlayer);
            if ((world.Tick + view.Id.Value) % EngineSmokeIntervalTicks == 0 && view.EngineSmokeOrigin is { } engine)
                _feedback?.EngineSmoke(view.Id, world.Tick, engine);
        }
    }

    private void DestroyView(EntityId id, long tick)
    {
        if (RemoveConstructedBuilding(id))
            return;
        _healthBars.Remove(id);
        if (_buildings.TryGetValue(id, out var building))
        {
            RemoveBuildingBar(building);
            ShowBuildingRuin(building);
            return;
        }

        var index = _views.FindIndex(view => view.Id == id);
        if (index < 0)
            return;

        var view = _views[index];
        _views.RemoveAt(index);
        if (_visibleLosses.Contains(id))
        {
            var casualties = _casualties ??= new BattlefieldCasualties(this,
                BattlefieldNavigation.BoxBounds(GetNode<CollisionShape3D>("Terrain/GroundCollision")));
            casualties.Emit(view, tick, view.Player == LocalPlayer);
            if (view.Kind == UnitKind.Tank)
                _feedback?.ExplodeTank(tick, view.GlobalPosition);
        }
        view.QueueFree();
    }
}
