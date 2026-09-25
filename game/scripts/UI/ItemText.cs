using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Kiln.Core.Foundation;
using Kiln.Core.Items;
using Kiln.Game.Items;

namespace Kiln.Game.UI;

/// <summary>
/// Turns an item into the words a player reads (ITM-09, FR-5.9).
/// </summary>
/// <remarks>
/// Every number here is described rather than named. "+12% damage" is a sentence a player can
/// act on; "DamagePct 0.12" is a debug dump that happens to be in a tooltip. Metin2's own
/// tooltips are largely the second kind, and the result is that most players never learn what
/// half their bonus lines do.
/// </remarks>
public static class ItemText
{
    /// <summary>
    /// The item's own description, and — when something comparable is worn — what changing to
    /// it would do (ITM-09).
    /// </summary>
    public static string Tooltip(ItemInstance item, ItemInstance? equipped = null) =>
        Describe(item, equipped, rich: false, playerLevel: int.MaxValue);

    /// <summary>
    /// The same description in colour, as BBCode for a RichTextLabel (REF-10): the name in its
    /// rarity's colour, a level the player has not reached in red, bonus lines in green, the
    /// comparison green where it gains and red where it loses.
    /// </summary>
    public static string RichTooltip(ItemInstance item, ItemInstance? equipped, int playerLevel) =>
        Describe(item, equipped, rich: true, playerLevel);

    // The colours the rich tooltip speaks in.
    private const string Plain = "d8dce2";
    private const string Muted = "8a929e";
    private const string Frame = "f2f2f2";
    private const string Grant = "9cc8ff";
    private const string Gain = "8fe39a";
    private const string Loss = "ff7b6b";
    private const string Gold = "f0c96a";

    private static string Describe(ItemInstance item, ItemInstance? equipped, bool rich, int playerLevel)
    {
        var spec = GameItems.Spec(item.DefId);
        var text = new StringBuilder();

        string C(string words, string colour) => rich ? $"[color=#{colour}]{Escape(words)}[/color]" : words;

        var name = GameItems.NameOf(item);
        var rarityColour = World.LootDrop.RarityColour(spec?.Rarity ?? Rarity.Common).ToHtml(false);

        text.Append(rich ? $"[font_size=17][b]{C(name, rarityColour)}[/b][/font_size]" : name);

        if (spec is null) return text.ToString();

        text.Append('\n').Append(C(Words.Of(spec.Rarity), Muted));

        if (spec.Slot is { } slot) text.Append(C(" · " + Words.Of(slot), Muted));

        // A level the player has not reached is the reason a click will not put it on.
        if (spec.LevelReq > 0)
        {
            text.Append(C(" · ", Muted)).Append(C(L10n.F("level {0}", spec.LevelReq), playerLevel < spec.LevelReq ? Loss : Muted));
        }

        // Frame numbers, already scaled by the upgrade level.
        if (spec.WeaponDamageMax > 0)
        {
            text.Append("\n\n").Append(C(L10n.F("Damage {0:0}–{1:0}", item.WeaponDamageMin(spec), item.WeaponDamageMax(spec)), Frame));
        }

        if (spec.ArmorValue > 0)
        {
            text.Append("\n\n").Append(C(L10n.F("Armour {0:0}", item.ArmorValue(spec)), Frame));
        }

        // What the item gives outright — a bracelet's attack, a ring's damage, an earring's
        // mana and healing — at its upgrade level. Left out, an accessory's tooltip showed
        // nothing of its own, and upgrading one looked like it did nothing at all.
        if (spec.Grants.Count > 0)
        {
            text.Append(spec.WeaponDamageMax > 0 || spec.ArmorValue > 0 ? "\n" : "\n\n");

            for (var i = 0; i < spec.Grants.Count; i++)
            {
                if (i > 0) text.Append('\n');
                text.Append(C(spec.Grants[i].Stat.Describe(item.ScaledGrant(spec.Grants[i])), Grant));
            }
        }

        if (item.UpgradeLevel > 0)
        {
            text.Append("  ").Append(C(L10n.F("(+{0:0}% from +{1})", UpgradeScaling.BonusPercent(item.UpgradeLevel), item.UpgradeLevel), Muted));
        }

        if (item.Bonuses.Count > 0)
        {
            text.Append('\n');

            foreach (var line in item.Bonuses)
            {
                text.Append('\n').Append(C((line.Locked ? "🔒 " : "") + line.Describe(), Gain));
            }
        }

        if (item.Sockets.Count > 0)
        {
            text.Append('\n');

            foreach (var socket in item.Sockets)
            {
                text.Append('\n').Append(C(DescribeSocket(socket), Muted));
            }
        }

        if (item.Count > 1) text.Append("\n\n").Append(C(L10n.F("{0} held", item.Count), Plain));

        if (spec.SellValue > 0)
        {
            text.Append("\n\n").Append(C(L10n.F("Sells for {0:N0} yang", (long)(spec.SellValue * ItemEconomy.VendorBuybackRate)), Gold));
        }

        if (equipped is not null && !ReferenceEquals(equipped, item))
        {
            AppendComparison(text, item, spec, equipped, rich);
        }

        return text.ToString();
    }

    /// <summary>Text made safe to sit inside BBCode: an item name must never open a tag.</summary>
    private static string Escape(string words) => words.Replace("[", "[lb]");

    /// <summary>
    /// What swapping would change, as signed lines.
    /// </summary>
    /// <remarks>
    /// Without this the player is comparing two lists of unrelated numbers in their head, and
    /// the honest answer to "is this better" is usually unknowable — a sword with more damage
    /// and fewer sockets against one with a vs-undead line is exactly the comparison the
    /// original leaves you to guess at. Showing the delta is what makes a gear decision a
    /// decision rather than a coin flip on the bigger number.
    /// </remarks>
    private static void AppendComparison(StringBuilder text, ItemInstance item, ItemSpec spec, ItemInstance equipped, bool rich)
    {
        var wornSpec = GameItems.Spec(equipped.DefId);

        if (wornSpec is null || wornSpec.Slot != spec.Slot) return;

        var lines = new List<(string Words, bool Gains)>();

        Line(lines, L10n.T("damage"), Average(item, spec), Average(equipped, wornSpec), "0");
        Line(lines, L10n.T("armour"), item.ArmorValue(spec), equipped.ArmorValue(wornSpec), "0");

        var mine = item.ModifiersFor(spec, GameItems.Catalogue);
        var theirs = equipped.ModifiersFor(wornSpec, GameItems.Catalogue);

        Line(lines, L10n.T("damage %"), mine.DamagePct * 100, theirs.DamagePct * 100, "0.#");
        Line(lines, L10n.T("skill damage %"), mine.SkillDamagePct * 100, theirs.SkillDamagePct * 100, "0.#");
        Line(lines, L10n.T("crit %"), mine.CritChance * 100, theirs.CritChance * 100, "0.#");
        Line(lines, L10n.T("pierce %"), mine.PierceChance * 100, theirs.PierceChance * 100, "0.#");
        Line(lines, L10n.T("evasion %"), mine.Evasion * 100, theirs.Evasion * 100, "0.#");
        Line(lines, L10n.T("max health"), mine.MaxHpFlat, theirs.MaxHpFlat, "0");
        Line(lines, L10n.T("max mana"), mine.MaxManaFlat, theirs.MaxManaFlat, "0");
        Line(lines, L10n.T("defence"), mine.DefenseFlat, theirs.DefenseFlat, "0");
        Line(lines, L10n.T("attack power"), mine.AttackPowerFlat, theirs.AttackPowerFlat, "0");
        Line(lines, L10n.T("attack speed"), mine.AttackSpeedFlat, theirs.AttackSpeedFlat, "0.#");
        Line(lines, L10n.T("move speed %"), mine.MoveSpeedPct * 100, theirs.MoveSpeedPct * 100, "0.#");
        Line(lines, L10n.T("healing after a kill %"), mine.KillRegenPct, theirs.KillRegenPct, "0.#");

        foreach (var family in mine.VsFamily.Keys.Union(theirs.VsFamily.Keys))
        {
            Line(lines, L10n.F("vs {0} %", BonusStat.FamilyName(family)),
                mine.VsFamilyBonus(family) * 100, theirs.VsFamilyBonus(family) * 100, "0.#");
        }

        var heading = L10n.F("— compared to {0} —", GameItems.NameOf(equipped));
        text.Append("\n\n").Append(rich ? $"[color=#{Muted}]{Escape(heading)}[/color]" : heading);

        if (lines.Count == 0)
        {
            var same = L10n.T("no difference");
            text.Append('\n').Append(rich ? $"[color=#{Muted}]{Escape(same)}[/color]" : same);
            return;
        }

        foreach (var (words, gains) in lines)
        {
            text.Append('\n').Append(rich ? $"[color=#{(gains ? Gain : Loss)}]{Escape(words)}[/color]" : words);
        }
    }

    private static double Average(ItemInstance item, ItemSpec spec) =>
        (item.WeaponDamageMin(spec) + item.WeaponDamageMax(spec)) / 2.0;

    private static void Line(List<(string Words, bool Gains)> into, string label, double mine, double theirs, string format)
    {
        var delta = mine - theirs;

        // Rounding noise would otherwise fill the panel with "+0" rows.
        if (Math.Abs(delta) < 0.05) return;

        into.Add(($"{(delta > 0 ? "+" : "−")}{Math.Abs(delta).ToString(format, L10n.Culture)} {label}", delta > 0));
    }

    private static string DescribeSocket(Socket socket)
    {
        if (!socket.IsOpen) return "◇ " + L10n.T("Sealed socket — needs a Boring Stone");
        if (socket.StoneId is null) return "◆ " + L10n.T("Empty socket");

        var stone = GameItems.Spec(socket.StoneId);
        var name = GameContent.IsLoaded && GameContent.Database.Items.TryGetValue(socket.StoneId, out var def)
            ? GameItems.Localise(def.Name)
            : socket.StoneId;

        if (stone is null || stone.Grants.Count == 0) return $"◆ {name}";

        return $"◆ {name} — {stone.Grants[0].Describe()}";
    }

    /// <summary>Short label for a grid cell: name plus stack size.</summary>
    public static string Label(ItemInstance item)
    {
        var name = GameItems.NameOf(item);

        return item.Count > 1 ? $"{name}\nx{item.Count}" : name;
    }
}
