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

    public int UnspentAttributePoints { get; private set; }
    public int UnspentSkillPoints { get; private set; }

    /// <summary>Points assigned by the player, on top of the starting values.</summary>
    public Attributes Assigned { get; private set; }

    /// <summary>Attribute points granted per level.</summary>
    public const int AttributePointsPerLevel = Attributes.PointsPerLevel;

    /// <summary>Skill points granted per level.</summary>
    public const int SkillPointsPerLevel = 1;

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

    /// <summary>Spends one attribute point. False when there are none left.</summary>
    public bool SpendAttributePoint(AttributeKind kind)
    {
        if (UnspentAttributePoints <= 0) return false;

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
        UnspentAttributePoints += Assigned.Total;
        Assigned = default;

        UnspentSkillPoints += skillPointsInUse;
    }

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
