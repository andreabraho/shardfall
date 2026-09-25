using System;
using Godot;
using Kiln.Game.Input;
using Kiln.Game.Player;

namespace Kiln.Game.UI;

/// <summary>
/// The row of abilities along the bottom of the screen: what each key does, and how long
/// until it can do it again (FR-12.1).
/// </summary>
/// <remarks>
/// Before this, cooldowns only existed in the F3 overlay, which is a debugging tool — a
/// player who has to open a debug panel to know whether Cleave is ready is playing a game
/// the HUD is not describing.
/// <para>
/// Every slot shows its key, read from the live binding rather than written in: a hint that
/// names the wrong key is worse than none, and Guard's hint named Space for an hour after
/// Space became the attack.
/// </para>
/// </remarks>
public partial class SkillBar : CanvasLayer
{
    private static readonly string[] SlotActions =
    [
        GameActions.Skill1, GameActions.Skill2, GameActions.Skill3,
        GameActions.Skill4, GameActions.Skill5, GameActions.Skill6, GameActions.Skill7,
    ];

    private SkillCaster? _caster;
    private DefensiveAbility? _guard;
    private HBoxContainer _row = null!;
    private SkillTip _tip = null!;
    private SkillSlot[] _slots = [];

    public override void _Ready()
    {
        Layer = 15;

        _row = new HBoxContainer { Name = "Row", MouseFilter = Control.MouseFilterEnum.Ignore };
        _row.AddThemeConstantOverride("separation", 6);
        _row.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
        _row.GrowHorizontal = Control.GrowDirection.Both;
        _row.GrowVertical = Control.GrowDirection.Begin;
        // Inside the task bar (REF-19), centred in its height.
        _row.OffsetBottom = -((TaskBar.Height - 66) / 2) + 1;

        AddChild(_row);

        _tip = new SkillTip { Name = "SkillTip" };
        AddChild(_tip);

        CallDeferred(nameof(Bind));
    }

    /// <summary>Finds the player's abilities. Deferred: the session readies before the player.</summary>
    private void Bind()
    {
        var player = GetTree().GetFirstNodeInGroup("player");

        _caster = player?.GetNodeOrNull<SkillCaster>("SkillCaster");
        _guard = player?.GetNodeOrNull<DefensiveAbility>("DefensiveAbility");

        if (_caster is null) return;

        // Rebuilt when the player rearranges the bar from the skill screen (REF-03).
        _caster.HotbarChanged += Rebuild;

        Rebuild();
    }

    private void Rebuild()
    {
        if (_caster is null) return;

        _tip.Hide();

        foreach (var child in _row.GetChildren()) child.QueueFree();

        var count = Math.Min(SlotActions.Length, _caster.Hotbar.Length);
        _slots = new SkillSlot[count + (_guard is null ? 0 : 1)];

        for (var i = 0; i < count; i++)
        {
            _slots[i] = Add(_caster.Hotbar[i], SlotActions[i], i);
        }

        // Guard sits apart from the numbered row, because it is the one ability the player
        // reaches for reactively and it is bound to a different hand position.
        if (_guard is not null)
        {
            _row.AddChild(new Control { CustomMinimumSize = new Vector2(10, 0), MouseFilter = Control.MouseFilterEnum.Ignore });
            _slots[^1] = Add(_guard.SkillId, GameActions.DefensiveAbility, -1);
        }
    }

    private SkillSlot Add(string skillId, string action, int index)
    {
        var slot = new SkillSlot { SkillId = skillId, Action = action, Slot = index };

        _row.AddChild(slot);
        slot.Describe();

        // The card is the bar's, not the slot's: only one is ever up, and it has to be able
        // to sit outside the slot it describes.
        slot.Hovered += entered =>
        {
            if (entered) _tip.Show(slot.Description, slot);
            else _tip.Hide();
        };

        slot.SkillDropped += (at, skillId) =>
        {
            _caster?.Assign(at, skillId);
            _tip.Hide();
        };

        slot.SkillRemoved += at =>
        {
            _caster?.Assign(at, "");
            _tip.Hide();
        };

        return slot;
    }

    /// <summary>Seconds between tooltip rebuilds. A tooltip is read, not watched.</summary>
    private const double RetitleEvery = 1.0;

    private double _retitleIn;

    public override void _Process(double delta)
    {
        if (_caster is null || !IsInstanceValid(_caster)) return;

        // Points and ranks change while the bar is on screen, and the tooltip quotes both.
        _retitleIn -= delta;

        if (_retitleIn <= 0)
        {
            _retitleIn = RetitleEvery;

            foreach (var slot in _slots)
            {
                if (slot.SkillId.Length > 0) slot.Retitle();
            }
        }

        for (var i = 0; i < _slots.Length; i++)
        {
            var slot = _slots[i];

            if (slot.SkillId.Length == 0) continue;

            var isGuard = _guard is not null && i == _slots.Length - 1;

            if (isGuard)
            {
                slot.Present(
                    learned: true,
                    remaining: _guard!.CooldownRemaining,
                    total: _guard.CooldownTotal,
                    affordable: _guard.Affordable,
                    active: _guard.IsGuarding,
                    rank: _caster.RankOf(slot.SkillId));
            }
            else
            {
                slot.Present(
                    learned: _caster.IsLearned(slot.SkillId),
                    remaining: _caster.CooldownRemaining(slot.SkillId),
                    total: _caster.CooldownTotal(slot.SkillId),
                    affordable: _caster.Affordable(slot.SkillId),
                    active: false,
                    rank: _caster.RankOf(slot.SkillId));
            }
        }
    }
}
