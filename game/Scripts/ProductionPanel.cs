using Godot;
using IronDominion.Domain;

namespace IronDominion.Game;

internal sealed class ProductionPanel
{
    private readonly PlayerId _player;
    private readonly int _tickRate;
    private readonly Label _credits = new() { Name = "Credits" };
    private readonly List<Row> _rows = new();

    public ProductionPanel(Node canvas, PlayerId player, GameConfiguration configuration,
        IReadOnlyList<Building> producers, Action<EntityId, UnitKind> train)
    {
        _player = player;
        _tickRate = configuration.TickRate;

        var panel = new PanelContainer
        {
            Name = "ProductionPanel",
            AnchorTop = 1f,
            AnchorBottom = 1f,
            OffsetLeft = 16f,
            OffsetTop = -146f,
            OffsetBottom = -16f
        };
        var style = new StyleBoxFlat { BgColor = new Color("192635ee"), BorderColor = new Color("526375") };
        style.SetBorderWidthAll(1);
        style.SetCornerRadiusAll(6);
        panel.AddThemeStyleboxOverride("panel", style);
        canvas.AddChild(panel);

        var margin = new MarginContainer();
        foreach (var side in new[] { "left", "right", "top", "bottom" })
            margin.AddThemeConstantOverride($"margin_{side}", 10);
        panel.AddChild(margin);

        var content = new VBoxContainer { Name = "QueueRows" };
        margin.AddChild(content);
        _credits.AddThemeFontSizeOverride("font_size", 20);
        content.AddChild(_credits);

        foreach (var producer in producers)
        {
            var unit = configuration.Units.Single(definition => definition.Kind == producer.TrainedUnitKind);
            var row = new HBoxContainer();
            content.AddChild(row);

            var infantry = unit.Kind == UnitKind.InfantrySquad;
            var button = new Button
            {
                Name = infantry ? "TrainInfantry" : "TrainTank",
                Text = infantry ? $"Infantry · {unit.Cost}" : $"Tank · {unit.Cost}",
                CustomMinimumSize = new Vector2(150f, 30f)
            };
            button.Pressed += () => train(producer.Id, unit.Kind);
            row.AddChild(button);

            var status = new Label
            {
                Name = infantry ? "InfantryQueue" : "TankQueue",
                CustomMinimumSize = new Vector2(150f, 0f)
            };
            row.AddChild(status);
            _rows.Add(new Row(producer.Id, unit.Cost, button, status));
        }
    }

    public void Update(IReadOnlyWorld world)
    {
        var credits = world.Credits(_player);
        SetLabel(_credits, $"{credits} credits");

        foreach (var row in _rows)
        {
            var alive = world.TryGetHitPoints(row.Producer, out var hp) && hp > 0;
            row.Button.Disabled = !alive || credits < row.Cost;
            var queue = world.TrainingQueue(row.Producer);
            var status = !alive ? "Destroyed" : QueueStatus(queue);
            SetLabel(row.Status, status);
        }
    }

    private string QueueStatus(IReadOnlyList<TrainingOrder> queue)
    {
        if (queue.Count == 0)
            return "Idle";
        if (queue[0].RemainingTicks == 0)
            return $"{queue.Count} · Exit blocked";

        var seconds = (int)Math.Ceiling((double)queue[0].RemainingTicks / _tickRate);
        return $"{queue.Count} queued · {seconds}s";
    }

    private static void SetLabel(Label label, string text)
    {
        if (label.Text != text)
            label.Text = text;
    }

    private sealed record Row(EntityId Producer, int Cost, Button Button, Label Status);
}
