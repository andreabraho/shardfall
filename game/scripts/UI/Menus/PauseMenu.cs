using Godot;
using Kiln.Core.Foundation;
using Kiln.Game.Input;
using Kiln.Game.Saving;

namespace Kiln.Game.UI.Menus;

/// <summary>
/// Esc in a map (UIX-02): the game stops, and saving, loading, settings and leaving are here.
/// </summary>
/// <remarks>
/// Esc already closes whatever panel is open, and that keeps priority: with the bag open, Esc
/// closes the bag and nothing else. Only with nothing open does it pause. Two meanings on one
/// key is fine as long as the first one always wins; what is not fine is closing the bag and
/// opening the pause menu on the same press.
/// </remarks>
public partial class PauseMenu : CanvasLayer
{
    private PanelContainer _content = null!;
    private bool _counted;

    public override void _Ready()
    {
        Layer = 40;
        ProcessMode = ProcessModeEnum.Always;
        Visible = false;

        Build();
        SettingsView.LanguageChanged += Relabel;
    }

    /// <summary>A new language: rebuild in it, and stay on the settings page it was chosen from.</summary>
    private void Relabel() => CallDeferred(nameof(Rebuild));

    /// <remarks>Deferred: the language picker that asked for this is one of the nodes it frees.</remarks>
    private void Rebuild()
    {
        foreach (var child in GetChildren())
        {
            RemoveChild(child);
            child.QueueFree();
        }

        Build();
        Page(new SettingsView(inGame: true));
    }

    private void Build()
    {
        var shade = new ColorRect
        {
            Color = new Color(0, 0, 0, 0.6f),
            MouseFilter = Control.MouseFilterEnum.Stop,
        };

        shade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(shade);

        var centre = new CenterContainer();
        centre.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(centre);

        var frame = new HBoxContainer();
        frame.AddThemeConstantOverride("separation", 16);
        centre.AddChild(frame);

        var menu = new PanelContainer();
        menu.AddThemeStyleboxOverride("panel", MenuStyle.Panel());
        frame.AddChild(menu);

        var buttons = new VBoxContainer();
        buttons.AddThemeConstantOverride("separation", 8);
        menu.AddChild(buttons);

        buttons.AddChild(MenuStyle.Label(L10n.T("Paused"), 24, MenuStyle.Gold));
        buttons.AddChild(new Control { CustomMinimumSize = new Vector2(0, 8) });
        buttons.AddChild(MenuStyle.Big(L10n.T("Resume"), Close));
        buttons.AddChild(MenuStyle.Big(L10n.T("Save game"), () => Page(new SaveList(SaveList.Purpose.Save))));
        buttons.AddChild(MenuStyle.Big(L10n.T("Load game"), () => Page(new SaveList(SaveList.Purpose.Load))));
        buttons.AddChild(MenuStyle.Big(L10n.T("Settings"), () => Page(new SettingsView(inGame: true))));
        buttons.AddChild(new Control { CustomMinimumSize = new Vector2(0, 8) });
        buttons.AddChild(MenuStyle.Big(L10n.T("Quit to menu"), () => SaveService.Instance?.QuitToMenu()));
        buttons.AddChild(MenuStyle.Big(L10n.T("Quit to desktop"), () => SaveService.Instance?.QuitGame()));
        buttons.AddChild(MenuStyle.Label(L10n.T("Leaving autosaves first."), 12, MenuStyle.Dim));

        _content = new PanelContainer { Visible = false };
        _content.AddThemeStyleboxOverride("panel", MenuStyle.Panel());
        frame.AddChild(_content);
    }

    public override void _ExitTree()
    {
        SettingsView.LanguageChanged -= Relabel;
        UiState.SetOpen(ref _counted, false);

        // A pause left on by a scene change would freeze the next map.
        if (Visible && IsInsideTree()) GetTree().Paused = false;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!@event.IsActionPressed(GameActions.Cancel)) return;

        if (Visible) Close();
        else if (!UiState.ModalOpen) Open();
        else return;

        GetViewport().SetInputAsHandled();
    }

    public void Open()
    {
        Visible = true;
        _content.Visible = false;
        UiState.SetOpen(ref _counted, true);
        GetTree().Paused = true;
    }

    public void Close()
    {
        Visible = false;
        UiState.SetOpen(ref _counted, false);
        GetTree().Paused = false;
    }

    private void Page(Control page)
    {
        foreach (var child in _content.GetChildren())
        {
            _content.RemoveChild(child);
            child.QueueFree();
        }

        _content.AddChild(page);
        _content.Visible = true;
    }
}
