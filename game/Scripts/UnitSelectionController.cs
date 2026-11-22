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
    private StaticBody3D _ground = null!;
    private Action<IReadOnlyList<EntityId>, SimVector, bool> _move = null!;
    private Action<IReadOnlyList<EntityId>> _hold = null!;
    private Func<GodotObject, EntityId?> _hostile = null!;
    private Action<IReadOnlyList<EntityId>, EntityId> _attack = null!;
    private Action<string> _reject = null!;
    private Vector2? _start;
    private Vector2 _pointer;
    private bool _dragging;

    public bool AttackMoveArmed { get; private set; }

    public void Initialize(UnitSelection selection, IReadOnlyList<UnitView> units,
        StaticBody3D ground, Action<IReadOnlyList<EntityId>, SimVector, bool> move,
        Action<IReadOnlyList<EntityId>> hold,
        Func<GodotObject, EntityId?> hostile, Action<IReadOnlyList<EntityId>, EntityId> attack, Action<string> reject)
    {
        _selection = selection;
        _units = units;
        _ground = ground;
        _move = move;
        _hold = hold;
        _hostile = hostile;
        _attack = attack;
        _reject = reject;
    }

    public override void _Ready() => GetWindow().FocusExited += CancelInput;

    public override void _ExitTree() => GetWindow().FocusExited -= CancelInput;

    public override void _Notification(int what)
    {
        if (what == NotificationPaused)
            CancelInput();
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        if (_selection is not { } selection)
            return;

        if (inputEvent is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } button)
        {
            var point = button.Position;
            if (AttackMoveArmed)
            {
                AttackMoveArmed = false;
                _pending.Enqueue(() => OrderSelected(selection, point, attackMove: true));
            }
            else
            {
                _start = _pointer = point;
            }
            GetViewport().SetInputAsHandled();
        }
        else if (inputEvent is InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true } move)
        {
            AttackMoveArmed = false;
            var point = move.Position;
            _pending.Enqueue(() => OrderSelected(selection, point));
            GetViewport().SetInputAsHandled();
        }
        else if (inputEvent.IsActionPressed("order_attack_move", exactMatch: true))
        {
            ArmAttackMove();
            GetViewport().SetInputAsHandled();
        }
        else if (inputEvent.IsActionPressed("order_hold_position", exactMatch: true))
        {
            RequestHoldPosition();
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
            {
                AttackMoveArmed = false;
                _pending.Enqueue(() => selection.RecallGroup(group));
            }

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

        ShowSelection(selection);
    }

    public void ArmAttackMove()
    {
        if (_selection is not { Selected.IsEmpty: false })
            return;

        CancelGesture();
        AttackMoveArmed = true;
    }

    public void RequestHoldPosition()
    {
        if (_selection is not { } selection)
            return;

        CancelInput();
        _pending.Enqueue(() =>
        {
            if (!selection.Selected.IsEmpty)
                _hold(selection.Selected);
        });
    }

    public void RefreshSelection()
    {
        if (_selection is not { } selection)
            return;

        selection.Select(selection.Selected);
        if (selection.Selected.IsEmpty)
            AttackMoveArmed = false;
        ShowSelection(selection);
    }

    private void ShowSelection(UnitSelection selection)
    {
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
        using var hit = Raycast(point, UnitView.PickingLayer);
        if (hit is not null && hit.TryGetValue("collider", out var collider) && collider.AsGodotObject() is UnitView unit)
            yield return unit.Id;
    }

    private void OrderSelected(UnitSelection selection, Vector2 point, bool attackMove = false)
    {
        if (selection.Selected.IsEmpty)
            return;

        using var hit = Raycast(point, _ground.CollisionLayer | UnitView.PickingLayer);
        if (hit is null || !hit.TryGetValue("collider", out var collider) || collider.AsGodotObject() is not { } actor)
        {
            _reject("outside_battlefield");
            return;
        }

        if (!attackMove && _hostile(actor) is { } targetId)
            _attack(selection.Selected, targetId);
        else if (actor == _ground)
        {
            var target = hit["position"].AsVector3();
            _move(selection.Selected, new SimVector(target.X, target.Z), attackMove);
        }
        else
        {
            _reject("blocked_ground");
        }
    }

    private Godot.Collections.Dictionary? Raycast(Vector2 point, uint mask)
    {
        var viewport = GetViewport();
        if (!viewport.GetVisibleRect().HasPoint(point) || viewport.GetCamera3D() is not { } camera)
            return null;

        var start = camera.ProjectRayOrigin(point);
        var end = start + camera.ProjectRayNormal(point) * camera.Far;
        using var query = PhysicsRayQueryParameters3D.Create(start, end, mask);
        return camera.GetWorld3D().DirectSpaceState.IntersectRay(query);
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

    private void CancelInput()
    {
        AttackMoveArmed = false;
        CancelGesture();
    }

    private void CancelGesture()
    {
        _start = null;
        _dragging = false;
        QueueRedraw();
    }
}
