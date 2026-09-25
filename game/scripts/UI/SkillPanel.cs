using System.Collections.Generic;
using System.Linq;
using Godot;
using Kiln.Core.Foundation;
using Kiln.Core.Progression;
using Kiln.Data.Definitions;
using Kiln.Game.Input;
using Kiln.Game.Items;

namespace Kiln.Game.UI;

/// <summary>
/// The skill screen, on K (REF-03; REF-19 in the original's style): where skill points are
/// spent and skills put on keys.
/// </summary>
/// <remarks>
/// The original's skill window: a framed column on the right of the screen, the points to
/// spend at the top, the skills in their two groups — Body and Mind — each on one row with its
/// icon, its name, its grade and a "+". The icon is what the player drags down onto the bar.
/// The full description is a hover away; the row only holds what changes as the skill grows.
/// <para>
/// It stands to the right, beside the minimap, and ends above the bar, so the bar is always
/// there to drop on.
/// </para>
/// </remarks>
public partial class SkillPanel : CanvasLayer
{
    private sealed record Row(SkillDef Def, Control Frame, Label Name, Label Grade, ProgressBar Progress, Button Plus, SkillHandle Handle);

    private const float Width = 340f;

    private static readonly Color Gold = new(0.96f, 0.86f, 0.58f);
    private static readonly Color Frame = new(0.62f, 0.50f, 0.28f);

    private readonly List<Row> _rows = [];

    private Player.PlayerCharacter? _character;
    private Player.SkillCaster? _caster;

    private Label _unspent = null!;
    private SkillTip _tip = null!;
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

        // A skill dropped on the bar changes what the icons say, and the drop happens on the
        // bar rather than here.
        if (_caster is not null) _caster.HotbarChanged += Refresh;

        BuildRows();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed(GameActions.ToggleSkills))
        {
            SetOpen(!Visible);
            GetViewport().SetInputAsHandled();
            return;
        }

        if (Visible && @event.IsActionPressed(GameActions.Cancel))
        {
            SetOpen(false);
            GetViewport().SetInputAsHandled();
        }
    }

    private void SetOpen(bool open)
    {
        Visible = open;
        UiState.SetOpen(ref _counted, open);

        if (!open) return;

        // Rebuilt rather than refreshed when the rows were never built — a panel opened before
        // the content finished loading would otherwise stay empty for the session.
        if (_rows.Count == 0) BuildRows();
        else Refresh();

        GD.Print($"[skill] screen open — {PlayerProfile.Progression.UnspentSkillPoints} point(s) to spend");
    }

    // A panel freed while open would leave the modal count raised and the player unable to move.
    public override void _ExitTree() => UiState.SetOpen(ref _counted, false);

    // ------------------------------------------------------------------ build

    private void Build()
    {
        var root = new PanelContainer
        {
            AnchorLeft = 1,
            AnchorRight = 1,
            AnchorTop = 0,
            AnchorBottom = 0,
            // Beside the minimap and the quest, not over them.
            OffsetLeft = -(Width + 324),
            OffsetRight = -324,
            OffsetTop = 60,
            GrowHorizontal = Control.GrowDirection.Begin,
            GrowVertical = Control.GrowDirection.End,
        };

        // Moved by holding its border or title and dragging (REF-19).
        PanelMover.Attach(root, "skills");

        root.AddThemeStyleboxOverride("panel", Box(new Color(0.06f, 0.055f, 0.05f, 0.96f), 2, 0));
        AddChild(root);

        // The bar's own card (REF-19): the same words on a row as on the slot, and on the +,
        // what the next point changes.
        _tip = new SkillTip { Name = "SkillTip", Beside = true };
        AddChild(_tip);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 0);
        root.AddChild(column);

        // Title bar.
        var bar = new PanelContainer();
        bar.AddThemeStyleboxOverride("panel", Box(new Color(0.20f, 0.14f, 0.07f), 0, 8, bottom: 1));
        column.AddChild(bar);

        var barRow = new HBoxContainer();
        bar.AddChild(barRow);

        var title = new Label { Text = L10n.T("Skills"), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        title.AddThemeFontSizeOverride("font_size", 15);
        title.AddThemeColorOverride("font_color", Gold);
        barRow.AddChild(title);

        var cross = new Button { Text = "✕", Flat = true, FocusMode = Control.FocusModeEnum.None };
        cross.AddThemeFontSizeOverride("font_size", 13);
        cross.Pressed += () => SetOpen(false);
        barRow.AddChild(cross);

        var body = new MarginContainer();
        body.AddThemeConstantOverride("margin_left", 12);
        body.AddThemeConstantOverride("margin_right", 12);
        body.AddThemeConstantOverride("margin_top", 10);
        body.AddThemeConstantOverride("margin_bottom", 12);
        column.AddChild(body);

        var inner = new VBoxContainer();
        inner.AddThemeConstantOverride("separation", 6);
        body.AddChild(inner);

        _unspent = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _unspent.AddThemeFontSizeOverride("font_size", 14);
        inner.AddChild(_unspent);

        _list = new VBoxContainer();
        _list.AddThemeConstantOverride("separation", 4);
        inner.AddChild(_list);

        var note = new Label
        {
            Text = L10n.F("A point learns a skill; {0} points master it. Beyond that it grows by being used.", SkillBook.MaxPoints)
                + "\n" + L10n.T("Drag an icon onto the bar to put the skill on a key."),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(Width - 24, 0),
        };

        note.AddThemeFontSizeOverride("font_size", 11);
        note.AddThemeColorOverride("font_color", new Color(0.58f, 0.56f, 0.50f));
        inner.AddChild(note);
    }

    private void BuildRows()
    {
        foreach (var child in _list.GetChildren()) child.QueueFree();

        _rows.Clear();

        if (!GameContent.IsLoaded) return;

        // The two groups, as the original splits a class's skills.
        foreach (var (tree, heading) in new[] { ("body", L10n.T("Body")), ("mental", L10n.T("Mind")) })
        {
            var skills = SkillText.WarriorSkills().Where(d => d.Tree == tree).ToList();

            if (skills.Count == 0) continue;

            _list.AddChild(Heading(heading));

            foreach (var def in skills) _list.AddChild(BuildRow(def));
        }

        Refresh();
    }

    private static Control Heading(string text)
    {
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 2);

        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", 13);
        label.AddThemeColorOverride("font_color", new Color(0.85f, 0.72f, 0.45f));
        box.AddChild(label);

        box.AddChild(new ColorRect { Color = new Color(Frame, 0.6f), CustomMinimumSize = new Vector2(0, 1) });

        return box;
    }

    private Control BuildRow(SkillDef def)
    {
        var frame = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Pass };
        frame.AddThemeStyleboxOverride("panel", Box(new Color(0.10f, 0.09f, 0.08f), 1, 4, edge: new Color(0.30f, 0.25f, 0.18f)));

        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Pass };
        row.AddThemeConstantOverride("separation", 8);
        frame.AddChild(row);

        // The icon, which is also the grip: drag it onto the bar.
        var handle = new SkillHandle { SkillId = def.Id, SkillName = GameItems.Localise(def.Name) };
        handle.SkillReturned += returned => Assign(returned, -1);
        row.AddChild(handle);

        var text = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            MouseFilter = Control.MouseFilterEnum.Pass,
        };

        text.AddThemeConstantOverride("separation", 1);
        row.AddChild(text);

        var name = new Label { ClipText = true, MouseFilter = Control.MouseFilterEnum.Pass };
        name.AddThemeFontSizeOverride("font_size", 13);
        text.AddChild(name);

        var grade = new Label { ClipText = true, MouseFilter = Control.MouseFilterEnum.Pass };
        grade.AddThemeFontSizeOverride("font_size", 11);
        text.AddChild(grade);

        var progress = new ProgressBar
        {
            MinValue = 0,
            MaxValue = 1,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(0, 4),
            MouseFilter = Control.MouseFilterEnum.Pass,
        };

        progress.AddThemeStyleboxOverride("background", new StyleBoxFlat { BgColor = new Color(0.04f, 0.035f, 0.03f) });
        text.AddChild(progress);

        var plus = new Button
        {
            Text = "+",
            CustomMinimumSize = new Vector2(28, 28),
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            FocusMode = Control.FocusModeEnum.None,
        };

        plus.AddThemeFontSizeOverride("font_size", 15);

        var id = def.Id;
        plus.Pressed += () =>
        {
            Invest(id);
            ShowChange(def, frame);
        };
        row.AddChild(plus);

        frame.MouseEntered += () => ShowDescription(def, frame);
        frame.MouseExited += () => _tip.Hide();
        plus.MouseEntered += () => ShowChange(def, frame);
        plus.MouseExited += () => ShowDescription(def, frame);

        _rows.Add(new Row(def, frame, name, grade, progress, plus, handle));

        return frame;
    }

    private static StyleBoxFlat Box(Color fill, int width, int margin, int bottom = -1, Color? edge = null) => new()
    {
        BgColor = fill,
        BorderColor = edge ?? Frame,
        BorderWidthTop = width,
        BorderWidthLeft = width,
        BorderWidthRight = width,
        BorderWidthBottom = bottom >= 0 ? bottom : width,
        ContentMarginLeft = margin + 4,
        ContentMarginRight = margin,
        ContentMarginTop = margin * 0.6f,
        ContentMarginBottom = margin * 0.6f,
    };

    /// <summary>The card the skill bar shows for this skill: its title and everything it does.</summary>
    private void ShowDescription(SkillDef def, Control at)
    {
        if (_character is null) return;

        var book = _character.Skills;
        var text = book.IsUnlocked(def.Id)
            ? SkillText.Title(def, book) + "\n" + SkillText.Describe(def, book)
            : GameItems.Localise(def.Name) + "\n" + L10n.T("Not learned. Spend a skill point on it with the +.");

        _tip.Show(text, at);
    }

    /// <summary>What the next point does, on the +.</summary>
    private void ShowChange(SkillDef def, Control at)
    {
        if (_character is null) return;

        _tip.Show(SkillText.Change(def, _character.Skills), at);
    }

    // ------------------------------------------------------------------ actions

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
        if (_caster is null)
        {
            GD.Print($"[skill] no point spent on {skillId}: the skill screen found no caster");
            return;
        }

        if (!_caster.Invest(skillId)) return;

        Audio.AudioDirector.Play(Kiln.Data.Ids.Sounds.SndLevelUp);
        Refresh();
    }

    // ------------------------------------------------------------------ refresh

    /// <summary>The colour of each grade, as the skill effects wear it (REF-03).</summary>
    private static Color RankColour(MasteryRank rank) => rank switch
    {
        MasteryRank.Master => new Color(1f, 0.78f, 0.35f),
        MasteryRank.GrandMaster => new Color(0.78f, 0.55f, 1f),
        MasteryRank.Perfect => new Color(1f, 0.86f, 0.35f),
        _ => new Color(0.80f, 0.84f, 0.90f),
    };

    private static string RankName(MasteryRank rank) => rank switch
    {
        MasteryRank.Master => L10n.T("Master"),
        MasteryRank.GrandMaster => L10n.T("Grand Master"),
        MasteryRank.Perfect => L10n.T("Perfect"),
        _ => "",
    };

    private void Refresh()
    {
        if (_character is null || _rows.Count == 0) return;

        var book = _character.Skills;
        var points = _character.Progression.UnspentSkillPoints;

        _unspent.Text = L10n.F("Skill points: {0}", points);
        _unspent.AddThemeColorOverride("font_color", points > 0 ? new Color(0.56f, 0.86f, 0.62f) : new Color(0.6f, 0.58f, 0.52f));

        foreach (var row in _rows)
        {
            var id = row.Def.Id;
            var spent = book.PointsIn(id);
            var rank = book.RankOf(id);
            var colour = RankColour(rank);

            row.Name.Text = GameItems.Localise(row.Def.Name);
            row.Name.AddThemeColorOverride("font_color", spent > 0 ? new Color(0.94f, 0.92f, 0.86f) : new Color(0.55f, 0.54f, 0.50f));

            // The grade: points while they are being bought, then the rank and the casts it
            // still takes to reach the next one.
            if (spent == 0)
            {
                row.Grade.Text = L10n.T("Not learned");
                row.Progress.Value = 0;
            }
            else if (!book.IsFullyInvested(id))
            {
                row.Grade.Text = L10n.F("Grade {0} / {1}", spent, SkillBook.MaxPoints);
                row.Progress.Value = (double)spent / SkillBook.MaxPoints;
            }
            else if (rank == MasteryRank.Perfect)
            {
                row.Grade.Text = RankName(rank);
                row.Progress.Value = 1;
            }
            else
            {
                var target = rank == MasteryRank.Master ? SkillBook.GrandMasterUses : SkillBook.PerfectUses;
                var uses = book.UsesOf(id);

                row.Grade.Text = L10n.F("{0}  ·  {1} / {2} uses", RankName(rank), uses, target);
                row.Progress.Value = (double)uses / target;
            }

            row.Grade.AddThemeColorOverride("font_color", spent > 0 ? colour : new Color(0.5f, 0.49f, 0.46f));
            row.Progress.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = colour });


            row.Plus.Disabled = _caster?.CanInvest(id) != true;
            row.Plus.Visible = !book.IsFullyInvested(id);

            // Guard Stance has its own key and is not on the numbered bar.
            var onBar = row.Def.CastType != "channel";

            row.Handle.Draggable = onBar;
            row.Handle.Learned = spent > 0;
            row.Handle.ShowKey(onBar ? _caster?.SlotOf(id) ?? -1 : -1, onBar ? null : GameActions.DescribeBinding(GameActions.DefensiveAbility));
        }
    }
}
