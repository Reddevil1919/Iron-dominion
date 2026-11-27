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
    private readonly Label _engineering = HudStyle.Label("EngineeringStatus", 13, HudStyle.Muted);

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
        _healthBar.CustomMinimumSize = new Vector2(154, 3);
        _healthBar.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
        rows.AddChild(_healthBar);
        rows.AddChild(_cover);
        rows.AddChild(_engineering);

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
        var engineers = 0;
        var repairing = 0;
        var protectedUnits = 0;
        long hp = 0;
        long maxHp = 0;
        foreach (var view in views)
        {
            if (!selected.Contains(view.Id) || !world.TryGetHitPoints(view.Id, out var current) || current <= 0)
                continue;

            if (view.Kind == UnitKind.InfantrySquad)
                infantry++;
            else if (view.Kind == UnitKind.Tank)
                tanks++;
            else
            {
                engineers++;
                if (world.RepairingTank(view.Id).HasValue)
                    repairing++;
            }
            if (world.HasCover(view.Id))
                protectedUnits++;
            hp += current;
            maxHp += _maximumHealth[view.Kind];
        }

        var count = infantry + tanks + engineers;
        _panel.Visible = count > 0;
        if (count == 0)
            return;

        var attackMoveLabel = _orders.AttackMoveArmed ? "Click ground" : "Attack move";
        if (_attackMove.Text != attackMoveLabel)
            _attackMove.Text = attackMoveLabel;
        _attackMove.Disabled = infantry + tanks == 0;
        var title = engineers == 1 ? "Engineers" : infantry == 1 ? "Infantry squad" : "Tank";
        HudStyle.SetText(_title, count == 1 ? title : $"{count} units selected");
        _composition.Visible = count > 1;
        if (count > 1)
        {
            var composition = $"{infantry} infantry · {tanks} {(tanks == 1 ? "tank" : "tanks")}";
            if (engineers > 0)
                composition += $" · {engineers} eng.";
            HudStyle.SetText(_composition, composition);
        }
        HudStyle.SetText(_health, $"{hp} / {maxHp} HP");
        var ratio = (double)hp / maxHp;
        _healthBar.Value = ratio;
        var healthColor = HudStyle.HealthColor(ratio);
        HudStyle.SetTextColor(_health, healthColor);
        HudStyle.SetProgressColor(_healthBar, healthColor);
        _cover.Visible = protectedUnits > 0;
        if (_cover.Visible)
            HudStyle.SetText(_cover, count == 1 ? "In cover" : $"{protectedUnits} in cover");
        _engineering.Visible = engineers > 0 && (count == 1 || repairing > 0);
        if (_engineering.Visible)
            HudStyle.SetText(_engineering, repairing > 0 ? "Repairing tank" : "Right-click a friendly tank to repair");
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
