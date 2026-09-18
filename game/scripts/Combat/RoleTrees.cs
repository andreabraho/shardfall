using Kiln.Core.Ai;
using Kiln.Core.Foundation;

namespace Kiln.Game.Combat;

/// <summary>
/// The behaviour tree for each enemy role (CBT-07).
/// <para>
/// These are the tactical layer of the redesign (doc 02 §2.4): a wave is composed of roles,
/// and solving it in the right kill order is the skill expression that replaces dodge timing
/// under click-to-move. Shielder before Mender before Bruiser is a decision the player makes
/// only because these behaviours are distinct and legible.
/// </para>
/// </summary>
public static class RoleTrees
{
    /// <summary>Radius within which a Shielder protects, and a Mender heals.</summary>
    private const float SupportRadius = 9f;

    /// <summary>Distance a ranged role tries to hold.</summary>
    private const float PreferredRange = 9f;

    /// <summary>Closer than this and a ranged role backs off instead of shooting.</summary>
    private const float TooCloseRange = 5f;

    public static BtNode<EnemyBrain> Build(EnemyRole role) => role switch
    {
        EnemyRole.Archer => Archer(),
        EnemyRole.Shielder => Shielder(),
        EnemyRole.Mender => Mender(),
        EnemyRole.Bomber => Bomber(),
        _ => Bruiser(),
    };

    /// <summary>Shared preamble: no target, or dragged too far, means go home.</summary>
    private static BtNode<EnemyBrain> Disengage() =>
        new BtSequence<EnemyBrain>(
            new BtCondition<EnemyBrain>(b => b.ShouldDisengage),
            new BtAction<EnemyBrain>((b, d) => b.ReturnHome(d)));

    /// <summary>Attack with the special if it is ready, otherwise the basic swing.</summary>
    private static BtNode<EnemyBrain> Attack() =>
        new BtSelector<EnemyBrain>(
            new BtAction<EnemyBrain>((b, d) => b.UseAbility(b.SpecialAbility, d), b => b.CancelAbility()),
            new BtAction<EnemyBrain>((b, d) => b.UseAbility(b.BasicAbility, d), b => b.CancelAbility()));

    /// <summary>
    /// Finish an attack already under way, before anything else gets a say.
    /// <para>
    /// Without this, a repositioning branch keeps interrupting its own wind-up: the enemy
    /// starts an attack, the player steps closer, the retreat branch takes over and cancels
    /// it, the player steps back, and it begins again — re-placing its telegraph on the
    /// player each time. It also defeats the commitment the whole telegraph design rests on.
    /// </para>
    /// </summary>
    private static BtNode<EnemyBrain> FinishCommitted() =>
        new BtSequence<EnemyBrain>(
            new BtCondition<EnemyBrain>(b => b.IsCommitted),
            Attack());

    /// <summary>Straight damage. The baseline the other roles are read against.</summary>
    private static BtNode<EnemyBrain> Bruiser() =>
        new BtSelector<EnemyBrain>(
            Disengage(),
            Attack(),
            new BtAction<EnemyBrain>((b, d) => b.Chase(d)));

    /// <summary>
    /// Ranged pressure. Holds distance and forces the player to close, which is the whole
    /// point of the role — standing still and trading is never the answer.
    /// </summary>
    private static BtNode<EnemyBrain> Archer() =>
        new BtSelector<EnemyBrain>(
            Disengage(),
            FinishCommitted(),

            // Backing off takes priority over starting a new shot, so an archer never
            // settles into melee — but never mid-attack, which is handled above.
            new BtSequence<EnemyBrain>(
                new BtCondition<EnemyBrain>(b => b.DistanceToTarget < TooCloseRange),
                new BtAction<EnemyBrain>((b, d) => b.Retreat(d))),

            Attack(),
            new BtAction<EnemyBrain>((b, d) => b.Chase(d)));

    /// <summary>
    /// Projects damage reduction onto nearby allies. Kill first — and the aura ends the
    /// instant it dies, so the player sees the decision pay off.
    /// </summary>
    private static BtNode<EnemyBrain> Shielder() =>
        new BtSelector<EnemyBrain>(
            Disengage(),

            // Maintained every tick and never consumes the turn: shielding is passive,
            // the shielder still fights.
            new BtOptional<EnemyBrain>(
                new BtAction<EnemyBrain>((b, d) => b.MaintainShield(SupportRadius, d))),

            Attack(),
            new BtAction<EnemyBrain>((b, d) => b.Chase(d)));

    /// <summary>
    /// Heals the most wounded ally on a cooldown, and keeps away from the player. Ignoring
    /// it means the pack never goes down.
    /// </summary>
    private static BtNode<EnemyBrain> Mender() =>
        new BtSelector<EnemyBrain>(
            Disengage(),
            FinishCommitted(),

            new BtCooldown<EnemyBrain>(
                6.0,
                new BtAction<EnemyBrain>((b, _) => b.HealLowestAlly(SupportRadius, 0.18))),

            new BtSequence<EnemyBrain>(
                new BtCondition<EnemyBrain>(b => b.DistanceToTarget < TooCloseRange),
                new BtAction<EnemyBrain>((b, d) => b.Retreat(d))),

            Attack(),
            new BtAction<EnemyBrain>((b, d) => b.Chase(d)));

    /// <summary>
    /// Charges in and detonates, killing itself. The threat is positional: it has to be
    /// pulled away from the pack, or dealt with before it arrives.
    /// </summary>
    private static BtNode<EnemyBrain> Bomber() =>
        new BtSelector<EnemyBrain>(
            Disengage(),

            new BtSequence<EnemyBrain>(
                new BtCondition<EnemyBrain>(b => b.InRangeOf(b.SpecialAbility) && b.AbilityReady(b.SpecialAbility)),
                new BtAction<EnemyBrain>((b, d) => b.Detonate(d), b => b.CancelAbility())),

            new BtAction<EnemyBrain>((b, d) => b.Chase(d)));
}
