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
        GameActions.Skill4, GameActions.Skill5, GameActions.Skill6,
    ];

    private SkillCaster? _caster;
    private DefensiveAbility? _guard;
    private HBoxContainer _row = null!;
    private SkillSlot[] _slots = [];

    public override void _Ready()
    {
        Layer = 15;

        _row = new HBoxContainer { Name = "Row", MouseFilter = Control.MouseFilterEnum.Ignore };
        _row.AddThemeConstantOverride("separation", 6);
        _row.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
        _row.GrowHorizontal = Control.GrowDirection.Both;
        _row.GrowVertical = Control.GrowDirection.Begin;
        _row.OffsetBottom = -14;

        AddChild(_row);

        CallDeferred(nameof(Bind));
    }

    /// <summary>Finds the player's abilities. Deferred: the session readies before the player.</summary>
    private void Bind()
    {
        var player = GetTree().GetFirstNodeInGroup("player");

        _caster = player?.GetNodeOrNull<SkillCaster>("SkillCaster");
        _guard = player?.GetNodeOrNull<DefensiveAbility>("DefensiveAbility");

        if (_caster is null) return;

        var count = Math.Min(SlotActions.Length, _caster.Hotbar.Length);
        _slots = new SkillSlot[count + (_guard is null ? 0 : 1)];

        for (var i = 0; i < count; i++)
        {
            _slots[i] = Add(_caster.Hotbar[i], SlotActions[i]);
        }

        // Guard sits apart from the numbered row, because it is the one ability the player
        // reaches for reactively and it is bound to a different hand position.
        if (_guard is not null)
        {
            _row.AddChild(new Control { CustomMinimumSize = new Vector2(10, 0), MouseFilter = Control.MouseFilterEnum.Ignore });
            _slots[^1] = Add(_guard.SkillId, GameActions.DefensiveAbility);
        }
    }

    private SkillSlot Add(string skillId, string action)
    {
        var slot = new SkillSlot { SkillId = skillId, Action = action };

        _row.AddChild(slot);
        slot.Describe();

        return slot;
    }

    public override void _Process(double delta)
    {
        if (_caster is null || !IsInstanceValid(_caster)) return;

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
