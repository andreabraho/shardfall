using System.Linq;
using Godot;
using Kiln.Core.Foundation;
using Kiln.Game.Saving;

namespace Kiln.Game.UI.Menus;

/// <summary>
/// The list of save files, to load one or to write one (UIX-01).
/// </summary>
/// <remarks>
/// Loading lists everything — the three slots, the quick save and the autosave ring — newest
/// first within each kind, and greys out what is empty or damaged rather than hiding it, so a
/// player looking for a save can see it is there and why it will not open.
/// <para>
/// Saving only offers the three slots: the quick save and the ring belong to their keys and
/// to the game. Writing over a slot that holds something takes a second press on a button that
/// says so. There is no dialog, because a dialog is one more thing to dismiss; the button
/// itself changes to the question.
/// </para>
/// </remarks>
public partial class SaveList : ScrollContainer
{
    public enum Purpose
    {
        Load,
        Save,
    }

    private readonly Purpose _purpose;
    private VBoxContainer _rows = null!;
    private Label _status = null!;
    private string? _armed;

    public SaveList(Purpose purpose)
    {
        _purpose = purpose;
        HorizontalScrollMode = ScrollMode.Disabled;
        CustomMinimumSize = new Vector2(560, 480);
    }

    public SaveList() : this(Purpose.Load)
    {
    }

    public override void _Ready()
    {
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 8);
        AddChild(column);

        column.AddChild(MenuStyle.Label(_purpose == Purpose.Load ? L10n.T("Load a game") : L10n.T("Save the game"), 17, MenuStyle.Heading));

        _rows = new VBoxContainer();
        _rows.AddThemeConstantOverride("separation", 6);
        column.AddChild(_rows);

        _status = MenuStyle.Label("", 13, MenuStyle.Gold, wrap: true);
        column.AddChild(_status);

        Refresh();
    }

    private void Refresh()
    {
        foreach (var child in _rows.GetChildren())
        {
            _rows.RemoveChild(child);
            child.QueueFree();
        }

        var summaries = SaveService.Summaries();

        var shown = _purpose == Purpose.Save
            ? summaries.Where(s => s.Kind == SlotKind.Manual)
            : summaries
                .OrderBy(s => s.Kind)
                .ThenByDescending(s => s.Written);

        foreach (var summary in shown) _rows.AddChild(Row(summary));

        if (_purpose == Purpose.Load)
        {
            _rows.AddChild(MenuStyle.Label(L10n.T("Autosaves are written at every shrine and every border; the oldest of five is replaced."),
                12, MenuStyle.Dim, wrap: true));
        }
    }

    private Control Row(SaveSummary summary)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);

        var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        text.AddChild(MenuStyle.Label(summary.Title, 15, summary.Readable ? MenuStyle.Text : MenuStyle.Dim));
        text.AddChild(MenuStyle.Label(summary.Describe(), 12, MenuStyle.Dim));
        row.AddChild(text);

        Button button;

        if (_purpose == Purpose.Load)
        {
            button = MenuStyle.Small(L10n.T("Load"), () => Load(summary));
            button.Disabled = !summary.Readable;
        }
        else
        {
            var armed = _armed == summary.Slot;

            button = MenuStyle.Small(armed ? L10n.T("Overwrite?") : L10n.T("Save here"), () => Save(summary));

            if (armed) button.AddThemeColorOverride("font_color", new Color(0.95f, 0.6f, 0.45f));
        }

        button.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        row.AddChild(button);

        return row;
    }

    private void Load(SaveSummary summary)
    {
        if (SaveService.Instance?.Load(summary.Slot) != true)
        {
            _status.Text = L10n.T("That save could not be loaded. The log says why.");
        }
    }

    private void Save(SaveSummary summary)
    {
        // A slot with something in it takes two presses; an empty one takes one.
        if (summary.Exists && _armed != summary.Slot)
        {
            _armed = summary.Slot;
            CallDeferred(nameof(Refresh));
            return;
        }

        _armed = null;

        var saved = SaveService.Instance?.Save(summary.Slot, summary.Title) == true;

        _status.Text = saved ? L10n.F("Saved to {0}.", summary.Title) : L10n.T("The game could not be saved here.");
        CallDeferred(nameof(Refresh));
    }
}
