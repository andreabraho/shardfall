using System.Collections.Generic;
using Godot;
using Kiln.Core.Foundation;
using Kiln.Core.Progression;
using Kiln.Data.Definitions;
using Kiln.Game.Input;
using Kiln.Game.Items;

namespace Kiln.Game.UI;

/// <summary>
/// The skill screen, on V (REF-03): where skill points are spent.
/// </summary>
/// <remarks>
/// Skills are no longer learned on their own. A level makes one eligible; a point learns it,
/// six more master it, and there are never enough points for all eight — which is what makes
/// the screen a decision rather than a list of buttons to click through.
/// <para>
/// Every skill is listed from the first visit, including the ones still too high to touch, so
/// the player can plan for Ground Slam at 20 instead of discovering it exists on the level it
/// arrives. Each row states what the skill actually does at the investment it has now, so the
/// cost of a point is read against what the point buys.
/// </para>
/// </remarks>
public partial class SkillPanel : CanvasLayer
{
    private sealed record Row(SkillDef Def, Label Title, Label Detail, Button Plus);

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

            var plus = new Button { Text = "+", CustomMinimumSize = new Vector2(38, 32) };
            var id = def.Id;
            plus.Pressed += () => Invest(id);
            row.AddChild(plus);

            _rows.Add(new Row(def, title, detail, plus));
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
        var level = _character.Progression.Level;

        _unspent.Text = points > 0
            ? L10n.F("{0} skill point(s) to spend", points)
            : L10n.T("No skill points to spend.");

        foreach (var row in _rows)
        {
            var spent = book.PointsIn(row.Def.Id);
            var locked = row.Def.UnlockLevel > level;

            row.Title.Text = SkillText.Title(row.Def, book);
            row.Title.AddThemeColorOverride("font_color", spent > 0
                ? new Color(0.92f, 0.94f, 0.98f)
                : locked ? new Color(0.42f, 0.46f, 0.52f) : new Color(0.72f, 0.78f, 0.86f));

            row.Detail.Text = locked
                ? L10n.F("Available at level {0}.", row.Def.UnlockLevel)
                : SkillText.Describe(row.Def, book);

            row.Plus.Disabled = _caster?.CanInvest(row.Def.Id) != true;
            row.Plus.TooltipText = locked
                ? L10n.F("Available at level {0}.", row.Def.UnlockLevel)
                : book.IsFullyInvested(row.Def.Id)
                    ? L10n.T("Fully invested. It ranks up from here by being used.")
                    : L10n.T("Spend a skill point.");
        }
    }
}
