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

    public BattlefieldFeedback(Node3D battlefield, Node canvas, DepotImpactSurfaces surfaces)
    {
        _effects = new BattlefieldEffects(battlefield, surfaces);

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

    public void Fire(ShotFired shot, UnitView attacker, ShotSurface target,
        bool sourceDestroyed, bool targetDestroyed) =>
        _effects.Fire(shot, attacker, target, sourceDestroyed, targetDestroyed);

    public void TrackTerrain(UnitView tank, long tick, float overshoot, bool friendly) =>
        _effects.TrackTerrain(tank, tick, overshoot, friendly);

    public void FootstepDust(EntityId squad, long tick, Vector3 position) => _effects.FootstepDust(squad, tick, position);

    public void EngineSmoke(EntityId tank, long tick, Vector3 position) => _effects.EngineSmoke(tank, tick, position);

    public void ExplodeTank(long tick, Vector3 position) => _effects.ExplodeTank(tick, position);

    public void Weld(TankRepaired repair, SurfaceImpact contact) => _effects.Weld(repair, contact);

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
            "invalid_repair_target" => "Choose a living friendly tank.",
            "tank_already_repaired" => "That tank is already at full health.",
            "no_repair_position" => "No reachable position to repair that tank.",
            "unit_cannot_attack" => "Engineers repair friendly tanks.",
            "actor_destroyed" => "That unit or building was destroyed.",
            "insufficient_credits" => "Not enough credits for that unit.",
            _ => "That order is unavailable."
        };
        HudStyle.SetText(_message, message);
        _notice.Visible = true;
        _noticeUntil = tick + NoticeTicks;
    }

    public void Update(long tick, BattlefieldFog fog)
    {
        _effects.Advance(tick, fog);
        if (_notice.Visible && tick >= _noticeUntil)
            _notice.Hide();
    }

    public void Render(double tickFraction) => _effects.Render(tickFraction);
}
