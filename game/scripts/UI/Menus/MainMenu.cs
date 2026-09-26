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
/// A new game asks for a difficulty and nothing else: there is one class and no name to type.
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

        choices.AddChild(MenuStyle.Big(L10n.T("New game"), () => Page(NewGamePage(), L10n.T("New game  ·  name your character and choose a difficulty"))));
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
        left.AddChild(MenuStyle.Label($"Godot {Engine.GetVersionInfo()["string"]}  ·  {(OS.IsDebugBuild() ? "debug" : "release")} build",
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

        // The character's name (REF-18): what the character list calls it.
        var name = new LineEdit
        {
            PlaceholderText = SaveService.DefaultName(),
            MaxLength = SaveService.NameLength,
            CustomMinimumSize = new Vector2(280, 34),
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
        };

        name.AddThemeFontSizeOverride("font_size", 16);
        column.AddChild(name);
        column.AddChild(MenuStyle.Label(L10n.T("It can be changed at any time from the pause menu. Loot and experience are the same on every tier."),
            12, MenuStyle.Dim, wrap: true));

        foreach (var tier in DifficultySettings.All)
        {
            var card = new Button
            {
                CustomMinimumSize = new Vector2(0, 94),
                Alignment = HorizontalAlignment.Left,
                ClipText = false,
            };

            var text = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
            text.SetAnchorsPreset(LayoutPreset.FullRect);
            text.OffsetLeft = 14;
            text.OffsetTop = 8;
            text.OffsetRight = -14;

            var label = tier.Tier == Difficulty.Disciple ? L10n.F("{0}  ·  recommended", Words.Of(tier.Tier)) : Words.Of(tier.Tier);
            var title = MenuStyle.Label(label, 17, MenuStyle.Gold);
            title.MouseFilter = MouseFilterEnum.Ignore;

            var about = MenuStyle.Label(MenuStyle.Describe(tier), 12, MenuStyle.Text, wrap: true);
            about.MouseFilter = MouseFilterEnum.Ignore;

            text.AddChild(title);
            text.AddChild(about);
            card.AddChild(text);

            var chosen = tier.Tier;
            card.Pressed += () => SaveService.Instance?.NewGame(chosen, name.Text);

            column.AddChild(card);
        }

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
