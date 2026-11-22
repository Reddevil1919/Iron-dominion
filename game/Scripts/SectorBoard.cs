using Godot;
using IronDominion.Domain;

namespace IronDominion.Game;

internal sealed class SectorBoard
{
    private static readonly Color NeutralColor = new("d4dccd");
    private static readonly Color ContestedColor = new("ebce75");
    private readonly Dictionary<EntityId, SectorView> _views = new();
    private readonly IReadOnlyDictionary<PlayerId, StandardMaterial3D> _teamMaterials;
    private readonly long _captureTicks;
    private readonly Camera3D? _camera;
    private float _symbolScale;

    public SectorBoard(Node3D markers, Node canvas, GameConfiguration configuration,
        IReadOnlyDictionary<PlayerId, StandardMaterial3D> teamMaterials)
    {
        _teamMaterials = teamMaterials;
        _captureTicks = (long)configuration.CaptureSeconds * configuration.TickRate;
        _camera = markers.GetViewport().GetCamera3D();
        var radius = configuration.CaptureRadiusMeters;
        var ring = radius > 0f ? CaptureOutline(radius) : null;

        var panel = HudStyle.Panel("SectorPanel");
        panel.OffsetLeft = 24;
        panel.OffsetTop = 24;
        panel.OffsetRight = 284;
        canvas.AddChild(panel);

        var rows = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        rows.AddThemeConstantOverride("separation", 7);
        panel.AddChild(rows);
        var heading = HudStyle.Label("SectorHeading", 14, HudStyle.Muted);
        heading.Text = "SECTORS";
        rows.AddChild(heading);

        var sectors = new List<Sector>();
        foreach (var (markerName, code, landmark, id) in new[]
        {
            ("Alpha", "A", "RELAY", new EntityId(11)),
            ("Bravo", "B", "SERVICE", new EntityId(12)),
            ("Charlie", "C", "CHECKPOINT", new EntityId(13))
        })
        {
            var marker = markers.GetNode<Marker3D>(markerName);
            sectors.Add(new Sector(id, null, new SimVector(marker.GlobalPosition.X, marker.GlobalPosition.Z)));

            DepotWayfinding.AddSectorStencil(marker, landmark, radius);
            var material = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };
            if (ring is not null)
            {
                marker.AddChild(new MeshInstance3D
                {
                    Name = "Ring", Mesh = ring, MaterialOverride = material, Position = new Vector3(0f, 0.048f, 0f),
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
                });
            }

            var symbol = new Label3D
            {
                Name = "Symbol", Text = code, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                FontSize = 48, OutlineSize = 6, PixelSize = 0.046f, Position = new Vector3(0f, 2.7f, 0f)
            };
            marker.AddChild(symbol);

            var row = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            row.AddThemeConstantOverride("separation", 3);
            var status = HudStyle.Label(markerName, 15, HudStyle.Text);
            var progress = HudStyle.Progress($"{markerName}Progress", HudStyle.Blue, Math.Max(1, _captureTicks));
            row.AddChild(status);
            row.AddChild(progress);
            rows.AddChild(row);
            _views.Add(id, new SectorView(code, material, symbol, status, progress));
        }

        Sectors = sectors.AsReadOnly();
    }

    public IReadOnlyList<Sector> Sectors { get; }

    public void Update(IReadOnlyWorld world, Func<SimVector, bool> canSeeSector)
    {
        var scale = Mathf.Clamp((_camera?.Size ?? 136f) / 90f, 0.56f, 1f);
        if (!Mathf.IsEqualApprox(scale, _symbolScale))
        {
            _symbolScale = scale;
            foreach (var marker in _views.Values)
                marker.Symbol.Scale = Vector3.One * scale;
        }

        foreach (var sector in world.Sectors)
        {
            var view = _views[sector.Id];
            var visible = canSeeSector(sector.Position);
            var contested = visible && sector.Contested;
            var challenger = visible ? sector.CapturingPlayer : null;
            var color = contested ? ContestedColor : TeamColor(sector.Owner ?? challenger);
            if (view.Material.AlbedoColor != color)
            {
                view.Material.AlbedoColor = color;
                view.Symbol.Modulate = color;
                HudStyle.SetTextColor(view.Status, color);
                HudStyle.SetProgressColor(view.Progress, color);
            }

            var status = $"{view.Code}   {OwnerName(sector.Owner)}";
            if (contested)
                status += " · Contested";
            else if (challenger is { } player && _captureTicks > 0)
                status += $" · {OwnerName(player)} {sector.CaptureTicks * 100 / _captureTicks}%";

            HudStyle.SetText(view.Status, status);
            view.Progress.Value = visible ? sector.CaptureTicks : 0;
        }
    }

    private static Mesh CaptureOutline(float radius)
    {
        const int arcs = 6;
        const int subdivisions = 10;
        var arcLength = Mathf.Tau / arcs;
        var inner = Mathf.Max(radius - 0.31f, 0f);
        using var surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        for (var arc = 0; arc < arcs; arc++)
        for (var step = 0; step < subdivisions; step++)
        {
            var start = (arc + 0.06f + (0.88f * step / subdivisions)) * arcLength;
            var end = start + 0.88f * arcLength / subdivisions;
            var a = new Vector3(Mathf.Cos(start), 0f, Mathf.Sin(start));
            var b = new Vector3(Mathf.Cos(end), 0f, Mathf.Sin(end));
            surface.AddVertex(a * radius);
            surface.AddVertex(b * radius);
            surface.AddVertex(a * inner);
            if (inner > 0f)
            {
                surface.AddVertex(b * radius);
                surface.AddVertex(b * inner);
                surface.AddVertex(a * inner);
            }
        }

        return surface.Commit();
    }

    private Color TeamColor(PlayerId? player) =>
        player is { } id ? _teamMaterials[id].AlbedoColor : NeutralColor;

    private static string OwnerName(PlayerId? player) => player?.Value switch
    {
        1 => "Blue",
        2 => "Orange",
        _ => "Neutral"
    };

    private sealed record SectorView(string Code, StandardMaterial3D Material, Label3D Symbol, Label Status, ProgressBar Progress);
}
