using System;
using Godot;
using Kiln.Core.Foundation;
using Kiln.Game.Saving;

namespace Kiln.Game.UI.Menus;

/// <summary>
/// The characters on this machine (REF-18): each with its level, where it stands, its
/// difficulty and how long it has been played — to play on, to open its saves, or to delete.
/// </summary>
/// <remarks>
/// Deleting takes a second press on a button that turns into the question, as overwriting a
/// slot does: a dialog is one more thing to dismiss, and a character is a lot to lose.
/// </remarks>
public partial class CharacterList : ScrollContainer
{
    private readonly Action<Control> _open;
    private VBoxContainer _rows = null!;
    private Label _status = null!;
    private string? _armed;

    /// <param name="open">Shows a page in the menu's content frame: a character's saves.</param>
    public CharacterList(Action<Control> open)
    {
        _open = open;
        HorizontalScrollMode = ScrollMode.Disabled;
        CustomMinimumSize = new Vector2(620, 480);
    }

    public CharacterList() : this(_ => { })
    {
    }

    public override void _Ready()
    {
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        column.AddThemeConstantOverride("separation", 8);
        AddChild(column);

        column.AddChild(MenuStyle.Label(L10n.T("Characters"), 17, MenuStyle.Heading));

        _rows = new VBoxContainer();
        _rows.AddThemeConstantOverride("separation", 10);
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

        var characters = SaveService.Characters();

        if (characters.Count == 0)
        {
            _rows.AddChild(MenuStyle.Label(L10n.T("No saved game yet."), 13, MenuStyle.Dim));
            return;
        }

        foreach (var character in characters) _rows.AddChild(Row(character));
    }

    private Control Row(CharacterSummary character)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);

        var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        text.AddChild(MenuStyle.Label(character.Name, 16, MenuStyle.Gold));
        text.AddChild(MenuStyle.Label(character.Describe(), 12, MenuStyle.Dim, wrap: true));
        row.AddChild(text);

        var play = MenuStyle.Small(L10n.T("Play"), () =>
        {
            if (SaveService.Instance?.LoadLatestOf(character.Id) != true)
            {
                _status.Text = L10n.T("That save could not be loaded. The log says why.");
            }
        });

        var saves = MenuStyle.Small(L10n.T("Saves"), () => _open(new SaveList(SaveList.Purpose.Load, character.Id, character.Name)));

        var armed = _armed == character.Id;
        var delete = MenuStyle.Small(armed ? L10n.T("Delete?") : L10n.T("Delete"), () => Delete(character));

        if (armed) delete.AddThemeColorOverride("font_color", new Color(0.95f, 0.6f, 0.45f));

        foreach (var button in new[] { play, saves, delete })
        {
            button.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            row.AddChild(button);
        }

        return row;
    }

    private void Delete(CharacterSummary character)
    {
        if (_armed != character.Id)
        {
            _armed = character.Id;
            CallDeferred(nameof(Refresh));
            return;
        }

        _armed = null;

        _status.Text = SaveService.DeleteCharacter(character.Id)
            ? L10n.F("{0} was deleted.", character.Name)
            : L10n.T("The character could not be deleted. The log says why.");

        CallDeferred(nameof(Refresh));
    }
}
