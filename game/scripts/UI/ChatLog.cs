using System.Collections.Generic;
using Godot;
using Kiln.Core.Foundation;

namespace Kiln.Game.UI;

/// <summary>
/// The message window above the skill bar: a short running log of what the game has just
/// given the player, starting with yang.
/// </summary>
/// <remarks>
/// A floating "+120" over a dying creature is gone before it is read in a crowd, and a quest
/// reward or a sale has no creature to float over at all. The log keeps the last few lines
/// where the eye already goes — just above the skills — so the player can look down and see
/// what came in. Written for any message; yang is the first thing that posts to it.
/// <para>
/// Yang is caught at the bag rather than at each place that pays it, so a new source of yang
/// is logged without anyone remembering to.
/// </para>
/// </remarks>
public partial class ChatLog : CanvasLayer
{
    private const int MaxLines = 3;

    private static readonly Color YangColour = new("f0c96a");

    private static ChatLog? _instance;

    private readonly Queue<Label> _lines = new();
    private VBoxContainer _list = null!;
    private PanelContainer _panel = null!;
    private Kiln.Core.Items.Inventory? _bag;
    private double _idle;

    public override void _Ready()
    {
        _instance = this;
        Layer = 14;

        _panel = new PanelContainer { Name = "Panel", MouseFilter = Control.MouseFilterEnum.Ignore };

        // Bottom left, just over the task bar (REF-19), where the original keeps its chat:
        // clear of the skill slots and the cards that rise above them.
        _panel.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
        _panel.GrowHorizontal = Control.GrowDirection.End;
        _panel.GrowVertical = Control.GrowDirection.Begin;
        _panel.OffsetLeft = 16;
        _panel.OffsetRight = 16 + 440;
        _panel.OffsetBottom = -(TaskBar.Height + 34);

        _panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.05f, 0.04f, 0.06f, 0.55f),
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
            ContentMarginLeft = 10,
            ContentMarginRight = 10,
            ContentMarginTop = 6,
            ContentMarginBottom = 6,
        });

        _list = new VBoxContainer { Name = "Lines", MouseFilter = Control.MouseFilterEnum.Ignore };
        _list.AddThemeConstantOverride("separation", 1);

        _panel.AddChild(_list);
        AddChild(_panel);

        _panel.Visible = false;
    }

    public override void _ExitTree()
    {
        if (_bag is not null) _bag.YangAdded -= OnYang;
        if (_instance == this) _instance = null;
    }

    /// <summary>Adds a line to the window.</summary>
    public static void Post(string text, Color colour) => _instance?.Add(text, colour);

    private void Add(string text, Color colour)
    {
        var line = new Label
        {
            Text = text,
            Modulate = colour,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };

        line.AddThemeFontSizeOverride("font_size", 15);
        line.AddThemeConstantOverride("outline_size", 4);
        line.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.8f));

        _list.AddChild(line);
        _lines.Enqueue(line);

        while (_lines.Count > MaxLines) _lines.Dequeue().QueueFree();

        _idle = 0;
        _panel.Visible = true;
        _panel.Modulate = Colors.White;
    }

    private void OnYang(long amount) => Add(L10n.F("You received {0:N0} gan.", amount), YangColour);

    public override void _Process(double delta)
    {
        // The bag is replaced when a save loads; follow whichever one the player has now.
        var bag = PlayerProfile.Bag;

        if (!ReferenceEquals(bag, _bag))
        {
            if (_bag is not null) _bag.YangAdded -= OnYang;

            _bag = bag;

            if (_bag is not null) _bag.YangAdded += OnYang;
        }

        if (!_panel.Visible) return;

        // Quiet for a while, and the window fades back to nothing rather than sitting over
        // the field; the next message brings it back with its history.
        _idle += delta;

        if (_idle > 12) _panel.Modulate = Colors.White with { A = Mathf.Max(0, 1f - (float)((_idle - 12) / 2)) };
        if (_idle > 14) _panel.Visible = false;
    }
}
