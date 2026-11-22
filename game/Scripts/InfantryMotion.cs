using Godot;

namespace IronDominion.Game;

internal sealed class InfantryMotion
{
    private const int Soldiers = 4;
    private const float ThighLength = 0.54f;
    private const float ShinLength = 0.51f;

    private static readonly Vector3[] Formation =
    [
        new(-1.24f, 0f, -1.28f), new(1.24f, 0f, -0.58f),
        new(-1.30f, 0f, 1.16f), new(1.27f, 0f, 1.48f)
    ];

    private readonly MultiMesh _torsos;
    private readonly MultiMesh _arms;
    private readonly MultiMesh _thighs;
    private readonly MultiMesh _boots;
    private float _stride;
    private float _pace;
    private float _crouch;

    public InfantryMotion(Node3D model, UnitArt art)
    {
        _torsos = AddLayer(model, "InfantryTorso", art.InfantryTorso, Soldiers);
        _arms = AddLayer(model, "InfantryArms", art.InfantryArms, Soldiers);
        _thighs = AddLayer(model, "InfantryThighs", art.InfantryThigh, 2 * Soldiers);
        _boots = AddLayer(model, "InfantryBoots", art.InfantryBoot, 2 * Soldiers);
        Update(0f, 0f, false, 0f, false);
    }

    public void Update(float delta, float speed, bool aiming, float recoil, bool suppressed)
    {
        if (delta > 0f && speed < 0.05f && _pace < 0.01f && !aiming
            && Mathf.Abs(_crouch - (suppressed ? 1f : 0f)) < 0.01f)
        {
            // Retain the resting pose after the transition has finished.
            if (_pace == 0f && _crouch == (suppressed ? 1f : 0f))
                return;
        }

        var blend = 1f - Mathf.Exp(-delta * 13f);
        _pace = Mathf.Lerp(_pace, Mathf.Clamp(speed / 4f, 0f, 1f), blend);
        _crouch = Mathf.Lerp(_crouch, suppressed ? 1f : 0f, blend);
        if (_pace < 0.01f && speed < 0.05f)
            _pace = 0f;
        if (Mathf.Abs(_crouch - (suppressed ? 1f : 0f)) < 0.01f)
            _crouch = suppressed ? 1f : 0f;

        _stride = Mathf.PosMod(_stride + speed * delta * Mathf.Tau / 1.65f, Mathf.Tau);
        for (var soldier = 0; soldier < Soldiers; soldier++)
        {
            var phase = _stride + soldier * 0.86f;
            var basePosition = Formation[soldier];
            var hip = basePosition + Vector3.Up * (1.09f - _crouch * 0.24f
                + _pace * (0.050f * Mathf.Cos(phase * 2f) - 0.13f));
            var lean = _pace * 0.19f + _crouch * 0.26f;
            var bodyPitch = new Basis(Vector3.Right, lean);
            var twist = new Basis(Vector3.Up, Mathf.Cos(phase) * 0.11f * _pace + (soldier - 1.5f) * 0.055f);
            _torsos.SetInstanceTransform(soldier, new Transform3D(twist * bodyPitch, hip));
            var aim = aiming ? -0.12f - recoil * 0.20f : 0.43f + _pace * 0.24f;
            _arms.SetInstanceTransform(soldier, new Transform3D(twist * new Basis(Vector3.Right, lean - aim),
                hip + bodyPitch * (Vector3.Up * 0.86f) + Vector3.Back * (recoil * 0.10f)));

            PlaceLeg(soldier * 2, hip + new Vector3(-0.17f, 0f, 0f), phase);
            PlaceLeg(soldier * 2 + 1, hip + new Vector3(0.17f, 0f, 0f), phase + Mathf.Pi);
        }
    }

    private void PlaceLeg(int index, Vector3 hip, float phase)
    {
        var swing = Mathf.Sin(phase);
        var thigh = swing * _pace * 0.79f + _crouch * 0.57f;
        var knee = -0.16f - Mathf.Max(0f, swing) * _pace * 0.97f - _crouch * 0.65f;
        var thighRotation = new Basis(Vector3.Right, thigh);
        var kneePosition = hip + thighRotation * (Vector3.Down * ThighLength);
        var shinRotation = new Basis(Vector3.Right, thigh + knee);
        var sole = kneePosition.Y + (shinRotation * (Vector3.Down * ShinLength)).Y - 0.06f;
        // Clamp ground penetration while blending between stride and crouch.
        kneePosition.Y -= Mathf.Min(0f, sole) * 0.55f;
        _thighs.SetInstanceTransform(index, new Transform3D(thighRotation, hip));
        _boots.SetInstanceTransform(index, new Transform3D(shinRotation, kneePosition));
    }

    private static MultiMesh AddLayer(Node3D model, string name, Mesh mesh, int instances)
    {
        var layer = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = mesh,
            InstanceCount = instances,
            CustomAabb = new Aabb(new Vector3(-3f, 0f, -3f), new Vector3(6f, 3f, 6f))
        };
        model.AddChild(new MultiMeshInstance3D
        {
            Name = name, Multimesh = layer, PhysicsInterpolationMode = Node.PhysicsInterpolationModeEnum.Off
        });
        return layer;
    }
}
