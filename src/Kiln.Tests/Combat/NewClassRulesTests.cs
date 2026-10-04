using Kiln.Core.Combat;
using Kiln.Core.Foundation;
using Xunit;

namespace Kiln.Tests.Combat;

/// <summary>
/// What the Archer, the Asura and the Dragonseer brought to the rules (CLS-04, 05, 06 — 2026-10-02).
/// </summary>
public class NewClassRulesTests
{
    private static double Attack(CharacterClass played, Attributes attributes) =>
        new StatBlock { Class = played, Attributes = attributes, WeaponDamage = 0, Level = 1 }.AttackPower;

    [Fact]
    public void TheArcherStrikesWithDexterity_AndALittleWithStrength()
    {
        var even = new Attributes(10, 10, 10, 10);

        Assert.Equal(10 * Attributes.AttackPerPoint, Attack(CharacterClass.Archer, new Attributes(10, 20, 10, 10)) - Attack(CharacterClass.Archer, even), 6);
        Assert.Equal(10 * Attributes.BladeAttackPerStr, Attack(CharacterClass.Archer, new Attributes(20, 10, 10, 10)) - Attack(CharacterClass.Archer, even), 6);
    }

    [Fact]
    public void TheDragonseerStrikesWithIntelligence_AndTheAsuraWithStrength()
    {
        var even = new Attributes(10, 10, 10, 10);

        Assert.Equal(10 * Attributes.AttackPerPoint, Attack(CharacterClass.Shaman, new Attributes(10, 10, 20, 10)) - Attack(CharacterClass.Shaman, even), 6);
        Assert.Equal(0, Attack(CharacterClass.Shaman, new Attributes(20, 10, 10, 10)) - Attack(CharacterClass.Shaman, even), 6);
        Assert.Equal(10 * Attributes.AttackPerPoint, Attack(CharacterClass.Sura, new Attributes(20, 10, 10, 10)) - Attack(CharacterClass.Sura, even), 6);
        Assert.Equal(0, Attack(CharacterClass.Sura, new Attributes(10, 10, 20, 10)) - Attack(CharacterClass.Sura, even), 6);
    }

    [Fact]
    public void Burning_TicksEverySecond_AndStacksToThree()
    {
        var set = new StatusEffectSet();

        for (var i = 0; i < 5; i++) set.Apply(StatusEffectSet.Burn(10, 5));

        Assert.Equal(3, set.Get(StatusKind.Burn)!.Stacks);
        Assert.Equal(30, set.Tick(1.0).Damage, 6);
    }

    [Fact]
    public void Burning_IsHarmful_SoACleanseClearsIt()
    {
        var set = new StatusEffectSet();

        set.Apply(StatusEffectSet.Burn(10, 5));
        set.Apply(StatusEffectSet.Focus(0.15));

        Assert.Equal(1, set.CleanseAll());
        Assert.False(set.Has(StatusKind.Burn));
        Assert.True(set.Has(StatusKind.Focus));
    }

    [Fact]
    public void TheLightStep_QuickensTheFeet()
    {
        var set = new StatusEffectSet();

        set.Apply(StatusEffectSet.Haste(0.35));

        Assert.Equal(1.35, set.MoveSpeedMultiplier, 6);
    }

    [Fact]
    public void TheLightStep_TheHexedBladeAndTheMail_AreNotHarmful()
    {
        var set = new StatusEffectSet { Warded = true };

        set.Apply(StatusEffectSet.Haste(0.35));
        set.Apply(StatusEffectSet.Leech(0.08));
        set.Apply(StatusEffectSet.Thorns(0.25));
        set.Apply(StatusEffectSet.Burn(10));

        Assert.True(set.Has(StatusKind.Haste));
        Assert.Equal(0.08, set.MagnitudeOf(StatusKind.Leech), 6);
        Assert.Equal(0.25, set.MagnitudeOf(StatusKind.Thorns), 6);
        Assert.False(set.Has(StatusKind.Burn));
    }

    [Fact]
    public void ACriticalChanceAddedForOneAttack_IsCappedWithTheRest()
    {
        var attacker = new StatBlock { Level = 10, Attributes = new Attributes(20, 0, 0, 10), WeaponDamage = 30 };
        var defender = new StatBlock { Level = 10, Family = MonsterFamily.Animal, FlatDefense = 0, FlatMaxHp = 1000 };
        var crits = 0;
        var rng = new DeterministicRng(7);

        for (var i = 0; i < 2000; i++)
        {
            var result = DamagePipeline.Resolve(new DamageRequest { Attacker = attacker, Defender = defender, BonusCritChance = 5.0 }, rng);

            if (result.Critical) crits++;
        }

        // Far more than the attacker's own 3%, and never past the cap of three in four.
        Assert.InRange(crits, 1400, 1600);
    }

    [Theory]
    [InlineData(0.18, 0, 0.18)]
    [InlineData(0.18, 50, 0.24)]
    [InlineData(0.18, 500, 0.40)]
    public void ABlessingGrowsWithIntelligence_UpToItsCap(double strength, int intelligence, double expected)
    {
        Assert.Equal(expected, Attributes.SeerScaled(strength, intelligence, Attributes.SeerBlessingPerInt, Attributes.SeerBlessingCap), 6);
    }

    [Fact]
    public void ABlessingAlreadyPastItsCap_IsNotCutBack()
    {
        Assert.Equal(0.45, Attributes.SeerScaled(0.45, 100, Attributes.SeerBlessingPerInt, Attributes.SeerBlessingCap), 6);
    }
}
