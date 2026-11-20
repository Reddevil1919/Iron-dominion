using Godot;

namespace IronDominion.Game;

public partial class RtsCamera : Node3D
{
    private const float MinZoomSize = 32f;
    private const float MaxZoomSize = 136f;
    private const float ZoomStep = 0.85f;
    private const float PanSpeedFactor = 0.60f;
    private const float EdgeMarginPixels = 24f;

    [Export] private Camera3D _view = null!;
    [Export] private CollisionShape3D _terrain = null!;

    private Rect2 _panBounds;

    public override void _Ready()
    {
        var box = (BoxShape3D)_terrain.Shape;
        var bounds = _terrain.GlobalTransform * new Aabb(-box.Size / 2f, box.Size);
        _panBounds = new Rect2(bounds.Position.X, bounds.Position.Z, bounds.Size.X, bounds.Size.Z);
    }

    public override void _Process(double delta)
    {
        var direction = Input.GetVector("camera_pan_left", "camera_pan_right", "camera_pan_up", "camera_pan_down");
        if (direction == Vector2.Zero)
            direction = GetEdgePanDirection();
        if (direction == Vector2.Zero)
            return;

        var basis = _view.GlobalBasis;
        var right = new Vector2(basis.X.X, basis.X.Z).Normalized();
        var back = new Vector2(basis.Z.X, basis.Z.Z).Normalized();
        var displacement = (right * direction.X + back * direction.Y) * (_view.Size * PanSpeedFactor * (float)delta);
        var focus = GlobalPosition;
        GlobalPosition = new Vector3(
            Mathf.Clamp(focus.X + displacement.X, _panBounds.Position.X, _panBounds.End.X),
            focus.Y,
            Mathf.Clamp(focus.Z + displacement.Y, _panBounds.Position.Y, _panBounds.End.Y));
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (inputEvent is not InputEventMouseButton { Pressed: true } wheel)
            return;

        var direction = wheel.ButtonIndex switch
        {
            MouseButton.WheelUp => 1,
            MouseButton.WheelDown => -1,
            _ => 0
        };
        if (direction == 0)
            return;

        var steps = wheel.Factor > 0 ? wheel.Factor : 1f;
        _view.Size = Mathf.Clamp(_view.Size * Mathf.Pow(ZoomStep, direction * steps), MinZoomSize, MaxZoomSize);
        GetViewport().SetInputAsHandled();
    }

    private Vector2 GetEdgePanDirection()
    {
        if (!GetWindow().HasFocus())
            return Vector2.Zero;

        var viewport = GetViewport();
        var rect = viewport.GetVisibleRect();
        var pointer = viewport.GetMousePosition();
        if (!rect.HasPoint(pointer))
            return Vector2.Zero;

        var margin = Mathf.Min(EdgeMarginPixels, Mathf.Min(rect.Size.X, rect.Size.Y) / 4f);
        return new Vector2(
            EdgeDirection(pointer.X, rect.Position.X, rect.End.X, margin),
            EdgeDirection(pointer.Y, rect.Position.Y, rect.End.Y, margin)).LimitLength();
    }

    private static int EdgeDirection(float pointer, float start, float end, float margin)
    {
        if (pointer < start + margin)
            return -1;
        return pointer >= end - margin ? 1 : 0;
    }
}
