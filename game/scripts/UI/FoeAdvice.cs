using System.Linq;
using Kiln.Core.Foundation;
using Kiln.Game.Combat;

namespace Kiln.Game.UI;

/// <summary>
/// What helps against a foe, for the tooltip over the target frame and the boss bar: its family, how
/// much the player's gear already adds against that family, and the stone that adds more.
/// </summary>
/// <remarks>
/// Every creature belongs to one family and every "against" bonus names one, so this is all a player needs
/// to choose stones and lines for a map: the family is the whole answer.
/// </remarks>
public static class FoeAdvice
{
    /// <summary>The advice, then what the foe drops: its table, by the chance each item has.</summary>
    /// <param name="dropTable">The foe's drop table, or null for none.</param>
    /// <param name="share">The share of each chance a kill pays: half from a foe far below the player.</param>
    public static string For(Combatant foe, Combatant? player, string? dropTable, double share = 1.0)
    {
        var advice = For(foe, player);
        var drops = Drops(dropTable, share);

        return drops.Length == 0 ? advice : advice + "\n\n" + drops;
    }

    /// <summary>The same for a creature: its own table, and its own share.</summary>
    public static string For(EnemyBrain enemy, Combatant? player)
    {
        var share = player is null ? 1.0 : Kiln.Core.Progression.ExperienceTable.LootShare(player.Stats.Level, enemy.Self.Stats.Level);

        return For(enemy.Self, player, enemy.DropTableId, share);
    }

    /// <summary>
    /// What a drop table gives (2026-10-07, at your call: "a way to see what it drops"): every item with its
    /// chance, the likeliest first, and the gan. A boss's or a shard's table also hands out a few items
    /// drawn among its gear; those are marked.
    /// </summary>
    public static string Drops(string? tableId, double share = 1.0)
    {
        if (tableId is null || !GameContent.IsLoaded || !GameContent.Database.DropTables.TryGetValue(tableId, out var table)) return "";

        if (table.Whole) share = 1.0;

        var lines = new System.Collections.Generic.List<string> { L10n.T("Drops:") };
        var culture = System.Globalization.CultureInfo.InvariantCulture;

        foreach (var entry in table.Entries.OrderByDescending(e => e.Chance))
        {
            var chance = System.Math.Min(1.0, entry.Chance * share) * 100;
            var mark = table.Picks > 0 && entry.Weight > 0 ? " ★" : "";
            var count = entry.CountRange.Length > 1 && entry.CountRange[1] > 1 ? $" ×{entry.CountRange[0]}–{entry.CountRange[1]}" : "";

            lines.Add($"  • {Items.GameItems.NameOfId(entry.Item)}{count}{mark}  {chance.ToString(chance < 1 ? "0.##" : "0.#", culture)}%");
        }

        if (table.Prize.Length > 0)
        {
            var chance = System.Math.Min(1.0, table.PrizeChance * share) * 100;
            var names = string.Join(", ", table.Prize.Select(Items.GameItems.NameOfId));

            lines.Add(L10n.F("One of {0}  {1}%", names, chance.ToString("0.#", culture)));
        }

        if (table.Picks > 0)
        {
            lines.Add(L10n.F("★ {0} more drawn among these every time.", System.Math.Max(1, (int)System.Math.Round(table.Picks * share))));
        }

        if (table.YangRange.Length > 1 && table.YangRange[1] > 0)
        {
            lines.Add(L10n.F("Gan: {0:N0}–{1:N0}", (long)(table.YangRange[0] * share), (long)(table.YangRange[1] * share)));
        }

        if (share < 1) lines.Add(L10n.T("Halved: it is far below your level."));

        return string.Join("\n", lines);
    }

    public static string For(Combatant foe, Combatant? player)
    {
        var family = foe.Stats.Family;
        var name = Kiln.Core.Items.BonusStat.FamilyName(family);
        var lines = new System.Collections.Generic.List<string>
        {
            L10n.F("Family: {0}", name),
            "",
            L10n.T("Useful bonuses:"),
            "  • " + L10n.F("damage against {0}", name),
        };

        // Humans are what players are; no stone is cut against them.
        if (family != MonsterFamily.Human)
        {
            lines.Add("  • " + Items.GameItems.Localise($"$item.stn_bane_{family.ToString().ToLowerInvariant()}.name"));
        }

        if (player is not null)
        {
            var now = player.Stats.Modifiers.VsFamilyBonus(family) * 100;
            lines.Add("");
            lines.Add(L10n.F("Yours now: +{0} % against {1}", now.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture), name));
        }

        return string.Join("\n", lines);
    }

    /// <summary>
    /// A name with a round "i" beside it, so the player can see there is something to hover
    /// (2026-10-07, at your call). The label stays centred: a blank the badge's width balances it.
    /// </summary>
    public static Godot.HBoxContainer Titled(Godot.Label title, out InfoBadge badge)
    {
        var row = new Godot.HBoxContainer { MouseFilter = Godot.Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 4);

        title.SizeFlagsHorizontal = Godot.Control.SizeFlags.ExpandFill;
        title.MouseFilter = Godot.Control.MouseFilterEnum.Ignore;

        badge = new InfoBadge();

        row.AddChild(new Godot.Control { CustomMinimumSize = new Godot.Vector2(InfoBadge.Size, 0), MouseFilter = Godot.Control.MouseFilterEnum.Ignore });
        row.AddChild(title);
        row.AddChild(badge);

        return row;
    }
}

