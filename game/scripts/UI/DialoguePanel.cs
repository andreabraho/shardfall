using System.Collections.Generic;
using Godot;
using Kiln.Core.Foundation;
using Kiln.Game.Input;

namespace Kiln.Game.UI;

/// <summary>
/// What a villager says, in a window of its own (QST-03, FR-8.2; REF-16 in the original's
/// style).
/// </summary>
/// <remarks>
/// The original's quest window: a framed box in the middle of the screen, the speaker's name
/// on its title bar, the words written out a letter at a time, and the buttons under them —
/// "Next" while there is more, "Close" at the end. Linear by requirement: no choices, nothing
/// to get wrong. The interact key or space finishes the line being written, then moves on;
/// Esc closes it at any point. It holds the input while open, so the click on a button does
/// not also walk the character off.
/// </remarks>
public partial class DialoguePanel : CanvasLayer
{
    /// <summary>How fast the words are written out.</summary>
    private const double LettersPerSecond = 55;

    private static readonly Color Gold = new(0.96f, 0.86f, 0.58f);
    private static readonly Color Frame = new(0.62f, 0.50f, 0.28f);

    private readonly Queue<string> _lines = new();

    private PanelContainer _box = null!;
    private Label _speaker = null!;
    private Label _text = null!;
    private Button _next = null!;
    private Button _close = null!;
    private bool _counted;
    private double _written;

    public override void _Ready()
    {
        Layer = 22;

        _box = new PanelContainer
        {
            Name = "Box",
            AnchorLeft = 0.5f,
            AnchorRight = 0.5f,
            AnchorTop = 0.42f,
            AnchorBottom = 0.42f,
            OffsetLeft = -250,
            OffsetRight = 250,
            GrowHorizontal = Control.GrowDirection.Both,
            GrowVertical = Control.GrowDirection.Both,
        };

        // Moved by holding its border or title and dragging (REF-19).
        PanelMover.Attach(_box, "dialogue");

        _box.AddThemeStyleboxOverride("panel", Box(new Color(0.06f, 0.055f, 0.05f, 0.96f), Frame, 2, 0));

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 0);
        _box.AddChild(column);

        // The title bar: the speaker, and a cross.
        var bar = new PanelContainer();
        bar.AddThemeStyleboxOverride("panel", Box(new Color(0.20f, 0.14f, 0.07f), Frame, 0, 8, bottom: 1));
        column.AddChild(bar);

        var barRow = new HBoxContainer();
        bar.AddChild(barRow);

        _speaker = new Label { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center };
        _speaker.AddThemeFontSizeOverride("font_size", 15);
        _speaker.AddThemeColorOverride("font_color", Gold);
        barRow.AddChild(_speaker);

        var cross = new Button { Text = "✕", Flat = true, FocusMode = Control.FocusModeEnum.None };
        cross.AddThemeFontSizeOverride("font_size", 13);
        cross.Pressed += Close;
        barRow.AddChild(cross);

        // The words.
        var body = new MarginContainer();
        body.AddThemeConstantOverride("margin_left", 20);
        body.AddThemeConstantOverride("margin_right", 20);
        body.AddThemeConstantOverride("margin_top", 16);
        body.AddThemeConstantOverride("margin_bottom", 10);
        column.AddChild(body);

        _text = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0, 96),
        };

        _text.AddThemeFontSizeOverride("font_size", 15);
        _text.AddThemeColorOverride("font_color", new Color(0.9f, 0.88f, 0.82f));
        _text.AddThemeConstantOverride("line_spacing", 4);
        body.AddChild(_text);

        // The buttons, bottom right.
        var foot = new MarginContainer();
        foot.AddThemeConstantOverride("margin_left", 16);
        foot.AddThemeConstantOverride("margin_right", 16);
        foot.AddThemeConstantOverride("margin_bottom", 14);
        column.AddChild(foot);

        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        buttons.AddThemeConstantOverride("separation", 8);
        foot.AddChild(buttons);

        _next = Menus.MenuStyle.Small(L10n.T("Next"), Advance);
        _close = Menus.MenuStyle.Small(L10n.T("Close"), Close);
        _next.FocusMode = Control.FocusModeEnum.None;
        _close.FocusMode = Control.FocusModeEnum.None;
        buttons.AddChild(_next);
        buttons.AddChild(_close);

        Visible = false;
    }

    public override void _ExitTree() => UiState.SetOpen(ref _counted, false);

    public bool IsOpen => Visible;

    public void Say(string speaker, string title, IReadOnlyList<string> lines)
    {
        if (lines.Count == 0) return;

        _lines.Clear();

        foreach (var line in lines) _lines.Enqueue(line);

        _speaker.Text = string.IsNullOrEmpty(title) ? speaker : $"{speaker}  ·  {title}";
        Visible = true;
        UiState.SetOpen(ref _counted, true);
        Advance();
    }

    public override void _Process(double delta)
    {
        if (!Visible || Writing is false) return;

        _written += delta * LettersPerSecond;
        _text.VisibleCharacters = (int)_written;
    }

    private bool Writing => _text.VisibleCharacters >= 0 && _text.VisibleCharacters < _text.GetTotalCharacterCount();

    /// <summary>Finishes the line being written, or shows the next one, or closes at the end.</summary>
    private void Advance()
    {
        if (Writing)
        {
            _text.VisibleCharacters = -1;
            return;
        }

        if (_lines.Count == 0)
        {
            Close();
            return;
        }

        _text.Text = _lines.Dequeue();
        _written = 0;
        _text.VisibleCharacters = 0;

        // "Next" while there is more to say; at the last line only "Close" is left.
        _next.Visible = _lines.Count > 0;
    }

    public void Close()
    {
        _lines.Clear();
        Visible = false;
        UiState.SetOpen(ref _counted, false);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible) return;

        if (@event.IsActionPressed(GameActions.Cancel))
        {
            Close();
        }
        else if (@event.IsActionPressed(GameActions.Interact)
                 || @event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Space })
        {
            Advance();
        }
        else
        {
            return;
        }

        GetViewport().SetInputAsHandled();
    }

    private static StyleBoxFlat Box(Color fill, Color border, int width, int margin, int bottom = -1) => new()
    {
        BgColor = fill,
        BorderColor = border,
        BorderWidthTop = width,
        BorderWidthLeft = width,
        BorderWidthRight = width,
        BorderWidthBottom = bottom >= 0 ? bottom : width,
        ContentMarginLeft = margin + 6,
        ContentMarginRight = margin,
        ContentMarginTop = margin * 0.6f,
        ContentMarginBottom = margin * 0.6f,
    };
}
