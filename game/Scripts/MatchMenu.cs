using Godot;
using IronDominion.Application;
using IronDominion.Domain;

namespace IronDominion.Game;

public partial class MatchMenu : Control
{
    private GameSession _session = null!;
    private PlayerId _localPlayer;
    private Control _overlay = null!;
    private Label _title = null!;
    private Label _detail = null!;
    private Button _pause = null!;
    private Button _resume = null!;
    private Button _restart = null!;
    private Button _save = null!;
    private Button _load = null!;
    private Action? _saveMatch;
    private Func<GameSession>? _loadMatch;
    private Func<bool>? _hasSave;
    private bool _restarting;
    internal Func<bool>? CloseConstruction { get; set; }

    public void Initialize(GameSession session, PlayerId localPlayer)
    {
        _session = session;
        _localPlayer = localPlayer;
    }

    public void EnablePersistence(Action save, Func<GameSession> load, Func<bool> hasSave)
    {
        _saveMatch = save;
        _loadMatch = load;
        _hasSave = hasSave;
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        _pause = AddButton(this, "PauseButton", "Pause", TogglePause);
        _pause.SetAnchorsAndOffsetsPreset(LayoutPreset.TopRight);
        _pause.OffsetLeft = -120f;
        _pause.OffsetRight = -24f;
        _pause.OffsetTop = 24f;
        _pause.OffsetBottom = 60f;

        _overlay = new Control { Name = "MatchOverlay", MouseFilter = MouseFilterEnum.Stop };
        AddChild(_overlay);
        _overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var shade = new ColorRect { Color = new Color("07111bc4"), MouseFilter = MouseFilterEnum.Ignore };
        _overlay.AddChild(shade);
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        _overlay.AddChild(center);
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var panel = HudStyle.Panel("MatchPanel", MouseFilterEnum.Stop);
        panel.CustomMinimumSize = new Vector2(336, 0);
        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_bottom", 8);
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 10);
        center.AddChild(panel);
        panel.AddChild(margin);
        margin.AddChild(content);

        _title = HudStyle.Label("MatchTitle", 32, HudStyle.Text);
        _detail = HudStyle.Label("MatchDetail", 15, HudStyle.Muted);
        _detail.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        content.AddChild(_title);
        content.AddChild(_detail);
        _resume = AddButton(content, "ResumeButton", "Resume", TogglePause);
        _save = AddButton(content, "SaveButton", "Save match", SavePausedMatch);
        _load = AddButton(content, "LoadButton", "Load saved match", LoadSavedMatch);
        _restart = AddButton(content, "RestartButton", "Restart match", RequestRestart);
        AddButton(content, "QuitButton", "Quit game", () => GetTree().Quit());
        _overlay.Hide();
    }

    public override void _Input(InputEvent inputEvent)
    {
        if (inputEvent is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
            return;

        GetViewport().SetInputAsHandled();
        if (CloseConstruction?.Invoke() != true)
            TogglePause();
    }

    public void ShowOutcome(MatchResult result)
    {
        var localVictory = result == MatchResult.PlayerOneWon && _localPlayer.Value == 1
            || result == MatchResult.PlayerTwoWon && _localPlayer.Value == 2;
        if (result == MatchResult.Draw)
            ShowOverlay("Draw", "Both headquarters fell on the same tick.", resumable: false, HudStyle.Amber);
        else if (localVictory)
            ShowOverlay("Victory", "The enemy headquarters was destroyed.", resumable: false, HudStyle.Green);
        else
            ShowOverlay("Defeat", "Your headquarters was destroyed.", resumable: false, HudStyle.Red);
    }

    private void TogglePause()
    {
        if (_session.World.Result != MatchResult.Running || _restarting)
            return;

        var paused = !_session.IsPaused;
        _session.SetPaused(paused);
        if (paused)
        {
            ShowOverlay("Paused", "", resumable: true, HudStyle.Text);
            return;
        }

        _resume.ReleaseFocus();
        _overlay.Hide();
        _pause.Show();
        GetTree().Paused = false;
    }

    private void ShowOverlay(string title, string detail, bool resumable, Color color)
    {
        _title.Text = title;
        HudStyle.SetTextColor(_title, color);
        _detail.Text = detail;
        HudStyle.SetTextColor(_detail, HudStyle.Muted);
        _detail.Visible = detail.Length > 0;
        _resume.Visible = resumable;
        _save.Visible = resumable && _saveMatch is not null;
        _load.Visible = _loadMatch is not null;
        _load.Disabled = _hasSave?.Invoke() != true;
        _pause.Hide();
        _overlay.Show();
        (resumable ? _resume : _restart).GrabFocus();
        GetTree().Paused = true;
    }

    private void SavePausedMatch()
    {
        if (!_session.IsPaused || _saveMatch is null)
            return;

        try
        {
            _saveMatch();
            _load.Disabled = false;
            ShowStatus("Match saved.", HudStyle.Green);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            ShowStatus($"Could not save the match. {exception.Message}", HudStyle.Red);
        }
    }

    private void LoadSavedMatch()
    {
        if (_loadMatch is null || _restarting)
            return;

        try
        {
            var loaded = _loadMatch();
            _session = loaded;
            if (loaded.World.Result == MatchResult.Running)
            {
                loaded.SetPaused(true);
                ShowOverlay("Paused", "Saved match loaded.", resumable: true, HudStyle.Text);
            }
            else
            {
                ShowOutcome(loaded.World.Result);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            ShowStatus($"Could not load the match. {exception.Message}", HudStyle.Red);
        }
    }

    private void ShowStatus(string message, Color color)
    {
        _detail.Text = message;
        HudStyle.SetTextColor(_detail, color);
        _detail.Show();
    }

    private void RequestRestart()
    {
        if (_restarting)
            return;

        _restarting = true;
        Callable.From(RestartScene).CallDeferred();
    }

    private void RestartScene()
    {
        var tree = GetTree();
        var result = tree.ReloadCurrentScene();
        if (result == Error.Ok)
        {
            tree.Paused = false;
        }
        else
        {
            _restarting = false;
            _detail.Text = "Could not restart the match.";
            _detail.Show();
            GD.PushError($"Could not reload the battlefield ({result}).");
        }
    }

    private static Button AddButton(Node parent, string name, string text, Action command)
    {
        var button = new Button { Name = name, Text = text, CustomMinimumSize = new Vector2(0, 36) };
        HudStyle.StyleButton(button);
        button.Pressed += command;
        parent.AddChild(button);
        return button;
    }
}
