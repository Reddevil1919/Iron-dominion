using Godot;
using IronDominion.AI;
using IronDominion.Application;
using IronDominion.Domain;
using IronDominion.Infrastructure.Configuration;

namespace IronDominion.Game;

public partial class Battlefield : Node3D
{
    private static readonly PlayerId LocalPlayer = new(1);
    private static readonly PlayerId Opponent = new(2);

    [Export] private UnitSelectionController _unitSelection = null!;
    [Export] private Node3D _unitRoot = null!;

    [Export] private NavigationRegion3D _navigationRegion = null!;

    private readonly List<UnitView> _views = new();
    private readonly Dictionary<EntityId, HealthBar> _healthBars = new();
    private readonly Dictionary<EntityId, StaticBody3D> _buildings = new();
    private readonly Dictionary<PlayerId, StandardMaterial3D> _teamMaterials = new();
    private ProductionPanel? _productionPanel;
    private SectorBoard? _sectorBoard;
    private SelectionPanel? _selectionPanel;
    private BattlefieldFeedback? _feedback;
    private MatchMenu? _matchMenu;
    private GameSession? _session;
    private EnemyCommander? _enemyCommander;
    private PlayerCommands? _enemyCommands;

    public override void _Ready()
    {
        try
        {
            var configuration = GameConfigurationLoader.LoadDefault();
            Engine.PhysicsTicksPerSecond = configuration.TickRate;
            SetUpUnits(configuration);
        }
        catch (InvalidDataException exception)
        {
            GD.PushError(exception.Message);
            SetPhysicsProcess(false);
            GetTree().Quit(1);
        }
    }

    private void SetUpUnits(GameConfiguration configuration)
    {
        var infantry = configuration.Units.Single(unit => unit.Kind == UnitKind.InfantrySquad);
        var tank = configuration.Units.Single(unit => unit.Kind == UnitKind.Tank);
        Unit[] units =
        [
            CreateUnit(1, LocalPlayer, infantry, -20, 18),
            CreateUnit(2, LocalPlayer, infantry, -15, 29),
            CreateUnit(3, LocalPlayer, tank, -17, 7),
            CreateUnit(4, Opponent, infantry, 20, -18),
            CreateUnit(5, Opponent, infantry, 15, -29),
            CreateUnit(6, Opponent, tank, 17, -7)
        ];
        _teamMaterials.Add(LocalPlayer, new StandardMaterial3D { AlbedoColor = new Color("3da9fc") });
        _teamMaterials.Add(Opponent, new StandardMaterial3D { AlbedoColor = new Color("f7793c") });
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
        var cover = GetNode<Node3D>("CoverZones").GetChildren().OfType<MeshInstance3D>().Select(ReadCoverZone);
        var world = new WorldEntities([.. units, blueHq, orangeHq, blueBarracks, orangeBarracks, .. sectorBoard.Sectors],
            cover);
        var ground = GetNode<StaticBody3D>("Terrain");
        var navigation = new BattlefieldNavigation(_navigationRegion, ground.GetNode<CollisionShape3D>("GroundCollision"),
            _buildings.Values.Select(body => body.GetNode<CollisionShape3D>("Collision"))
                .Append(GetNode<CollisionShape3D>("Roadblock/Collision")).ToArray());
        var sight = new BattlefieldSight(GetWorld3D(), ground.CollisionLayer, _buildings);
        var session = new GameSession(world, configuration, navigation, sight);
        _session = session;
        var menuLayer = new CanvasLayer { Name = "MatchMenuLayer", Layer = 2, ProcessMode = ProcessModeEnum.Always };
        var menu = new MatchMenu { Name = "MatchMenu" };
        menu.Initialize(session, LocalPlayer);
        _matchMenu = menu;
        AddChild(menuLayer);
        menuLayer.AddChild(menu);

        if (!OS.IsDebugBuild() || !OS.GetCmdlineUserArgs().Contains("--passive-opponent"))
        {
            _enemyCommander = new EnemyCommander(configuration);
            _enemyCommands = session.Commands.ForPlayer(Opponent);
        }
        var enemyBuildings = new[] { orangeHq, orangeBarracks }.ToDictionary(building => _buildings[building.Id],
            building => building.Id);
        var selection = new UnitSelection(LocalPlayer, id => session.World.LivingUnitOwner(id));
        var feedback = new BattlefieldFeedback(this, canvas);
        _feedback = feedback;
        _selectionPanel = new SelectionPanel(canvas, selection, configuration);
        _unitSelection.Initialize(selection, _views, ground,
            (selected, destination) => session.Commands.ScheduleMove(LocalPlayer, selected, destination),
            actor => actor switch
            {
                UnitView view when session.World.LivingUnitOwner(view.Id) == Opponent => view.Id,
                StaticBody3D body when enemyBuildings.TryGetValue(body, out var id)
                    && session.World.TryGetHitPoints(id, out var hp) && hp > 0 => id,
                _ => null
            },
            (selected, target) => session.Commands.ScheduleAttack(LocalPlayer, selected, target),
            reason => feedback.Reject(reason, session.World.Tick));
        _productionPanel = new ProductionPanel(canvas, LocalPlayer, configuration,
            [blueBarracks, blueHq], (producer, kind) => session.Commands.ScheduleTraining(LocalPlayer, producer, kind));
        _productionPanel.Update(session.World);
        sectorBoard.Update(session.World);
        UpdateUnitViews(session.World);
    }

    private void AddUnitView(Unit unit)
    {
        var view = UnitView.Create(unit, _teamMaterials[unit.Owner!.Value]);
        _unitRoot.AddChild(view);
        view.ResetPhysicsInterpolation();
        _views.Add(view);
        _healthBars.Add(unit.Id, new HealthBar(view, unit.HitPoints, 4.2f, 0.10f));
    }

    private Building AddBuilding(EntityId id, PlayerId owner, BuildingDefinition definition, string path)
    {
        var body = GetNode<StaticBody3D>(path);
        var exit = body.GetNode<Marker3D>("ProductionExit").GlobalPosition;
        _buildings.Add(id, body);
        _healthBars.Add(id, new HealthBar(body, definition.MaxHp, 7.3f, 0.14f));
        return new Building(id, owner, new SimVector(body.GlobalPosition.X, body.GlobalPosition.Z),
            definition.Kind, definition.MaxHp, new SimVector(exit.X, exit.Z));
    }

    private static CoverZone ReadCoverZone(MeshInstance3D mesh)
    {
        var bounds = mesh.GlobalTransform * mesh.GetAabb();
        return new CoverZone(new SimVector(bounds.Position.X, bounds.Position.Z),
            new SimVector(bounds.End.X, bounds.End.Z));
    }

    private static Unit CreateUnit(int id, PlayerId player, UnitDefinition definition, float x, float z) =>
        new(new EntityId(id), player, new SimVector(x, z), definition.Kind, definition.MaxHp);

    public override void _PhysicsProcess(double delta)
    {
        if (_session is not { } session)
            return;

        var events = session.AdvanceOneTick();
        if (session.World.Result == MatchResult.Running
            && _enemyCommander is { } commander && _enemyCommands is { } commands
            && EnemyCommander.IsDecisionTick(session.World.Tick))
        {
            commander.Plan(session.Observe(Opponent), commands);
        }

        var destroyed = false;
        foreach (var gameEvent in events)
        {
            if (gameEvent is CommandProcessed order && order.Command.Player == LocalPlayer && !order.Result.Accepted)
                _feedback?.Reject(order.Result.RejectionCode, order.Tick);
            if (gameEvent is ShotFired shot && FindActorPosition(shot.Attacker, session.World) is { } origin
                && FindActorPosition(shot.Target, session.World) is { } target)
                _feedback?.Trace(origin, target, shot.Tick);
            if (gameEvent is EntityDamaged damage && _healthBars.TryGetValue(damage.Entity, out var bar))
                bar.SetHitPoints(damage.RemainingHitPoints);
            if (gameEvent is UnitSpawned spawn)
                AddUnitView(spawn.Unit);
            if (gameEvent is EntityDestroyed death)
            {
                DestroyView(death.Entity);
                destroyed = true;
            }
        }

        if (destroyed)
            _unitSelection.RefreshSelection();

        _productionPanel?.Update(session.World);
        _sectorBoard?.Update(session.World);
        _selectionPanel?.Update(session.World, _views);
        _feedback?.Update(session.World.Tick);
        UpdateUnitViews(session.World);
        if (session.World.Result != MatchResult.Running)
            _matchMenu?.ShowOutcome(session.World.Result);
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

    private void UpdateUnitViews(IReadOnlyWorld world)
    {
        foreach (var view in _views)
        {
            if (!world.TryGetUnitPosition(view.Id, out var position))
                continue;

            view.Position = new Vector3(position.X, 0f, position.Z);
            view.ShowCover(world.HasCover(view.Id));
        }
    }

    private void DestroyView(EntityId id)
    {
        _healthBars.Remove(id);
        if (_buildings.TryGetValue(id, out var building))
        {
            building.GetNode<Sprite3D>("HealthBar").QueueFree();
            var ruin = new StandardMaterial3D { AlbedoColor = new Color("505862") };
            building.GetNode<MeshInstance3D>("Base").MaterialOverride = ruin;
            building.GetNode<MeshInstance3D>("Roof").MaterialOverride = ruin;
            return;
        }

        var index = _views.FindIndex(view => view.Id == id);
        if (index < 0)
            return;

        var view = _views[index];
        _views.RemoveAt(index);
        view.QueueFree();
    }
}
