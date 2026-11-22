using Godot;
using IronDominion.Application;
using IronDominion.Domain;

namespace IronDominion.Game;

internal sealed class SelectionPanel
{
    private readonly UnitSelection _selection;
    private readonly UnitSelectionController _orders;
    private readonly Button _attackMove;
    private readonly IReadOnlyDictionary<UnitKind, int> _maximumHealth;
    private readonly PanelContainer _panel = HudStyle.Panel("SelectionPanel");
    private readonly Label _title = HudStyle.Label("SelectionTitle", 20, HudStyle.Text);
    private readonly Label _composition = HudStyle.Label("Composition", 14, HudStyle.Muted);
    private readonly Label _health = HudStyle.Label("SelectionHealth", 16, HudStyle.Green);
    private readonly ProgressBar _healthBar = HudStyle.Progress("SelectionHealthBar", HudStyle.Green);
    private readonly Label _cover = HudStyle.Label("SelectionCover", 14, HudStyle.Amber);

    public SelectionPanel(Node canvas, UnitSelection selection, GameConfiguration configuration,
        UnitSelectionController orders)
    {
        _selection = selection;
        _orders = orders;
        _maximumHealth = configuration.Units.ToDictionary(unit => unit.Kind, unit => unit.MaxHp);
        _panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomRight);
        _panel.OffsetLeft = -292;
        _panel.OffsetRight = -24;
        _panel.OffsetTop = -198;
        _panel.OffsetBottom = -24;
        _panel.Visible = false;
        canvas.AddChild(_panel);

        var rows = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        rows.AddThemeConstantOverride("separation", 7);
        _panel.AddChild(rows);
        rows.AddChild(_title);
        rows.AddChild(_composition);
        rows.AddChild(_health);
        rows.AddChild(_healthBar);
        rows.AddChild(_cover);

        var orderButtons = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        orderButtons.AddThemeConstantOverride("separation", 6);
        rows.AddChild(orderButtons);
        _attackMove = AddOrderButton(orderButtons, "AttackMoveButton", "Attack move", "Shift+A, then click a destination",
            orders.ArmAttackMove);
        AddOrderButton(orderButtons, "HoldPositionButton", "Hold position", "H", orders.RequestHoldPosition);
    }

    public void Update(IReadOnlyWorld world, IReadOnlyList<UnitView> views)
    {
        var selected = _selection.Selected;
        if (selected.IsEmpty)
        {
            _panel.Visible = false;
            return;
        }

        var infantry = 0;
        var tanks = 0;
        var protectedUnits = 0;
        var suppressedUnits = 0;
        long hp = 0;
        long maxHp = 0;
        foreach (var view in views)
        {
            if (!selected.Contains(view.Id) || !world.TryGetHitPoints(view.Id, out var current) || current <= 0)
                continue;

            if (view.Kind == UnitKind.InfantrySquad)
                infantry++;
            else
                tanks++;
            if (world.HasCover(view.Id))
                protectedUnits++;
            if (world.IsSuppressed(view.Id))
                suppressedUnits++;
            hp += current;
            maxHp += _maximumHealth[view.Kind];
        }

        var count = infantry + tanks;
        _panel.Visible = count > 0;
        if (count == 0)
            return;

        var attackMoveLabel = _orders.AttackMoveArmed ? "Click ground" : "Attack move";
        if (_attackMove.Text != attackMoveLabel)
            _attackMove.Text = attackMoveLabel;
        HudStyle.SetText(_title, count == 1 ? infantry == 1 ? "Infantry squad" : "Tank" : $"{count} units selected");
        _composition.Visible = count > 1;
        if (count > 1)
            HudStyle.SetText(_composition, $"{infantry} infantry · {tanks} {(tanks == 1 ? "tank" : "tanks")}");
        HudStyle.SetText(_health, $"{hp} / {maxHp} HP");
        var ratio = (double)hp / maxHp;
        _healthBar.Value = ratio;
        var healthColor = HudStyle.HealthColor(ratio);
        HudStyle.SetTextColor(_health, healthColor);
        HudStyle.SetProgressColor(_healthBar, healthColor);
        _cover.Visible = protectedUnits > 0 || suppressedUnits > 0;
        if (_cover.Visible)
        {
            var coverText = count == 1 ? "In cover" : $"{protectedUnits} in cover";
            var suppressionText = count == 1 ? "Suppressed" : $"{suppressedUnits} suppressed";
            var status = protectedUnits == 0 ? suppressionText
                : suppressedUnits == 0 ? coverText
                : $"{coverText} · {suppressionText}";
            HudStyle.SetText(_cover, status);
            HudStyle.SetTextColor(_cover, suppressedUnits > 0 ? HudStyle.Red : HudStyle.Amber);
        }
    }

    private static Button AddOrderButton(Node row, string name, string label, string hint, Action pressed)
    {
        var button = new Button
        {
            Name = name,
            Text = label,
            TooltipText = hint,
            FocusMode = Control.FocusModeEnum.None,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 28)
        };
        HudStyle.StyleButton(button);
        button.AddThemeFontSizeOverride("font_size", 13);
        button.Pressed += pressed;
        row.AddChild(button);
        return button;
    }
}
