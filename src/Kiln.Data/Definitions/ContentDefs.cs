using Kiln.Core.Foundation;

namespace Kiln.Data.Definitions;

/// <summary>
/// Every definition type carries its own id and the file it came from, so a validation
/// error can point at the exact file the designer needs to open.
/// </summary>
public interface IContentDef
{
    string Id { get; }

    /// <summary>Set by the loader, not present in JSON.</summary>
    string SourceFile { get; set; }
}

public abstract class ContentDefBase : IContentDef
{
    public string Id { get; init; } = string.Empty;
    public string SourceFile { get; set; } = string.Empty;
    public override string ToString() => $"{GetType().Name}({Id})";
}

// ---------------------------------------------------------------------------
// Items
// ---------------------------------------------------------------------------

public sealed class ItemDef : ContentDefBase
{
    public string Name { get; init; } = string.Empty;
    public EquipSlot? Slot { get; init; }
    public string[] ClassRestriction { get; init; } = [];
    public int LevelReq { get; init; }
    public Rarity Rarity { get; init; }
    public int[] GridSize { get; init; } = [1, 1];
    public Dictionary<string, double> BaseStats { get; init; } = [];
    public int Sockets { get; init; }
    public int[] BonusLineCount { get; init; } = [0, 0];
    public string? BonusPool { get; init; }
    public string? UpgradePath { get; init; }
    public int SellValue { get; init; }
    public string? Visual { get; init; }

    /// <summary>How many fit in one grid cell. 1 means the item never stacks; equipment never does.</summary>
    public int MaxStack { get; init; } = 1;
}

public sealed class BonusPoolDef : ContentDefBase
{
    public BonusLineDef[] Lines { get; init; } = [];
}

public sealed class BonusLineDef
{
    public string Id { get; init; } = string.Empty;
    public string Stat { get; init; } = string.Empty;
    public double[] Range { get; init; } = [0, 0];
    public double Weight { get; init; }
}

public sealed class UpgradePathDef : ContentDefBase
{
    public UpgradeStepDef[] Steps { get; init; } = [];

    /// <summary>
    /// Must stay "consume_materials_only" for every standard path (doc 02 §4.1, FR-5.5).
    /// The validator enforces it: failure costs resources, never progress.
    /// </summary>
    public string OnFailure { get; init; } = "consume_materials_only";
}

public sealed class UpgradeStepDef
{
    public int To { get; init; }
    public int Yang { get; init; }
    public Dictionary<string, int> Mats { get; init; } = [];
    public double Chance { get; init; }

    /// <summary>Guaranteed success after this many consecutive failures. 0 = not applicable (chance is 1.0).</summary>
    public int Pity { get; init; }
}

// ---------------------------------------------------------------------------
// Enemies
// ---------------------------------------------------------------------------

public sealed class EnemyDef : ContentDefBase
{
    public string Name { get; init; } = string.Empty;
    public MonsterFamily Family { get; init; }
    public EnemyRole Role { get; init; }
    public int Level { get; init; }
    public EnemyStatsDef Stats { get; init; } = new();
    public double StaggerPool { get; init; }
    public AbilityDef[] Abilities { get; init; } = [];
    public string Ai { get; init; } = string.Empty;
    public double AggroRadius { get; init; }
    public double LeashRadius { get; init; }
    public int Xp { get; init; }
    public string? DropTable { get; init; }
    public string? Visual { get; init; }
    public double VisualScale { get; init; } = 1.0;
    public string? VisualTint { get; init; }
}

public sealed class EnemyStatsDef
{
    public double Hp { get; init; }
    public double AttackPower { get; init; }
    public double Defense { get; init; }
    public double MoveSpeed { get; init; }
}

public sealed class AbilityDef
{
    public string Id { get; init; } = string.Empty;
    public double Cooldown { get; init; }

    /// <summary>Wind-up in seconds at the Disciple baseline. Scaled per difficulty.</summary>
    public double Windup { get; init; }

    /// <summary>Null for untelegraphed melee swings; set for anything with a ground decal.</summary>
    public TelegraphDef? Telegraph { get; init; }

    public double DamageCoef { get; init; }

    /// <summary>
    /// Distance at which the ability can be used. Zero falls back to the enemy's melee
    /// reach, or the telegraph radius when it has one.
    /// <para>
    /// Kept separate from the telegraph radius because for a ranged placed attack the two
    /// are different things: an archer's volley is thrown eleven metres and explodes across
    /// three. Conflating them makes ranged roles unable to attack at all.
    /// </para>
    /// </summary>
    public double Range { get; init; }

    /// <summary>"self" centres the telegraph on the caster; "target" places it on the victim.</summary>
    public string Placement { get; init; } = "self";

    /// <summary>Status effect applied on hit, if any (FR-3.4).</summary>
    public StatusApplicationDef? Applies { get; init; }
}

/// <summary>A status an ability inflicts. Durations and magnitudes stay in data so a status
/// can be retuned without touching code.</summary>
public sealed class StatusApplicationDef
{
    /// <summary>poison | bleed | stun | slow | weaken | vulnerability</summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>Damage per tick for damage-over-time, or a fraction for slow/weaken.</summary>
    public double Magnitude { get; init; }

    public double Duration { get; init; } = 3.0;

    /// <summary>Probability of applying, 0..1. Defaults to always.</summary>
    public double Chance { get; init; } = 1.0;
}

/// <summary>
/// A telegraphed area. Validated by BAL-03 against player move speed so a mechanic is
/// always avoidable under click-to-move — see <c>TelegraphEscapeRule</c>.
/// </summary>
public sealed class TelegraphDef
{
    public TelegraphShape Shape { get; init; }

    /// <summary>Radius (circle/ring), length (line/cone) in metres.</summary>
    public double Radius { get; init; }

    /// <summary>Cone angle in degrees; ignored for other shapes.</summary>
    public double Angle { get; init; }
}

// ---------------------------------------------------------------------------
// Skills
// ---------------------------------------------------------------------------

public sealed class SkillDef : ContentDefBase
{
    public string Name { get; init; } = string.Empty;
    public CharacterClass Class { get; init; }
    public string Tree { get; init; } = string.Empty;
    public int UnlockLevel { get; init; }
    public double ManaCost { get; init; }
    public double Cooldown { get; init; }
    public string CastType { get; init; } = "instant";
    public SkillTargeting Targeting { get; init; }
    public double Radius { get; init; }
    public double DamageCoef { get; init; }
    public int Hits { get; init; } = 1;
    public double Stagger { get; init; }

    /// <summary>Seconds a buff or channel lasts. Zero for instant effects.</summary>
    public double Duration { get; init; }

    /// <summary>Strength of a non-damage effect: a damage reduction, a heal fraction, a shield size.</summary>
    public double Magnitude { get; init; }

    /// <summary>What each mastery rank changes, if anything.</summary>
    public SkillMasteryDef? Mastery { get; init; }
}

/// <summary>
/// Per-rank overrides for a skill.
/// <para>
/// Ranks are meant to change <em>how a skill behaves</em>, not just multiply its numbers
/// (doc 02 §5). A Whirlwind that gains a fourth rotation and then pulls enemies in is
/// growth the player can feel; one that quietly does 8% more is a spreadsheet entry.
/// </para>
/// </summary>
public sealed class SkillMasteryDef
{
    public SkillRankDef? Master { get; init; }
    public SkillRankDef? GrandMaster { get; init; }
    public SkillRankDef? Perfect { get; init; }
}

/// <summary>Values that replace the skill's defaults at a given rank. Null means unchanged.</summary>
public sealed class SkillRankDef
{
    public double? Radius { get; init; }
    public double? Cooldown { get; init; }
    public double? ManaCost { get; init; }
    public double? DamageCoef { get; init; }
    public int? Hits { get; init; }
    public double? Stagger { get; init; }
    public double? Duration { get; init; }
    public double? Magnitude { get; init; }

    /// <summary>Localisation key describing the change, for the skill tooltip.</summary>
    public string? Note { get; init; }
}

// ---------------------------------------------------------------------------
// Quests
// ---------------------------------------------------------------------------

public sealed class QuestDef : ContentDefBase
{
    public string Name { get; init; } = string.Empty;
    public int Act { get; init; }
    public QuestType Type { get; init; }
    public int LevelReq { get; init; }
    public string[] Prerequisites { get; init; } = [];
    public ObjectiveDef[] Objectives { get; init; } = [];
    public QuestRewardsDef Rewards { get; init; } = new();
    public string? Dialogue { get; init; }
}

public sealed class ObjectiveDef
{
    public ObjectiveType Type { get; init; }
    public string Target { get; init; } = string.Empty;
    public int Count { get; init; } = 1;
}

public sealed class QuestRewardsDef
{
    public int Xp { get; init; }
    public int Yang { get; init; }
    public string[] Items { get; init; } = [];

    /// <summary>
    /// A mechanical unlock: a recipe, a feature, a vendor, a companion ability.
    /// FR-8.4 requires every side quest to grant one of these or a non-trivial reward —
    /// no pure "kill 20 wolves" quests.
    /// </summary>
    public string? Unlock { get; init; }
}

// ---------------------------------------------------------------------------
// Visuals — the art-swap boundary (ENG-07, NFR-M.4)
// ---------------------------------------------------------------------------

/// <summary>
/// Maps a logical visual id (<c>mesh_placeholder_quadruped</c>) to whatever currently
/// renders it. Today that is a primitive; later it is a model path. Nothing in the game
/// code ever names a mesh directly, so swapping placeholders for real art is a data edit
/// and never a code edit.
/// </summary>
public sealed class VisualDef : ContentDefBase
{
    /// <summary>"capsule", "box", "cylinder", "sphere", "monolith" — or "model" once art exists.</summary>
    public string Primitive { get; init; } = "capsule";

    /// <summary>Set only when <see cref="Primitive"/> is "model": res:// path to the scene.</summary>
    public string? ModelPath { get; init; }

    public double Height { get; init; } = 1.8;
    public double Radius { get; init; } = 0.4;

    /// <summary>Hex colour used for the placeholder material; ignored once a model is set.</summary>
    public string Color { get; init; } = "#c0c0c0";
}

// ---------------------------------------------------------------------------
// Drop tables
// ---------------------------------------------------------------------------

public sealed class DropTableDef : ContentDefBase
{
    public DropEntryDef[] Entries { get; init; } = [];
    public int[] YangRange { get; init; } = [0, 0];
}

public sealed class DropEntryDef
{
    public string Item { get; init; } = string.Empty;
    public double Weight { get; init; }
    public double Chance { get; init; } = 1.0;
    public int[] CountRange { get; init; } = [1, 1];
}
