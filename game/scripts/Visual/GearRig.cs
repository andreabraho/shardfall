using System.Collections.Generic;
using Godot;
using Kiln.Core.Foundation;
using Kiln.Data.Definitions;

namespace Kiln.Game.Visual;

/// <summary>What the player wears, as far as the model can show it (REF-22).</summary>
/// <param name="Hands">0 with no weapon, 1 for a sword, 2 for a great sword.</param>
/// <param name="Upgrade">The weapon's upgrade level: from +7 it glows.</param>
/// <param name="Shield">The worn shield's rarity, or null with none.</param>
/// <param name="Helmet">Whether a helmet is worn.</param>
/// <param name="Cape">Whether the armour is epic or better.</param>
public sealed record GearLook(int Hands, int Upgrade, Rarity? Shield, bool Helmet, bool Cape);

/// <summary>
/// The parts of a model that stand for worn gear, shown and hidden to match it (REF-22).
/// </summary>
/// <remarks>
/// A character pack ships each figure holding everything it comes with — the Knight carries two
/// swords and four shields at once, one inside the other. Every part named in the visual's
/// data starts hidden, and only what the player wears is shown: the sword or the great sword,
/// the shield whose shape goes with its rarity, the helmet, and the cape over epic armour.
/// </remarks>
public sealed class GearRig
{
    private readonly MeshInstance3D? _oneHand;
    private readonly MeshInstance3D? _twoHand;
    private readonly List<MeshInstance3D> _shields = [];
    private readonly MeshInstance3D? _helmet;
    private readonly MeshInstance3D? _cape;

    private GearRig(Node3D model, GearPartsDef parts)
    {
        _oneHand = Part(model, parts.OneHand);
        _twoHand = Part(model, parts.TwoHand);
        _helmet = Part(model, parts.Helmet);
        _cape = Part(model, parts.Cape);

        foreach (var name in parts.Shields)
        {
            if (Part(model, name) is { } shield) _shields.Add(shield);
        }

        foreach (var part in All()) part.Visible = false;
    }

    /// <summary>
    /// Hides the parts a visual never shows, and returns the rig for one that wears gear
    /// (null for any other model).
    /// </summary>
    public static GearRig? Build(Node3D model, VisualDef def)
    {
        foreach (var name in def.Hide)
        {
            if (Part(model, name) is { } part) part.Visible = false;
        }

        return def.Gear is null ? null : new GearRig(model, def.Gear);
    }

    public void Wear(GearLook look)
    {
        if (_oneHand is not null) _oneHand.Visible = look.Hands == 1;
        if (_twoHand is not null) _twoHand.Visible = look.Hands == 2;

        // A great sword takes both hands, and a shield is not drawn clipping through its grip.
        var shield = look.Shield is { } rarity && look.Hands != 2 ? ShieldFor(rarity) : -1;

        for (var i = 0; i < _shields.Count; i++) _shields[i].Visible = i == shield;

        if (_helmet is not null) _helmet.Visible = look.Helmet;
        if (_cape is not null) _cape.Visible = look.Cape;

        // The glow belongs to whichever blade is in hand, and leaves the other.
        WeaponGlow.Set(_oneHand, look.Hands == 1 ? look.Upgrade : 0);
        WeaponGlow.Set(_twoHand, look.Hands == 2 ? look.Upgrade : 0);
    }

    /// <summary>Round for common and fine, then one shape per step up, the last for anything beyond.</summary>
    private int ShieldFor(Rarity rarity)
    {
        if (_shields.Count == 0) return -1;

        var step = rarity switch
        {
            Rarity.Common or Rarity.Fine => 0,
            Rarity.Rare => 1,
            Rarity.Epic => 2,
            _ => 3,
        };

        return Mathf.Min(step, _shields.Count - 1);
    }

    private IEnumerable<MeshInstance3D> All()
    {
        foreach (var part in new[] { _oneHand, _twoHand, _helmet, _cape })
        {
            if (part is not null) yield return part;
        }

        foreach (var shield in _shields) yield return shield;
    }

    private static MeshInstance3D? Part(Node3D model, string name)
    {
        if (name.Length == 0) return null;

        if (model.FindChild(name, recursive: true, owned: false) is MeshInstance3D part) return part;

        GD.PushWarning($"[gear] the model has no part '{name}'.");
        return null;
    }
}
