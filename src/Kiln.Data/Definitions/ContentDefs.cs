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
    public AbilityDef[] Abilities { get; init; } = [];
    public string Ai { get; init; } = string.Empty;
    public double AggroRadius { get; init; }
    public double LeashRadius { get; init; }
    public int Xp { get; init; }
    public string? DropTable { get; init; }
    public string? Visual { get; init; }
    public double VisualScale { get; init; } = 1.0;
    public string? VisualTint { get; init; }

    /// <summary>
    /// 0..1, taken off any chance to stun this creature (REF-01). 1 is immune: every boss at
    /// the end of a tower is, so a boss fight is never won by stun-locking it.
    /// </summary>
    public double StunResist { get; init; }

    /// <summary>What sets this creature apart from others of its role (REF-04).</summary>
    public EnemyTraitsDef Traits { get; init; } = new();

    /// <summary>
    /// A boss (REF-05): a boss plate and health bar, a leash measured from the player rather
    /// than from home, and only half its lost health back when it does give up.
    /// </summary>
    public bool Boss { get; init; }

    /// <summary>
    /// A unique boss: its health shows in the bar across the top of the screen. Ordinary
    /// bosses keep to their plate (2026-09-24): the bar is kept for the few that are an event.
    /// </summary>
    public bool BossBar { get; init; }

    /// <summary>What a boss does as its health falls, highest threshold first.</summary>
    public BossPhaseDef[] Phases { get; init; } = [];
}

/// <summary>
/// One turn in a boss fight, taken once when health first falls to <see cref="At"/> (REF-05).
/// Any mix of the effects; each defaults to off.
/// </summary>
public sealed class BossPhaseDef
{
    /// <summary>Health fraction at which the phase begins.</summary>
    public double At { get; init; }

    /// <summary>Creature called to the boss's side.</summary>
    public string? Adds { get; init; }

    public int AddCount { get; init; }

    /// <summary>Faster and harder-hitting from here on, by the boss's enrage traits.</summary>
    public bool Enrage { get; init; }

    /// <summary>Multiplies every cooldown from here on: 0.5 attacks twice as often.</summary>
    public double CooldownScale { get; init; } = 1.0;

    /// <summary>A status every blow inflicts from here on — Vaelith's frost.</summary>
    public StatusApplicationDef? Applies { get; init; }

    /// <summary>Vanishes and reappears at a distance from the player.</summary>
    public bool Teleport { get; init; }
}

/// <summary>
/// The small twists that make one creature play differently from another of the same role
/// (REF-04). Every one defaults to off, except the call for help every creature makes.
/// </summary>
public sealed class EnemyTraitsDef
{
    /// <summary>Metres within which others join the fight when this one is pulled or hit.</summary>
    public double CallRadius { get; init; } = 12.0;

    /// <summary>Never starts a fight: attacks only once it, or one of its pack, is hit.</summary>
    public bool Passive { get; init; }

    /// <summary>How many arrive together when a spawn field brings one in.</summary>
    public int Group { get; init; } = 1;

    /// <summary>Circles round to the player's side before closing in.</summary>
    public bool Flank { get; init; }

    /// <summary>Health fraction at which it enrages; 0 never does.</summary>
    public double EnrageBelow { get; init; }

    /// <summary>Move speed multiplier once enraged.</summary>
    public double EnrageSpeed { get; init; } = 1.3;

    /// <summary>Damage multiplier once enraged.</summary>
    public double EnrageDamage { get; init; } = 1.3;

    /// <summary>Fraction of a close hit's damage turned back on the player.</summary>
    public double Thorns { get; init; }

    /// <summary>Health fraction it gets back up with, once, after its first death. 0 stays down.</summary>
    public double Rise { get; init; }
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
    /// <summary>
    /// Roughly where this skill is meant to come in. It orders the skill screen and tells
    /// the player what it was written for — it gates nothing (REF-03): every skill can be
    /// bought at any level, and the points are the only thing standing between them.
    /// </summary>
    public int SuggestedLevel { get; init; }
    public double ManaCost { get; init; }
    public double Cooldown { get; init; }
    public string CastType { get; init; } = "instant";
    public SkillTargeting Targeting { get; init; }
    public double Radius { get; init; }

    /// <summary>How wide a cone-targeted skill opens, in degrees. Ignored by every other shape.</summary>
    public double ConeAngle { get; init; } = 100;

    /// <summary>
    /// Seconds between the hits of a multi-hit skill. Short by default, so a flurry reads as
    /// one blow landing several times; long for a skill whose hits should each be felt.
    /// </summary>
    public double HitInterval { get; init; } = 0.17;

    /// <summary>
    /// How the Warrior moves while casting it: empty for a swing, "spin" for a turn on the
    /// spot every hit. Named rather than keyed to a clip, so a model without the clip still
    /// has something to fall back to.
    /// </summary>
    public string Motion { get; init; } = "";
    public double DamageCoef { get; init; }
    public int Hits { get; init; } = 1;

    /// <summary>Seconds a buff or channel lasts. Zero for instant effects.</summary>
    public double Duration { get; init; }

    /// <summary>Strength of a non-damage effect: a damage reduction, a heal fraction, a shield size.</summary>
    public double Magnitude { get; init; }

    /// <summary>What each mastery rank changes, if anything.</summary>
    public SkillMasteryDef? Mastery { get; init; }

    /// <summary>
    /// A status the skill puts on what it hits (REF-01): Ground Slam's stun, Shield Bash's
    /// defence debuff. Physical skills control, mental skills weaken.
    /// </summary>
    public StatusApplicationDef? Applies { get; init; }
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
    public double? ConeAngle { get; init; }
    public double? Cooldown { get; init; }
    public double? ManaCost { get; init; }
    public double? DamageCoef { get; init; }
    public int? Hits { get; init; }
    public double? Duration { get; init; }
    public double? Magnitude { get; init; }

    /// <summary>Localisation key describing the change, for the skill tooltip.</summary>
    public string? Note { get; init; }
}

// ---------------------------------------------------------------------------
// Shards
// ---------------------------------------------------------------------------

/// <summary>
/// A shard tier (SHD-09). Tier changes wave composition, pulse pattern and loot rather than
/// only the size of the numbers — a tier 5 that is a tier 1 with more health teaches the
/// player nothing (doc 02 §3).
/// </summary>
public sealed class ShardDef : ContentDefBase
{
    public string Name { get; init; } = string.Empty;
    public int Tier { get; init; }
    public int Level { get; init; }
    public double Hp { get; init; }

    /// <summary>Seconds between pulses, measured from the end of the previous one.</summary>
    public double PulseInterval { get; init; } = 6.0;

    /// <summary>Wind-up at the Disciple baseline. Validated by BAL-03 like every telegraph.</summary>
    public double PulseWindup { get; init; } = 2.4;

    public double PulseRadius { get; init; } = 9.0;
    public double PulseDamageCoef { get; init; } = 1.0;
    public double ReclamationSeconds { get; init; } = 12.0;
    public double ReclamationHeal { get; init; } = 0.30;

    public ShardWaveDef[] Waves { get; init; } = [];

    /// <summary>Modifiers this node can roll on respawn. Empty means it is always plain.</summary>
    public string[] Modifiers { get; init; } = [];

    public string? DropTable { get; init; }
    public string? Visual { get; init; }

    /// <summary>Shard Essence granted on the break, before any modifier bonus.</summary>
    public int Essence { get; init; } = 1;

    /// <summary>
    /// Chance that the fight calls the map's boss into it at phase two (REF-06): what makes a
    /// stone something to walk up to carefully.
    /// </summary>
    public double BossChance { get; init; }
}

public sealed class ShardWaveDef
{
    /// <summary>one | two | three</summary>
    public string Phase { get; init; } = "one";

    public WaveSlotDef[] Slots { get; init; } = [];
}

public sealed class WaveSlotDef
{
    /// <summary>bruiser | archer | shielder | mender | bomber</summary>
    public string Role { get; init; } = "bruiser";

    public int Count { get; init; } = 1;

    /// <summary>Marks the add whose death interrupts the reclamation cast.</summary>
    public bool Anchor { get; init; }
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
// Villagers (QST-06)
// ---------------------------------------------------------------------------

/// <summary>
/// Someone standing in a village who can be talked to.
/// </summary>
/// <remarks>
/// Where they stand is the scene's business, like a shrine or a camp: the scene places an
/// <c>NpcNode</c> and names this id. What they say and sell is here.
/// </remarks>
public sealed class NpcDef : ContentDefBase
{
    public string Name { get; init; } = string.Empty;

    /// <summary>Shown under the name: "Elder", "Smith", "Merchant".</summary>
    public string Title { get; init; } = string.Empty;

    public NpcRole Role { get; init; }
    public string Zone { get; init; } = string.Empty;
    public string Visual { get; init; } = string.Empty;
    public string? Tint { get; init; }

    /// <summary>
    /// What they say, in order, one line per press of the key (FR-8.2). No choices.
    /// </summary>
    public string[] Lines { get; init; } = [];

    /// <summary>
    /// Quest id → what they say while that quest is the active one, before the usual lines.
    /// </summary>
    /// <remarks>How the elder points down the road without a journal.</remarks>
    public Dictionary<string, string> QuestLines { get; init; } = new(StringComparer.Ordinal);

    /// <summary>What a merchant sells. Null for everyone else.</summary>
    public StockDef? Stock { get; init; }
}

public sealed class StockDef
{
    /// <summary>Always on the shelf, in any quantity: the materials the benches eat.</summary>
    public string[] Staples { get; init; } = [];

    /// <summary>How many pieces of gear are on the shelf at once. They change on level-up.</summary>
    public int Rotating { get; init; }

    /// <summary>The best rarity the shelf ever carries. Better than this has to be found.</summary>
    public Rarity MaxRarity { get; init; } = Rarity.Fine;
}

// ---------------------------------------------------------------------------
// Sounds (AUD-01)
// ---------------------------------------------------------------------------

/// <summary>
/// One sound the game can make, by what it means rather than which file it is.
/// </summary>
/// <remarks>
/// Code says "a hit landed" (<c>snd_hit</c>); this row says which files that is today. The
/// same split as visuals: sourcing sounds (AUD-02) is a data edit, never a code edit, and a
/// sound with no files yet is simply silent.
/// </remarks>
public sealed class SoundDef : ContentDefBase
{
    /// <summary>res:// paths. One is picked at random each time, so repeats do not grate.</summary>
    public string[] Files { get; init; } = [];

    /// <summary>music | effects | ui</summary>
    public string Bus { get; init; } = "effects";

    /// <summary>Loudness relative to the bus, in decibels.</summary>
    public double VolumeDb { get; init; }

    /// <summary>Random pitch either side of normal, as a fraction: 0.08 is ±8%.</summary>
    public double PitchJitter { get; init; }

    /// <summary>How many copies may play at once; the oldest is cut off to make room.</summary>
    public int MaxVoices { get; init; } = 4;

    /// <summary>Heard from where it happens, fading with distance, rather than flat.</summary>
    public bool Positional { get; init; }

    /// <summary>Metres at which a positional sound is no longer heard.</summary>
    public double MaxDistance { get; init; } = 30;

    /// <summary>Music only: loops until something else is asked for.</summary>
    public bool Loop { get; init; }
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

    /// <summary>
    /// Degrees to turn the model about Y so that its front matches the engine's.
    /// </summary>
    /// <remarks>
    /// Godot treats -Z as forward and the motor turns bodies on that assumption, but a model
    /// faces wherever its author pointed it. A pack that faces +Z makes every character in
    /// the game walk backwards, which is exactly what the first one did.
    /// <para>
    /// Per-visual rather than a constant, because the next pack will have its own opinion and
    /// this is the only honest place to record whose.
    /// </para>
    /// </remarks>
    public double ModelYaw { get; init; }

    public double Height { get; init; } = 1.8;
    public double Radius { get; init; } = 0.4;

    /// <summary>
    /// Hex colour used for the placeholder material. Once a model is set, it is the colour the
    /// model glows in where something draws a glow — a shard's veins.
    /// </summary>
    public string Color { get; init; } = "#c0c0c0";

    /// <summary>
    /// "height" scales a model evenly to <see cref="Height"/> — right for anything with a body.
    /// "stretch" fills a column <see cref="Radius"/> wide and <see cref="Height"/> tall, for a
    /// rock that has to stand on end as a shard.
    /// </summary>
    public string Fit { get; init; } = "height";
}

// ---------------------------------------------------------------------------
// Drop tables
// ---------------------------------------------------------------------------

public sealed class DropTableDef : ContentDefBase
{
    public DropEntryDef[] Entries { get; init; } = [];

    /// <summary>
    /// Items drawn by weight on top of each entry's own chance: a boss's guaranteed drops
    /// (REF-05). Entries with no weight — its materials — are never drawn.
    /// </summary>
    public int Picks { get; init; }
    public int[] YangRange { get; init; } = [0, 0];
}

public sealed class DropEntryDef
{
    public string Item { get; init; } = string.Empty;
    public double Weight { get; init; }
    public double Chance { get; init; } = 1.0;
    public int[] CountRange { get; init; } = [1, 1];
}

// ---------------------------------------------------------------------------
// Zones — the world graph (WLD-01, WLD-02, WLD-03)
// ---------------------------------------------------------------------------

/// <summary>
/// A zone as content: who it is for, what it connects to, and the tuning of the things
/// inside it. Coordinates are deliberately absent — see <see cref="Kiln.Core.World.Zone"/>.
/// </summary>
public sealed class ZoneDef : ContentDefBase
{
    public string Name { get; init; } = string.Empty;

    /// <summary>hub | wilds | dungeon</summary>
    public string Kind { get; init; } = "wilds";

    /// <summary>[min, max] character level this zone is built for.</summary>
    public int[] LevelBand { get; init; } = [1, 1];

    /// <summary>res:// path to the scene that lays it out.</summary>
    public string Scene { get; init; } = string.Empty;

    public ZoneExitDef[] Exits { get; init; } = [];
    public ShrineDef[] Shrines { get; init; } = [];
    public SpawnFieldDef[] SpawnFields { get; init; } = [];

    /// <summary>The villages inside this map. The scene decides where they stand.</summary>
    public SafeRegionDef[] SafeRegions { get; init; } = [];

    /// <summary>Shard node ids this zone may host. The scene decides where they stand.</summary>
    public string[] Shards { get; init; } = [];

    /// <summary>
    /// How many shards stand on the map at once (REF-06). Each takes a random spot when it
    /// appears, and a new one every time it comes back: none has a fixed place.
    /// </summary>
    public int ShardCount { get; init; }

    /// <summary>
    /// The floors of a tower, in descending order (FR-7.11). Empty for every zone that is not
    /// a dungeon.
    /// </summary>
    /// <remarks>
    /// Nested in the zone rather than given its own table because a floor belongs to exactly
    /// one dungeon and always will, and the rules the validator enforces across them — five
    /// verbs, no repeat in a row, a boss every third — are rules about one zone's list. A flat
    /// table would have to reconstruct that grouping before it could check anything.
    /// </remarks>
    public FloorDef[] Floors { get; init; } = [];

    /// <summary>The music that plays here: a sound id with <c>loop</c> set. Empty for silence.</summary>
    public string Music { get; init; } = string.Empty;
}

/// <summary>
/// One floor of a tower: the task that opens the way down, and what furniture stands on it.
/// </summary>
/// <remarks>
/// Depth is the array order, not a field. A floor declaring it is the fourth while sitting
/// third in the list is a contradiction nobody can resolve from the data, and the list has an
/// order anyway.
/// </remarks>
public sealed class FloorDef
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;

    /// <summary>break | hold | find | carry | race | fight</summary>
    public string Task { get; init; } = "break";

    /// <summary>How many things must go down. A boss counts as one.</summary>
    public int Targets { get; init; } = 1;

    /// <summary>Lookalikes that score nothing. <c>find</c> only.</summary>
    public int Decoys { get; init; }

    /// <summary>Duration for <c>hold</c>, deadline for <c>race</c>.</summary>
    public double Seconds { get; init; }

    /// <summary>Enemy id for <c>fight</c>.</summary>
    public string Boss { get; init; } = string.Empty;

    /// <summary>What the floor keeps sending while its task runs.</summary>
    public string[] Waves { get; init; } = [];

    /// <summary>
    /// A shard id: the floor's targets are that stone rather than a seal (REF, Demon Tower).
    /// <c>break</c> and <c>race</c> only.
    /// </summary>
    public string Stone { get; init; } = string.Empty;

    /// <summary>How many hunters the floor keeps alive at all times, on top of its waves.</summary>
    public int Pack { get; init; }

    /// <summary>How many monsters each lantern lets out when it breaks. <c>find</c> only.</summary>
    public int Burst { get; init; }

    /// <summary>How many monsters guard each keystone; it cannot be broken until they are dead. <c>carry</c> only.</summary>
    public int Guard { get; init; }

    /// <summary>How many monsters the boss calls at 75, 50 and 25 per cent health. <c>fight</c> only.</summary>
    public int BossAdds { get; init; }

    /// <summary>Whether the boss enrages at half health — faster, harder. <c>fight</c> only.</summary>
    public bool Enrage { get; init; }

    /// <summary>How many arrive per wave.</summary>
    public int WaveSize { get; init; } = 3;

    /// <summary>Seconds between waves.</summary>
    public double WaveSeconds { get; init; } = 14.0;

    /// <summary>A shrine standing on this floor, or empty. Must be one the zone declares.</summary>
    public string Shrine { get; init; } = string.Empty;

    /// <summary>Whether an upgrade bench stands here (FR-7.14).</summary>
    public bool Bench { get; init; }

    /// <summary>
    /// Whether the bench here is the tower smith's, with one free upgrade (the original's smith):
    /// given on arriving from the floor below, or on finishing a boss floor.
    /// </summary>
    public bool Gift { get; init; }

    /// <summary>Whether this floor carries a corner nothing walks into (FR-7.18).</summary>
    public bool Refuge { get; init; }
}

/// <summary>
/// Safe ground inside a map (WLD-12): nothing spawns in it, nothing follows the player into
/// it, and nothing can hurt them while they stand in it.
/// </summary>
/// <remarks>
/// Nested in its zone for the same reason a shrine is: a village belongs to exactly one map,
/// and making that true by construction is cheaper than a rule enforcing it.
/// </remarks>
public sealed class SafeRegionDef
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;

    /// <summary>Metres from the marker the scene places. The walls are decoration; this is the promise.</summary>
    public double Radius { get; init; } = 20.0;
}

public sealed class ZoneExitDef
{
    public string To { get; init; } = string.Empty;
    public int RequiredLevel { get; init; }
    public string? RequiredQuest { get; init; }
}

/// <summary>
/// Nested in its zone rather than declared globally, so "a shrine belongs to exactly one
/// zone" is true by construction instead of being another rule the validator has to enforce.
/// </summary>
public sealed class ShrineDef
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;

    /// <summary>False for a dungeon checkpoint: it saves and respawns, but you cannot warp in.</summary>
    public bool FastTravel { get; init; } = true;
}

public sealed class SpawnFieldDef
{
    public string Id { get; init; } = string.Empty;
    public SpawnEntryDef[] Entries { get; init; } = [];

    /// <summary>How many stand here when the player is present.</summary>
    public int Count { get; init; } = 3;

    public double RespawnSeconds { get; init; } = 28.0;

    /// <summary>Beyond this the field stops putting creatures in the world.</summary>
    public double ActivationRadius { get; init; } = 55.0;

    /// <summary>Scatter radius around the field's centre.</summary>
    public double Radius { get; init; } = 7.0;
}

public sealed class SpawnEntryDef
{
    public string Enemy { get; init; } = string.Empty;
    public double Weight { get; init; } = 1.0;
}

// ---------------------------------------------------------------------------
// Greybox kit — world geometry as data (WLD-04, via the ENG-07 boundary)
// ---------------------------------------------------------------------------

/// <summary>
/// One reusable piece of world geometry: a wall segment, a ramp, a gate, a rock.
/// </summary>
/// <remarks>
/// The same boundary the character visuals use, applied to level geometry. A scene names
/// <c>kit_wall_straight</c> and never a mesh, so replacing the grey box with a modelled wall
/// is an edit to this file and to nothing else — including the villages, which will be built
/// almost entirely out of these.
/// </remarks>
public sealed class KitPieceDef : ContentDefBase
{
    /// <summary>box | ramp | cylinder | sphere | arch — or "model" once art exists.</summary>
    public string Shape { get; init; } = "box";

    /// <summary>Set only when <see cref="Shape"/> is "model": res:// path to the scene.</summary>
    public string? ModelPath { get; init; }

    /// <summary>[x, y, z] metres. A piece is authored at its real size so scenes need no scaling.</summary>
    public double[] Size { get; init; } = [1, 1, 1];

    public string Color { get; init; } = "#6b7280";

    /// <summary>False for decoration the player walks through — grass, banners, decals.</summary>
    public bool Solid { get; init; } = true;

    /// <summary>
    /// The shape the collider takes, when it differs from the shape that is drawn.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="Shape"/> because they are separate concerns, and the day
    /// they shared a field proved it: pointing the gate at a model changed its collision
    /// from two legs to one solid box, and sealed the village. What a piece looks like and
    /// what a player can walk through must be sayable independently.
    /// <para>
    /// Empty means "the same as what is drawn", which is right for every primitive. A piece
    /// with a model must state it, since a model says nothing about what should stop you.
    /// </para>
    /// </remarks>
    public string Collision { get; init; } = string.Empty;

    /// <summary>
    /// Whether the piece is baked into the navigation mesh. Solid-but-not-navigation is for
    /// anything placed after the bake; navigation-but-not-solid makes no sense and is rejected.
    /// </summary>
    public bool Navigation { get; init; } = true;

    /// <summary>
    /// How a model fills the piece's box: "stretch" to fill it exactly (walls, floors —
    /// anything that tiles), or "height" to scale evenly until it stands the box's height and
    /// keep its own proportions (trees, rocks, houses — anything a stretch would visibly bend).
    /// </summary>
    public string Fit { get; init; } = "stretch";

    /// <summary>
    /// Alternatives to <see cref="ModelPath"/>, one picked per placed piece. A tree line of
    /// five different trees is a wood; of one tree repeated, it is a pattern.
    /// </summary>
    /// <remarks>
    /// The pick comes from where the piece stands, never from a random number: the same scene
    /// must look the same every time it is loaded, and on everyone's machine.
    /// </remarks>
    public string[] Models { get; init; } = [];

    /// <summary>
    /// How many copies of the model make up the piece's length, side by side along X. A kit
    /// drawn on a two-metre grid fills an eight-metre wall with four modules rather than one
    /// module stretched four times as wide.
    /// </summary>
    public int Tile { get; init; } = 1;

    /// <summary>Turns each placed copy a different way about its own axis, again from where it stands.</summary>
    public bool RandomYaw { get; init; }

    /// <summary>
    /// The collider's size, when it should differ from the drawn box — a tree is stopped by
    /// its trunk, not by its crown. Empty means the box itself.
    /// </summary>
    public double[] CollisionSize { get; init; } = [];
}
