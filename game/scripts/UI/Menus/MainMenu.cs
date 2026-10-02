using System.Collections.Generic;
using System.Linq;
using Godot;
using Kiln.Core.Combat;
using Kiln.Core.Foundation;
using Kiln.Game.Saving;

namespace Kiln.Game.UI.Menus;

/// <summary>
/// The first screen (UIX-02): continue, start again, load, settings, quit.
/// </summary>
/// <remarks>
/// "Continue" is first and does the obvious thing — the newest save that opens, the same one
/// F12 loads in a map — and says underneath where that is, so pressing it is never a guess.
/// A new game asks for a class (the Warrior or the Ninja), a man or a woman, a name and a difficulty.
/// </remarks>
public partial class MainMenu : Control
{
    private PanelContainer _content = null!;
    private VBoxContainer _page = null!;
    private Label _pageTitle = null!;

    /// <summary>Set when the language changes, so the rebuilt menu opens where the player was.</summary>
    private static bool _reopenSettings;

    public override void _ExitTree() => SettingsView.LanguageChanged -= Relabel;

    /// <summary>The whole menu is built in one language; a new one means building it again.</summary>
    private void Relabel()
    {
        _reopenSettings = true;
        GetTree().CallDeferred(SceneTree.MethodName.ReloadCurrentScene);
    }

    public override void _Ready()
    {
        SettingsView.LanguageChanged += Relabel;

        SetAnchorsPreset(LayoutPreset.FullRect);

        var backdrop = new TextureRect
        {
            Texture = new GradientTexture2D
            {
                Gradient = new Gradient
                {
                    Colors = [new Color(0.16f, 0.11f, 0.06f), new Color(0.03f, 0.025f, 0.02f)],
                },
                FillFrom = new Vector2(0.2f, 0.1f),
                FillTo = new Vector2(0.9f, 1f),
            },
            StretchMode = TextureRect.StretchModeEnum.Scale,
        };

        backdrop.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(backdrop);

        var frame = new HBoxContainer();
        frame.SetAnchorsPreset(LayoutPreset.FullRect);
        frame.OffsetLeft = 140;
        frame.OffsetTop = 140;
        frame.OffsetRight = -140;
        frame.OffsetBottom = -100;
        frame.AddThemeConstantOverride("separation", 80);
        AddChild(frame);

        var left = new VBoxContainer { CustomMinimumSize = new Vector2(340, 0) };
        left.AddThemeConstantOverride("separation", 10);
        frame.AddChild(left);

        var logo = MenuStyle.Label("TEMINS", 76, MenuStyle.Gold);
        logo.AddThemeColorOverride("font_outline_color", new Color(0.25f, 0.15f, 0.05f));
        logo.AddThemeConstantOverride("outline_size", 10);
        left.AddChild(logo);
        left.AddChild(new Control { CustomMinimumSize = new Vector2(0, 24) });

        // In the original's style (2026-09-26): the choices in a window of their own, and the
        // page each opens a second window beside it, named on its title bar.
        var menu = MenuStyle.Window(L10n.T("Menu"), null, out var choices, out _);
        menu.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        left.AddChild(menu);

        // The character played last, whoever it is (REF-18).
        var newest = SaveService.Characters().FirstOrDefault();

        var resume = MenuStyle.Big(L10n.T("Continue"), () => SaveService.Instance?.LoadLatest(anyCharacter: true));
        resume.Disabled = newest is null;
        choices.AddChild(resume);

        var where = MenuStyle.Label(newest is null ? L10n.T("No saved game yet.") : $"{newest.Name}  ·  {newest.Newest.Describe()}", 12, MenuStyle.Dim, wrap: true);
        where.CustomMinimumSize = new Vector2(260, 0);
        choices.AddChild(where);
        choices.AddChild(new Control { CustomMinimumSize = new Vector2(0, 4) });

        choices.AddChild(MenuStyle.Big(L10n.T("New game"), () => Page(NewGamePage(), L10n.T("New game  ·  a class, a man or a woman, a name and a difficulty"))));
        choices.AddChild(MenuStyle.Big(L10n.T("Load game"), () => Page(new CharacterList(page => Page(page)))));
        choices.AddChild(MenuStyle.Big(L10n.T("Settings"), () => Page(new SettingsView(inGame: false))));
        choices.AddChild(new Control { CustomMinimumSize = new Vector2(0, 4) });
        choices.AddChild(MenuStyle.Big(L10n.T("Quit"), () => GetTree().Quit()));

        left.AddChild(new Control { CustomMinimumSize = new Vector2(0, 12) });

        // CC BY 3.0 asks for the credit where the player can see it (ASSET-LICENSES.md).
        left.AddChild(MenuStyle.Label("Icons made by Lorc and Delapouite. Available on https://game-icons.net (CC BY 3.0)", 11, MenuStyle.Dim));
        left.AddChild(MenuStyle.Label("Music by Kevin MacLeod (incompetech.com) and Alexander Nakarada (serpentsoundstudios.com), licensed under CC BY 4.0", 11, MenuStyle.Dim));
        left.AddChild(MenuStyle.Label("Ambient sounds by JC Sounds, licensed under CC BY 4.0", 11, MenuStyle.Dim));

        var spacer = new Control { SizeFlagsVertical = SizeFlags.ExpandFill };
        left.AddChild(spacer);
        left.AddChild(MenuStyle.Label($"v{ProjectSettings.GetSetting("application/config/version")}  ·  Godot {Engine.GetVersionInfo()["string"]}  ·  {(OS.IsDebugBuild() ? "debug" : "release")} build",
            11, MenuStyle.Dim));

        _content = MenuStyle.Window("", () => _content.Visible = false, out _page, out _pageTitle);
        _content.Visible = false;
        _content.SizeFlagsVertical = SizeFlags.ShrinkBegin;
        _content.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        frame.AddChild(_content);

        if (_reopenSettings)
        {
            _reopenSettings = false;
            Page(new SettingsView(inGame: false));
        }

        // Back on the menu means out of any map: nothing is paused and nothing owns the input.
        GetTree().Paused = false;
        UiState.Reset();
        World.GameWorld.Leave();
        Audio.AudioDirector.Music(Kiln.Data.Ids.Sounds.MusMenu);
        Audio.AudioDirector.Ambience("");
    }

    private Control NewGamePage()
    {
        var column = new VBoxContainer { CustomMinimumSize = new Vector2(560, 0) };
        column.AddThemeConstantOverride("separation", 10);

        // The class (CLS-01, 2026-10-01): the Warrior, or the Ninja with a dagger in each hand.
        var played = CharacterClass.Warrior;

        // And its one branch of skills (2026-10-02): the Warrior is of the body or of the mind.
        var path = PlayerProfile.BodyPath;
        // The portraits follow the class: the Ninja's are its own (CLS-03), and the Mental Warrior's.
        var portraits = new List<(Button Card, string Body)>();

        void Portray()
        {
            var prefix = played switch
            {
                CharacterClass.Blade => "ninja_",
                CharacterClass.Archer => "archer_",
                CharacterClass.Sura => "asura_",
                CharacterClass.Shaman => "seer_",
                _ => path == PlayerProfile.MentalPath ? "mental_" : "",
            };

            foreach (var (shown, of) in portraits) shown.Icon = GD.Load<Texture2D>($"res://assets/ui/portrait_{prefix}{of}.png");
        }

        // Six of them (CLS-04, 05, 06): two rows of three.
        var classes = new GridContainer { Columns = 3 };
        classes.AddThemeConstantOverride("h_separation", 12);
        classes.AddThemeConstantOverride("v_separation", 8);
        var classGroup = new ButtonGroup();

        foreach (var (choice, branch, label) in new[]
        {
            (CharacterClass.Warrior, PlayerProfile.BodyPath, L10n.T("Body Warrior")),
            (CharacterClass.Warrior, PlayerProfile.MentalPath, L10n.T("Mental Warrior")),
            (CharacterClass.Blade, "", L10n.T("Ninja")),
            (CharacterClass.Archer, "", L10n.T("Archer")),
            (CharacterClass.Sura, "", L10n.T("Asura")),
            (CharacterClass.Shaman, "", L10n.T("Dragonseer")),
        })
        {
            var card = new Button
            {
                ToggleMode = true,
                ButtonGroup = classGroup,
                ButtonPressed = choice == played && branch == path,
                Text = label,
                CustomMinimumSize = new Vector2(176, 40),
            };

            card.AddThemeFontSizeOverride("font_size", 16);
            card.Toggled += on =>
            {
                if (!on) return;

                played = choice;
                path = branch;
                Portray();
            };
            classes.AddChild(card);
        }

        column.AddChild(classes);

        // A man or a woman (CLS-00, 2026-09-30), each shown as the game will dress them.
        var body = PlayerProfile.Man;
        var bodies = new HBoxContainer();
        bodies.AddThemeConstantOverride("separation", 12);
        var group = new ButtonGroup();

        foreach (var (choice, label) in new[] { (PlayerProfile.Man, L10n.T("Man")), (PlayerProfile.Woman, L10n.T("Woman")) })
        {
            var card = new Button
            {
                ToggleMode = true,
                ButtonGroup = group,
                ButtonPressed = choice == body,
                Text = label,
                Icon = GD.Load<Texture2D>($"res://assets/ui/portrait_{choice}.png"),
                ExpandIcon = true,
                IconAlignment = HorizontalAlignment.Center,
                VerticalIconAlignment = VerticalAlignment.Top,
                CustomMinimumSize = new Vector2(150, 176),
            };

            card.AddThemeFontSizeOverride("font_size", 16);
            card.Toggled += on => { if (on) body = choice; };
            bodies.AddChild(card);
            portraits.Add((card, choice));
        }

        column.AddChild(bodies);

        // The character's name (REF-18): what the character list calls it. It must be given (2026-10-02,
        // at your call): until it is, the game cannot be begun.
        var name = new LineEdit
        {
            PlaceholderText = L10n.T("Character name"),
            MaxLength = SaveService.NameLength,
            CustomMinimumSize = new Vector2(280, 34),
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
        };

        name.AddThemeFontSizeOverride("font_size", 16);
        column.AddChild(name);

        // The difficulty (2026-10-02, at your call): a row of names, each telling what it is only
        // when the pointer rests on it — the four cards with their text ran off the screen.
        column.AddChild(MenuStyle.Label(L10n.T("Difficulty"), 13, MenuStyle.Dim));

        var difficulty = Difficulty.Disciple;
        var tiers = new HBoxContainer();
        tiers.AddThemeConstantOverride("separation", 8);
        var tierGroup = new ButtonGroup();

        foreach (var tier in DifficultySettings.All)
        {
            var chosen = tier.Tier;
            var card = new Button
            {
                ToggleMode = true,
                ButtonGroup = tierGroup,
                ButtonPressed = chosen == difficulty,
                Text = Words.Of(chosen),
                TooltipText = (chosen == Difficulty.Disciple ? L10n.F("{0}  ·  recommended", Words.Of(chosen)) + "\n" : "") + MenuStyle.Describe(tier).Replace(". ", ".\n").Replace("; ", ";\n"),
                CustomMinimumSize = new Vector2(128, 38),
            };

            card.AddThemeFontSizeOverride("font_size", 15);
            card.Toggled += on => { if (on) difficulty = chosen; };
            tiers.AddChild(card);
        }

        column.AddChild(tiers);
        column.AddChild(MenuStyle.Label(L10n.T("It can be changed at any time from the pause menu. Loot and experience are the same on every tier."),
            12, MenuStyle.Dim, wrap: true));

        // And the one way in: Begin, once the character has a name.
        var unnamed = MenuStyle.Label(L10n.T("Give your character a name to begin."), 12, MenuStyle.Gold, wrap: true);
        column.AddChild(unnamed);

        var begin = new Button
        {
            Text = L10n.T("Begin"),
            Disabled = true,
            CustomMinimumSize = new Vector2(220, 46),
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
        };

        begin.AddThemeFontSizeOverride("font_size", 18);
        begin.Pressed += () =>
        {
            if (name.Text.Trim().Length > 0) SaveService.Instance?.NewGame(difficulty, name.Text.Trim(), body, played, path);
        };
        column.AddChild(begin);

        name.TextChanged += typed =>
        {
            var named = typed.Trim().Length > 0;

            unnamed.Visible = !named;
            begin.Disabled = !named;
        };

        if (SaveService.AnySave())
        {
            column.AddChild(MenuStyle.Label(L10n.T("Every character keeps its own saves: the new one never writes over another's."),
                12, MenuStyle.Dim, wrap: true));
        }

        return column;
    }

    /// <summary>Opens a page in the window beside the menu, its name on the title bar.</summary>
    private void Page(Control page, string? title = null)
    {
        foreach (var child in _page.GetChildren())
        {
            _page.RemoveChild(child);
            child.QueueFree();
        }

        _pageTitle.Text = title ?? page switch
        {
            SaveList saves => saves.Title,
            CharacterList => CharacterList.Title,
            SettingsView => SettingsView.Title,
            _ => "",
        };

        _page.AddChild(page);
        _content.Visible = true;
    }
}
