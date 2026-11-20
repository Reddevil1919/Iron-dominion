using Godot;
using IronDominion.Application;
using IronDominion.Domain;

namespace IronDominion.Game;

public partial class UnitSelectionController : Control
{
    private const float DragThreshold = 6f;
    private static readonly Color BoxColor = new("dcff67");
    private static readonly Color BoxFill = new(BoxColor, 0.16f);

    private readonly Queue<Action> _pending = new();
    private UnitSelection? _selection;
    private IReadOnlyList<UnitView> _units = [];
    private Vector2? _start;
    private Vector2 _pointer;
    private bool _dragging;

    public void Initialize(UnitSelection selection, IReadOnlyList<UnitView> units)
    {
        _selection = selection;
        _units = units;
    }

    public override void _Ready() => GetWindow().FocusExited += CancelGesture;

    public override void _ExitTree() => GetWindow().FocusExited -= CancelGesture;

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (_selection is not { } selection)
            return;

        if (inputEvent is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } button)
        {
            _start = _pointer = button.Position;
            GetViewport().SetInputAsHandled();
        }
        else if (inputEvent is InputEventKey { Pressed: true, Echo: false } key)
        {
            var code = key.PhysicalKeycode == Key.None ? key.Keycode : key.PhysicalKeycode;
            var group = (int)code - (int)Key.Key1 + 1;
            if (group is < 1 or > 9)
                return;

            if (key.CtrlPressed)
                _pending.Enqueue(() => selection.AssignGroup(group));
            else
                _pending.Enqueue(() => selection.RecallGroup(group));

            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Input(InputEvent inputEvent)
    {
        if (_start is not { } start || _selection is not { } selection)
            return;

        if (inputEvent is InputEventMouseMotion motion)
        {
            _pointer = motion.Position;
            _dragging |= start.DistanceSquaredTo(_pointer) >= DragThreshold * DragThreshold;
            QueueRedraw();
        }
        else if (inputEvent is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false } button)
        {
            if (!button.Canceled)
            {
                var pointer = button.Position;
                var isBox = _dragging || start.DistanceSquaredTo(pointer) >= DragThreshold * DragThreshold;
                var toggle = button.ShiftPressed;
                var bounds = new Rect2(start, pointer - start).Abs();
                _pending.Enqueue(() => selection.Select(isBox ? BoxHits(bounds) : ClickHits(pointer), toggle));
            }

            CancelGesture();
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_pending.Count == 0 || _selection is not { } selection)
            return;

        while (_pending.TryDequeue(out var update))
            update();

        foreach (var unit in _units)
            unit.ShowSelection(selection.Selected.Contains(unit.Id));
    }

    public override void _Draw()
    {
        if (_dragging && _start is { } start)
        {
            var bounds = new Rect2(start, _pointer - start).Abs();
            DrawRect(bounds, BoxFill);
            DrawRect(bounds, BoxColor, filled: false, width: 1f);
        }
    }

    private IEnumerable<EntityId> ClickHits(Vector2 point)
    {
        var viewport = GetViewport();
        if (!viewport.GetVisibleRect().HasPoint(point) || viewport.GetCamera3D() is not { } camera)
            yield break;

        var start = camera.ProjectRayOrigin(point);
        var end = start + camera.ProjectRayNormal(point) * camera.Far;
        using var query = PhysicsRayQueryParameters3D.Create(start, end, UnitView.PickingLayer);
        var hit = camera.GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (hit.TryGetValue("collider", out var collider) && collider.AsGodotObject() is UnitView unit)
            yield return unit.Id;
    }

    private IEnumerable<EntityId> BoxHits(Rect2 box)
    {
        var viewport = GetViewport();
        if (viewport.GetCamera3D() is not { } camera)
            yield break;

        var bounds = viewport.GetVisibleRect().Intersection(box);
        foreach (var unit in _units)
        {
            var center = unit.SelectionCenter;
            if (!camera.IsPositionBehind(center) && bounds.HasPoint(camera.UnprojectPosition(center)))
                yield return unit.Id;
        }
    }

    private void CancelGesture()
    {
        _start = null;
        _dragging = false;
        QueueRedraw();
    }
}
