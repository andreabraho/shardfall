using Godot;
using Kiln.Game.Input;

namespace Kiln.Game;

/// <summary>
/// Window mode handling: fullscreen by default, toggled with F11 or Alt+Enter.
/// <para>
/// The game ships fullscreen because a window sized exactly to the screen sits under the
/// taskbar — it looks full screen while leaving the bar visible along the bottom, which is
/// the worst of both. Toggling is essential anyway: a player debugging, streaming or using
/// two monitors needs a real window.
/// </para>
/// </summary>
public partial class DisplaySettings : Node
{
    public override void _Ready() => ProcessMode = ProcessModeEnum.Always;

    public override void _UnhandledInput(InputEvent @event)
    {
        var altEnter = @event is InputEventKey { Pressed: true, Echo: false, AltPressed: true, Keycode: Key.Enter };

        if (!altEnter && !@event.IsActionPressed(GameActions.ToggleFullscreen)) return;

        ToggleFullscreen();
        GetViewport().SetInputAsHandled();
    }

    /// <summary>The key toggle. Remembered, like the same choice made in the settings.</summary>
    public static void ToggleFullscreen()
    {
        Settings.GameSettings.Fullscreen = !IsFullscreen;
        SetFullscreen(Settings.GameSettings.Fullscreen);
        Settings.GameSettings.Save();
    }

    public static bool IsFullscreen => DisplayServer.WindowGetMode()
        is DisplayServer.WindowMode.Fullscreen or DisplayServer.WindowMode.ExclusiveFullscreen;

    public static void SetFullscreen(bool fullscreen)
    {
        if (fullscreen == IsFullscreen) return;

        DisplayServer.WindowSetMode(fullscreen ? DisplayServer.WindowMode.Fullscreen : DisplayServer.WindowMode.Windowed);

        if (!fullscreen) CentreWindow();
    }

    /// <summary>Puts the restored window in the middle of the screen it is on.</summary>
    private static void CentreWindow()
    {
        var screen = DisplayServer.WindowGetCurrentScreen();
        var usable = DisplayServer.ScreenGetUsableRect(screen);
        var size = DisplayServer.WindowGetSize();

        DisplayServer.WindowSetPosition(usable.Position + ((usable.Size - size) / 2));
    }
}
