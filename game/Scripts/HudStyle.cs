using Godot;

namespace IronDominion.Game;

internal static class HudStyle
{
    public static readonly Color Text = new("edf5fa");
    public static readonly Color Muted = new("9eafbf");
    public static readonly Color Blue = new("72cdf6");
    public static readonly Color Green = new("9cdeab");
    public static readonly Color Amber = new("f0cb7d");
    public static readonly Color Red = new("f18c80");

    public static PanelContainer Panel(string name, Control.MouseFilterEnum mouseFilter = Control.MouseFilterEnum.Ignore)
    {
        var panel = new PanelContainer { Name = name, MouseFilter = mouseFilter };
        var style = Box(new Color("111d2bf2"), new Color("3d5369"), 8);
        style.ContentMarginLeft = style.ContentMarginRight = 14;
        style.ContentMarginTop = style.ContentMarginBottom = 12;
        panel.AddThemeStyleboxOverride("panel", style);
        return panel;
    }

    public static Label Label(string name, int size, Color color)
    {
        var label = new Label { Name = name, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    public static ProgressBar Progress(string name, Color color, double maximum = 1)
    {
        var progress = new ProgressBar
        {
            Name = name,
            MaxValue = maximum,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(0, 4),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        progress.AddThemeStyleboxOverride("background", Box(new Color("26394b"), Colors.Transparent, 2));
        progress.AddThemeStyleboxOverride("fill", Box(color, Colors.Transparent, 2));
        return progress;
    }

    public static void StyleTrainingButton(Button button)
    {
        button.AddThemeStyleboxOverride("normal", Box(new Color("1e3a53"), new Color("466780"), 5));
        button.AddThemeStyleboxOverride("hover", Box(new Color("2a4e6c"), new Color("6595b1"), 5));
        button.AddThemeStyleboxOverride("pressed", Box(new Color("346582"), new Color("7db6d3"), 5));
        button.AddThemeStyleboxOverride("disabled", Box(new Color("202d3b"), new Color("324250"), 5));
        button.AddThemeColorOverride("font_color", Text);
        button.AddThemeColorOverride("font_disabled_color", Muted);
    }

    public static void SetText(Label label, string text)
    {
        if (label.Text != text)
            label.Text = text;
    }

    public static void SetTextColor(Label label, Color color)
    {
        if (label.GetThemeColor("font_color") != color)
            label.AddThemeColorOverride("font_color", color);
    }

    public static void SetProgressColor(ProgressBar progress, Color color)
    {
        var fill = (StyleBoxFlat)progress.GetThemeStylebox("fill");
        if (fill.BgColor != color)
            fill.BgColor = color;
    }

    public static Color HealthColor(double ratio) =>
        ratio <= 0.25 ? Red : ratio <= 0.6 ? Amber : Green;

    private static StyleBoxFlat Box(Color fill, Color border, int radius)
    {
        var box = new StyleBoxFlat { BgColor = fill, BorderColor = border };
        box.SetCornerRadiusAll(radius);
        if (border.A > 0)
            box.SetBorderWidthAll(1);
        return box;
    }
}
