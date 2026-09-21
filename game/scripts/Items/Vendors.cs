using System.Collections.Generic;
using Kiln.Core.Economy;
using Kiln.Data.Definitions;

namespace Kiln.Game.Items;

/// <summary>
/// The merchants' shelves, kept across borders (ITM-10).
/// </summary>
/// <remarks>
/// Lives beside the character rather than in the village scene, so a shelf the player has
/// bought from stays bought from when they come back from a hunt, and a sale can still be
/// undone after a trip through the gate. Dropped with the character on a load or a new game:
/// the shelf is rebuilt from the seed and the level, so it comes back as it was, and the
/// buyback list holds items from a game that no longer exists.
/// </remarks>
public static class Vendors
{
    private static readonly Dictionary<string, Vendor> Open = new(System.StringComparer.Ordinal);

    /// <summary>The merchant's shelf, stocked for the player's current level.</summary>
    public static Vendor For(NpcDef npc)
    {
        if (!Open.TryGetValue(npc.Id, out var vendor))
        {
            var stock = npc.Stock ?? new StockDef();

            vendor = new Vendor(npc.Id, GameItems.Catalogue, stock.Staples, GameItems.Catalogue.Specs,
                stock.Rotating, stock.MaxRarity);

            Open[npc.Id] = vendor;
        }

        vendor.Restock(PlayerProfile.Progression.Level, GameSession.Seed, GameItems.Factory);

        return vendor;
    }

    public static void Reset() => Open.Clear();
}
