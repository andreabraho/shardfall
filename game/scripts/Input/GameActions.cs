using System.Collections.Generic;
using Godot;

namespace Kiln.Game.Input;

/// <summary>
/// Every input action and its default binding, defined in code rather than project.godot.
/// <para>
/// One source of truth, and rebinding (FR-1.8) becomes a normal runtime operation instead
/// of something that has to round-trip through a project setting. Installed once by
/// Bootstrap before any scene loads.
/// </para>
/// </summary>
public static class GameActions
{
    // Movement — click-to-move is primary (D3); WASD is the alternative scheme (FR-1.7).
    public const string MoveCommand = "move_command";
    public const string MoveForward = "move_forward";
    public const string MoveBack = "move_back";
    public const string MoveLeft = "move_left";
    public const string MoveRight = "move_right";

    // Combat

    /// <summary>A swing at whatever is in front, with nothing selected (MOV-11).</summary>
    public const string Attack = "attack";

    public const string DefensiveAbility = "defensive_ability";
    public const string Skill1 = "skill_1";
    public const string Skill2 = "skill_2";
    public const string Skill3 = "skill_3";
    public const string Skill4 = "skill_4";
    public const string Skill5 = "skill_5";
    public const string Skill6 = "skill_6";
    public const string HealthFlask = "health_flask";

    // Camera
    public const string CameraRotateLeft = "camera_rotate_left";
    public const string CameraRotateRight = "camera_rotate_right";
    public const string CameraDrag = "camera_drag";
    public const string CameraZoomIn = "camera_zoom_in";
    public const string CameraZoomOut = "camera_zoom_out";

    // Meta
    public const string ToggleDebugOverlay = "toggle_debug_overlay";
    public const string ToggleFullscreen = "toggle_fullscreen";
    public const string Cancel = "cancel";
    public const string ToggleInventory = "toggle_inventory";
    public const string ToggleUpgradeBench = "toggle_upgrade_bench";
    public const string ToggleCharacter = "toggle_character";
    public const string ToggleMap = "toggle_map";

    /// <summary>Shrines, and later NPCs and doors. One key for "use the thing I am standing at".</summary>
    public const string Interact = "interact";

    /// <summary>Held down to label the borders of the map. Alt, as the genre has taught.</summary>
    public const string RevealLabels = "reveal_labels";

    // Development only — the arena spawner (see DebugSpawner). Stripped from release builds.
    public const string DebugSpawnWave = "debug_spawn_wave";
    public const string DebugSpawnOne = "debug_spawn_one";
    public const string DebugToggleRespawn = "debug_toggle_respawn";
    public const string DebugGrantResources = "debug_grant_resources";
    public const string DebugCompleteQuests = "debug_complete_quests";

    // Saving (UIX-01).
    public const string QuickSave = "quick_save";
    public const string QuickLoad = "quick_load";
    public const string NewGame = "new_game";

    private static readonly Dictionary<string, InputEvent[]> Defaults = new()
    {
        [MoveCommand] = [Mouse(MouseButton.Left)],
        [MoveForward] = [Key(Godot.Key.W)],
        [MoveBack] = [Key(Godot.Key.S)],
        [MoveLeft] = [Key(Godot.Key.A)],
        [MoveRight] = [Key(Godot.Key.D)],

        // Space is the key pressed more than every other key combined, so it holds the swing.
        // Guard is a tap on a ten-second cooldown and does not need to be under the thumb.
        [Attack] = [Key(Godot.Key.Space)],
        [DefensiveAbility] = [Key(Godot.Key.Shift)],
        [Skill1] = [Key(Godot.Key.Key1)],
        [Skill2] = [Key(Godot.Key.Key2)],
        [Skill3] = [Key(Godot.Key.Key3)],
        [Skill4] = [Key(Godot.Key.Key4)],
        [Skill5] = [Key(Godot.Key.Key5)],
        [Skill6] = [Key(Godot.Key.Key6)],
        [HealthFlask] = [Key(Godot.Key.Q)],

        // Z/X rather than Z/C: C is the near-universal key for the character sheet, and a
        // sheet nobody can find is a sheet nobody uses.
        [CameraRotateLeft] = [Key(Godot.Key.Z)],
        [CameraRotateRight] = [Key(Godot.Key.X)],
        // Right drag to look, with middle kept as a second way in: it costs nothing to leave
        // the old binding in place for anybody whose hand already knows it.
        [CameraDrag] = [Mouse(MouseButton.Right), Mouse(MouseButton.Middle)],
        [CameraZoomIn] = [Mouse(MouseButton.WheelUp)],
        [CameraZoomOut] = [Mouse(MouseButton.WheelDown)],

        [ToggleDebugOverlay] = [Key(Godot.Key.F3)],
        [ToggleFullscreen] = [Key(Godot.Key.F11)],
        [Cancel] = [Key(Godot.Key.Escape)],
        [ToggleInventory] = [Key(Godot.Key.I)],
        [ToggleUpgradeBench] = [Key(Godot.Key.U)],
        [ToggleCharacter] = [Key(Godot.Key.C)],
        [ToggleMap] = [Key(Godot.Key.M)],
        [Interact] = [Key(Godot.Key.F)],
        [RevealLabels] = [Key(Godot.Key.Alt)],
        [DebugSpawnWave] = [Key(Godot.Key.F5)],
        [DebugSpawnOne] = [Key(Godot.Key.F6)],
        [DebugToggleRespawn] = [Key(Godot.Key.F7)],
        [DebugGrantResources] = [Key(Godot.Key.F8)],
        [DebugCompleteQuests] = [Key(Godot.Key.F9)],
        [QuickSave] = [Key(Godot.Key.F10)],
        [QuickLoad] = [Key(Godot.Key.F12)],
        [NewGame] = [new InputEventKey { PhysicalKeycode = Godot.Key.F12, ShiftPressed = true }],
    };

    private static InputEvent Key(Key key) => new InputEventKey { PhysicalKeycode = key };

    private static InputEvent Mouse(MouseButton button) => new InputEventMouseButton { ButtonIndex = button };

    /// <summary>Installs default bindings. Idempotent — safe to call more than once.</summary>
    public static void Install()
    {
        foreach (var (action, events) in Defaults)
        {
            if (!InputMap.HasAction(action))
            {
                InputMap.AddAction(action);
            }
            else
            {
                InputMap.ActionEraseEvents(action);
            }

            foreach (var e in events)
            {
                InputMap.ActionAddEvent(action, e);
            }
        }
    }

    /// <summary>Rebinds one action to a single event (MOV-07). Returns false for an unknown action.</summary>
    public static bool Rebind(string action, InputEvent to)
    {
        if (!Defaults.ContainsKey(action)) return false;

        if (!InputMap.HasAction(action)) InputMap.AddAction(action);
        InputMap.ActionEraseEvents(action);
        InputMap.ActionAddEvent(action, to);
        return true;
    }

    public static void ResetToDefaults() => Install();

    /// <summary>Human-readable current binding, for a settings screen.</summary>
    public static string DescribeBinding(string action)
    {
        if (!InputMap.HasAction(action)) return "—";

        foreach (var e in InputMap.ActionGetEvents(action))
        {
            // Godot appends " (Physical)" to a physical key. True, and noise on a HUD hint
            // or a settings row — the player only ever needed the key.
            return e.AsText().Replace(" (Physical)", "").Replace(" - Physical", "");
        }

        return "unbound";
    }

    public static IEnumerable<string> AllActions => Defaults.Keys;
}
