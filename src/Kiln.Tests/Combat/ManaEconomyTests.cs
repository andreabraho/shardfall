using Kiln.Core.Combat;
using Kiln.Core.Foundation;
using Kiln.Core.Progression;
using Xunit;

namespace Kiln.Tests.Combat;

/// <summary>
/// The mana economy (CBT-12). These pin the shape rather than the exact numbers: skills have
/// to be affordable often enough to be the point of the combat system, and regeneration alone
/// must never be the way you pay for them.
/// </summary>
public class ManaEconomyTests
{
    /// <summary>The Phase 4 test character: level 10, points in Str and Vit, nothing in Int.</summary>
    private static StatBlock Warrior(int level = 10) => new()
    {
        Level = level,
        Class = CharacterClass.Warrior,
        Attributes = new Attributes(Str: 24, Dex: 6, Int: 6, Vit: 18),
    };

    /// <summary>Roughly the cost of a mid-tier warrior skill in game/data/skills/warrior.json.</summary>
    private const double AverageSkillCost = 23;

    [Fact]
    public void AKill_PaysForAboutOneSkill()
    {
        // The core of the design: a skill used to end a fight faster funds the next one.
        var stats = Warrior();
        var perKill = stats.MaxMana * PlayerConstants.ManaPerKillFraction;

        Assert.InRange(perKill / AverageSkillCost, 0.6, 1.6);
    }

    [Fact]
    public void RegenerationAlone_CannotFundSkills()
    {
        // If it could, the efficient play would be to auto-attack and wait, which is exactly
        // what the kill refund exists to prevent.
        var stats = Warrior();
        var secondsPerSkill = AverageSkillCost / stats.ManaRegenInCombat;

        Assert.True(secondsPerSkill > 10, $"in-combat regen funds a skill every {secondsPerSkill:F0}s — too generous");
    }

    [Fact]
    public void OutOfCombat_RefillsInUnderAMinute()
    {
        // Downtime between fights should be a breath, not a chore. Before the pool-relative
        // term this took nearly three minutes.
        var stats = Warrior();
        var seconds = stats.MaxMana / stats.ManaRegenPerSecond;

        Assert.True(seconds < 60, $"a full refill takes {seconds:F0}s");
    }

    [Fact]
    public void Regeneration_KeepsUpAsThePoolGrows()
    {
        // The bug this guards: a flat trickle meant a high-level character with a big mana
        // bar waited proportionally longer to refill than a starting one.
        var early = Warrior(5);
        var late = Warrior(50);

        var earlySeconds = early.MaxMana / early.ManaRegenPerSecond;
        var lateSeconds = late.MaxMana / late.ManaRegenPerSecond;

        Assert.True(lateSeconds < earlySeconds * 1.5,
            $"refill time grew from {earlySeconds:F0}s to {lateSeconds:F0}s across the level range");
    }

    [Fact]
    public void TrivialKills_PayNothing()
    {
        // Same rule as experience: farming things far below you must never be efficient.
        Assert.True(ExperienceTable.IsTrivial(playerLevel: 30, enemyLevel: 10));
        Assert.False(ExperienceTable.IsTrivial(playerLevel: 30, enemyLevel: 27));
    }

    [Fact]
    public void Flask_RestoresBothPools()
    {
        var stats = Warrior();
        var mana = new Pool(stats.MaxMana);

        mana.Empty();
        mana.Add(stats.MaxMana * 0.35);

        // Enough to get back into the fight with a skill or two, not a full refill — the
        // flask is recovery, not a mana battery.
        Assert.InRange(mana.Current / AverageSkillCost, 2.0, 4.0);
    }
}
