using Godot;
using Kiln.Core.Combat;
using Kiln.Data.Definitions;

namespace Kiln.Game.Combat;

/// <summary>
/// Puts a status from data on a combatant: an enemy's poison on the player, a skill's stun or
/// defence debuff on an enemy. One place, so both sides roll and resist the same way.
/// </summary>
public static class StatusApplication
{
    /// <summary>
    /// Rolls the chance and applies the status. A stun's chance is reduced by the victim's stun
    /// resistance first; a resistance of 1 is immune (REF-01). Returns true when it landed.
    /// </summary>
    public static bool Try(StatusApplicationDef? spec, Combatant victim, double stunResist = 0)
    {
        if (spec is null || !victim.IsAlive) return false;

        var chance = spec.Chance;

        if (spec.Kind == "stun") chance *= 1 - System.Math.Clamp(stunResist, 0, 1);

        if (chance <= 0 || !GameSession.CombatRng.Chance(chance)) return false;

        var effect = spec.Kind switch
        {
            "poison" => StatusEffectSet.Poison(spec.Magnitude, spec.Duration),
            "bleed" => StatusEffectSet.Bleed(spec.Magnitude, spec.Duration),
            "stun" => StatusEffectSet.Stun(spec.Duration),
            "slow" => StatusEffectSet.Slow(spec.Magnitude, spec.Duration),
            "weaken" => StatusEffectSet.Weaken(spec.Magnitude, spec.Duration),
            "vulnerability" => StatusEffectSet.Vulnerability(spec.Magnitude, spec.Duration),
            _ => null,
        };

        if (effect is null)
        {
            GD.PushWarning($"[status] unknown status kind '{spec.Kind}'.");
            return false;
        }

        victim.Statuses.Apply(effect);

        return true;
    }
}
