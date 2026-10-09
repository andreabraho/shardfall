using Kiln.Core.Combat;

namespace Kiln.Core.Progression;

/// <summary>A level gained, for the caller to present.</summary>
public readonly record struct LevelUp(int NewLevel, int AttributePoints, int SkillPoints);

/// <summary>
/// The player's level, experience and unspent points (PRG-01/02).
/// <para>
/// Engine-free so the balance simulator can walk a whole campaign through exactly the same
/// code the game runs, which is the only way "no grinding required" can be a verified
/// property rather than a hope.
/// </para>
/// </summary>
public sealed class CharacterProgression
{
    private readonly List<LevelUp> _pending = [];

    public int Level { get; private set; } = 1;

    /// <summary>Experience accumulated toward the next level, not the lifetime total.</summary>
    public long Experience { get; private set; }

    public long ExperienceForNextLevel => ExperienceTable.ToNextLevel(Level);

    public double LevelProgress => ExperienceForNextLevel == 0
        ? 1.0
        : Math.Clamp(Experience / (double)ExperienceForNextLevel, 0, 1);

    public bool IsMaxLevel => Level >= ExperienceTable.MaxLevel;

    public int UnspentAttributePoints { get; private set; } = StartingAttributePoints;
    public int UnspentSkillPoints { get; private set; } = StartingSkillPoints;

    /// <summary>Points assigned by the player, on top of the starting values.</summary>
    public Attributes Assigned { get; private set; }

    /// <summary>Attribute points granted per level.</summary>
    public const int AttributePointsPerLevel = Attributes.PointsPerLevel;

    /// <summary>Skill points granted per level.</summary>
    public const int SkillPointsPerLevel = 1;

    /// <summary>
    /// Points a new character starts with, so the first skill can be bought before the first
    /// level-up rather than after it (REF-03).
    /// </summary>
    public const int StartingSkillPoints = 1;

    /// <summary>
    /// Status points a new character starts with (2026-09-27, at your call): the training's
    /// first lesson is spending them.
    /// </summary>
    public const int StartingAttributePoints = 3;

    /// <summary>
    /// The most an attribute can reach from points placed (2026-10-03, at your call, as in the original).
    /// What gear adds on top is not held to it.
    /// </summary>
    public const int MaxAttribute = 90;

    /// <summary>
    /// Grants experience, already adjusted for zone band by the caller. Returns every level
    /// gained, because a single large quest reward can cross more than one.
    /// </summary>
    public IReadOnlyList<LevelUp> Grant(long amount)
    {
        _pending.Clear();

        if (amount <= 0 || IsMaxLevel) return _pending;

        Experience += amount;

        while (!IsMaxLevel && Experience >= ExperienceForNextLevel)
        {
            Experience -= ExperienceForNextLevel;
            Level++;

            UnspentAttributePoints += AttributePointsPerLevel;
            UnspentSkillPoints += SkillPointsPerLevel;

            _pending.Add(new LevelUp(Level, UnspentAttributePoints, UnspentSkillPoints));
        }

        // At the cap experience stops accumulating rather than sitting in a bar that can
        // never fill.
        if (IsMaxLevel) Experience = 0;

        return _pending;
    }

    /// <summary>
    /// Takes back a share of the experience toward the current level — what dying costs
    /// (REF-02). Returns how much was actually lost, which is what the player is told.
    /// </summary>
    /// <remarks>
    /// The share is of the whole level, not of what has been earned so far, so the loss is the
    /// same however far along the bar is. It stops at empty: a death never takes a level, and
    /// never touches points already spent.
    /// </remarks>
    public long LoseExperience(double fraction)
    {
        if (fraction <= 0 || Experience <= 0) return 0;

        var lost = Math.Min(Experience, (long)Math.Round(ExperienceForNextLevel * Math.Clamp(fraction, 0, 1)));

        Experience -= lost;

        return lost;
    }

    /// <summary>
    /// Hands the player points outside a level-up: a quest that rewards one, and the debug
    /// tools that need a rank without the campaign behind it.
    /// </summary>
    public void GrantPoints(int attributePoints = 0, int skillPoints = 0)
    {
        UnspentAttributePoints += Math.Max(0, attributePoints);
        UnspentSkillPoints += Math.Max(0, skillPoints);
    }

    /// <summary>Spends one attribute point. False when there are none left, or the attribute is already at <see cref="MaxAttribute"/>.</summary>
    public bool SpendAttributePoint(AttributeKind kind)
    {
        if (UnspentAttributePoints <= 0 || IsCapped(kind)) return false;

        UnspentAttributePoints--;

        Assigned = kind switch
        {
            AttributeKind.Str => Assigned with { Str = Assigned.Str + 1 },
            AttributeKind.Dex => Assigned with { Dex = Assigned.Dex + 1 },
            AttributeKind.Int => Assigned with { Int = Assigned.Int + 1 },
            _ => Assigned with { Vit = Assigned.Vit + 1 },
        };

        return true;
    }

    public bool SpendSkillPoint()
    {
        if (UnspentSkillPoints <= 0) return false;

        UnspentSkillPoints--;
        return true;
    }

    /// <summary>
    /// Returns every assigned point for reallocation (FR-2.6).
    /// <para>
    /// Free and instant by design. In an MMO a ruined build is fixed by rolling a new
    /// character; offline it is a ruined save, so charging for a respec only punishes
    /// experimentation — which is the part of a build system worth encouraging.
    /// </para>
    /// </summary>
    public void Respec(int skillPointsInUse = 0)
    {
        RespecAttributes();
        ReturnSkillPoints(skillPointsInUse);
    }

    /// <summary>
    /// What a shrine asks, per level of the character, to give back the status points (2026-10-07, at
    /// your call: the two refunds apart, and priced — no longer free).
    /// </summary>
    public const long AttributeRespecPerLevel = 1000;

    /// <summary>What a shrine asks, per level, to give back the skill points: twice the status points'.</summary>
    public const long SkillRespecPerLevel = 2000;

    public long AttributeRespecCost => AttributeRespecPerLevel * Level;

    public long SkillRespecCost => SkillRespecPerLevel * Level;

    /// <summary>Returns every status point placed, and nothing else.</summary>
    public void RespecAttributes()
    {
        UnspentAttributePoints += Assigned.Total;
        Assigned = default;
    }

    /// <summary>Takes back the skill points a refunded skill book gave up.</summary>
    public void ReturnSkillPoints(int points)
    {
        UnspentSkillPoints += Math.Max(0, points);
    }

    /// <summary>Whether points can no longer raise this attribute.</summary>
    public bool IsCapped(AttributeKind kind) => kind switch
    {
        AttributeKind.Str => TotalAttributes.Str,
        AttributeKind.Dex => TotalAttributes.Dex,
        AttributeKind.Int => TotalAttributes.Int,
        _ => TotalAttributes.Vit,
    } >= MaxAttribute;

    /// <summary>Total attributes: the starting values plus everything assigned.</summary>
    public Attributes TotalAttributes => new(
        Attributes.StartingValue + Assigned.Str,
        Attributes.StartingValue + Assigned.Dex,
        Attributes.StartingValue + Assigned.Int,
        Attributes.StartingValue + Assigned.Vit);

    /// <summary>Restores saved state (doc 06 §9).</summary>
    public void Load(int level, long experience, int attributePoints, int skillPoints, Attributes assigned)
    {
        Level = Math.Clamp(level, 1, ExperienceTable.MaxLevel);
        Experience = Math.Max(0, experience);
        UnspentAttributePoints = Math.Max(0, attributePoints);
        UnspentSkillPoints = Math.Max(0, skillPoints);
        Assigned = assigned;
    }
}

public enum AttributeKind
{
    Str,
    Dex,
    Int,
    Vit,
}
