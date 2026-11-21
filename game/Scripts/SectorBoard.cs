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

    public SectorBoard(Node3D markers, Node canvas, GameConfiguration configuration,
        IReadOnlyDictionary<PlayerId, StandardMaterial3D> teamMaterials)
    {
        _teamMaterials = teamMaterials;
        _captureTicks = (long)configuration.CaptureSeconds * configuration.TickRate;
        var radius = configuration.CaptureRadiusMeters;
        var ring = radius > 0f
            ? new TorusMesh { InnerRadius = Math.Max(0f, radius - 0.4f), OuterRadius = radius, Rings = 40, RingSegments = 8 }
            : null;

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
        foreach (var (markerName, code, id) in new[]
        {
            ("Alpha", "A", new EntityId(11)),
            ("Bravo", "B", new EntityId(12)),
            ("Charlie", "C", new EntityId(13))
        })
        {
            var marker = markers.GetNode<Marker3D>(markerName);
            sectors.Add(new Sector(id, null, new SimVector(marker.GlobalPosition.X, marker.GlobalPosition.Z)));

            var material = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };
            if (ring is not null)
            {
                marker.AddChild(new MeshInstance3D
                {
                    Name = "Ring", Mesh = ring, MaterialOverride = material, Position = new Vector3(0f, 0.05f, 0f),
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
                });
            }

            var symbol = new Label3D
            {
                Name = "Symbol", Text = code, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                FontSize = 48, OutlineSize = 8, PixelSize = 0.075f, Position = new Vector3(0f, 5f, 0f)
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

    public void Update(IReadOnlyWorld world)
    {
        foreach (var sector in world.Sectors)
        {
            var view = _views[sector.Id];
            var color = sector.Contested ? ContestedColor : TeamColor(sector.Owner ?? sector.CapturingPlayer);
            if (view.Material.AlbedoColor != color)
            {
                view.Material.AlbedoColor = color;
                view.Symbol.Modulate = color;
                HudStyle.SetTextColor(view.Status, color);
                HudStyle.SetProgressColor(view.Progress, color);
            }

            var status = $"{view.Code}   {OwnerName(sector.Owner)}";
            if (sector.Contested)
                status += " · Contested";
            else if (sector.CapturingPlayer is { } challenger && _captureTicks > 0)
                status += $" · {OwnerName(challenger)} {sector.CaptureTicks * 100 / _captureTicks}%";

            HudStyle.SetText(view.Status, status);
            view.Progress.Value = sector.CaptureTicks;
        }
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
