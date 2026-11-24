using Godot;
using IronDominion.Domain;

namespace IronDominion.Game;

internal sealed class BattlefieldContactShadows
{
    private readonly Layer _tracks;
    private readonly Layer _boots;
    private readonly float _height;

    public BattlefieldContactShadows(Node3D battlefield, float groundHeight)
    {
        _height = groundHeight + 0.058f;
        _tracks = new Layer(battlefield, "TrackContactShadows", 1f);
        _boots = new Layer(battlefield, "InfantryContactShadows", 0f);
    }

    public static void AddCargo(Node3D yard, IReadOnlyList<Vector3> pallets)
    {
        var cargo = new Layer(yard, "CargoContactShadows", 2f);
        cargo.Begin(pallets.Count);
        foreach (var pallet in pallets)
            cargo.Add(Stamp(Basis.Identity, pallet + Vector3.Up * 0.009f, 1.88f, 1.58f));
        cargo.Finish();
    }

    public void Render(IReadOnlyList<UnitView> views)
    {
        _tracks.Begin(views.Count);
        _boots.Begin(views.Count * InfantryRig.Formation.Length);
        foreach (var view in views)
        {
            if (!view.IsVisibleInTree())
                continue;

            var pose = view.ContactPose;
            if (view.Kind == UnitKind.Tank)
            {
                _tracks.Add(Stamp(pose.Basis, pose.Origin with { Y = _height }, 4.03f, 5.15f));
                continue;
            }

            foreach (var member in InfantryRig.Formation)
            {
                var contact = pose * member;
                _boots.Add(Stamp(pose.Basis, contact with { Y = _height }, 0.73f, 0.95f));
            }
        }
        _tracks.Finish();
        _boots.Finish();
    }

    private static Transform3D Stamp(Basis heading, Vector3 site, float width, float length) =>
        new(heading.ScaledLocal(new Vector3(width, 1f, length)), site);

    private sealed class Layer
    {
        private readonly MultiMesh _mesh;
        private int _used;

        public Layer(Node3D parent, string name, float profile)
        {
            using var plane = new PlaneMesh { Size = Vector2.One };
            _mesh = new MultiMesh { Mesh = plane, TransformFormat = MultiMesh.TransformFormatEnum.Transform3D };
            using var material = new ShaderMaterial
            {
                Shader = GD.Load<Shader>("res://Shaders/contact_shadow.gdshader"), RenderPriority = -2
            };
            material.SetShaderParameter("profile", profile);
            parent.AddChild(new MultiMeshInstance3D
            {
                Name = name, Multimesh = _mesh, MaterialOverride = material,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                PhysicsInterpolationMode = Node.PhysicsInterpolationModeEnum.Off
            });
        }

        public void Begin(int capacity)
        {
            _used = 0;
            if (_mesh.InstanceCount < capacity)
                _mesh.InstanceCount = Math.Max(capacity, _mesh.InstanceCount * 2);
        }

        public void Add(Transform3D transform) => _mesh.SetInstanceTransform(_used++, transform);

        public void Finish()
        {
            if (_mesh.VisibleInstanceCount != _used)
                _mesh.VisibleInstanceCount = _used;
        }
    }
}
