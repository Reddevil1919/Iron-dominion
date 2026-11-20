using Godot;
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
    private readonly Dictionary<EntityId, StaticBody3D> _headquarters = new();
    private GameSession? _session;

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
        var blue = new StandardMaterial3D { AlbedoColor = new Color("3da9fc") };
        var orange = new StandardMaterial3D { AlbedoColor = new Color("f7793c") };
        foreach (var unit in units)
        {
            var view = UnitView.Create(unit, unit.Owner == LocalPlayer ? blue : orange);
            _unitRoot.AddChild(view);
            view.ResetPhysicsInterpolation();
            _views.Add(view);
            _healthBars.Add(unit.Id, new HealthBar(view, unit.HitPoints, 4.2f, 0.10f));
        }

        var headquarters = configuration.Buildings.Single(building => building.Kind == BuildingKind.Headquarters);
        var blueHq = AddHeadquarters(new EntityId(7), LocalPlayer, headquarters.MaxHp, "BlueHQ");
        var orangeHq = AddHeadquarters(new EntityId(8), Opponent, headquarters.MaxHp, "OrangeHQ");
        var world = new WorldEntities([.. units, blueHq, orangeHq]);
        var ground = GetNode<StaticBody3D>("Terrain");
        var navigation = new BattlefieldNavigation(_navigationRegion, ground.GetNode<CollisionShape3D>("GroundCollision"),
        [
            GetNode<CollisionShape3D>("Roadblock/Collision"),
            GetNode<CollisionShape3D>("BlueHQ/Collision"),
            GetNode<CollisionShape3D>("OrangeHQ/Collision")
        ]);
        var sight = new BattlefieldSight(GetWorld3D(), ground.CollisionLayer, _headquarters);
        var session = new GameSession(world, configuration, navigation, sight);
        _session = session;
        var opposingHQ = _headquarters[orangeHq.Id];
        _unitSelection.Initialize(
            new UnitSelection(LocalPlayer, world, id => session.World.TryGetHitPoints(id, out var hp) && hp > 0),
            _views, ground,
            (selected, destination) => session.Commands.ScheduleMove(LocalPlayer, selected, destination),
            actor => actor switch
            {
                UnitView view when world.Find(view.Id)?.Owner == Opponent => view.Id,
                _ when actor == opposingHQ && session.World.TryGetHitPoints(orangeHq.Id, out var hp) && hp > 0 => orangeHq.Id,
                _ => null
            },
            (selected, target) => session.Commands.ScheduleAttack(LocalPlayer, selected, target));
    }

    private Building AddHeadquarters(EntityId id, PlayerId owner, int maxHp, string path)
    {
        var body = GetNode<StaticBody3D>(path);
        _headquarters.Add(id, body);
        _healthBars.Add(id, new HealthBar(body, maxHp, 7.3f, 0.14f));
        return new Building(id, owner, new SimVector(body.Position.X, body.Position.Z),
            BuildingKind.Headquarters, maxHp);
    }

    private static Unit CreateUnit(int id, PlayerId player, UnitDefinition definition, float x, float z) =>
        new(new EntityId(id), player, new SimVector(x, z), definition.Kind, definition.MaxHp);

    public override void _PhysicsProcess(double delta)
    {
        if (_session is not { } session)
            return;

        var events = session.AdvanceOneTick();
        var destroyed = false;
        foreach (var gameEvent in events)
        {
            if (gameEvent is EntityDamaged damage && _healthBars.TryGetValue(damage.Entity, out var bar))
                bar.SetHitPoints(damage.RemainingHitPoints);
            if (gameEvent is EntityDestroyed death)
            {
                DestroyView(death.Entity);
                destroyed = true;
            }
        }

        if (destroyed)
            _unitSelection.RefreshSelection();

        foreach (var view in _views)
        {
            if (session.World.TryGetUnitPosition(view.Id, out var position))
                view.Position = new Vector3(position.X, 0f, position.Z);
        }
    }

    private void DestroyView(EntityId id)
    {
        _healthBars.Remove(id);
        if (_headquarters.TryGetValue(id, out var headquarters))
        {
            headquarters.GetNode<Sprite3D>("HealthBar").QueueFree();
            var ruin = new StandardMaterial3D { AlbedoColor = new Color("505862") };
            headquarters.GetNode<MeshInstance3D>("Base").MaterialOverride = ruin;
            headquarters.GetNode<MeshInstance3D>("Roof").MaterialOverride = ruin;
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
