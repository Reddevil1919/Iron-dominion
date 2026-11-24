using Godot;

namespace IronDominion.Game;

internal sealed class TankArt
{
    public const float TrackGauge = 1.42f;
    public const float TrackPitch = 0.20f;
    private const string AssetRoot = "res://Assets/ReferenceTank/";
    private static readonly Texture2D Albedo = GD.Load<Texture2D>(AssetRoot + "armor-albedo.svg");
    private static readonly Texture2D Normal = GD.Load<Texture2D>(AssetRoot + "armor-normal.svg");
    private static readonly Texture2D Orm = GD.Load<Texture2D>(AssetRoot + "armor-orm.svg");

    public Mesh Hull { get; } = GD.Load<Mesh>(AssetRoot + "tank-hull.obj");
    public Mesh Turret { get; } = GD.Load<Mesh>(AssetRoot + "tank-turret.obj");
    public Mesh Gun { get; } = GD.Load<Mesh>(AssetRoot + "tank-gun.obj");
    public Mesh Tracks { get; } = BuildTracks();
    public ShaderMaterial Intact { get; }
    public ShaderMaterial Scuffed { get; }
    public ShaderMaterial Scorched { get; }

    public TankArt(Color identification, bool local)
    {
        var paint = new Color(local ? "6a765e" : "9a8065");
        Intact = Finish(paint, identification, 0f);
        Scuffed = Finish(paint, identification, 1f);
        Scorched = Finish(paint, identification, 2f);
    }

    private static ShaderMaterial Finish(Color paint, Color team, float stage)
    {
        var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://Shaders/armor_finish.gdshader") };
        BindAtlas(material);
        material.SetShaderParameter("paint_tint", paint);
        material.SetShaderParameter("team_tint", team);
        material.SetShaderParameter("damage_stage", stage);
        return material;
    }

    public static void BindAtlas(ShaderMaterial material)
    {
        material.SetShaderParameter("armor_albedo", Albedo);
        material.SetShaderParameter("armor_normal", Normal);
        material.SetShaderParameter("armor_orm", Orm);
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
        surface.GenerateTangents();
        return surface.Commit();
    }
}
