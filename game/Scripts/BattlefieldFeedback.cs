using Godot;

namespace IronDominion.Game;

internal sealed class BattlefieldFeedback
{
    private const int TrailTicks = 4;
    private const int NoticeTicks = 50;
    private readonly ImmediateMesh _mesh = new();
    private readonly StandardMaterial3D _material = new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        AlbedoColor = new Color("fff0bc")
    };
    private readonly MeshInstance3D _trailsView;
    private readonly List<Trail> _trails = new();
    private readonly PanelContainer _notice = HudStyle.Panel("OrderNotice");
    private readonly Label _message = HudStyle.Label("OrderMessage", 15, HudStyle.Amber);
    private long _noticeUntil;
    private bool _trailsChanged;

    public BattlefieldFeedback(Node3D battlefield, Node canvas)
    {
        _trailsView = new MeshInstance3D
        {
            Name = "CombatTrails", Mesh = _mesh, Visible = false,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
        };
        battlefield.AddChild(_trailsView);

        _notice.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight);
        _notice.OffsetLeft = -304;
        _notice.OffsetRight = -24;
        _notice.OffsetTop = 24;
        _notice.Visible = false;
        _message.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _message.CustomMinimumSize = new Vector2(248, 0);
        _notice.AddChild(_message);
        canvas.AddChild(_notice);
    }

    public void Trace(Vector3 origin, Vector3 target, long tick)
    {
        _trails.Add(new Trail(origin, target, tick + TrailTicks));
        _trailsChanged = true;
    }

    public void Reject(string? reason, long tick)
    {
        var message = reason switch
        {
            "unreachable_destination" => "No route to that position.",
            "blocked_ground" => "That ground is blocked.",
            "outside_battlefield" => "Choose a point on the battlefield.",
            "target_not_visible" => "Your units cannot see that target.",
            "invalid_target" => "That target is no longer available.",
            "actor_destroyed" => "That unit or building was destroyed.",
            "insufficient_credits" => "Not enough credits for that unit.",
            _ => "That order is unavailable."
        };
        HudStyle.SetText(_message, message);
        _notice.Visible = true;
        _noticeUntil = tick + NoticeTicks;
    }

    public void Update(long tick)
    {
        if (_notice.Visible && tick >= _noticeUntil)
            _notice.Hide();

        var expired = _trails.RemoveAll(trail => trail.Until <= tick);
        if (!_trailsChanged && expired == 0)
            return;

        _trailsChanged = false;
        _mesh.ClearSurfaces();
        _trailsView.Visible = _trails.Count > 0;
        if (_trails.Count == 0)
            return;

        _mesh.SurfaceBegin(Mesh.PrimitiveType.Lines, _material);
        foreach (var trail in _trails)
        {
            _mesh.SurfaceAddVertex(trail.Origin);
            _mesh.SurfaceAddVertex(trail.Target);
        }
        _mesh.SurfaceEnd();
    }

    private readonly record struct Trail(Vector3 Origin, Vector3 Target, long Until);
}
