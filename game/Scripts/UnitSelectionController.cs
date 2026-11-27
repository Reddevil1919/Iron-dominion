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
    private Action<IReadOnlyList<EntityId>, EntityId> _repair = null!;
    private Action<string> _reject = null!;
    private Vector2? _start;
    private Vector2 _pointer;
    private bool _dragging;
    private bool _mouseInWindow = true;
    private bool _windowFocused = true;
    private Input.CursorShape _cursor = Input.CursorShape.Arrow;

    public bool AttackMoveArmed { get; private set; }

    public bool HasAttackCursor() => _cursor == Input.CursorShape.Cross;
    public bool HasRepairCursor() => _cursor == Input.CursorShape.Help;

    public void Initialize(UnitSelection selection, IReadOnlyList<UnitView> units,
        StaticBody3D ground, Action<IReadOnlyList<EntityId>, SimVector, bool> move,
        Action<IReadOnlyList<EntityId>> hold,
        Func<GodotObject, EntityId?> hostile, Action<IReadOnlyList<EntityId>, EntityId> attack,
        Action<IReadOnlyList<EntityId>, EntityId> repair, Action<string> reject)
    {
        _selection = selection;
        _units = units;
        _ground = ground;
        _move = move;
        _hold = hold;
        _hostile = hostile;
        _attack = attack;
        _repair = repair;
        _reject = reject;
    }

    public override void _Ready()
    {
        var window = GetWindow();
        using var wrench = GD.Load<Texture2D>("res://Assets/repair_cursor.svg");
        Input.SetCustomMouseCursor(wrench, Input.CursorShape.Help, new Vector2(18, 17));
        window.FocusExited += OnFocusExited;
        window.FocusEntered += OnFocusEntered;
        window.MouseExited += OnMouseExited;
        window.MouseEntered += OnMouseEntered;
    }

    public override void _ExitTree()
    {
        var window = GetWindow();
        window.FocusExited -= OnFocusExited;
        window.FocusEntered -= OnFocusEntered;
        window.MouseExited -= OnMouseExited;
        window.MouseEntered -= OnMouseEntered;
        SetCursor(Input.CursorShape.Arrow);
        Input.SetCustomMouseCursor(null, Input.CursorShape.Help);
    }

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
        if (_selection is not { } selection)
            return;

        if (_pending.Count != 0)
        {
            while (_pending.TryDequeue(out var update))
                update();
            ShowSelection(selection);
        }

        var viewport = GetViewport();
        if (!_mouseInWindow || !_windowFocused || _start.HasValue || AttackMoveArmed
            || viewport.GuiGetHoveredControl() is not null)
        {
            SetCursor(Input.CursorShape.Arrow);
            return;
        }

        using var hit = RaycastOrderTarget(viewport.GetMousePosition());
        var actor = HitActor(hit);
        var repair = FriendlyTank(actor, selection) is not null
            && _units.Any(view => view.Kind == UnitKind.EngineerSquad && selection.Selected.Contains(view.Id));
        var attack = actor is not null && _hostile(actor).HasValue
            && (selection.Selected.IsEmpty || HasCombatSelection(selection));
        SetCursor(repair ? Input.CursorShape.Help : attack ? Input.CursorShape.Cross : Input.CursorShape.Arrow);
    }

    private void SetCursor(Input.CursorShape cursor)
    {
        if (_cursor == cursor)
            return;
        _cursor = cursor;
        Input.SetDefaultCursorShape(cursor);
    }

    private void OnFocusExited()
    {
        _windowFocused = false;
        CancelInput();
    }

    private void OnFocusEntered() => _windowFocused = true;

    private void OnMouseExited()
    {
        _mouseInWindow = false;
        SetCursor(Input.CursorShape.Arrow);
    }

    private void OnMouseEntered() => _mouseInWindow = true;

    public void ArmAttackMove()
    {
        if (_selection is not { Selected.IsEmpty: false } selection || !HasCombatSelection(selection))
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

    internal void CancelConstructionInput() => CancelInput();

    public void ResetForLoad()
    {
        _pending.Clear();
        CancelInput();
        _selection?.Reset();
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

        using var hit = RaycastOrderTarget(point);
        if (hit is null || HitActor(hit) is not { } actor)
        {
            _reject("outside_battlefield");
            return;
        }

        if (!attackMove && _hostile(actor) is { } targetId)
        {
            var combat = _units.Where(view => view.Kind.CanAttack() && selection.Selected.Contains(view.Id))
                .Select(view => view.Id).ToArray();
            if (combat.Length == 0)
                _reject("unit_cannot_attack");
            else
                _attack(combat, targetId);
        }
        else if (!attackMove && FriendlyTank(actor, selection) is { } tank)
        {
            var engineers = _units.Where(view => view.Kind == UnitKind.EngineerSquad && selection.Selected.Contains(view.Id))
                .Select(view => view.Id).ToArray();
            if (engineers.Length == 0)
                _reject("blocked_ground");
            else
                _repair(engineers, tank.Id);
        }
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

    private Godot.Collections.Dictionary? RaycastOrderTarget(Vector2 point) =>
        Raycast(point, _ground.CollisionLayer | UnitView.PickingLayer);

    private static GodotObject? HitActor(Godot.Collections.Dictionary? hit) =>
        hit is not null && hit.TryGetValue("collider", out var collider) ? collider.AsGodotObject() : null;

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
        SetCursor(Input.CursorShape.Arrow);
    }

    private bool HasCombatSelection(UnitSelection selection) =>
        _units.Any(view => view.Kind.CanAttack() && selection.Selected.Contains(view.Id));

    private static UnitView? FriendlyTank(GodotObject? actor, UnitSelection selection) =>
        actor is UnitView { Kind: UnitKind.Tank } tank && selection.CanSelect(tank.Id) ? tank : null;

    private void CancelGesture()
    {
        _start = null;
        _dragging = false;
        QueueRedraw();
    }
}
