using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
    public static string Tooltip(ItemInstance item, ItemInstance? equipped = null)
    {
        var spec = GameItems.Spec(item.DefId);
        var text = new StringBuilder();

        text.Append(GameItems.NameOf(item));

        if (spec is null) return text.ToString();

        text.Append('\n').Append(spec.Rarity.ToString().ToLowerInvariant());

        if (spec.Slot is { } slot) text.Append(" · ").Append(slot.ToString().ToLowerInvariant());

        if (spec.LevelReq > 0) text.Append(" · level ").Append(spec.LevelReq);

        // Frame numbers, already scaled by the upgrade level.
        if (spec.WeaponDamageMax > 0)
        {
            text.Append("\n\nDamage ")
                .Append(item.WeaponDamageMin(spec).ToString("0"))
                .Append('–')
                .Append(item.WeaponDamageMax(spec).ToString("0"));
        }

        if (spec.ArmorValue > 0)
        {
            text.Append("\n\nArmour ").Append(item.ArmorValue(spec).ToString("0"));
        }

        if (item.UpgradeLevel > 0)
        {
            text.Append("  (+").Append(UpgradeScaling.BonusPercent(item.UpgradeLevel).ToString("0")).Append("% from +")
                .Append(item.UpgradeLevel).Append(')');
        }

        if (item.Bonuses.Count > 0)
        {
            text.Append('\n');

            foreach (var line in item.Bonuses)
            {
                text.Append('\n').Append(line.Locked ? "🔒 " : "").Append(line.Describe());
            }
        }

        if (item.Sockets.Count > 0)
        {
            text.Append("\n");

            foreach (var socket in item.Sockets)
            {
                text.Append('\n').Append(DescribeSocket(socket));
            }
        }

        if (item.Count > 1) text.Append("\n\n").Append(item.Count).Append(" held");

        if (spec.SellValue > 0)
        {
            text.Append("\n\nSells for ")
                .Append((long)(spec.SellValue * ItemEconomy.VendorBuybackRate))
                .Append(" yang");
        }

        if (equipped is not null && !ReferenceEquals(equipped, item))
        {
            AppendComparison(text, item, spec, equipped);
        }

        return text.ToString();
    }

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
    private static void AppendComparison(StringBuilder text, ItemInstance item, ItemSpec spec, ItemInstance equipped)
    {
        var wornSpec = GameItems.Spec(equipped.DefId);

        if (wornSpec is null || wornSpec.Slot != spec.Slot) return;

        var lines = new List<string>();

        Line(lines, "damage", Average(item, spec), Average(equipped, wornSpec), "0");
        Line(lines, "armour", item.ArmorValue(spec), equipped.ArmorValue(wornSpec), "0");

        var mine = item.ModifiersFor(spec, GameItems.Catalogue);
        var theirs = equipped.ModifiersFor(wornSpec, GameItems.Catalogue);

        Line(lines, "damage %", mine.DamagePct * 100, theirs.DamagePct * 100, "0.#");
        Line(lines, "skill damage %", mine.SkillDamagePct * 100, theirs.SkillDamagePct * 100, "0.#");
        Line(lines, "crit %", mine.CritChance * 100, theirs.CritChance * 100, "0.#");
        Line(lines, "pierce %", mine.PierceChance * 100, theirs.PierceChance * 100, "0.#");
        Line(lines, "evasion %", mine.Evasion * 100, theirs.Evasion * 100, "0.#");
        Line(lines, "max health", mine.MaxHpFlat, theirs.MaxHpFlat, "0");
        Line(lines, "max mana", mine.MaxManaFlat, theirs.MaxManaFlat, "0");
        Line(lines, "defence", mine.DefenseFlat, theirs.DefenseFlat, "0");
        Line(lines, "attack power", mine.AttackPowerFlat, theirs.AttackPowerFlat, "0");
        Line(lines, "attack speed", mine.AttackSpeedFlat, theirs.AttackSpeedFlat, "0.#");
        Line(lines, "move speed %", mine.MoveSpeedPct * 100, theirs.MoveSpeedPct * 100, "0.#");

        foreach (var family in mine.VsFamily.Keys.Union(theirs.VsFamily.Keys))
        {
            Line(lines, $"vs {family.ToString().ToLowerInvariant()}s %",
                mine.VsFamilyBonus(family) * 100, theirs.VsFamilyBonus(family) * 100, "0.#");
        }

        text.Append("\n\n— compared to ").Append(GameItems.NameOf(equipped)).Append(" —");

        if (lines.Count == 0)
        {
            text.Append("\nno difference");
            return;
        }

        foreach (var line in lines) text.Append('\n').Append(line);
    }

    private static double Average(ItemInstance item, ItemSpec spec) =>
        (item.WeaponDamageMin(spec) + item.WeaponDamageMax(spec)) / 2.0;

    private static void Line(List<string> into, string label, double mine, double theirs, string format)
    {
        var delta = mine - theirs;

        // Rounding noise would otherwise fill the panel with "+0" rows.
        if (Math.Abs(delta) < 0.05) return;

        into.Add($"{(delta > 0 ? "+" : "−")}{Math.Abs(delta).ToString(format)} {label}");
    }

    private static string DescribeSocket(Socket socket)
    {
        if (!socket.IsOpen) return "◇ Sealed socket — needs a Boring Stone";
        if (socket.StoneId is null) return "◆ Empty socket";

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
