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

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);

        var backdrop = new TextureRect
        {
            Texture = new GradientTexture2D
            {
                Gradient = new Gradient
                {
                    Colors = [new Color(0.10f, 0.08f, 0.07f), new Color(0.03f, 0.03f, 0.04f)],
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

        left.AddChild(MenuStyle.Label("KILN", 76, MenuStyle.Gold));
        left.AddChild(MenuStyle.Label("working title", 14, MenuStyle.Dim));
        left.AddChild(new Control { CustomMinimumSize = new Vector2(0, 40) });

        var newest = SaveService.Summaries()
            .Where(s => s.Readable)
            .OrderByDescending(s => s.Written)
            .FirstOrDefault();

        var resume = MenuStyle.Big("Continue", () => SaveService.Instance?.LoadLatest());
        resume.Disabled = newest is null;
        left.AddChild(resume);
        left.AddChild(MenuStyle.Label(newest is null ? "No saved game yet." : newest.Describe(), 12, MenuStyle.Dim));
        left.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6) });

        left.AddChild(MenuStyle.Big("New game", () => Page(NewGamePage())));
        left.AddChild(MenuStyle.Big("Load game", () => Page(new SaveList(SaveList.Purpose.Load))));
        left.AddChild(MenuStyle.Big("Settings", () => Page(new SettingsView(inGame: false))));
        left.AddChild(MenuStyle.Big("Quit", () => GetTree().Quit()));

        var spacer = new Control { SizeFlagsVertical = SizeFlags.ExpandFill };
        left.AddChild(spacer);
        left.AddChild(MenuStyle.Label($"Godot {Engine.GetVersionInfo()["string"]}  ·  {(OS.IsDebugBuild() ? "debug" : "release")} build",
            11, MenuStyle.Dim));

        _content = new PanelContainer
        {
            Visible = false,
            SizeFlagsVertical = SizeFlags.ShrinkBegin,
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
        };

        _content.AddThemeStyleboxOverride("panel", MenuStyle.Panel(0.92f));
        frame.AddChild(_content);

        // Back on the menu means out of any map: nothing is paused and nothing owns the input.
        GetTree().Paused = false;
        UiState.Reset();
        World.GameWorld.Leave();
    }

    private Control NewGamePage()
    {
        var column = new VBoxContainer { CustomMinimumSize = new Vector2(560, 0) };
        column.AddThemeConstantOverride("separation", 10);

        column.AddChild(MenuStyle.Label("New game  ·  choose a difficulty", 17, MenuStyle.Heading));
        column.AddChild(MenuStyle.Label("It can be changed at any time from the pause menu. Loot and experience are the same on every tier.",
            12, MenuStyle.Dim, wrap: true));

        foreach (var tier in DifficultySettings.All)
        {
            var card = new Button
            {
                CustomMinimumSize = new Vector2(0, 76),
                Alignment = HorizontalAlignment.Left,
                ClipText = false,
            };

            var text = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
            text.SetAnchorsPreset(LayoutPreset.FullRect);
            text.OffsetLeft = 14;
            text.OffsetTop = 8;
            text.OffsetRight = -14;

            var name = tier.Tier == Difficulty.Disciple ? "Disciple  ·  recommended" : tier.Tier.ToString();
            var title = MenuStyle.Label(name, 17, MenuStyle.Gold);
            title.MouseFilter = MouseFilterEnum.Ignore;

            var about = MenuStyle.Label(MenuStyle.Describe(tier), 12, MenuStyle.Text, wrap: true);
            about.MouseFilter = MouseFilterEnum.Ignore;

            text.AddChild(title);
            text.AddChild(about);
            card.AddChild(text);

            var chosen = tier.Tier;
            card.Pressed += () => SaveService.Instance?.NewGame(chosen);

            column.AddChild(card);
        }

        if (SaveService.AnySave())
        {
            column.AddChild(MenuStyle.Label("Your saves stay where they are. The first autosave of the new game replaces the oldest autosave.",
                12, MenuStyle.Dim, wrap: true));
        }

        return column;
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
