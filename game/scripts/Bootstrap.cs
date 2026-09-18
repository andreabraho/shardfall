using Godot;
using Sohan.Game.Debug;

namespace Sohan.Game;

/// <summary>
/// First thing the game runs. Loads content, installs the debug overlay, and fails loudly
/// rather than starting in a broken state — a game that boots with missing content produces
/// bug reports that waste hours.
/// </summary>
public partial class Bootstrap : Node
{
    public override void _Ready()
    {
        GD.Print($"Sohan — {(OS.IsDebugBuild() ? "debug" : "release")} build, Godot {Engine.GetVersionInfo()["string"]}");

        if (!GameContent.Load())
        {
            GD.PushError("Startup aborted: content failed to load. See errors above.");

            if (!OS.IsDebugBuild())
            {
                OS.Alert("Game data is missing or corrupt. Please reinstall.", "Sohan");
                GetTree().Quit(1);
            }

            return;
        }

        AddChild(new DebugOverlay { Name = "DebugOverlay" });

        DebugOverlay.Register("content", () =>
            GameContent.IsLoaded ? $"{GameContent.Database.TotalDefinitions} defs" : "not loaded");
    }
}
