using Godot;
using IronDominion.Domain;

namespace IronDominion.Game;

public partial class UnitView : StaticBody3D
{
    public const int PickingLayerNumber = 2;
    public const uint PickingLayer = 1u << (PickingLayerNumber - 1);

    private MeshInstance3D _ring = null!;
    private Label3D? _coverIndicator;
    private InfantryMotion? _infantry;
    private MeshInstance3D? _gun;
    private MeshInstance3D? _turret;
    private Node3D _model = null!;
    private float _movementHeading;
    private float _aimHeading;
    private float _speed;
    private float _shotTime;
    private bool _suppressed;

    public EntityId Id { get; private set; }
    public UnitKind Kind { get; private set; }
    public PlayerId Player { get; private set; }

    public Vector3 ShotOrigin => _gun is { } gun
        ? gun.ToGlobal(new Vector3(0f, 0f, -2.70f))
        : _model.ToGlobal(new Vector3(0f, _suppressed ? 1.42f : 1.68f, -1.15f));

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
            view.AddTank(art);
        else
            view.AddInfantry(art);

        return view;
    }

    public void ShowSelection(bool selected) => _ring.Visible = selected;

    public void ApplyPosition(Vector3 position, float tickRate)
    {
        var step = position - Position;
        _speed = step.Length() * tickRate;
        if (_speed > 0.05f)
            _movementHeading = Heading(step);
        Position = position;
    }

    public void PlayShot(Vector3 origin, Vector3 target)
    {
        var direction = target - origin;
        if (new Vector2(direction.X, direction.Z).LengthSquared() < 0.001f)
            return;

        _aimHeading = Heading(direction);
        _shotTime = 0.48f;
        if (_turret is null)
            _model.Rotation = new Vector3(0f, _aimHeading, 0f);
        else
        {
            _turret.Rotation = new Vector3(0f, _aimHeading - _model.Rotation.Y, 0f);
            _gun!.Position = new Vector3(0f, 0.38f, -0.83f);
        }
    }

    public override void _Process(double delta)
    {
        if (!IsVisibleInTree())
            return;

        var seconds = (float)delta;
        var aiming = _shotTime > 0f;
        var recoil = Mathf.Clamp((_shotTime - 0.27f) / 0.21f, 0f, 1f);
        var heading = _turret is null && aiming ? _aimHeading : _movementHeading;
        var turn = 1f - Mathf.Exp(-seconds * 11f);
        _model.Rotation = new Vector3(0f, Mathf.LerpAngle(_model.Rotation.Y, heading, turn), 0f);
        if (_turret is { } turret && _gun is { } gun)
        {
            var targetAngle = aiming ? _aimHeading - _model.Rotation.Y : 0f;
            turret.Rotation = new Vector3(0f, Mathf.LerpAngle(turret.Rotation.Y, targetAngle, turn), 0f);
            gun.Position = new Vector3(0f, 0.38f, -1.12f + recoil * 0.29f);
        }
        else
            _infantry?.Update(seconds, _speed, aiming, recoil, _suppressed);
        _shotTime = Mathf.Max(0f, _shotTime - seconds);
    }

    private static float Heading(Vector3 direction) => Mathf.Atan2(-direction.X, -direction.Z);

    public void ShowInfantryStatus(bool covered, bool suppressed)
    {
        _suppressed = suppressed;
        if (_coverIndicator is not { } indicator)
            return;

        indicator.Visible = covered || suppressed;
        if (!indicator.Visible)
            return;

        var status = (covered, suppressed) switch
        {
            (true, true) => "COVER · SUPPRESSED",
            (true, false) => "COVER",
            _ => "SUPPRESSED"
        };
        if (indicator.Text != status)
            indicator.Text = status;
        indicator.Modulate = suppressed ? HudStyle.Red : HudStyle.Amber;
    }

    private void AddTank(UnitArt art)
    {
        _model.AddChild(new MeshInstance3D { Name = "TracksAndHull", Mesh = art.TankHull });
        _turret = new MeshInstance3D
        {
            Name = "Turret", Mesh = art.TankTurret, Position = Vector3.Up * 1.43f
        };
        _gun = new MeshInstance3D
        {
            Name = "Gun", Mesh = art.TankGun, Position = new Vector3(0f, 0.38f, -1.12f)
        };
        _model.AddChild(_turret);
        _turret.AddChild(_gun);
    }

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
        _infantry = new InfantryMotion(_model, art);
    }
}
