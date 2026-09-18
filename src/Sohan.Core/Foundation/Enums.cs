namespace Sohan.Core.Foundation;

/// <summary>
/// Monster families. The "damage vs family" bonus axis is one of the best parts of the
/// original's itemisation and is kept deliberately (doc 01 §2.6).
/// </summary>
public enum MonsterFamily
{
    Animal,
    Undead,
    Devil,
    Orc,
    Human,
    Mystic,
}

/// <summary>
/// Combat roles. These carry the tactical layer of the redesign (doc 02 §2.4): a wave is
/// composed, and solving it in the right kill order is the skill expression that replaces
/// dodge timing under click-to-move.
/// </summary>
public enum EnemyRole
{
    /// <summary>Straight damage; the baseline threat.</summary>
    Bruiser,

    /// <summary>Ranged pressure; forces the player to close distance.</summary>
    Archer,

    /// <summary>Projects damage reduction onto nearby allies. Kill first.</summary>
    Shielder,

    /// <summary>Heals the shard and elites. Kill second.</summary>
    Mender,

    /// <summary>Suicide AoE; must be pulled away from the fight.</summary>
    Bomber,
}

public enum Rarity
{
    Common,
    Fine,
    Rare,
    Epic,
    Relic,
}

public enum EquipSlot
{
    Weapon,
    Armor,
    Helmet,
    Shield,
    Boots,
    Bracelet,
    Necklace,
    Earring,
    Ring1,
    Ring2,
}

/// <summary>
/// Difficulty tiers (doc 02 §1). Changeable at any time from the menu (FR-11.4).
/// Note that difficulty NEVER changes drop tables or XP — only HP, damage, telegraph
/// duration and flask charges.
/// </summary>
public enum Difficulty
{
    Wanderer,
    Disciple,
    Adept,
    Shardbound,
}

public enum CharacterClass
{
    Warrior,
    Blade,
    Sura,
    Shaman,
}

public enum SkillTargeting
{
    Self,
    SelfAoe,
    SingleTarget,
    GroundAoe,
    Cone,
    Line,
}

public enum ObjectiveType
{
    Kill,
    Collect,
    Reach,
    Interact,
    Talk,
    Shard,
    Escort,
    Survive,
}

public enum QuestType
{
    Story,
    Side,
    Tutorial,
}

/// <summary>Telegraph shapes. The library is shared so BAL-03 can validate every one.</summary>
public enum TelegraphShape
{
    Circle,
    Cone,
    Line,
    Ring,
}
