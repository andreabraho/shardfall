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
/// Compact on purpose, and pinned to the top of the screen. The first version gave every skill
/// its full description and grew taller than the window: centred, it ran off both edges and sat
/// on top of the skill bar — the one thing a skill has to be dragged onto. Each card now holds
/// one line of numbers, the full text is on the card's tooltip, and the panel ends well above
/// the bar so the bar is always there to drop on.
/// </para>
/// </remarks>
public partial class SkillPanel : CanvasLayer
{
    private sealed record Card(SkillDef Def, PanelContainer Frame, Label Title, Label Line, Button Plus, SkillHandle Handle);

    /// <summary>Width of one skill card. Two of them side by side make the panel.</summary>
    private const float CardWidth = 400f;

    private readonly List<Card> _cards = [];

    private Player.PlayerCharacter? _character;
    private Player.SkillCaster? _caster;

    private Label _unspent = null!;
    private GridContainer _grid = null!;
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

        BuildCards();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed(GameActions.ToggleSkills))
        {
            Visible = !Visible;
            UiState.SetOpen(ref _counted, Visible);

            // Rebuilt rather than refreshed when the cards were never built — a panel opened
            // before the content finished loading would otherwise stay empty for the session.
            if (Visible && _cards.Count == 0) BuildCards();
            else if (Visible) Refresh();

            if (Visible)
            {
                GD.Print($"[skill] screen open — {PlayerProfile.Progression.UnspentSkillPoints} point(s) to spend");
            }

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
        // Anchored to the top centre and grown downward only, so however many skills there
        // are it can never reach down over the bar.
        var root = new PanelContainer
        {
            AnchorLeft = 0.5f,
            AnchorRight = 0.5f,
            AnchorTop = 0f,
            AnchorBottom = 0f,
            OffsetTop = 48,
            GrowHorizontal = Control.GrowDirection.Both,
            GrowVertical = Control.GrowDirection.End,
        };

        root.AddThemeStyleboxOverride("panel", SkillText.Panel());
        AddChild(root);

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 16);
        margin.AddThemeConstantOverride("margin_right", 16);
        margin.AddThemeConstantOverride("margin_top", 12);
        margin.AddThemeConstantOverride("margin_bottom", 12);
        root.AddChild(margin);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 8);
        margin.AddChild(column);

        var header = new HBoxContainer();
        header.AddThemeConstantOverride("separation", 16);
        column.AddChild(header);

        var title = new Label { Text = L10n.T("Skills") };
        title.AddThemeFontSizeOverride("font_size", 18);
        header.AddChild(title);

        _unspent = new Label { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _unspent.AddThemeFontSizeOverride("font_size", 15);
        _unspent.AddThemeColorOverride("font_color", new Color("8fd3a8"));
        header.AddChild(_unspent);

        _grid = new GridContainer { Columns = 2 };
        _grid.AddThemeConstantOverride("h_separation", 8);
        _grid.AddThemeConstantOverride("v_separation", 8);
        column.AddChild(_grid);

        var note = new Label
        {
            Text = L10n.F("A point learns a skill; {0} points master it. Mastery beyond that is earned by casting.",
                      SkillBook.MaxPoints)
                + "\n" + L10n.T("Drag a skill down onto the bar to put it on a key, and back up here to take it off.")
                + "\n" + L10n.T("Hover a skill for everything it does."),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2((CardWidth * 2) + 8, 0),
        };

        note.AddThemeFontSizeOverride("font_size", 11);
        note.AddThemeColorOverride("font_color", new Color(0.55f, 0.60f, 0.66f));
        column.AddChild(note);
    }

    private void BuildCards()
    {
        foreach (var child in _grid.GetChildren()) child.QueueFree();

        _cards.Clear();

        if (!GameContent.IsLoaded) return;

        foreach (var def in SkillText.WarriorSkills())
        {
            var frame = new PanelContainer
            {
                CustomMinimumSize = new Vector2(CardWidth, 0),
                MouseFilter = Control.MouseFilterEnum.Pass,
            };

            frame.AddThemeStyleboxOverride("panel", CardStyle());
            _grid.AddChild(frame);

            var margin = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Pass };
            margin.AddThemeConstantOverride("margin_left", 10);
            margin.AddThemeConstantOverride("margin_right", 8);
            margin.AddThemeConstantOverride("margin_top", 6);
            margin.AddThemeConstantOverride("margin_bottom", 6);
            frame.AddChild(margin);

            var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Pass };
            row.AddThemeConstantOverride("separation", 8);
            margin.AddChild(row);

            var text = new VBoxContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                MouseFilter = Control.MouseFilterEnum.Pass,
            };

            text.AddThemeConstantOverride("separation", 1);
            row.AddChild(text);

            var title = new Label { ClipText = true, MouseFilter = Control.MouseFilterEnum.Pass };
            title.AddThemeFontSizeOverride("font_size", 14);
            text.AddChild(title);

            var line = new Label { ClipText = true, MouseFilter = Control.MouseFilterEnum.Pass };
            line.AddThemeFontSizeOverride("font_size", 12);
            line.AddThemeColorOverride("font_color", new Color(0.66f, 0.72f, 0.80f));
            text.AddChild(line);

            var id = def.Id;

            // The grip. A skill is put on a key by dragging it down onto the bar — the gesture
            // says where the skill is going, which a dropdown listing "Key 4" never quite does.
            var handle = new SkillHandle { SkillId = id, SkillName = GameItems.Localise(def.Name) };

            handle.SkillReturned += returned => Assign(returned, -1);
            row.AddChild(handle);

            var plus = new Button
            {
                Text = "+",
                CustomMinimumSize = new Vector2(36, 34),
                SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            };

            plus.Pressed += () => Invest(id);
            row.AddChild(plus);

            _cards.Add(new Card(def, frame, title, line, plus, handle));
        }

        Refresh();
    }

    private static StyleBoxFlat CardStyle() => new()
    {
        BgColor = new Color(0.11f, 0.12f, 0.15f, 1f),
        BorderColor = new Color(0.22f, 0.25f, 0.30f),
        BorderWidthTop = 1,
        BorderWidthBottom = 1,
        BorderWidthLeft = 1,
        BorderWidthRight = 1,
        CornerRadiusTopLeft = 4,
        CornerRadiusTopRight = 4,
        CornerRadiusBottomLeft = 4,
        CornerRadiusBottomRight = 4,
    };

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

    private void Refresh()
    {
        if (_character is null || _cards.Count == 0) return;

        var book = _character.Skills;
        var points = _character.Progression.UnspentSkillPoints;

        _unspent.Text = points > 0
            ? L10n.F("{0} skill point(s) to spend", points)
            : L10n.T("No skill points to spend.");

        foreach (var card in _cards)
        {
            var spent = book.PointsIn(card.Def.Id);

            card.Title.Text = SkillText.Title(card.Def, book);
            card.Title.AddThemeColorOverride("font_color", spent > 0
                ? new Color(0.92f, 0.94f, 0.98f)
                : new Color(0.62f, 0.67f, 0.74f));

            card.Line.Text = spent > 0
                ? SkillText.Summary(card.Def, book)
                : L10n.T("Not learned") + "  ·  " + SkillText.Summary(card.Def, book);

            // The whole description, a hover away: the card only has room for the numbers.
            card.Frame.TooltipText = SkillText.Title(card.Def, book) + "\n" + SkillText.Describe(card.Def, book);

            card.Plus.Disabled = _caster?.CanInvest(card.Def.Id) != true;
            card.Plus.TooltipText = book.IsFullyInvested(card.Def.Id)
                ? L10n.T("Fully invested. It ranks up from here by being used.")
                : points > 0
                    ? L10n.T("Spend a skill point.")
                    : L10n.T("No skill points left.");

            // Guard Stance has its own key and is not on the numbered bar.
            var onBar = card.Def.CastType != "channel";

            card.Handle.Visible = onBar;

            if (onBar) card.Handle.ShowKey(_caster?.SlotOf(card.Def.Id) ?? -1);
        }
    }
}
