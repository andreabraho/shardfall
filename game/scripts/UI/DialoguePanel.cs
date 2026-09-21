using System.Collections.Generic;
using Godot;
using Kiln.Game.Input;

namespace Kiln.Game.UI;

/// <summary>
/// What a villager says, one line at a time, at the bottom of the screen (QST-03, FR-8.2).
/// </summary>
/// <remarks>
/// Linear by requirement: no choices, nothing to get wrong. The interact key, a click or
/// space moves on; the last line closes it, and Esc closes it at any point. It holds the
/// input while open, so the click that turns the page does not also walk the character off.
/// </remarks>
public partial class DialoguePanel : CanvasLayer
{
    private readonly Queue<string> _lines = new();

    private PanelContainer _box = null!;
    private Label _speaker = null!;
    private Label _text = null!;
    private Label _hint = null!;
    private bool _counted;

    public override void _Ready()
    {
        Layer = 22;

        _box = new PanelContainer
        {
            Name = "Box",
            AnchorLeft = 0.5f,
            AnchorRight = 0.5f,
            AnchorTop = 1,
            AnchorBottom = 1,
            OffsetLeft = -340,
            OffsetRight = 340,
            OffsetTop = -250,
            OffsetBottom = -120,
            GrowHorizontal = Control.GrowDirection.Both,
            GrowVertical = Control.GrowDirection.Begin,
        };

        _box.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.07f, 0.07f, 0.08f, 0.94f),
            BorderColor = new Color(0.55f, 0.47f, 0.3f),
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            ContentMarginLeft = 20,
            ContentMarginRight = 20,
            ContentMarginTop = 14,
            ContentMarginBottom = 12,
        });

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 8);

        _speaker = new Label();
        _speaker.AddThemeFontSizeOverride("font_size", 16);
        _speaker.AddThemeColorOverride("font_color", new Color(0.96f, 0.86f, 0.58f));

        _text = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _text.AddThemeFontSizeOverride("font_size", 15);
        _text.AddThemeColorOverride("font_color", new Color(0.9f, 0.88f, 0.82f));

        _hint = new Label { HorizontalAlignment = HorizontalAlignment.Right };
        _hint.AddThemeFontSizeOverride("font_size", 11);
        _hint.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.58f));

        column.AddChild(_speaker);
        column.AddChild(_text);
        column.AddChild(_hint);
        _box.AddChild(column);
        AddChild(_box);

        _box.GuiInput += e =>
        {
            if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            {
                Advance();
                _box.AcceptEvent();
            }
        };

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

    private void Advance()
    {
        if (_lines.Count == 0)
        {
            Close();
            return;
        }

        _text.Text = _lines.Dequeue();
        _hint.Text = _lines.Count > 0 ? "F / click — more     Esc — close" : "F / click — close";
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
                 || @event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Space }
                 || @event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
        {
            Advance();
        }
        else
        {
            return;
        }

        GetViewport().SetInputAsHandled();
    }
}
