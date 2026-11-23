using Godot;

namespace IronDominion.Game;

internal sealed class TankArt
{
    public const float TrackGauge = 1.42f;
    public const float TrackPitch = 0.20f;
    private static readonly Basis AlongZ = new(Vector3.Right, Mathf.Pi / 2f);
    private static readonly Basis AlongX = new(Vector3.Forward, Mathf.Pi / 2f);

    public Mesh Hull { get; }
    public Mesh Turret { get; }
    public Mesh Gun { get; }
    public Mesh Tracks { get; } = BuildTracks();
    public ShaderMaterial Scuffed { get; } = GD.Load<ShaderMaterial>("res://Shaders/tank_armor_scuffed.tres");
    public ShaderMaterial Scorched { get; } = GD.Load<ShaderMaterial>("res://Shaders/tank_armor_scorched.tres");

    public TankArt(Color identification, bool local)
    {
        using var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://Shaders/armor_finish.gdshader") };
        var armor = local ? "586346" : "7c6a53";
        Hull = VehicleHull(identification, local ? "626b50" : "8c7861", material);
        Turret = VehicleTurret(identification, armor, material);
        Gun = VehicleGun(armor, material);
    }

    private static Mesh BuildTracks()
    {
        const float radius = 0.411f;
        const float height = 0.46f;
        const float wheel = 1.94f;
        const float width = 0.72f;
        const int arcSections = 8;

        // Order the belt from the bottom front, along the bottom to the rear and back over the top.
        var sections = new List<(Vector2 Center, Vector2 Normal, float Distance)>
        {
            (new Vector2(height - radius, -wheel), Vector2.Left, 0f)
        };
        var arcLength = Mathf.Pi * radius;
        for (var end = 0; end < 2; end++)
        for (var segment = 0; segment <= arcSections; segment++)
        {
            var arc = (float)segment / arcSections;
            var angle = (end - 0.5f + arc) * Mathf.Pi;
            var radial = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
            var center = new Vector2(height, end == 0 ? wheel : -wheel) + radial * radius;
            var distance = 2f * wheel * (end + 1) + arcLength * (end + arc);
            sections.Add((center, radial, distance));
        }

        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        var loopLength = sections[^1].Distance;
        var linkCount = Mathf.RoundToInt(loopLength / TrackPitch);
        for (var side = 0; side < 2; side++)
        {
            var x = side == 0 ? -TrackGauge : TrackGauge;
            var start = side * sections.Count * 2;
            for (var row = 0; row < sections.Count; row++)
            {
                var (center, normal, distance) = sections[row];
                for (var edge = 0; edge < 2; edge++)
                {
                    surface.SetUV(new Vector2(distance * linkCount / loopLength, edge));
                    surface.SetColor(new Color(side, 0f, 0f));
                    surface.SetNormal(new Vector3(0f, normal.X, normal.Y));
                    surface.AddVertex(new Vector3(x + (edge - 0.5f) * width, center.X, center.Y));
                }

                if (row == sections.Count - 1)
                    continue;
                var a = start + row * 2;
                surface.AddIndex(a);
                surface.AddIndex(a + 2);
                surface.AddIndex(a + 1);
                surface.AddIndex(a + 1);
                surface.AddIndex(a + 2);
                surface.AddIndex(a + 3);
            }
        }
        return surface.Commit();
    }

    private static Mesh VehicleHull(Color identification, string armor, Material material)
    {
        var mesh = new ArtMesh();
        mesh.Box(new(0f, 0.61f, 0.10f), new(2.45f, 0.61f, 4.80f), "40443b");
        for (var side = -1; side <= 1; side += 2)
        {
            var x = side * 1.42f;
            for (var wheel = -2; wheel <= 2; wheel++)
                mesh.Cylinder(new(x, 0.46f, wheel * 0.85f), new(0.81f, 0.74f, 0.81f), new Color("525449"), AlongX);
            mesh.Cylinder(new(x, 0.46f, -1.94f), new(0.74f, 0.69f, 0.74f), new Color("484942"), AlongX);
            mesh.Cylinder(new(x, 0.46f, 1.94f), new(0.74f, 0.69f, 0.74f), new Color("484942"), AlongX);
            mesh.Box(new(x, 0.95f, 0), new(0.73f, 0.17f, 5.04f), "343831");
            for (var panel = -2; panel <= 2; panel++)
                mesh.Box(new(x, 1.00f, panel * 0.99f), new(0.75f, 0.30f, 0.92f), armor);
        }

        mesh.Box(new(0, 0.89f, 0.12f), new(2.48f, 0.93f, 4.96f), armor);
        mesh.Box(new(0, 1.28f, 0.05f), new(2.90f, 0.29f, 4.56f), armor);
        mesh.Box(new(0, 1.15f, -2.26f), new(2.7f, 0.48f, 0.91f), new Color(armor), new Basis(Vector3.Right, -0.28f));
        mesh.Box(new(0, 1.43f, 1.94f), new(2.38f, 0.08f, 0.55f), "393e35");
        for (var slat = -4; slat <= 4; slat++)
            mesh.Box(new(slat * 0.23f, 1.48f, 1.94f), new(0.07f, 0.03f, 0.51f), "767b6b");

        mesh.Box(new(0f, 0.81f, 2.53f), new(2.57f, 0.15f, 0.20f), "45493d");
        mesh.Box(new(-0.93f, 1.02f, 2.50f), new(0.21f, 0.13f, 0.14f), "804a42");
        mesh.Box(new(0.93f, 1.02f, 2.50f), new(0.21f, 0.13f, 0.14f), "804a42");
        mesh.Box(new(0, 1.47f, 1.13f), new(1.26f, 0.037f, 0.27f), identification);
        mesh.Box(new(-1.02f, 1.05f, -2.57f), new(0.33f, 0.18f, 0.14f), "b7b59d");
        mesh.Box(new(1.02f, 1.05f, -2.57f), new(0.33f, 0.18f, 0.14f), "b7b59d");
        return mesh.Build(material);
    }

    private static Mesh VehicleTurret(Color identification, string armor, Material material)
    {
        var mesh = new ArtMesh();
        mesh.Cylinder(new(0, 0.03f, 0.08f), new(2.28f, 0.20f, 2.16f), new Color("343a30"));
        mesh.Box(new(0, 0.36f, 0.08f), new(2.41f, 0.64f, 2.62f), armor);
        mesh.Box(new(0, 0.61f, 0.08f), new(2.18f, 0.23f, 2.43f), armor);
        for (var side = -1; side <= 1; side += 2)
            mesh.Box(new(side * 0.84f, 0.37f, -1.01f), new(0.84f, 0.59f, 1.16f), new Color(armor),
                new Basis(Vector3.Up, side * 0.19f));

        for (var tile = -1; tile <= 1; tile++)
        {
            mesh.Box(new(-1.22f, 0.45f, tile * 0.61f), new(0.15f, 0.23f, 0.53f), "53584b");
            mesh.Box(new(1.22f, 0.45f, tile * 0.61f), new(0.15f, 0.23f, 0.53f), "53584b");
        }
        mesh.Box(new(0, 0.39f, 1.5f), new(2.19f, 0.50f, 0.40f), "4b5141");
        mesh.Box(new(0, 0.66f, 1.36f), new(1.34f, 0.06f, 0.29f), identification);
        mesh.Cylinder(new(-0.59f, 0.73f, 0.23f), new(0.64f, 0.15f, 0.64f), new Color("41463a"));
        mesh.Cylinder(new(0.63f, 0.74f, -0.08f), new(0.60f, 0.14f, 0.60f), new Color("41463a"));
        mesh.Box(new(0.64f, 0.86f, -0.18f), new(0.34f, 0.21f, 0.28f), "343936");
        mesh.Cylinder(new(-0.76f, 1.16f, 1.04f), new(0.043f, 1.11f, 0.043f), new Color("41473e"),
            new Basis(Vector3.Right, -0.14f));
        return mesh.Build(material);
    }

    private static Mesh VehicleGun(string armor, Material material)
    {
        var mesh = new ArtMesh();
        mesh.Box(new(0, 0, 0.03f), new(0.66f, 0.52f, 0.57f), armor);
        mesh.Cylinder(new(0, 0, -1.28f), new(0.21f, 2.69f, 0.21f), new Color("515b49"), AlongZ);
        mesh.Cylinder(new(0, 0, -2.45f), new(0.28f, 0.43f, 0.28f), new Color("3d4438"), AlongZ);
        mesh.Cylinder(new(0, 0, -2.68f), new(0.16f, 0.027f, 0.16f), new Color("222722"), AlongZ);
        return mesh.Build(material);
    }
}
