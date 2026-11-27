using Godot;
using IronDominion.Domain;

namespace IronDominion.Game;

public partial class UnitView : StaticBody3D
{
    public const int PickingLayerNumber = 2;
    public const uint PickingLayer = 1u << (PickingLayerNumber - 1);
    private const float ShotAimDuration = 0.48f;

    private MeshInstance3D _ring = null!;
    private Label3D? _coverIndicator;
    private InfantryMotion? _infantry;
    private TankMotion? _tank;
    private Node3D _model = null!;
    private float _movementHeading;
    private float _aimHeading;
    private float _speed;
    private float _shotTime;
    private bool _covered;
    private bool _suppressed;
    private MeshInstance3D? _repairArmor;
    private long _repairTick;

    public EntityId Id { get; private set; }
    public UnitKind Kind { get; private set; }
    public PlayerId Player { get; private set; }

    public Vector3 ShotOrigin(int rifle = 0) => _tank?.Muzzle ?? _infantry!.Muzzle(rifle);
    public Mesh? TurretMesh => _tank?.TurretMesh;
    internal MeshInstance3D? ArmorMesh => _tank?.Hull;
    internal Transform3D ContactPose => _model.GetGlobalTransformInterpolated();
    internal Vector3? EngineSmokeOrigin => _tank?.EngineSmokeOrigin;
    internal Vector3 TrackForward => -_model.GlobalBasis.Z;
    internal Vector3 RearTrack(int side) => _model.ToGlobal(new Vector3(side * TankArt.TrackGauge, 0f, 2.08f));

    public Vector3 SelectionCenter => GlobalPosition + Vector3.Up;

    internal static UnitView Create(Unit unit, UnitArt art)
    {
        var tank = unit.Kind == UnitKind.Tank;
        var view = new UnitView
        {
            Name = $"Unit{unit.Id.Value}",
            Id = unit.Id,
            Kind = unit.Kind,
            Player = unit.Owner!.Value,
            Position = new Vector3(unit.Position.X, 0f, unit.Position.Z),
            CollisionLayer = PickingLayer,
            CollisionMask = 0
        };
        view.AddChild(new CollisionShape3D
        {
            Position = Vector3.Up * 1.5f,
            Shape = new BoxShape3D { Size = tank ? new Vector3(7, 3, 6) : new Vector3(5, 3, 5) }
        });
        view.AddChild(new MeshInstance3D
        {
            Name = "SelectionRing",
            Position = Vector3.Up * 0.12f,
            Mesh = new TorusMesh { InnerRadius = 4.05f, OuterRadius = 4.3f, Rings = 32, RingSegments = 6 },
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color("dcff67"),
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
            },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Visible = false
        });
        view._ring = view.GetNode<MeshInstance3D>("SelectionRing");

        var heading = view.Player.Value == 1 ? -Mathf.Pi / 2f : Mathf.Pi / 2f;
        view._movementHeading = heading;
        view._aimHeading = heading;
        view._model = new Node3D { Name = "Model", Rotation = new Vector3(0f, heading, 0f) };
        view.AddChild(view._model);
        if (tank)
            view._tank = new TankMotion(view._model, art.Tank, unit.HitPoints);
        else
            view.AddInfantry(art);

        return view;
    }

    public void ShowSelection(bool selected) => _ring.Visible = selected;

    public float ApplyPosition(Vector3 position, float tickRate)
    {
        var step = position - Position;
        var distance = step.Length();
        _speed = distance * tickRate;
        if (_speed > 0.05f)
            _movementHeading = Heading(step);
        Position = position;
        return distance;
    }

    internal float? AdvanceTreadMarks(float distance) => _tank?.AdvanceTreadMarks(distance);

    internal Vector3? TakeFootfall() => _infantry?.TakeFootfall();

    internal void ShowTankDamage(int hitPoints) => _tank?.ShowDamage(hitPoints);

    internal void ShowRepair(MeshInstance3D? armor, long tick)
    {
        _repairArmor = armor;
        _repairTick = tick;
    }

    internal SurfaceImpact WeldContact(MeshInstance3D armor, double tick)
    {
        var direction = armor.GlobalPosition - GlobalPosition;
        var lead = InfantryRig.Formation[0].Rotated(Vector3.Up, Heading(direction));
        var seam = Mathf.Sin((float)(tick % 160) * Mathf.Tau / 40f) * 0.22f;
        return DepotImpactSurfaces.WeldOnArmor(armor, GlobalPosition + lead, seam);
    }

    internal InfantryMotion? DetachInfantry(Node3D parent)
    {
        if (_infantry is not { } infantry)
            return null;

        _model.Reparent(parent);
        _model.ResetPhysicsInterpolation();
        _infantry = null;
        SetProcess(false);
        return infantry;
    }

    public void PlayShot(Vector3 origin, Vector3 target)
    {
        var direction = target - origin;
        if (new Vector2(direction.X, direction.Z).LengthSquared() < 0.001f)
            return;

        _aimHeading = Heading(direction);
        _shotTime = ShotAimDuration;
        if (_infantry is { } infantry)
        {
            if (_speed < 0.05f)
                _movementHeading = _aimHeading;
            _model.Rotation = new Vector3(0f, _aimHeading, 0f);
            infantry.BeginBurst();
        }
        else
            _tank?.Fire(_model.Rotation.Y, _aimHeading);
    }

    public override void _Process(double delta)
    {
        if (!IsVisibleInTree())
            return;

        var seconds = (float)delta;
        var aiming = _shotTime > 0f;
        var recoil = Mathf.Clamp((_shotTime - 0.27f) / 0.21f, 0f, 1f);
        var weld = GodotObject.IsInstanceValid(_repairArmor) && _repairArmor is { } armor
            ? WeldContact(armor, _repairTick + Engine.GetPhysicsInterpolationFraction()).Point : (Vector3?)null;
        var desiredHeading = weld is { } contact ? Heading(contact - GlobalPosition)
            : _tank is null && aiming ? _aimHeading : _movementHeading;
        var previousHeading = _model.Rotation.Y;
        var turn = 1f - Mathf.Exp(-seconds * 11f);
        var rotation = Mathf.AngleDifference(previousHeading, desiredHeading) * turn;
        if (_infantry is not null)
            rotation = Mathf.Clamp(rotation, -InfantryMotion.MaxTurnRate * seconds, InfantryMotion.MaxTurnRate * seconds);
        var heading = previousHeading + rotation;
        _model.Rotation = new Vector3(0f, heading, 0f);
        if (_tank is { } tank)
            tank.Update(seconds, _speed, previousHeading, heading, aiming ? _aimHeading : null, recoil);
        else if (_infantry is { } infantry && seconds > 0f)
        {
            var localVelocity = Vector3.Forward.Rotated(Vector3.Up, _movementHeading - heading) * _speed;
            infantry.Update(seconds, localVelocity, rotation / seconds,
                aiming ? ShotAimDuration - _shotTime : null, _covered, _suppressed, weld);
        }
        _shotTime = Mathf.Max(0f, _shotTime - seconds);
    }

    private static float Heading(Vector3 direction) => Mathf.Atan2(-direction.X, -direction.Z);

    public void ShowInfantryStatus(bool covered, bool suppressed, bool snapPose)
    {
        _covered = covered;
        _suppressed = suppressed;
        if (snapPose)
            _infantry?.SetRestingStance(covered, suppressed);
        if (_coverIndicator is not { } indicator)
            return;

        indicator.Visible = covered;
    }

    public override void _ExitTree() => _tank?.Dispose();

    private void AddInfantry(UnitArt art)
    {
        _coverIndicator = new Label3D
        {
            Name = "CoverIndicator",
            Text = "COVER",
            Position = Vector3.Up * 6f,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 28,
            OutlineSize = 6,
            Modulate = new Color("f6d88d"),
            PixelSize = 0.065f,
            Visible = false
        };
        AddChild(_coverIndicator);
        var engineer = Kind == UnitKind.EngineerSquad;
        _infantry = new InfantryMotion(_model, engineer ? art.Engineers : art.Infantry, engineer);
    }
}
