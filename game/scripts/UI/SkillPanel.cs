using System.Collections.Generic;
using Godot;
using Kiln.Core.Foundation;
using Kiln.Core.Progression;
using Kiln.Data.Definitions;
using Kiln.Game.Input;
using Kiln.Game.Items;

namespace Kiln.Game.UI;

/// <summary>
/// The skill screen, on K (REF-03): where skill points are spent and skills put on keys.
/// </summary>
/// <remarks>
/// Skills are no longer learned on their own, and no longer gated on level: a point learns
/// any of them, six more master it, and there are never enough points for all eight — which is
/// what makes the screen a decision rather than a list of buttons to click through.
/// <para>
/// Each row states what the skill does at the investment it has now, so the cost of a point is
/// read against what the point buys, and carries a grip: a skill is put on a key by dragging it
/// down onto the bar, which stays on screen behind the panel, and taken off it by dragging it
/// back up onto the list.
/// </para>
/// </remarks>
public partial class SkillPanel : CanvasLayer
{
    private sealed record Row(SkillDef Def, Label Title, Label Detail, Button Plus, SkillHandle Handle);

    private readonly List<Row> _rows = [];

    private Player.PlayerCharacter? _character;
    private Player.SkillCaster? _caster;

    private Label _unspent = null!;
    private VBoxContainer _list = null!;
    private bool _counted;

    public override void _Ready()
    {
        Layer = 24;
        Build();
        Visible = false;
        CallDeferred(nameof(Bind));
    }

    private void Bind()
    {
        var player = GetTree().GetFirstNodeInGroup("player");

        _character = player?.GetNodeOrNull<Player.PlayerCharacter>("PlayerCharacter");
        _caster = player?.GetNodeOrNull<Player.SkillCaster>("SkillCaster");

        if (_character is not null)
        {
            _character.LeveledUp += _ => Refresh();
            _character.ExperienceChanged += Refresh;
        }

        // A skill dropped on the bar changes what the grips say, and the drop happens on the
        // bar rather than here.
        if (_caster is not null) _caster.HotbarChanged += Refresh;

        BuildRows();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed(GameActions.ToggleSkills))
        {
            Visible = !Visible;
            UiState.SetOpen(ref _counted, Visible);

            // Rebuilt rather than refreshed when the rows were never built — a panel opened
            // before the content finished loading would otherwise stay empty for the session.
            if (Visible && _rows.Count == 0) BuildRows();
            else if (Visible) Refresh();

            GetViewport().SetInputAsHandled();
            return;
        }

        if (Visible && @event.IsActionPressed(GameActions.Cancel))
        {
            Visible = false;
            UiState.SetOpen(ref _counted, false);
            GetViewport().SetInputAsHandled();
        }
    }

    // A panel freed while open would leave the modal count raised and the player unable to move.
    public override void _ExitTree() => UiState.SetOpen(ref _counted, false);

    // ------------------------------------------------------------------ build

    private void Build()
    {
        var root = new PanelContainer
        {
            AnchorLeft = 0.5f,
            AnchorTop = 0.5f,
            AnchorRight = 0.5f,
            AnchorBottom = 0.5f,
            GrowHorizontal = Control.GrowDirection.Both,
            GrowVertical = Control.GrowDirection.Both,
        };

        root.AddThemeStyleboxOverride("panel", SkillText.Panel());
        AddChild(root);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 22);
        margin.AddThemeConstantOverride("margin_right", 22);
        margin.AddThemeConstantOverride("margin_top", 16);
        margin.AddThemeConstantOverride("margin_bottom", 16);
        root.AddChild(margin);

        var column = new VBoxContainer { CustomMinimumSize = new Vector2(620, 0) };
        column.AddThemeConstantOverride("separation", 8);
        margin.AddChild(column);

        var title = new Label { Text = L10n.T("Skills") };
        title.AddThemeFontSizeOverride("font_size", 18);
        column.AddChild(title);

        _unspent = new Label();
        _unspent.AddThemeFontSizeOverride("font_size", 15);
        _unspent.AddThemeColorOverride("font_color", new Color("8fd3a8"));
        column.AddChild(_unspent);

        column.AddChild(new HSeparator());

        _list = new VBoxContainer();
        _list.AddThemeConstantOverride("separation", 10);
        column.AddChild(_list);

        var note = new Label
        {
            Text = L10n.F("A point learns a skill; {0} points master it. Mastery beyond that is earned by casting.",
                      SkillBook.MaxPoints)
                + "\n" + L10n.T("Drag a skill down onto the bar to put it on a key, and back up here to take it off.")
                + "\n" + L10n.T("There are never enough points for everything — that choice is the build. Any shrine refunds them, free."),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };

        note.AddThemeFontSizeOverride("font_size", 11);
        note.AddThemeColorOverride("font_color", new Color(0.55f, 0.60f, 0.66f));
        column.AddChild(note);
    }

    private void BuildRows()
    {
        foreach (var child in _list.GetChildren()) child.QueueFree();

        _rows.Clear();

        if (!GameContent.IsLoaded) return;

        foreach (var def in SkillText.WarriorSkills())
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 10);
            _list.AddChild(row);

            var text = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            text.AddThemeConstantOverride("separation", 2);
            row.AddChild(text);

            var title = new Label();
            title.AddThemeFontSizeOverride("font_size", 15);
            text.AddChild(title);

            var detail = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
            detail.AddThemeFontSizeOverride("font_size", 12);
            detail.AddThemeColorOverride("font_color", new Color(0.66f, 0.72f, 0.80f));
            text.AddChild(detail);

            var id = def.Id;

            // The grip. A skill is put on a key by dragging it down onto the bar, which stays
            // on screen behind this panel — the gesture says where the skill is going, which a
            // dropdown listing "Key 4" never quite does.
            var handle = new SkillHandle { SkillId = id, SkillName = GameItems.Localise(def.Name) };

            handle.SkillReturned += returned => Assign(returned, -1);
            row.AddChild(handle);

            var plus = new Button { Text = "+", CustomMinimumSize = new Vector2(38, 32) };
            plus.Pressed += () => Invest(id);
            row.AddChild(plus);

            _rows.Add(new Row(def, title, detail, plus, handle));
        }

        Refresh();
    }

    /// <summary>Moves a skill onto a key, or off the bar entirely.</summary>
    private void Assign(string skillId, int slot)
    {
        if (_caster is null) return;

        if (slot < 0)
        {
            var was = _caster.SlotOf(skillId);

            if (was >= 0) _caster.Assign(was, "");
        }
        else
        {
            _caster.Assign(slot, skillId);
        }

        Refresh();
    }

    private void Invest(string skillId)
    {
        if (_caster?.Invest(skillId) != true) return;

        Audio.AudioDirector.Play(Kiln.Data.Ids.Sounds.SndLevelUp);
        Refresh();
    }

    private void Refresh()
    {
        if (_character is null || _rows.Count == 0) return;

        var book = _character.Skills;
        var points = _character.Progression.UnspentSkillPoints;

        _unspent.Text = points > 0
            ? L10n.F("{0} skill point(s) to spend", points)
            : L10n.T("No skill points to spend.");

        foreach (var row in _rows)
        {
            var spent = book.PointsIn(row.Def.Id);

            row.Title.Text = SkillText.Title(row.Def, book);
            row.Title.AddThemeColorOverride("font_color", spent > 0
                ? new Color(0.92f, 0.94f, 0.98f)
                : new Color(0.72f, 0.78f, 0.86f));

            row.Detail.Text = SkillText.Describe(row.Def, book)
                + (spent > 0 ? "" : "\n" + L10n.F("Not learned. Written for about level {0}.", row.Def.SuggestedLevel));

            row.Plus.Disabled = _caster?.CanInvest(row.Def.Id) != true;
            row.Plus.TooltipText = book.IsFullyInvested(row.Def.Id)
                ? L10n.T("Fully invested. It ranks up from here by being used.")
                : points > 0
                    ? L10n.T("Spend a skill point.")
                    : L10n.T("No skill points left.");

            // Guard Stance has its own key and is not on the numbered bar.
            var onBar = row.Def.CastType != "channel";

            row.Handle.Visible = onBar;

            if (onBar) row.Handle.ShowKey(_caster?.SlotOf(row.Def.Id) ?? -1);
        }
    }
}
