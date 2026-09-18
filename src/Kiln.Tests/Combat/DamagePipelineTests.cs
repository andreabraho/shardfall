using Kiln.Core.Combat;
using Kiln.Core.Foundation;
using Xunit;

namespace Kiln.Tests.Combat;

public class DamagePipelineTests
{
    private static StatBlock Attacker(int level = 10, int str = 20, int dex = 0) => new()
    {
        Level = level,
        Class = CharacterClass.Warrior,
        Attributes = new Attributes(str, dex, 0, 10),
        WeaponDamage = 30,
    };

    private static StatBlock Defender(double defense = 0, MonsterFamily family = MonsterFamily.Animal) => new()
    {
        Level = 10,
        Family = family,
        FlatDefense = defense,
        FlatMaxHp = 1000,
    };

    /// <summary>No crit, no pierce, no evade — isolates the arithmetic under test.</summary>
    private static DeterministicRng NoLuck() => new(1);

    private static DamageRequest Request(StatBlock attacker, StatBlock defender) => new()
    {
        Attacker = attacker,
        Defender = defender,
    };

    [Fact]
    public void Deals_AtLeastOneDamage_EvenAgainstExtremeDefense()
    {
        // A connecting hit must always show feedback, or heavy armour reads as a broken game.
        var request = Request(Attacker(str: 6), Defender(defense: 100_000));
        var result = DamagePipeline.Resolve(request, NoLuck());

        Assert.True(result.Amount >= 1);
    }

    [Fact]
    public void MoreAttackPower_DealsMoreDamage()
    {
        var weak = DamagePipeline.ExpectedDamage(Request(Attacker(str: 10), Defender()));
        var strong = DamagePipeline.ExpectedDamage(Request(Attacker(str: 40), Defender()));

        Assert.True(strong > weak);
    }

    [Fact]
    public void MoreDefense_ReducesDamage()
    {
        var soft = DamagePipeline.ExpectedDamage(Request(Attacker(), Defender(defense: 0)));
        var armoured = DamagePipeline.ExpectedDamage(Request(Attacker(), Defender(defense: 200)));

        Assert.True(armoured < soft);
    }

    [Fact]
    public void Mitigation_IsCapped_SoDefenceNeverReachesInvulnerability()
    {
        var defender = Defender(defense: 10_000_000);
        Assert.Equal(StatBlock.MitigationCap, defender.DamageReductionAgainst(10), 6);
    }

    [Fact]
    public void Mitigation_HasDiminishingReturns()
    {
        var d = Defender();

        d.FlatDefense = 100;
        var first = d.DamageReductionAgainst(10);

        d.FlatDefense = 200;
        var second = d.DamageReductionAgainst(10);

        d.FlatDefense = 300;
        var third = d.DamageReductionAgainst(10);

        // Each +100 defence buys less than the previous +100.
        Assert.True(second - first > third - second);
    }

    [Fact]
    public void VsFamilyBonus_AppliesOnlyToThatFamily()
    {
        var attacker = Attacker();
        attacker.Modifiers.VsFamily[MonsterFamily.Undead] = 0.50;

        var vsUndead = DamagePipeline.ExpectedDamage(Request(attacker, Defender(family: MonsterFamily.Undead)));
        var vsAnimal = DamagePipeline.ExpectedDamage(Request(attacker, Defender(family: MonsterFamily.Animal)));

        Assert.True(vsUndead > vsAnimal);
        Assert.Equal(1.5, vsUndead / vsAnimal, 3);
    }

    [Fact]
    public void Crit_MultipliesDamage()
    {
        // Crit chance is capped at 50%, so this samples rather than forcing a crit.
        var attacker = Attacker();
        attacker.Modifiers.CritChance = 1.0;
        attacker.Modifiers.PierceChance = -1; // isolate: no pierce branch

        var request = Request(attacker, Defender(defense: 50));

        double critTotal = 0, plainTotal = 0;
        int crits = 0, plains = 0;

        for (ulong seed = 0; seed < 2000; seed++)
        {
            var result = DamagePipeline.Resolve(request, new DeterministicRng(seed));
            if (result.Evaded) continue;

            if (result.Critical) { critTotal += result.Amount; crits++; }
            else { plainTotal += result.Amount; plains++; }
        }

        Assert.True(crits > 0 && plains > 0);
        Assert.InRange(crits / (double)(crits + plains), 0.45, 0.55);
        Assert.Equal(attacker.CritDamage, (critTotal / crits) / (plainTotal / plains), 1);
    }

    [Fact]
    public void CritChance_IsCapped()
    {
        var attacker = Attacker();
        attacker.Modifiers.CritChance = 5.0;

        Assert.Equal(StatBlock.CritChanceCap, attacker.CritChance, 6);
    }

    [Fact]
    public void CanCritFalse_NeverCrits()
    {
        var attacker = Attacker();
        attacker.Modifiers.CritChance = 1.0;

        var request = Request(attacker, Defender()) with { CanCrit = false };

        for (var i = 0; i < 50; i++)
        {
            Assert.False(DamagePipeline.Resolve(request, new DeterministicRng((ulong)i)).Critical);
        }
    }

    [Fact]
    public void Pierce_IgnoresMitigation()
    {
        // Pierce is capped at 35%, so find the pierced rolls among many seeds.
        var attacker = Attacker();
        attacker.Modifiers.PierceChance = 1.0;

        var request = Request(attacker, Defender(defense: 500));
        var pierced = 0;

        for (ulong seed = 0; seed < 500; seed++)
        {
            var result = DamagePipeline.Resolve(request, new DeterministicRng(seed));
            if (!result.Pierced) continue;

            pierced++;
            Assert.Equal(0, result.MitigationApplied);
        }

        Assert.True(pierced > 0, "no pierced hit occurred across 500 seeds");
        Assert.InRange(pierced / 500.0, 0.30, 0.40);
    }

    [Fact]
    public void Evasion_CanCauseAMiss()
    {
        // Capped at 30%, so this checks the rate rather than a single guaranteed miss.
        var defender = Defender();
        defender.Modifiers.Evasion = 1.0;

        var request = Request(Attacker(), defender);
        var evaded = 0;

        for (ulong seed = 0; seed < 1000; seed++)
        {
            var result = DamagePipeline.Resolve(request, new DeterministicRng(seed));
            if (!result.Evaded) continue;

            evaded++;
            Assert.Equal(0, result.Amount);
        }

        Assert.InRange(evaded / 1000.0, 0.25, 0.35);
    }

    [Fact]
    public void Evasion_IsCapped()
    {
        var defender = Defender();
        defender.Modifiers.Evasion = 5.0;

        Assert.Equal(StatBlock.EvasionCap, defender.Evasion, 6);
    }

    [Fact]
    public void Unavoidable_BypassesEvasion()
    {
        var defender = Defender();
        defender.Modifiers.Evasion = 1.0;

        var request = Request(Attacker(), defender) with { Unavoidable = true };
        var result = DamagePipeline.Resolve(request, NoLuck());

        Assert.False(result.Evaded);
        Assert.True(result.Amount > 0);
    }

    [Fact]
    public void Resistance_ReducesDamage_AndIsCapped()
    {
        var defender = Defender();
        defender.Modifiers.Resist[DamageElement.Fire] = 5.0;

        Assert.Equal(StatBlock.ResistCap, defender.ResistTo(DamageElement.Fire), 6);

        var plain = DamagePipeline.ExpectedDamage(Request(Attacker(), Defender()));
        var resisted = DamagePipeline.ExpectedDamage(
            Request(Attacker(), defender) with { Element = DamageElement.Fire });

        Assert.True(resisted < plain);
    }

    [Fact]
    public void DifficultyMultiplier_ScalesDamage()
    {
        var baseline = DamagePipeline.ExpectedDamage(Request(Attacker(), Defender()));

        var shardbound = DamagePipeline.ExpectedDamage(
            Request(Attacker(), Defender()) with
            {
                DifficultyDamageMultiplier = DifficultySettings.Shardbound.EnemyDamageMultiplier,
            });

        Assert.Equal(baseline * 2.40, shardbound, 3);
    }

    [Fact]
    public void SameSeed_ProducesIdenticalResults()
    {
        // NFR-R.2: without this the balance simulator cannot reproduce anything.
        var request = Request(Attacker(dex: 200), Defender(defense: 80));

        for (ulong seed = 0; seed < 25; seed++)
        {
            var a = DamagePipeline.Resolve(request, new DeterministicRng(seed));
            var b = DamagePipeline.Resolve(request, new DeterministicRng(seed));

            Assert.Equal(a, b);
        }
    }

    [Fact]
    public void Variance_StaysWithinFivePercent()
    {
        var attacker = Attacker();
        var defender = Defender();

        // Remove every random branch except the variance roll itself. Defender evasion
        // must go too: the default 6 DEX gives a small but non-zero miss chance.
        attacker.Modifiers.CritChance = -1;
        attacker.Modifiers.PierceChance = -1;
        defender.Modifiers.Evasion = -1;

        var expected = DamagePipeline.ExpectedDamage(Request(attacker, defender));
        var min = expected * DamagePipeline.VarianceMin;
        var max = expected * DamagePipeline.VarianceMax;

        for (ulong seed = 0; seed < 300; seed++)
        {
            var result = DamagePipeline.Resolve(Request(attacker, defender), new DeterministicRng(seed));

            Assert.False(result.Evaded);
            Assert.InRange(result.Amount, (int)Math.Floor(min) - 1, (int)Math.Ceiling(max) + 1);
        }
    }

    [Fact]
    public void ExpectedDamage_TracksSampledAverage()
    {
        // The simulator relies on ExpectedDamage; if it drifts from reality every balance
        // conclusion drawn from it is wrong.
        var attacker = Attacker(dex: 150);
        var defender = Defender(defense: 60);
        defender.Modifiers.Evasion = 0.10;

        var request = Request(attacker, defender);
        var rng = new DeterministicRng(4242);

        double total = 0;
        const int samples = 40_000;

        for (var i = 0; i < samples; i++)
        {
            total += DamagePipeline.Resolve(request, rng).Amount;
        }

        var sampled = total / samples;
        var expected = DamagePipeline.ExpectedDamage(request);

        Assert.InRange(sampled, expected * 0.95, expected * 1.05);
    }
}
