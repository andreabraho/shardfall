using Kiln.Core.Combat;
using Kiln.Core.Encounters;
using Kiln.Core.Foundation;
using Xunit;

namespace Kiln.Tests.Encounters;

/// <summary>
/// The three-phase shard fight (SHD-02). Written as statements about the fight's shape,
/// because the numbers are content and will move.
/// </summary>
public class ShardEncounterTests
{
    private static ShardTier Tier(double reclamation = 10) => new(
        Id: "shd_test",
        Tier: 1,
        Level: 10,
        MaxHp: 1000,
        PulseInterval: 6,

        // Long enough that an 8m ring is escapable even on Shardbound, where the wind-up is
        // scaled to 57% of the authored value. The fixture has to obey the rule it checks.
        PulseWindup: 3.5,
        PulseRadius: 8,
        PulseDamageCoef: 1,
        ReclamationSeconds: reclamation,
        ReclamationHealFraction: 0.30,
        Waves:
        [
            new ShardWave(ShardPhase.One, [new WaveSlot(EnemyRole.Bruiser, 3)]),
            new ShardWave(ShardPhase.Two, [new WaveSlot(EnemyRole.Bruiser, 2), new WaveSlot(EnemyRole.Archer, 1)]),
            new ShardWave(ShardPhase.Three,
                [new WaveSlot(EnemyRole.Bruiser, 2), new WaveSlot(EnemyRole.Mender, 1, IsAnchor: true)]),
        ],
        ModifierPool: [ShardModifier.Frenzied, ShardModifier.Warded],
        DropTable: null);

    private static ShardEncounter Fight(ShardModifier modifier = ShardModifier.None, double reclamation = 10) =>
        new(Tier(reclamation), modifier, DifficultySettings.Disciple);

    private static ShardWorldState World(int adds = 3, bool anchor = true, bool inZone = true) =>
        new(adds, anchor, inZone);

    /// <summary>Runs the fight forward, collecting everything it emitted.</summary>
    private static List<ShardEvent> Run(
        ShardEncounter fight, double seconds, double healthFraction, ShardWorldState world, double step = 0.1)
    {
        var events = new List<ShardEvent>();

        for (var t = 0.0; t < seconds; t += step)
        {
            events.AddRange(fight.Tick(step, healthFraction, world));
        }

        return events;
    }

    [Fact]
    public void Dormant_UntilThePlayerEntersTheZone()
    {
        var fight = Fight();

        var quiet = Run(fight, 30, 1.0, World(inZone: false));

        Assert.Empty(quiet);
        Assert.Equal(ShardPhase.Dormant, fight.Phase);

        fight.Tick(0.1, 1.0, World());

        Assert.Equal(ShardPhase.One, fight.Phase);
    }

    [Fact]
    public void Engaging_SpawnsTheFirstWaveExactlyOnce()
    {
        var fight = Fight();
        var events = new List<ShardEvent>();

        events.AddRange(fight.Tick(0.1, 1.0, World()));
        events.AddRange(Run(fight, 20, 1.0, World()));

        Assert.Single(events.Where(e => e.Kind == ShardEventKind.SpawnWave && e.Phase == ShardPhase.One));
    }

    [Fact]
    public void EachPhase_BringsItsOwnWaveOnce()
    {
        var fight = Fight();
        var events = new List<ShardEvent>();

        events.AddRange(fight.Tick(0.1, 1.00, World()));
        events.AddRange(Run(fight, 5, 0.80, World()));
        events.AddRange(Run(fight, 5, 0.50, World()));
        events.AddRange(Run(fight, 5, 0.20, World()));

        foreach (var phase in (ShardPhase[])[ShardPhase.One, ShardPhase.Two, ShardPhase.Three])
        {
            Assert.Single(events.Where(e => e.Kind == ShardEventKind.SpawnWave && e.Phase == phase));
        }
    }

    [Fact]
    public void Pulses_AlternateTelegraphAndStrike()
    {
        // The rhythm the whole fight is built on. A strike without a telegraph in front of it
        // is damage the player could not have avoided.
        var fight = Fight();
        fight.Tick(0.1, 1.0, World());

        var events = Run(fight, 40, 1.0, World());
        var pulses = events
            .Where(e => e.Kind is ShardEventKind.PulseTelegraph or ShardEventKind.PulseStrike)
            .ToList();

        Assert.NotEmpty(pulses);
        Assert.Equal(ShardEventKind.PulseTelegraph, pulses[0].Kind);

        for (var i = 1; i < pulses.Count; i++)
        {
            Assert.NotEqual(pulses[i - 1].Kind, pulses[i].Kind);
        }
    }

    [Fact]
    public void PulseTelegraph_LastsLongEnoughToWalkOut()
    {
        // BAL-03, checked at the encounter rather than only in the content validator, so a
        // tier built in code cannot sidestep the rule.
        foreach (var difficulty in DifficultySettings.All)
        {
            var fight = new ShardEncounter(Tier(), ShardModifier.None, difficulty);

            Assert.True(
                fight.PulseWindup >= PlayerConstants.TimeToEscape(Tier().PulseRadius),
                $"{difficulty.Tier}: {fight.PulseWindup:F2}s wind-up for an 8m ring");
        }
    }

    [Fact]
    public void PhaseTwo_AddsASecondRing()
    {
        var fight = Fight();
        fight.Tick(0.1, 1.0, World());

        Assert.Equal(1, fight.PulseRings);

        Run(fight, 1, 0.50, World());

        Assert.Equal(2, fight.PulseRings);
    }

    [Fact]
    public void PhaseThree_StartsTheReclamationCast()
    {
        var fight = Fight();
        fight.Tick(0.1, 1.0, World());

        var events = Run(fight, 1, 0.20, World());

        Assert.Contains(events, e => e.Kind == ShardEventKind.ReclamationStarted);
        Assert.True(fight.IsReclaiming);
    }

    [Fact]
    public void KillingTheAnchor_InterruptsTheCast()
    {
        // The tension beat that replaces racing other players: a deadline you can act on.
        var fight = Fight();
        fight.Tick(0.1, 1.0, World());
        Run(fight, 1, 0.20, World());

        var events = Run(fight, 2, 0.20, World(anchor: false));

        Assert.Contains(events, e => e.Kind == ShardEventKind.ReclamationInterrupted);
        Assert.DoesNotContain(events, e => e.Kind == ShardEventKind.ReclamationCompleted);
        Assert.False(fight.IsReclaiming);
    }

    [Fact]
    public void IgnoringTheAnchor_LetsTheCastLand()
    {
        var fight = Fight(reclamation: 5);
        fight.Tick(0.1, 1.0, World());

        var events = Run(fight, 8, 0.20, World());
        var completed = events.Single(e => e.Kind == ShardEventKind.ReclamationCompleted);

        Assert.Equal(0.30, completed.Duration, 3);
    }

    [Fact]
    public void ACompletedCast_StartsAnother()
    {
        // Letting one land must not end the mechanic, or eating the heal once and then
        // ignoring the anchor would be the optimal play.
        var fight = Fight(reclamation: 3);
        fight.Tick(0.1, 1.0, World());

        var events = Run(fight, 14, 0.20, World());

        Assert.True(events.Count(e => e.Kind == ShardEventKind.ReclamationCompleted) >= 2);
        Assert.True(events.Count(e => e.Kind == ShardEventKind.ReclamationStarted) >= 3);
    }

    [Fact]
    public void HealingBackOverAThreshold_DoesNotRespawnAnEarlierWave()
    {
        // A completed cast lifts the shard above 33%. Re-entering phase two there would spawn
        // its wave again, so letting a cast land would cost the heal *and* a fresh wave.
        var fight = Fight();
        fight.Tick(0.1, 1.0, World());

        Run(fight, 2, 0.20, World());

        var afterHeal = Run(fight, 5, 0.50, World());

        Assert.DoesNotContain(afterHeal, e => e.Kind == ShardEventKind.SpawnWave && e.Phase == ShardPhase.Two);
        Assert.Equal(ShardPhase.Three, fight.Phase);
    }

    [Fact]
    public void ReachingZero_Breaks_AndStopsEverything()
    {
        var fight = Fight();
        fight.Tick(0.1, 1.0, World());

        var breaking = Run(fight, 1, 0.0, World());

        Assert.Contains(breaking, e => e.Kind == ShardEventKind.Broken);
        Assert.Equal(ShardPhase.Broken, fight.Phase);
        Assert.False(fight.IsReclaiming);

        // Nothing else may fire afterwards — a pulse landing after the break would kill a
        // player who had already won.
        Assert.Empty(Run(fight, 30, 0.0, World()));
    }

    [Fact]
    public void Warded_MakesTheShardResistantWhileAddsLive()
    {
        var warded = Fight(ShardModifier.Warded);
        var plain = Fight();

        Assert.True(warded.DamageReduction(addsAlive: 3) > 0);
        Assert.Equal(0, warded.DamageReduction(addsAlive: 0));
        Assert.Equal(0, plain.DamageReduction(addsAlive: 3));
    }

    [Fact]
    public void Leaving_ResetsTheFight()
    {
        // Otherwise a shard could be whittled down over many visits, which turns the
        // encounter into a chore rather than a fight.
        var fight = Fight();
        fight.Tick(0.1, 1.0, World());
        Run(fight, 5, 0.20, World());

        fight.Reset();

        Assert.Equal(ShardPhase.Dormant, fight.Phase);
        Assert.False(fight.IsReclaiming);

        // And it can be fought again from the top, wave and all.
        var reengaged = fight.Tick(0.1, 1.0, World());

        Assert.Equal(ShardPhase.One, fight.Phase);
        Assert.Contains(reengaged, e => e.Kind == ShardEventKind.SpawnWave && e.Phase == ShardPhase.One);
    }

    [Fact]
    public void ModifierRoll_StaysInsideThePool()
    {
        var tier = Tier();
        var rng = new DeterministicRng(7);

        for (var i = 0; i < 200; i++)
        {
            Assert.Contains(tier.RollModifier(rng), tier.ModifierPool);
        }
    }

    [Fact]
    public void AnEmptyModifierPool_IsAlwaysPlain()
    {
        // The first shard a player meets should teach the base rhythm before it starts varying.
        var tier = Tier() with { ModifierPool = [] };

        Assert.Equal(ShardModifier.None, tier.RollModifier(new DeterministicRng(1)));
    }
}
