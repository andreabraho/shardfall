using Godot;
using Kiln.Game.Debug;
using Kiln.Game.Input;

namespace Kiln.Game;

/// <summary>
/// First thing the game runs, registered as an autoload so it completes before any scene
/// loads. Installs input bindings, loads content, installs the debug overlay, and fails
/// loudly rather than starting in a broken state — a game that boots with missing content
/// produces bug reports that waste hours.
/// </summary>
public partial class Bootstrap : Node
{
    public override void _Ready()
    {
        GD.Print($"Kiln — {(OS.IsDebugBuild() ? "debug" : "release")} build, Godot {Engine.GetVersionInfo()["string"]}");

        // Before anything can query an action. Scenes assume these exist.
        GameActions.Install();

        if (!GameContent.Load())
        {
            GD.PushError("Startup aborted: content failed to load. See errors above.");

            if (!OS.IsDebugBuild())
            {
                OS.Alert("Game data is missing or corrupt. Please reinstall.", "Kiln");
                GetTree().Quit(1);
            }

            return;
        }

        // Projects content into the shapes the item simulation works with. Must follow the
        // content load and precede any scene that mints an item.
        Items.GameItems.Load();

        // The zone graph and the shrine network, before any zone scene enters the tree.
        World.GameWorld.Load();

        AddChild(new DebugOverlay { Name = "DebugOverlay" });
        AddChild(new DisplaySettings { Name = "DisplaySettings" });
        AddChild(new Saving.SaveService { Name = "SaveService" });

        DebugOverlay.Register("content", () =>
            GameContent.IsLoaded ? $"{GameContent.Database.TotalDefinitions} defs" : "not loaded");
    }
}
