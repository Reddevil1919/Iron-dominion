using Godot;

namespace IronDominion.Game;

internal static class DepotWayfinding
{
    private static readonly Color LanePaint = new("b9ad83");
    private static readonly Color ChalkPaint = new("cdc6a6");
    private static readonly Color HazardPaint = new("c6a266");
    private static readonly Color StencilPaint = new("343e37");
    private static readonly StandardMaterial3D Paint = new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        VertexColorUseAsAlbedo = true,
        VertexColorIsSrgb = true
    };

    public static void AddGroundMarkings(Node3D battlefield)
    {
        var mesh = new ArtMesh();
        for (var side = -1; side <= 1; side += 2)
        {
            // Short arrows on each approach keep the dashed center line clear.
            Arrow(mesh, new Vector2(side * 48f, side * 1.77f), new Vector2(-side, 0f));
            Arrow(mesh, new Vector2(side * 38f, side * 1.77f), new Vector2(-side, 0f));
            Line(mesh, new Vector2(side * 54f, -2.96f), new Vector2(side * 54f, 2.96f), 0.24f, ChalkPaint, 0.020f);
            for (var stripe = -2; stripe <= 2; stripe++)
                Line(mesh, new Vector2(side * 54.9f, stripe * 1.14f),
                    new Vector2(side * 56.4f, stripe * 1.14f), 0.32f, LanePaint, 0.020f);
        }

        battlefield.AddChild(Marking("RoadMarkings", mesh));
        foreach (var patch in battlefield.GetNode<Node3D>("CoverZones").GetChildren().OfType<MeshInstance3D>())
            MarkCover(patch);
    }

    public static void AddSectorStencil(Marker3D marker, string label, float radius)
    {
        // Keep the stencil and bay pieces inside the capture boundary.
        if (radius < 2f)
            return;

        var mesh = new ArtMesh();
        var width = radius * 0.55f;
        var end = radius * 0.55f;
        for (var side = -1; side <= 1; side += 2)
        {
            var x = width * side;
            Line(mesh, new Vector2(x, -end), new Vector2(x, -end + 0.76f), 0.15f, LanePaint, 0.022f);
            Line(mesh, new Vector2(x, end), new Vector2(x, end - 0.76f), 0.15f, LanePaint, 0.022f);
            Line(mesh, new Vector2(x, end), new Vector2(x - side * 0.76f, end), 0.15f, LanePaint, 0.022f);
            Line(mesh, new Vector2(x, -end), new Vector2(x - side * 0.76f, -end), 0.15f, LanePaint, 0.022f);
        }

        marker.AddChild(Marking("ApronPaint", mesh));
        marker.AddChild(new Label3D
        {
            Name = "GroundStencil", Text = label, FontSize = 48, OutlineSize = 0,
            PixelSize = Mathf.Min(radius * 0.0055f, 0.033f), Modulate = StencilPaint,
            Position = new Vector3(0f, 0.036f, Mathf.Min(radius * 0.30f, 1.65f)),
            Rotation = new Vector3(-Mathf.Pi / 2f, 0f, 0f),
            AlphaCut = Label3D.AlphaCutMode.Discard,
            DoubleSided = false, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        });
    }

    private static void MarkCover(MeshInstance3D patch)
    {
        var edge = patch.GetNode<MeshInstance3D>("ProtectedEdge");
        var mesh = new ArtMesh();
        var outline = patch.GetAabb();
        var strip = edge.GetAabb();
        var y = edge.Position.Y + strip.End.Y + 0.006f;
        var inward = -Mathf.Sign(edge.Position.X);
        var center = edge.Position.X;

        // The angled paint sits on the authored protective strip. The dashed side serves as an exit boundary.
        for (var z = strip.Position.Z + 0.30f; z < strip.End.Z - 0.36f; z += 1.08f)
        {
            Line(mesh, new Vector2(center + inward * 0.20f, z), new Vector2(center - inward * 0.20f, z + 0.42f),
                0.15f, HazardPaint, y);
        }

        var farEdge = inward > 0f ? outline.End.X - 0.22f : outline.Position.X + 0.22f;
        for (var z = outline.Position.Z + 0.46f; z < outline.End.Z - 1.0f; z += 1.45f)
            Line(mesh, new Vector2(farEdge, z), new Vector2(farEdge, z + 0.78f), 0.13f, LanePaint, y);

        patch.AddChild(Marking("CoverPaint", mesh));
    }

    private static void Arrow(ArtMesh mesh, Vector2 origin, Vector2 direction)
    {
        var tip = origin + direction * 1.35f;
        var shoulder = tip - direction * 0.86f;
        var across = direction.Orthogonal() * 0.53f;
        Line(mesh, origin - direction * 0.78f, tip, 0.19f, ChalkPaint, 0.020f);
        Line(mesh, tip, shoulder + across, 0.19f, ChalkPaint, 0.020f);
        Line(mesh, tip, shoulder - across, 0.19f, ChalkPaint, 0.020f);
    }

    private static void Line(ArtMesh mesh, Vector2 start, Vector2 end, float width, Color color, float elevation)
    {
        var delta = end - start;
        var center = (start + end) * 0.5f;
        var rotation = new Basis(Vector3.Up, Mathf.Atan2(-delta.Y, delta.X));
        mesh.Box(new Vector3(center.X, elevation, center.Y), new Vector3(delta.Length(), 0.008f, width), color, rotation);
    }

    private static MeshInstance3D Marking(string name, ArtMesh mesh) => new()
    {
        Name = name, Mesh = mesh.Build(Paint), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
    };
}
