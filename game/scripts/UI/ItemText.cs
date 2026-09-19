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
    public static string Tooltip(ItemInstance item)
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

        return text.ToString();
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
