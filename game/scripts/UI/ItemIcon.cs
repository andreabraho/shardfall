using System.Collections.Generic;
using Godot;
using Kiln.Core.Foundation;

namespace Kiln.Game.UI;

/// <summary>
/// The stand-in for an item icon: a rarity-coloured tile, ringed when the item is worn.
/// </summary>
/// <remarks>
/// Art for items is deferred (ITM-13), so this draws the placeholder rather than waiting for
/// it. The point is the shape of the slot, not the picture in it: lists ask for a
/// <see cref="Texture2D"/> today and will ask for exactly the same thing when real icons
/// arrive, so the swap is a change of source and not a change of layout.
/// <para>
/// Worn is drawn as a ring around the tile rather than as a second column or a word in the
/// label. It has to survive being read at a glance down a list of twenty entries, and a
/// difference in outline is visible in peripheral vision where a word is not.
/// </para>
/// </remarks>
public static class ItemIcon
{
    private const int Size = 22;
    private const int Inset = 4;

    private static readonly Dictionary<(Rarity, bool), ImageTexture> Cache = [];

    private static readonly Color Ring = new(1f, 0.82f, 0.45f);

    /// <summary>The tile for a rarity, ringed when <paramref name="worn"/>.</summary>
    public static Texture2D For(Rarity rarity, bool worn)
    {
        if (Cache.TryGetValue((rarity, worn), out var cached)) return cached;

        var colour = World.LootDrop.RarityColour(rarity);
        var image = Image.CreateEmpty(Size, Size, false, Image.Format.Rgba8);

        image.Fill(new Color(0, 0, 0, 0));

        for (var x = 0; x < Size; x++)
        {
            for (var y = 0; y < Size; y++)
            {
                var edge = x < 2 || y < 2 || x >= Size - 2 || y >= Size - 2;

                if (worn && edge)
                {
                    image.SetPixel(x, y, Ring);
                    continue;
                }

                var inside = x >= Inset && y >= Inset && x < Size - Inset && y < Size - Inset;

                // The corners are cut so the tile reads as an object rather than as a swatch
                // of colour, which at this size is the whole difference between the two.
                var corner = (x - Inset) + (y - Inset) < 2
                    || (Size - 1 - Inset - x) + (y - Inset) < 2
                    || (x - Inset) + (Size - 1 - Inset - y) < 2
                    || (Size - 1 - Inset - x) + (Size - 1 - Inset - y) < 2;

                if (inside && !corner) image.SetPixel(x, y, colour with { A = 0.9f });
            }
        }

        var texture = ImageTexture.CreateFromImage(image);

        Cache[(rarity, worn)] = texture;

        return texture;
    }

    /// <summary>A slot's name as a person would write it.</summary>
    public static string NameOf(EquipSlot slot) => slot switch
    {
        EquipSlot.Ring1 => L10n.T("Ring 1"),
        EquipSlot.Ring2 => L10n.T("Ring 2"),
        _ => Words.Of(slot),
    };
}
