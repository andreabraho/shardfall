using System.Collections.Generic;
using Godot;
using Kiln.Core.Items;

namespace Kiln.Game.UI;

/// <summary>
/// The picture an item is drawn with in the bag (REF-10).
/// </summary>
/// <remarks>
/// Icons are white shapes on a clear ground, tinted with the item's rarity where they are
/// drawn, so one picture serves every rarity of a kind. An item with no icon is drawn with
/// its name instead, which is what every item was before there were icons.
/// </remarks>
public static class ItemIcons
{
    /// <summary>Where the icon files live, one per picture name.</summary>
    private const string Folder = "res://assets/icons/items/";

    private static readonly Dictionary<string, Texture2D?> Cache = [];

    /// <summary>The icon for an item, or null when it has none.</summary>
    public static Texture2D? For(string defId, ItemSpec spec) => Load(PictureOf(defId, spec));

    /// <summary>
    /// Which picture an item is drawn with: its own, by id, when there is one, else the one
    /// for its kind — a sword, a helmet, a ring.
    /// </summary>
    private static string PictureOf(string defId, ItemSpec spec)
    {
        if (ResourceLoader.Exists(Folder + defId + ".svg")) return defId;

        return defId[..System.Math.Max(0, defId.IndexOf('_'))] switch
        {
            // A great sword has its own picture once there is one, and the sword's until then.
            "wpn" => spec.Hands == 2 && ResourceLoader.Exists(Folder + "greatsword.svg") ? "greatsword" : "sword",
            "arm" => "armour",
            "hlm" => "helmet",
            "shd" => "shield",
            "bts" => "boots",
            "brc" => "bracelet",
            "nck" => "necklace",
            "ear" => "earring",
            "rng" => "ring",
            "stn" => "stone",
            "mat" => "material",
            _ => "",
        };
    }

    private static Texture2D? Load(string picture)
    {
        if (picture.Length == 0) return null;
        if (Cache.TryGetValue(picture, out var known)) return known;

        var path = Folder + picture + ".svg";
        var texture = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;

        Cache[picture] = texture;
        return texture;
    }
}
