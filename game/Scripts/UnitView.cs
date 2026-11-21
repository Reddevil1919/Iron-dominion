using Godot;
using IronDominion.Domain;

namespace IronDominion.Game;

public partial class UnitView : StaticBody3D
{
    public const uint PickingLayer = 1u << 1;

    private MeshInstance3D _ring = null!;
    private Label3D? _coverIndicator;

    public EntityId Id { get; private set; }
    public UnitKind Kind { get; private set; }

    public Vector3 SelectionCenter => GlobalPosition + Vector3.Up;

    public static UnitView Create(Unit unit, StandardMaterial3D teamMaterial)
    {
        var tank = unit.Kind == UnitKind.Tank;
        var view = new UnitView
        {
            Name = $"Unit{unit.Id.Value}",
            Id = unit.Id,
            Kind = unit.Kind,
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

        if (tank)
            view.AddTank(teamMaterial);
        else
            view.AddInfantry(teamMaterial);

        return view;
    }

    public void ShowSelection(bool selected) => _ring.Visible = selected;

    public void ShowCover(bool covered)
    {
        if (_coverIndicator is { } indicator && indicator.Visible != covered)
            indicator.Visible = covered;
    }

    private void AddTank(StandardMaterial3D material)
    {
        AddMesh(new BoxMesh { Size = new Vector3(6.5f, 1.5f, 5.2f) }, new Vector3(0, 0.75f, 0), material);
        AddMesh(new BoxMesh { Size = new Vector3(3.2f, 0.8f, 2.6f) }, new Vector3(0, 1.9f, 0),
            new StandardMaterial3D { AlbedoColor = new Color("303943") });
    }

    private void AddInfantry(StandardMaterial3D material)
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

        var soldier = new CapsuleMesh { Radius = 0.7f, Height = 2.4f, RadialSegments = 10, Rings = 4 };
        foreach (var x in new[] { -1.4f, 1.4f })
        foreach (var z in new[] { -1.4f, 1.4f })
            AddMesh(soldier, new Vector3(x, 1.2f, z), material);
    }

    private void AddMesh(Mesh mesh, Vector3 position, Material material)
    {
        AddChild(new MeshInstance3D { Mesh = mesh, Position = position, MaterialOverride = material });
    }
}
