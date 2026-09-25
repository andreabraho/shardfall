using System;
using Godot;
using Kiln.Core.Foundation;
using Kiln.Core.Progression;
using Kiln.Game.Input;

namespace Kiln.Game.UI;

/// <summary>One square on the skill bar, drawn by hand so a cooldown is a sweep, not a number alone.</summary>
public partial class SkillSlot : Control
{
    private const float Side = 66f;

    private static readonly Color Frame = new(0.62f, 0.56f, 0.44f);
    private static readonly Color Body = new(0.86f, 0.52f, 0.34f);
    private static readonly Color Mental = new(0.44f, 0.62f, 0.9f);

    private bool _learned;
    private double _remaining;
    private double _total = 1;
    private bool _affordable = true;
    private bool _active;
    private MasteryRank _rank;

    private string _name = "";
    private string _key = "";
    private Color _tint = Frame;

    public string SkillId { get; set; } = "";
    public string Action { get; set; } = "";

    /// <summary>
    /// Which numbered key this is, or -1 for a slot that is not part of the bar — Guard
    /// Stance, which has its own key and cannot be rearranged.
    /// </summary>
    public int Slot { get; set; } = -1;

    /// <summary>What the skill does, in full. Shown by the bar on a card above the slot.</summary>
    public string Description { get; private set; } = "";

    /// <summary>Told when a skill is dropped on this slot: the slot index and the skill.</summary>
    [Signal] public delegate void SkillDroppedEventHandler(int slot, string skillId);

    /// <summary>Told when the mouse comes to rest here, or leaves.</summary>
    [Signal] public delegate void HoveredEventHandler(bool entered);

    /// <summary>Told when the skill on this slot is taken off the bar.</summary>
    [Signal] public delegate void SkillRemovedEventHandler(int slot);

    /// <summary>True while a drag this slot started is in the air.</summary>
    private bool _dragging;

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(Side, Side);
        MouseFilter = MouseFilterEnum.Pass;

        MouseEntered += () => EmitSignal(SignalName.Hovered, true);
        MouseExited += () => EmitSignal(SignalName.Hovered, false);
    }

    // ------------------------------------------------------------------ dragging

    /// <summary>
    /// Picks the skill up off the bar, so slots can be rearranged by dragging one onto
    /// another (REF-03). A slot that is not part of the numbered bar is not draggable.
    /// </summary>
    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (Slot < 0 || SkillId.Length == 0) return default;

        SetDragPreview(SkillDrag.Preview(_name));
        _dragging = true;

        return SkillDrag.Data(SkillId);
    }

    /// <summary>
    /// A skill dragged off the bar and let go anywhere that does not take it is removed from
    /// the bar (REF-03) — the way the original clears a slot, and the way a player tries first.
    /// Let go on another slot and it moves there instead; that drop counts as a success, so
    /// this never fires for it.
    /// </summary>
    public override void _Notification(int what)
    {
        if (what != NotificationDragEnd || !_dragging) return;

        _dragging = false;

        if (!GetViewport().GuiIsDragSuccessful() && Slot >= 0 && SkillId.Length > 0)
        {
            EmitSignal(SignalName.SkillRemoved, Slot);
        }
    }

    /// <summary>Right-click empties the slot: the same thing, for anyone who does not drag.</summary>
    public override void _GuiInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right }) return;
        if (Slot < 0 || SkillId.Length == 0) return;

        AcceptEvent();
        EmitSignal(SignalName.SkillRemoved, Slot);
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data) =>
        Slot >= 0 && SkillDrag.SkillOf(data) is not null;

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        if (SkillDrag.SkillOf(data) is not { } skillId) return;

        EmitSignal(SignalName.SkillDropped, Slot, skillId);
    }

    /// <summary>Reads the parts that do not change while playing: name, key, tree, tooltip.</summary>
    public void Describe()
    {
        _key = GameActions.DescribeBinding(Action);

        if (SkillId.Length == 0 || !GameContent.IsLoaded
            || !GameContent.Database.Skills.TryGetValue(SkillId, out var def))
        {
            Description = L10n.F("Empty. Drag a skill here from the skill screen ({0}).",
                GameActions.DescribeBinding(GameActions.ToggleSkills));

            return;
        }

        _name = Items.GameItems.Localise(def.Name);
        _tint = def.Tree == "mental" ? Mental : Body;

        Retitle();
    }

    /// <summary>
    /// Rewrites the tooltip from what the player has invested (REF-03), so hovering a slot
    /// says what this cast will do rather than what the skill does on paper.
    /// </summary>
    public void Retitle()
    {
        if (SkillId.Length == 0 || !GameContent.IsLoaded
            || !GameContent.Database.Skills.TryGetValue(SkillId, out var def))
        {
            return;
        }

        var book = GetTree().GetFirstNodeInGroup("player")
            ?.GetNodeOrNull<Player.PlayerCharacter>("PlayerCharacter")?.Skills;

        if (book is null)
        {
            Description = $"{_name}   [{_key}]";
            return;
        }

        // Kept as text rather than as a TooltipText: the bar draws its own card above the
        // slot, because the engine's tooltip follows the cursor and lands on top of the bar.
        Description = (book.IsUnlocked(SkillId)
                ? $"{SkillText.Title(def, book)}   [{_key}]\n" + SkillText.Describe(def, book)
                : $"{_name}   [{_key}]\n" + L10n.T("Not learned. Spend a skill point on it with K."))
            + (Slot >= 0 ? "\n" + L10n.T("Right-click, or drag it off the bar, to remove it.") : "");
    }

    public void Present(bool learned, double remaining, double total, bool affordable, bool active, MasteryRank rank)
    {
        // Rounded to a tenth before comparing, so a slot sitting still is not redrawn every
        // frame just because a float moved by a microsecond. Readiness is compared on its own:
        // the last step of a cooldown goes from 0.04 to 0, both round to 0.0, and without this
        // the slot stayed dark at "0.0" with the skill already usable.
        var tick = Math.Round(remaining, 1);
        var cooling = remaining > 0;

        if (learned == _learned && cooling == _remaining > 0 && tick == Math.Round(_remaining, 1) && affordable == _affordable
            && active == _active && rank == _rank && Math.Abs(total - _total) < 0.001)
        {
            return;
        }

        _learned = learned;
        _remaining = remaining;
        _total = Math.Max(0.01, total);
        _affordable = affordable;
        _active = active;
        _rank = rank;

        QueueRedraw();
    }

    public override void _Draw()
    {
        var rect = new Rect2(Vector2.Zero, new Vector2(Side, Side));
        var font = ThemeDB.FallbackFont;

        DrawRect(rect, new Color(0.07f, 0.07f, 0.09f, 0.88f));

        if (SkillId.Length == 0)
        {
            DrawRect(rect, new Color(Frame, 0.25f), filled: false, width: 1.5f);
            DrawString(font, new Vector2(4, 13), _key, HorizontalAlignment.Left, -1, 11, new Color(1, 1, 1, 0.3f));
            return;
        }

        var ready = _learned && _remaining <= 0;
        var colour = !_learned ? new Color(0.4f, 0.4f, 0.42f)
            : !_affordable ? new Color(0.35f, 0.45f, 0.75f)
            : _tint;

        // The face: the skill's tree colour, dimmed until it can actually be used.
        DrawRect(rect.Grow(-3), new Color(colour, ready && _affordable ? 0.32f : 0.14f));

        // The icon when the skill has one (REF-19), the name while it does not.
        if (SkillIcons.For(SkillId) is { } icon)
        {
            var tint = ready ? colour.Lightened(0.3f) : new Color(colour, 0.45f);
            DrawTextureRect(icon, rect.Grow(-10), false, tint);
        }
        else
        {
            DrawName(font, colour, ready);
        }

        // The sweep. It drains downward as the cooldown runs, so how full it is reads at a
        // glance without reading the number.
        if (_learned && _remaining > 0)
        {
            var share = (float)Math.Clamp(_remaining / _total, 0, 1);
            DrawRect(new Rect2(0, 0, Side, Side * share), new Color(0, 0, 0, 0.62f));

            var seconds = _remaining >= 10 ? $"{_remaining:0}" : $"{_remaining:0.0}";
            DrawString(font, new Vector2(0, Side * 0.62f), seconds, HorizontalAlignment.Center, Side, 20, Colors.White);
        }

        if (!_learned)
        {
            DrawString(font, new Vector2(0, Side - 6), L10n.T("unlearned"), HorizontalAlignment.Center, Side, 11,
                new Color(0.75f, 0.75f, 0.78f));
        }

        // The border says the state: gold while it is running (Guard held up), bright when
        // ready, dull otherwise.
        var border = _active ? new Color(1f, 0.84f, 0.4f)
            : ready && _affordable ? colour
            : new Color(Frame, 0.45f);

        DrawRect(rect, border, filled: false, width: _active ? 3f : 1.5f);

        // Key in the corner, on a small plate so it reads over any face colour.
        var keySize = font.GetStringSize(_key, HorizontalAlignment.Left, -1, 11);
        DrawRect(new Rect2(1, 1, keySize.X + 6, 14), new Color(0, 0, 0, 0.7f));
        DrawString(font, new Vector2(4, 12), _key, HorizontalAlignment.Left, -1, 11, new Color(1f, 0.92f, 0.75f));

        if (_rank != MasteryRank.Normal)
        {
            var mark = _rank switch
            {
                MasteryRank.Master => "M",
                MasteryRank.GrandMaster => "G",
                _ => "P",
            };

            DrawString(font, new Vector2(Side - 12, 12), mark, HorizontalAlignment.Left, -1, 11, new Color(1f, 0.84f, 0.4f));
        }
    }

    /// <summary>The name, broken over two lines where it has two words.</summary>
    private void DrawName(Font font, Color colour, bool ready)
    {
        var words = _name.Split(' ', 2);
        var shade = ready ? new Color(1, 1, 1, 0.95f) : new Color(1, 1, 1, 0.45f);

        if (words.Length == 1)
        {
            DrawString(font, new Vector2(0, Side * 0.6f), words[0], HorizontalAlignment.Center, Side, 11, shade);
            return;
        }

        DrawString(font, new Vector2(0, Side * 0.5f), words[0], HorizontalAlignment.Center, Side, 11, shade);
        DrawString(font, new Vector2(0, Side * 0.5f + 13), words[1], HorizontalAlignment.Center, Side, 11, shade);
    }
}
