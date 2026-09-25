using System.Collections.Generic;
using System.Linq;
using Godot;
using Kiln.Core.Foundation;
using Kiln.Data.Definitions;

namespace Kiln.Game.Visual;

/// <summary>What the player wears, as far as the model can show it (REF-22).</summary>
/// <param name="Hands">0 with no weapon, 1 for a sword, 2 for a great sword.</param>
/// <param name="Upgrade">The weapon's upgrade level: from +7 it glows.</param>
/// <param name="Helmet">Whether a helmet is worn.</param>
/// <param name="Cape">Whether the armour is epic or better.</param>
/// <remarks>
/// No shield (2026-09-25, at your call): as in the original, a shield is worn for what it
/// gives and never drawn.
/// </remarks>
public sealed record GearLook(int Hands, int Upgrade, bool Helmet, bool Cape);

/// <summary>
/// The parts of a model that stand for worn gear, shown and hidden to match it (REF-22).
/// </summary>
/// <remarks>
/// A part is either a mesh the model already has — the Knight's helmet and cape — or a
/// separate scene put in a hand: KayKit 2.0 ships its swords as files of their own, made to
/// sit on the skeleton's hand slots. Every part starts hidden, and only what the player wears
/// is shown: the sword or the great sword, the helmet, and the cape over epic armour.
/// </remarks>
public sealed class GearRig
{
    /// <summary>The bone a weapon is held on.</summary>
    private const string WeaponHand = "handslot.r";

    private readonly MeshInstance3D? _oneHand;
    private readonly MeshInstance3D? _twoHand;
    private readonly List<MeshInstance3D> _helmet = [];
    private readonly MeshInstance3D? _cape;

    private GearRig(Node3D model, GearPartsDef parts)
    {
        _oneHand = Part(model, parts.OneHand, WeaponHand);
        _twoHand = Part(model, parts.TwoHand, WeaponHand);
        _cape = Part(model, parts.Cape, WeaponHand);

        foreach (var name in parts.Helmet)
        {
            if (Part(model, name, WeaponHand) is { } piece) _helmet.Add(piece);
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
            if (model.FindChild(name, recursive: true, owned: false) is MeshInstance3D part) part.Visible = false;
        }

        return def.Gear is null ? null : new GearRig(model, def.Gear);
    }

    public void Wear(GearLook look)
    {
        if (_oneHand is not null) _oneHand.Visible = look.Hands == 1;
        if (_twoHand is not null) _twoHand.Visible = look.Hands == 2;

        foreach (var piece in _helmet) piece.Visible = look.Helmet;

        if (_cape is not null) _cape.Visible = look.Cape;

        // The glow belongs to whichever blade is in hand, and leaves the other.
        WeaponGlow.Set(_oneHand, look.Hands == 1 ? look.Upgrade : 0);
        WeaponGlow.Set(_twoHand, look.Hands == 2 ? look.Upgrade : 0);
    }

    private IEnumerable<MeshInstance3D> All()
    {
        foreach (var part in new[] { _oneHand, _twoHand, _cape })
        {
            if (part is not null) yield return part;
        }

        foreach (var piece in _helmet) yield return piece;
    }

    /// <summary>
    /// A part by name: a mesh of the model's own, or — for a res:// path — that scene put on
    /// <paramref name="bone"/>, returned as its mesh.
    /// </summary>
    private static MeshInstance3D? Part(Node3D model, string name, string bone)
    {
        if (name.Length == 0) return null;

        if (!name.StartsWith("res://"))
        {
            if (model.FindChild(name, recursive: true, owned: false) is MeshInstance3D own) return own;

            GD.PushWarning($"[gear] the model has no part '{name}'.");
            return null;
        }

        var skeleton = model.FindChildren("*", "Skeleton3D", true, false).OfType<Skeleton3D>().FirstOrDefault();

        if (skeleton is null || skeleton.FindBone(bone) < 0)
        {
            GD.PushWarning($"[gear] the model has no bone '{bone}' to hold '{name}'.");
            return null;
        }

        if (ResourceLoader.Load<PackedScene>(name)?.Instantiate() is not Node3D held)
        {
            GD.PushWarning($"[gear] '{name}' did not load.");
            return null;
        }

        // One attachment per hand, shared by everything that hand can hold.
        var slot = skeleton.GetNodeOrNull<BoneAttachment3D>(bone);

        if (slot is null)
        {
            slot = new BoneAttachment3D { Name = bone, BoneName = bone };
            skeleton.AddChild(slot);
        }

        slot.AddChild(held);

        return held as MeshInstance3D ?? held.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>().FirstOrDefault();
    }
}
