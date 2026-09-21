using Kiln.Core.Combat;
using Kiln.Core.Encounters;
using Kiln.Core.Foundation;

namespace Kiln.Game.UI;

/// <summary>
/// The words for the game's enums, in the player's language (UIX-06).
/// </summary>
/// <remarks>
/// Every value spelled out, never built from the enum's own name. <c>Rarity.Epic.ToString()</c>
/// is English that no translator ever sees; a literal here is found by the string scan and
/// lands in the translation table.
/// </remarks>
public static class Words
{
    public static string Of(Rarity rarity) => rarity switch
    {
        Rarity.Common => L10n.T("common"),
        Rarity.Fine => L10n.T("fine"),
        Rarity.Rare => L10n.T("rare"),
        Rarity.Epic => L10n.T("epic"),
        _ => L10n.T("relic"),
    };

    public static string Of(EquipSlot slot) => slot switch
    {
        EquipSlot.Weapon => L10n.T("weapon"),
        EquipSlot.Armor => L10n.T("armour"),
        EquipSlot.Helmet => L10n.T("helmet"),
        EquipSlot.Shield => L10n.T("shield"),
        EquipSlot.Boots => L10n.T("boots"),
        EquipSlot.Bracelet => L10n.T("bracelet"),
        EquipSlot.Necklace => L10n.T("necklace"),
        EquipSlot.Earring => L10n.T("earring"),
        _ => L10n.T("ring"),
    };

    public static string Of(StatusKind kind) => kind switch
    {
        StatusKind.Poison => L10n.T("Poisoned"),
        StatusKind.Bleed => L10n.T("Bleeding"),
        StatusKind.Stun => L10n.T("Stunned"),
        StatusKind.Slow => L10n.T("Slowed"),
        StatusKind.Weaken => L10n.T("Weakened"),
        _ => L10n.T("Vulnerable"),
    };

    public static string Of(ShardModifier modifier) => modifier switch
    {
        ShardModifier.Frenzied => L10n.T("Frenzied"),
        ShardModifier.Warded => L10n.T("Warded"),
        ShardModifier.Venomous => L10n.T("Venomous"),
        ShardModifier.Twin => L10n.T("Twin"),
        _ => "",
    };

    public static string Of(Kiln.Core.Progression.MasteryRank rank) => rank switch
    {
        Kiln.Core.Progression.MasteryRank.Normal => L10n.T("Normal"),
        Kiln.Core.Progression.MasteryRank.Master => L10n.T("Master"),
        Kiln.Core.Progression.MasteryRank.GrandMaster => L10n.T("Grand Master"),
        _ => L10n.T("Perfect"),
    };

    /// <summary>The three-letter attribute names on the character sheet.</summary>
    public static string Of(Kiln.Core.Progression.AttributeKind kind) => kind switch
    {
        Kiln.Core.Progression.AttributeKind.Str => L10n.T("STR"),
        Kiln.Core.Progression.AttributeKind.Dex => L10n.T("DEX"),
        Kiln.Core.Progression.AttributeKind.Int => L10n.T("INT"),
        _ => L10n.T("VIT"),
    };

    public static string Of(Difficulty tier) => tier switch
    {
        Difficulty.Wanderer => L10n.T("Wanderer"),
        Difficulty.Disciple => L10n.T("Disciple"),
        Difficulty.Adept => L10n.T("Adept"),
        _ => L10n.T("Shardbound"),
    };
}
