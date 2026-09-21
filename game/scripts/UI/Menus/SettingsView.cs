using System.Linq;
using Godot;
using Kiln.Core.Combat;
using Kiln.Core.Foundation;
using Kiln.Game.Input;
using Kiln.Game.Settings;

namespace Kiln.Game.UI.Menus;

/// <summary>
/// The settings page (UIX-02), shared by the main menu and the pause menu.
/// </summary>
/// <remarks>
/// Every change applies the moment it is made and is written straight away. There is no
/// "Apply" button: a setting that looks changed and is not is the most common settings bug
/// there is, and the fix is to not have the two states at all.
/// <para>
/// Difficulty is only here in a running game (FR-11.4: changeable any time). From the main
/// menu there is no game to change it for; a new game asks for it.
/// </para>
/// </remarks>
public partial class SettingsView : ScrollContainer
{
    private readonly bool _inGame;

    public SettingsView(bool inGame)
    {
        _inGame = inGame;
        HorizontalScrollMode = ScrollMode.Disabled;
        CustomMinimumSize = new Vector2(560, 480);
    }

    public SettingsView() : this(false)
    {
    }

    public override void _Ready()
    {
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 10);
        AddChild(column);

        // -- display
        column.AddChild(MenuStyle.Label(L10n.T("Display"), 17, MenuStyle.Heading));

        Toggle(column, L10n.T("Fullscreen  (F11)"), GameSettings.Fullscreen, on => GameSettings.Fullscreen = on);
        Toggle(column, L10n.T("Vertical sync"), GameSettings.VSync, on => GameSettings.VSync = on);

        var caps = new OptionButton();

        foreach (var cap in GameSettings.FpsCaps) caps.AddItem(cap == 0 ? L10n.T("No limit") : L10n.F("{0} fps", cap));

        caps.Selected = System.Math.Max(0, System.Array.IndexOf(GameSettings.FpsCaps, GameSettings.MaxFps));
        caps.ItemSelected += index => Change(() => GameSettings.MaxFps = GameSettings.FpsCaps[(int)index]);
        Row(column, L10n.T("Frame limit"), caps);

        Slider(column, L10n.T("3D resolution"), GameSettings.RenderScale, 0.5f, 1f, v => GameSettings.RenderScale = v,
            v => L10n.F("{0:P0}", v));
        column.AddChild(MenuStyle.Label(L10n.T("Lower it on a slow machine: the world renders smaller, the text stays sharp."),
            12, MenuStyle.Dim, wrap: true));

        // -- audio
        column.AddChild(new HSeparator());
        column.AddChild(MenuStyle.Label(L10n.T("Audio"), 17, MenuStyle.Heading));

        Slider(column, L10n.T("Master"), GameSettings.MasterVolume, 0, 1, v => GameSettings.MasterVolume = v, v => L10n.F("{0:P0}", v));
        Slider(column, L10n.T("Music"), GameSettings.MusicVolume, 0, 1, v => GameSettings.MusicVolume = v, v => L10n.F("{0:P0}", v));
        Slider(column, L10n.T("Effects"), GameSettings.EffectsVolume, 0, 1, v => GameSettings.EffectsVolume = v, v => L10n.F("{0:P0}", v));
        column.AddChild(MenuStyle.Label(L10n.T("The game has no sounds yet; these are ready for them."),
            12, MenuStyle.Dim, wrap: true));

        // -- difficulty
        if (_inGame)
        {
            column.AddChild(new HSeparator());
            column.AddChild(MenuStyle.Label(L10n.T("Difficulty"), 17, MenuStyle.Heading));

            var tiers = new OptionButton();
            var about = MenuStyle.Label("", 12, MenuStyle.Dim, wrap: true);

            foreach (var tier in DifficultySettings.All) tiers.AddItem(Words.Of(tier.Tier));

            tiers.Selected = DifficultySettings.All.ToList().IndexOf(GameSession.Difficulty);
            about.Text = MenuStyle.Describe(GameSession.Difficulty);

            tiers.ItemSelected += index =>
            {
                GameSession.SetDifficulty(DifficultySettings.All[(int)index]);
                about.Text = MenuStyle.Describe(GameSession.Difficulty)
                    + "  " + L10n.T("Creatures already standing keep the strength they were born with.");
            };

            Row(column, L10n.T("Tier"), tiers);
            column.AddChild(about);
        }

        // -- language
        column.AddChild(new HSeparator());
        column.AddChild(MenuStyle.Label(L10n.T("Language"), 17, MenuStyle.Heading));

        var languages = GameSettings.Languages();
        var picker = new OptionButton();

        foreach (var code in languages) picker.AddItem(LanguageName(code));

        picker.Selected = System.Math.Max(0, languages.IndexOf(GameSettings.Language));
        picker.ItemSelected += index =>
        {
            GameSettings.Language = languages[(int)index];
            GameSettings.ApplyLanguage();
            GameSettings.Save();
            LanguageChanged?.Invoke();
        };

        Row(column, L10n.T("Language"), picker);

        if (languages.Count == 1)
        {
            column.AddChild(MenuStyle.Label(L10n.T("Only English so far. Italian comes with the translation pass."), 12, MenuStyle.Dim, wrap: true));
        }

        // -- controls
        column.AddChild(new HSeparator());
        column.AddChild(MenuStyle.Label(L10n.T("Controls"), 17, MenuStyle.Heading));

        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", 24);
        grid.AddThemeConstantOverride("v_separation", 3);

        foreach (var (what, keys) in Controls)
        {
            grid.AddChild(MenuStyle.Label(what, 13, MenuStyle.Text));
            grid.AddChild(MenuStyle.Label(keys, 13, MenuStyle.Gold));
        }

        column.AddChild(grid);
        column.AddChild(MenuStyle.Label(L10n.T("Changing keys comes later; this is what they are."), 12, MenuStyle.Dim));
    }

    private static string Key(string action) => GameActions.DescribeBinding(action);

    /// <summary>Raised after the language changes, so the page that holds this view can rebuild.</summary>
    public static event System.Action? LanguageChanged;

    /// <summary>Each language named in itself, as a player looking for theirs would.</summary>
    private static string LanguageName(string code) => code switch
    {
        "en" => "English",
        "it" => "Italiano",
        "de" => "Deutsch",
        "fr" => "Français",
        "es" => "Español",
        _ => code,
    };

    /// <summary>What each key does, read from the live bindings so this page cannot lie.</summary>
    private static (string What, string Keys)[] Controls =>
    [
        (L10n.T("Move (click, or hold)"), Key(GameActions.MoveCommand)),
        (L10n.T("Move with the keyboard"), $"{Key(GameActions.MoveForward)} {Key(GameActions.MoveLeft)} {Key(GameActions.MoveBack)} {Key(GameActions.MoveRight)}"),
        (L10n.T("Attack"), Key(GameActions.Attack)),
        (L10n.T("Guard"), Key(GameActions.DefensiveAbility)),
        (L10n.T("Skills"), $"{Key(GameActions.Skill1)} – {Key(GameActions.Skill6)}"),
        (L10n.T("Health flask"), Key(GameActions.HealthFlask)),
        (L10n.T("Talk, use"), Key(GameActions.Interact)),
        (L10n.T("Inventory"), Key(GameActions.ToggleInventory)),
        (L10n.T("Character"), Key(GameActions.ToggleCharacter)),
        (L10n.T("Workbench"), Key(GameActions.ToggleUpgradeBench)),
        (L10n.T("Map"), Key(GameActions.ToggleMap)),
        (L10n.T("Show labels (hold)"), Key(GameActions.RevealLabels)),
        (L10n.T("Turn camera"), $"{Key(GameActions.CameraRotateLeft)} / {Key(GameActions.CameraRotateRight)}"),
        (L10n.T("Look around (hold)"), Key(GameActions.CameraDrag)),
        (L10n.T("Pause, close a panel"), Key(GameActions.Cancel)),
        (L10n.T("Save  /  load newest"), $"{Key(GameActions.QuickSave)}  /  {Key(GameActions.QuickLoad)}"),
    ];

    private static void Change(System.Action set)
    {
        set();
        GameSettings.Apply();
        GameSettings.Save();
    }

    private static void Row(VBoxContainer column, string label, Control control)
    {
        var row = new HBoxContainer();
        var name = MenuStyle.Label(label, 14, MenuStyle.Text);

        name.CustomMinimumSize = new Vector2(180, 0);
        control.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        row.AddChild(name);
        row.AddChild(control);
        column.AddChild(row);
    }

    private static void Toggle(VBoxContainer column, string label, bool value, System.Action<bool> set)
    {
        var box = new CheckButton { ButtonPressed = value };

        box.Toggled += on => Change(() => set(on));
        Row(column, label, box);
    }

    private static void Slider(VBoxContainer column, string label, float value, float min, float max,
        System.Action<float> set, System.Func<float, string> show)
    {
        var holder = new HBoxContainer();
        var slider = new HSlider
        {
            MinValue = min,
            MaxValue = max,
            Step = 0.05,
            Value = value,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };

        var readout = MenuStyle.Label(show(value), 13, MenuStyle.Gold);
        readout.CustomMinimumSize = new Vector2(52, 0);
        readout.HorizontalAlignment = HorizontalAlignment.Right;

        slider.ValueChanged += v =>
        {
            readout.Text = show((float)v);
            Change(() => set((float)v));
        };

        holder.AddChild(slider);
        holder.AddChild(readout);
        Row(column, label, holder);
    }
}
