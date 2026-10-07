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

