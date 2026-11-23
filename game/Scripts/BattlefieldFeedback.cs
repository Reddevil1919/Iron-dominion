using Godot;
using IronDominion.Domain;

namespace IronDominion.Game;

internal sealed class BattlefieldFeedback
{
    private const int NoticeTicks = 50;
    private readonly BattlefieldEffects _effects;
    private readonly PanelContainer _notice = HudStyle.Panel("OrderNotice");
    private readonly Label _message = HudStyle.Label("OrderMessage", 15, HudStyle.Amber);
    private long _noticeUntil;

    public BattlefieldFeedback(Node3D battlefield, Node canvas)
    {
        _effects = new BattlefieldEffects(battlefield);

        _notice.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight);
        _notice.OffsetLeft = -304;
        _notice.OffsetRight = -24;
        _notice.OffsetTop = 72;
        _notice.Visible = false;
        _message.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _message.CustomMinimumSize = new Vector2(248, 0);
        _notice.AddChild(_message);
        canvas.AddChild(_notice);
    }

    public void Fire(ShotFired shot, UnitView attacker, Vector3 target, float targetRadius,
        bool sourceDestroyed, bool targetDestroyed) =>
        _effects.Fire(shot, attacker, target, targetRadius, sourceDestroyed, targetDestroyed);

    public void TrackDust(EntityId tank, long tick, Vector3 position) => _effects.TrackDust(tank, tick, position);

    public void ExplodeTank(long tick, Vector3 position) => _effects.ExplodeTank(tick, position);

    public void Reset()
    {
        _notice.Hide();
        _effects.Reset();
    }

    public void Reject(string? reason, long tick)
    {
        var message = reason switch
        {
            "unreachable_destination" => "No route to that position.",
            "blocked_ground" => "That ground is blocked.",
            "outside_battlefield" => "Choose a point on the battlefield.",
            "target_not_visible" => "Your units cannot see that target.",
            "no_firing_position" => "No reachable firing position found.",
            "invalid_target" => "That target is no longer available.",
            "actor_destroyed" => "That unit or building was destroyed.",
            "insufficient_credits" => "Not enough credits for that unit.",
            _ => "That order is unavailable."
        };
        HudStyle.SetText(_message, message);
        _notice.Visible = true;
        _noticeUntil = tick + NoticeTicks;
    }

    public void Update(long tick, IReadOnlySet<EntityId> visibleActors)
    {
        _effects.Advance(tick, visibleActors);
        if (_notice.Visible && tick >= _noticeUntil)
            _notice.Hide();
    }

    public void Render(double tickFraction) => _effects.Render(tickFraction);
}
