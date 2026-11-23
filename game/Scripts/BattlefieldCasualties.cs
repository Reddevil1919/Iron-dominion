using Godot;
using IronDominion.Domain;

namespace IronDominion.Game;

internal sealed class BattlefieldCasualties : IDisposable
{
    private const float GroundHoldSeconds = 3f;
    private const float StainLifetime = 13f;
    private const float DebrisGravity = 17f;

    private readonly Node3D _root = new() { Name = "CasualtyEffects", PhysicsInterpolationMode = Node.PhysicsInterpolationModeEnum.Off };
    private readonly List<TankWreck> _wrecks = [];
    private readonly List<FallenSquad> _squads = [];
    private readonly Aabb _floor;
    private Mesh[]? _fragmentMeshes;
    private StandardMaterial3D? _charred;
    private PlaneMesh? _markMesh;
    private Shader? _markShader;
    private long _tick = -1;

    public BattlefieldCasualties(Node3D battlefield, Aabb floor)
    {
        _floor = floor;
        battlefield.AddChild(_root);
    }

    public void Emit(UnitView unit, long tick, bool friendly)
    {
        var site = unit.GlobalPosition;
        if (unit.TurretMesh is { } turret)
            ScatterTank(unit.Id, tick, site, friendly, turret);
        else
            DropSquad(unit, tick, site, friendly);
    }

    private void ScatterTank(EntityId id, long tick, Vector3 site, bool friendly, Mesh turret)
    {
        var meshes = _fragmentMeshes ??= BuildFragments();
        var charred = _charred ??= new StandardMaterial3D
        {
            AlbedoColor = new Color("424039"), Metallic = 0.43f, Roughness = 0.88f
        };
        var root = new Node3D { Name = $"Wreck{id.Value}", Position = site };
        _root.AddChild(root);
        var random = new Random(id.Value);
        var fragments = new Fragment[9];
        for (var index = 0; index < fragments.Length; index++)
        {
            var mesh = index == 0 ? turret : meshes[index % meshes.Length];
            var size = index == 0 ? 0.91f : 0.72f + random.NextSingle() * 0.45f;
            var yaw = random.NextSingle() * Mathf.Tau;
            var launch = Mathf.Tau * index / fragments.Length + random.NextSingle() * 0.47f;
            var outward = new Vector3(Mathf.Cos(launch), 0f, Mathf.Sin(launch));
            var bounds = mesh.GetAabb();
            var restHeight = 0.04f - bounds.Position.Y * size;
            var start = outward * (index == 0 ? 0f : 0.45f + random.NextSingle() * 0.9f);
            start.Y = index == 0 ? 1.46f : 0.75f + random.NextSingle() * 0.6f;
            var velocity = outward * (index == 0 ? 4.4f : 3.6f + random.NextSingle() * 4.1f);
            velocity.Y = index == 0 ? 6.8f : 3.8f + random.NextSingle() * 4.8f;
            var touchdown = (velocity.Y + Mathf.Sqrt(velocity.Y * velocity.Y
                + 2f * DebrisGravity * (start.Y - restHeight))) / DebrisGravity;
            var node = new MeshInstance3D
            {
                Name = $"Fragment{index}", Mesh = mesh, Scale = Vector3.One * size,
                Position = start, MaterialOverride = charred
            };
            root.AddChild(node);
            fragments[index] = new Fragment(node, start, velocity, yaw, 4f + random.NextSingle() * 5f,
                restHeight, touchdown);
        }

        _wrecks.Add(new TankWreck(root, tick, site, friendly, fragments));
    }

    private void DropSquad(UnitView unit, long tick, Vector3 site, bool friendly)
    {
        var body = new Node3D { Name = $"FallenSquad{unit.Id.Value}" };
        _root.AddChild(body);
        if (unit.DetachInfantry(body) is not { } infantry)
        {
            body.QueueFree();
            return;
        }

        var contacts = infantry.BeginFall();
        var stains = new Node3D { Name = $"Blood{unit.Id.Value}", Visible = false };
        _root.AddChild(stains);
        var mesh = _markMesh ??= new PlaneMesh { Size = Vector2.One };
        var shader = _markShader ??= GD.Load<Shader>("res://Shaders/casualty_ground_stain.gdshader");
        var material = new ShaderMaterial { Shader = shader, RenderPriority = -2 };
        material.SetShaderParameter("age_seconds", 0f);
        for (var index = 0; index < contacts.Length; index++)
        {
            var position = contacts[index];
            position.Y = _floor.End.Y + 0.058f;
            var yaw = unit.Rotation.Y + index * 1.83f;
            stains.AddChild(new MeshInstance3D
            {
                Name = $"Stain{index}", Mesh = mesh, MaterialOverride = material,
                Position = position, Rotation = new Vector3(0f, yaw, 0f),
                Scale = new Vector3(0.92f + index * 0.09f, 1f, 1.16f),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            });
        }
        _squads.Add(new FallenSquad(body, stains, material, infantry, tick, site, friendly));
    }

    public void Advance(long tick, BattlefieldFog fog)
    {
        _tick = tick;
        foreach (var wreck in _wrecks)
            wreck.Root.Visible = IsVisible(wreck.Born, wreck.Site, wreck.Friendly, fog);
        foreach (var squad in _squads)
            squad.Visible = IsVisible(squad.Born, squad.Site, squad.Friendly, fog);
    }

    private bool IsVisible(long born, Vector3 site, bool friendly, BattlefieldFog fog) =>
        friendly || born == _tick || fog.CanSeeGround(new SimVector(site.X, site.Z));

    public void Render(double fraction)
    {
        if (_tick < 0)
            return;

        for (var index = _wrecks.Count - 1; index >= 0; index--)
        {
            var wreck = _wrecks[index];
            var age = Age(wreck.Born, fraction);
            var hasFragments = false;
            foreach (var piece in wreck.Fragments)
            {
                if (age >= piece.Touchdown + GroundHoldSeconds)
                {
                    piece.View.Hide();
                    continue;
                }

                hasFragments = true;
                PoseFragment(wreck.Site, piece, age);
            }
            if (!hasFragments)
                RemoveWreck(index);
        }

        for (var index = _squads.Count - 1; index >= 0; index--)
        {
            var squad = _squads[index];
            var age = Age(squad.Born, fraction);
            var groundAge = age - InfantryMotion.FallSeconds;
            if (groundAge >= StainLifetime)
            {
                RemoveSquad(index);
                continue;
            }

            if (squad.Body is { } body)
            {
                if (groundAge >= GroundHoldSeconds)
                {
                    Free(body);
                    squad.Body = null;
                    squad.Motion = null;
                }
                else
                {
                    body.Visible = squad.Visible;
                    if (groundAge < 0f || !squad.Settled)
                        squad.Motion!.PoseFall(age);
                    squad.Settled = groundAge >= 0f;
                }
            }

            squad.Stains.Visible = groundAge >= 0f && squad.Visible;
            if (groundAge >= 0f)
                squad.Material.SetShaderParameter("age_seconds", groundAge);
        }
    }

    private float Age(long born, double fraction) =>
        Mathf.Max(0f, (float)((_tick - born + fraction) / Engine.PhysicsTicksPerSecond));

    private void PoseFragment(Vector3 site, Fragment piece, float age)
    {
        var time = Mathf.Min(age, piece.Touchdown);
        var position = piece.Start + piece.Velocity * time;
        position.X = Mathf.Clamp(site.X + position.X, _floor.Position.X + 0.45f, _floor.End.X - 0.45f) - site.X;
        position.Z = Mathf.Clamp(site.Z + position.Z, _floor.Position.Z + 0.45f, _floor.End.Z - 0.45f) - site.Z;
        position.Y = Mathf.Max(piece.RestHeight, position.Y - DebrisGravity * 0.5f * time * time);
        var turn = time * piece.Spin;
        var airborne = 1f - Mathf.SmoothStep(piece.Touchdown - 0.19f, piece.Touchdown, time);
        piece.View.Position = position;
        piece.View.Rotation = new Vector3(Mathf.Sin(turn) * airborne * 1.7f, piece.Yaw + turn,
            Mathf.Sin(turn * 0.74f) * airborne * 1.1f);
    }

    private static Mesh[] BuildFragments()
    {
        var plate = new ArtMesh();
        plate.Box(Vector3.Zero, new Vector3(1.4f, 0.13f, 0.61f), "79705d");
        plate.Box(new Vector3(-0.21f, 0.055f, 0.26f), new Vector3(0.72f, 0.15f, 0.40f),
            new Color("675f4d"), new Basis(Vector3.Up, 0.31f));
        var track = new ArtMesh();
        for (var link = -1; link <= 1; link++)
            track.Box(new Vector3(link * 0.27f, 0f, 0f), new Vector3(0.23f, 0.14f, 0.72f), "484945");
        var gear = new ArtMesh();
        gear.Cylinder(Vector3.Zero, new Vector3(0.69f, 0.20f, 0.69f), new Color("555651"));
        gear.Cylinder(new Vector3(0f, 0.14f, 0f), new Vector3(0.34f, 0.15f, 0.34f), new Color("777367"));
        return [plate.Build(), track.Build(), gear.Build()];
    }

    private void RemoveWreck(int index)
    {
        Free(_wrecks[index].Root);
        _wrecks.RemoveAt(index);
    }

    private void RemoveSquad(int index)
    {
        var squad = _squads[index];
        if (squad.Body is { } body)
            Free(body);
        Free(squad.Stains);
        squad.Material.Dispose();
        _squads.RemoveAt(index);
    }

    private static void Free(Node3D node)
    {
        node.Hide();
        node.QueueFree();
    }

    public void Reset()
    {
        while (_wrecks.Count > 0)
            RemoveWreck(_wrecks.Count - 1);
        while (_squads.Count > 0)
            RemoveSquad(_squads.Count - 1);
        _tick = -1;
    }

    public void Dispose()
    {
        foreach (var squad in _squads)
            squad.Material.Dispose();
        if (_fragmentMeshes is { } meshes)
            foreach (var mesh in meshes)
                mesh.Dispose();
        _charred?.Dispose();
        _markMesh?.Dispose();
    }

    private sealed record TankWreck(Node3D Root, long Born, Vector3 Site, bool Friendly, Fragment[] Fragments);
    private readonly record struct Fragment(MeshInstance3D View, Vector3 Start, Vector3 Velocity,
        float Yaw, float Spin, float RestHeight, float Touchdown);

    private sealed class FallenSquad(Node3D body, Node3D stains, ShaderMaterial material, InfantryMotion motion,
        long born, Vector3 site, bool friendly)
    {
        public Node3D? Body { get; set; } = body;
        public Node3D Stains { get; } = stains;
        public ShaderMaterial Material { get; } = material;
        public InfantryMotion? Motion { get; set; } = motion;
        public long Born { get; } = born;
        public Vector3 Site { get; } = site;
        public bool Friendly { get; } = friendly;
        public bool Visible { get; set; }
        public bool Settled { get; set; }
    }
}
