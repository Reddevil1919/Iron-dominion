using Godot;
using IronDominion.Application;
using IronDominion.Domain;

namespace IronDominion.Game;

internal partial class ConstructionMenu : Node
{
    private readonly PanelContainer _panel = HudStyle.Panel("ConstructionPanel", Control.MouseFilterEnum.Stop);
    private readonly Label _status = HudStyle.Label("PlacementStatus", 14, HudStyle.Text);
    private readonly List<Entry> _entries = [];
    private Func<GameSession> _session = null!;
    private Action _beginPlacement = null!;
    private PlayerId _player;
    private float _ground;
    private BuildingDefinition? _selected;
    private Node3D _preview = null!;
    private StandardMaterial3D _fill = null!;
    private StandardMaterial3D _edge = null!;
    private int _turns;
    private long? _pending;
    private Button _toggle = null!;

    public void Initialize(Node canvas, Node3D battlefield, GameConfiguration configuration, PlayerId player,
        float ground, Func<GameSession> session, Action beginPlacement)
    {
        _session = session;
        _beginPlacement = beginPlacement;
        _player = player;
        _ground = ground;

        _toggle = new Button { Name = "ConstructionButton", Text = "Construction  [B]" };
        HudStyle.StyleButton(_toggle);
        _toggle.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight);
        _toggle.OffsetLeft = -280;
        _toggle.OffsetRight = -132;
        _toggle.OffsetTop = 24;
        _toggle.OffsetBottom = 58;
        _toggle.Pressed += Toggle;
        canvas.AddChild(_toggle);

        _panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight);
        _panel.OffsetLeft = -307;
        _panel.OffsetRight = -24;
        _panel.OffsetTop = 70;
        canvas.AddChild(_panel);
        var rows = new VBoxContainer();
        rows.AddThemeConstantOverride("separation", 7);
        _panel.AddChild(rows);
        var title = HudStyle.Label("ConstructionTitle", 20, HudStyle.Text);
        title.Text = "Construction";
        rows.AddChild(title);
        foreach (var definition in configuration.Buildings)
        {
            if (definition.Construction is not { } construction)
                continue;

            var name = DisplayName(definition.Kind);
            var button = new Button
            {
                Name = $"Place{definition.Kind}",
                Text = $"{name}   ·   {construction.Cost} cr   ·   {(double)construction.BuildTicks / configuration.TickRate:0.#} s",
                CustomMinimumSize = new Vector2(252, 30)
            };
            HudStyle.StyleButton(button);
            button.Pressed += () => Select(definition);
            rows.AddChild(button);
            var reason = HudStyle.Label($"Availability{definition.Kind}", 12, HudStyle.Muted);
            rows.AddChild(reason);
            _entries.Add(new Entry(definition, button, reason));
        }
        _status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        rows.AddChild(_status);
        _panel.Hide();

        _fill = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha
        };
        _edge = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };
        _preview = new Node3D { Name = "PlacementPreview", Visible = false };
        battlefield.AddChild(_preview);
    }

    private void Toggle()
    {
        if (Cancel())
            return;
        _panel.Show();
        _status.Text = "Choose a building to place.";
        UpdateAvailability();
    }

    internal bool Cancel()
    {
        if (!_panel.Visible)
            return false;
        _selected = null;
        _pending = null;
        _preview.Hide();
        _panel.Hide();
        return true;
    }

    public override void _Input(InputEvent input)
    {
        if (input is InputEventKey { Pressed: true, Echo: false } key && key.Keycode == Key.B)
        {
            Toggle();
            GetViewport().SetInputAsHandled();
        }
        else if (_selected is not null && input is InputEventKey { Pressed: true, Echo: false, Keycode: Key.R })
        {
            _turns = (_turns + 1) % 4;
            _preview.Rotation = new Vector3(0, _turns * Mathf.Pi / 2, 0);
            RefreshPreview(GetViewport().GetMousePosition());
            GetViewport().SetInputAsHandled();
        }
        else if (_selected is not null && input is InputEventMouseButton
            { Pressed: true, ButtonIndex: MouseButton.Left } click && GetViewport().GuiGetHoveredControl() is null)
        {
            var placement = RefreshPreview(click.Position);
            if (_pending is null && placement is { } order)
                _pending = _session().Commands.ScheduleBuild(_player, order.Headquarters, order.Kind,
                    order.Position, order.QuarterTurns).Sequence;
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Process(double delta)
    {
        if (_selected is not null && _pending is null)
            RefreshPreview(GetViewport().GetMousePosition());
    }

    internal void Update(IReadOnlyList<IGameEvent> events)
    {
        if (_panel.Visible)
            UpdateAvailability();
        foreach (var item in events)
        {
            if (item is not CommandProcessed { Command: BuildCommand order } result
                || order.Player != _player || order.Sequence != _pending)
                continue;
            _pending = null;
            if (result.Result.Accepted)
                Cancel();
            else
                _status.Text = Reason(result.Result.RejectionCode);
        }
    }

    private EntityId? Headquarters() => _session().ConstructionHeadquarters(_player);

    private void UpdateAvailability()
    {
        var headquarters = Headquarters();
        foreach (var entry in _entries)
        {
            var result = _session().BuildAvailability(_player, headquarters, entry.Definition.Kind);
            entry.Button.Disabled = !result.Accepted;
            HudStyle.SetText(entry.Reason, result.Accepted ? "Available" : Reason(result.RejectionCode));
        }
    }

    private void Select(BuildingDefinition definition)
    {
        _beginPlacement();
        _selected = definition;
        _turns = 0;
        _preview.Rotation = Vector3.Zero;
        foreach (var child in _preview.GetChildren())
        {
            _preview.RemoveChild(child);
            child.QueueFree();
        }
        var plan = definition.Construction!;
        var x = plan.Width / 2;
        var z = plan.Depth / 2;
        ConstructionModels.Box(_preview, new Vector3(0, 0.02f, 0), new Vector3(plan.Width, 0.04f, plan.Depth), _fill);
        foreach (var side in new[] { -1, 1 })
        {
            ConstructionModels.Box(_preview, new Vector3(0, 0.08f, side * z), new Vector3(plan.Width, 0.08f, 0.18f), _edge);
            ConstructionModels.Box(_preview, new Vector3(side * x, 0.08f, 0), new Vector3(0.18f, 0.08f, plan.Depth), _edge);
        }
        ConstructionModels.AddSuperstructure(_preview, definition, _fill);
        RefreshPreview(GetViewport().GetMousePosition());
    }

    private BuildCommand? RefreshPreview(Vector2 pointer)
    {
        var view = GetViewport();
        var camera = view.GetCamera3D();
        if (camera is null || !view.GetVisibleRect().HasPoint(pointer) || view.GuiGetHoveredControl() is not null
            || new Plane(Vector3.Up, _ground).IntersectsRay(camera.ProjectRayOrigin(pointer),
                camera.ProjectRayNormal(pointer)) is not { } position)
        {
            _preview.Hide();
            HudStyle.SetText(_status, "Point to the ground  ·  R rotate  ·  Esc cancel");
            return null;
        }

        var destination = new SimVector(Mathf.Round(position.X), Mathf.Round(position.Z));
        _preview.Position = new Vector3(destination.X, _ground + 0.11f, destination.Z);
        _preview.Show();
        var order = new BuildCommand(_player, _session().World.Tick + 1, 0, Headquarters() ?? default,
            _selected!.Kind, destination, _turns);
        var result = _session().CheckBuild(order);
        var color = new Color(result.Accepted ? "63e6a7" : "ff716b");
        _edge.AlbedoColor = color;
        _fill.AlbedoColor = new Color(color, 0.35f);
        HudStyle.SetText(_status, result.Accepted ? "Placement valid  ·  Click to build\nR rotate  ·  Esc cancel"
            : $"Placement refused  ·  {Reason(result.RejectionCode)}\nR rotate  ·  Esc cancel");
        return result.Accepted ? order : null;
    }

    private static string Reason(string? reason) => reason switch
    {
        "insufficient_credits" => "Insufficient credits",
        "outside_construction_zone" => "Outside your construction zone",
        "invalid_building_location" => "Footprint or exit outside the map",
        "building_location_occupied" => "Space or access is occupied",
        "reserve_headquarters_limit" => "Reserve HQ already built or planned",
        "invalid_actor" or "actor_destroyed" => "Requires an operational HQ",
        _ => "Construction unavailable"
    };

    private static string DisplayName(BuildingKind kind) => kind switch
    {
        BuildingKind.Headquarters => "Headquarters",
        BuildingKind.Barracks => "Barracks",
        BuildingKind.TankFactory => "Tank factory",
        BuildingKind.Airport => "Airport",
        _ => "Research center"
    };

    private sealed record Entry(BuildingDefinition Definition, Button Button, Label Reason);
}
