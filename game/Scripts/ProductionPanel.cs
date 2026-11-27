using Godot;
using IronDominion.Domain;

namespace IronDominion.Game;

internal sealed class ProductionPanel
{
    private readonly PlayerId _player;
    private readonly int _tickRate;
    private readonly Label _credits = HudStyle.Label("Credits", 26, HudStyle.Text);
    private readonly Label _income = HudStyle.Label("Income", 14, HudStyle.Green);
    private readonly List<Row> _rows = new();

    public ProductionPanel(Node canvas, PlayerId player, GameConfiguration configuration,
        IReadOnlyList<Building> producers, Action<EntityId, UnitKind> train)
    {
        _player = player;
        _tickRate = configuration.TickRate;

        var panel = HudStyle.Panel("ProductionPanel", Control.MouseFilterEnum.Stop);
        panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomLeft);
        panel.OffsetLeft = 24;
        panel.OffsetRight = 358;
        panel.OffsetTop = -220;
        panel.OffsetBottom = -24;
        canvas.AddChild(panel);

        var content = new VBoxContainer { Name = "QueueRows" };
        content.AddThemeConstantOverride("separation", 8);
        panel.AddChild(content);

        var budget = new HBoxContainer();
        budget.AddThemeConstantOverride("separation", 12);
        budget.AddChild(_credits);
        budget.AddChild(_income);
        content.AddChild(budget);

        foreach (var producer in producers)
        foreach (var unit in configuration.Units.Where(definition => producer.CanTrain(definition.Kind)))
        {
            var name = unit.Kind switch
            {
                UnitKind.InfantrySquad => "Infantry",
                UnitKind.EngineerSquad => "Engineers",
                _ => "Tank"
            };
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 12);
            content.AddChild(row);

            var button = new Button
            {
                Name = $"Train{name}",
                Text = $"{name} · {unit.Cost}",
                CustomMinimumSize = new Vector2(136, 34)
            };
            HudStyle.StyleButton(button);
            button.Pressed += () => train(producer.Id, unit.Kind);
            row.AddChild(button);

            var info = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            info.AddThemeConstantOverride("separation", 3);
            row.AddChild(info);
            var status = HudStyle.Label($"{name}Queue", 14, HudStyle.Muted);
            var progress = HudStyle.Progress($"{name}Progress", HudStyle.Blue,
                Math.Max(1, unit.BuildTicks));
            info.AddChild(status);
            info.AddChild(progress);
            _rows.Add(new Row(producer.Id, unit.Kind, unit.Cost, unit.BuildTicks, button, status, progress));
        }
    }

    public void Update(IReadOnlyWorld world)
    {
        var credits = world.Credits(_player);
        HudStyle.SetText(_credits, $"{credits} credits");
        HudStyle.SetText(_income, $"+{world.IncomePerSecond(_player)} credits/s");

        foreach (var row in _rows)
        {
            var alive = world.TryGetHitPoints(row.Producer, out var hp) && hp > 0;
            row.Button.Disabled = !alive || credits < row.Cost;
            row.Button.TooltipText = !alive ? "Training building destroyed" : credits < row.Cost
                ? $"Requires {row.Cost} credits" : $"Train for {row.Cost} credits";

            var queue = world.TrainingQueue(row.Producer);
            var count = queue.Count(order => order.Kind == row.Kind);
            var training = queue.Count > 0 && queue[0].Kind == row.Kind;
            var remaining = training ? queue[0].RemainingTicks : row.BuildTicks;
            var blocked = training && remaining == 0;
            var status = !alive ? "Destroyed" : count == 0 ? "Idle" : blocked ? $"{count} · Exit blocked"
                : !training ? $"{count} queued · Waiting" : $"{count} queued · {Math.Ceiling((double)remaining / _tickRate):0}s";
            HudStyle.SetText(row.Status, status);
            var color = !alive ? HudStyle.Red : blocked ? HudStyle.Amber : !training ? HudStyle.Muted : HudStyle.Text;
            HudStyle.SetTextColor(row.Status, color);
            HudStyle.SetProgressColor(row.Progress, blocked ? HudStyle.Amber : HudStyle.Blue);
            row.Progress.Value = alive && training
                ? Math.Max(1, row.BuildTicks) - remaining
                : 0;
        }
    }

    private sealed record Row(EntityId Producer, UnitKind Kind, int Cost, int BuildTicks, Button Button, Label Status,
        ProgressBar Progress);
}
