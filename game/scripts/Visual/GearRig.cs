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
/// <param name="SwordSkin">The sword skin worn (REF-23), on either blade, or null.</param>
/// <param name="ArmourSkin">The armour skin worn (REF-23), or null.</param>
/// <param name="Aura">The aura worn (REF-23), round either blade, or null.</param>
/// <remarks>
/// No shield (2026-09-25, at your call): as in the original, a shield is worn for what it
/// gives and never drawn.
/// </remarks>
/// <param name="ArmourUpgrade">The armour's upgrade level: from +8 it sparkles (2026-09-28).</param>
/// <param name="WeaponModel">
/// The weapon's own model (2026-09-29): every sword and great sword has one, made in Blender.
/// Null keeps the model's default blade for that hand.
/// </param>
/// <param name="ArmourModel">
/// The armour's own plates (2026-09-29): a scene of pieces named bone__&lt;bone&gt;, each hung on
/// that bone. Null wears none over the model's own body.
/// </param>
/// <param name="HelmetModel">
/// The helmet's own model (2026-09-30), pieces named the same way, worn in place of the model's
/// own helmet. Null keeps the model's own.
/// </param>
public sealed record GearLook(int Hands, int Upgrade, bool Helmet, bool Cape, CosmeticDef? SwordSkin = null, CosmeticDef? ArmourSkin = null, CosmeticDef? Aura = null, int ArmourUpgrade = 0, string? WeaponModel = null, string? ArmourModel = null, string? HelmetModel = null);

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

    private MeshInstance3D? _oneHand;
    private MeshInstance3D? _twoHand;

    /// <summary>The same blade in the other hand, for a body that fights with two (CLS-03).</summary>
    private readonly MeshInstance3D? _offHand;

    /// <summary>True for a body that holds a blade in each hand.</summary>
    public bool DualWield => _offHand is not null;

    /// <summary>The scene each hand holds now, so a blade is only swapped when the weapon's model changes.</summary>
    private string _oneHandPath;
    private string _twoHandPath;

    private readonly GearPartsDef _parts;

    /// <summary>The armour's plates on their bones, and the scene they came from.</summary>
    private readonly List<MeshInstance3D> _plates = [];
    private string _platesPath = "";

    /// <summary>The same for a modelled helmet.</summary>
    private readonly List<MeshInstance3D> _helm = [];
    private string _helmPath = "";
    private readonly List<MeshInstance3D> _helmet = [];
    private readonly MeshInstance3D? _cape;

    /// <summary>The unarmoured torso, hidden under a modelled armour.</summary>
    private readonly MeshInstance3D? _torso;

    /// <summary>The model's own meshes but the face: what an armour skin recolours.</summary>
    private readonly List<MeshInstance3D> _body = [];

    private string _armourSkin = "";
    private string? _aura;
    private readonly Node3D _model;
    private int _armourUpgrade = -1;

    private GearRig(Node3D model, GearPartsDef parts)
    {
        _model = model;
        _parts = parts;

        // Plain clothes in place of the model's painted armour, before anything is collected.
        Graft(model, parts);
        _torso = parts.Torso.Length > 0 ? model.FindChild(parts.Torso, recursive: true, owned: false) as MeshInstance3D : null;

        // Before the swords are put in its hands: those are not the armour.
        foreach (var mesh in model.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
        {
            if (mesh.Name != parts.Face) _body.Add(mesh);
        }

        _oneHand = Named(Part(model, parts.OneHand, WeaponHand));
        _offHand = parts.OffHand.Length > 0 && parts.Blade.Length > 0 ? Part(model, parts.Blade, parts.OffHand) : null;
        _twoHand = Named(Part(model, parts.TwoHand, WeaponHand));
        _oneHandPath = parts.OneHand;
        _twoHandPath = parts.TwoHand;
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
        // A body with a blade of its own (the Ninja's daggers, CLS-03) holds it whatever the
        // weapon, one in each hand.
        if (_parts.Blade.Length > 0) look = look with { Hands = look.Hands > 0 ? 1 : 0, WeaponModel = _parts.Blade };

        // And one that keeps its own clothes wears no plates over them.
        if (_parts.OwnClothes) look = look with { ArmourModel = null };

        if (_offHand is not null) _offHand.Visible = look.Hands > 0;

        // The weapon's own model in the hand that holds it; the other hand keeps what it has.
        var swapped = look.Hands switch
        {
            1 => Hold(ref _oneHand, ref _oneHandPath, look.WeaponModel ?? _parts.OneHand),
            2 => Hold(ref _twoHand, ref _twoHandPath, look.WeaponModel ?? _parts.TwoHand),
            _ => false,
        };

        // A new blade wears no aura yet: put the worn one on again below.
        if (swapped) _aura = "";

        if (_oneHand is not null) _oneHand.Visible = look.Hands == 1;
        if (_twoHand is not null) _twoHand.Visible = look.Hands == 2;

        // A modelled helmet is worn in place of the model's own.
        foreach (var piece in _helmet) piece.Visible = look.Helmet && look.HelmetModel is null;

        // A modelled armour covers the torso; without one the plain clothes show.
        if (_torso is not null) _torso.Visible = look.ArmourModel is null;

        // A modelled armour brings its own cape, if any: the model's would pass through its plates.
        if (_cape is not null) _cape.Visible = look.Cape && look.ArmourModel is null;

        // A sword skin is on both blades, so changing blade keeps it (REF-23).
        BladeSkin.Set(_oneHand, look.SwordSkin);
        BladeSkin.Set(_twoHand, look.SwordSkin);

        // So is the aura: round the sword, as the original's (2026-09-26), not round the feet.
        if (look.Aura?.Id != _aura)
        {
            _aura = look.Aura?.Id;
            BladeAura.Set(_oneHand, look.Aura);
            BladeAura.Set(_twoHand, look.Aura);
        }

        // The glow belongs to whichever blade is in hand, and leaves the other; with a skin on,
        // it is the skin's colour.
        Color? tint = look.SwordSkin is { } sword ? new Color(sword.Color) : null;
        WeaponGlow.Set(_oneHand, look.Hands == 1 ? look.Upgrade : 0, tint);
        WeaponGlow.Set(_twoHand, look.Hands == 2 ? look.Upgrade : 0, tint);

        DressArmour(look.ArmourSkin);
        DressPlates(look.ArmourModel, _plates, ref _platesPath);
        DressPlates(look.Helmet ? look.HelmetModel : null, _helm, ref _helmPath);

        // The same glints round the body for +8 and +9 armour (2026-09-28). Rebuilt only when
        // the level changes, so wearing something else does not restart them.
        var armourGlints = look.ArmourUpgrade >= 9 ? 9 : look.ArmourUpgrade >= 8 ? 8 : 0;

        if (armourGlints != _armourUpgrade)
        {
            _armourUpgrade = armourGlints;
            UpgradeSparkles.OnBody(_model, _body, armourGlints);
        }
    }

    /// <summary>
    /// Turns the blades between how they strike (0) and how they are carried (1): the rest grip
    /// of <see cref="GearPartsDef.RestGrip"/>, mirrored in the other hand. Nothing on a body without one.
    /// </summary>
    public void Carry(float amount)
    {
        if (_parts.RestGrip.Length < 3) return;

        var turn = _parts.RestGrip;

        foreach (var (blade, side) in new[] { (_oneHand, 1f), (_offHand, -1f) })
        {
            if (blade is null || !GodotObject.IsInstanceValid(blade)) continue;

            // The whole scene that was put in the hand, not just its mesh.
            Node3D held = blade;
            while (held.GetParent() is Node3D parent && parent is not BoneAttachment3D) held = parent;

            var rest = Quaternion.FromEuler(new Vector3(Mathf.DegToRad(turn[0]), Mathf.DegToRad(turn[1]) * side, Mathf.DegToRad(turn[2]) * side));

            held.Quaternion = Quaternion.Identity.Slerp(rest, amount);
        }
    }

    /// <summary>
    /// A ribbon behind the blade in hand (REF-21), from <paramref name="delay"/> seconds from
    /// now for <paramref name="seconds"/>. Nothing with no blade drawn.
    /// </summary>
    public void Trail(double delay, double seconds, Color colour)
    {
        var blade = _twoHand is { Visible: true } ? _twoHand : _oneHand is { Visible: true } ? _oneHand : null;

        BladeTrail.On(blade)?.Emit(delay, seconds, colour);
    }

    /// <summary>
    /// An armour skin (REF-23): the whole Knight but its face in the skin's recoloured texture —
    /// helmet and cape too — or back in its own with none.
    /// </summary>
    private void DressArmour(CosmeticDef? skin)
    {
        var id = skin?.Id ?? "";

        if (id == _armourSkin) return;

        _armourSkin = id;

        var texture = skin is not null && ResourceLoader.Exists(skin.Texture) ? ResourceLoader.Load<Texture2D>(skin.Texture) : null;

        if (skin is not null && texture is null) GD.PushWarning($"[cosmetic] '{skin.Id}' texture '{skin.Texture}' is missing.");

        foreach (var mesh in _body)
        {
            if (!GodotObject.IsInstanceValid(mesh) || mesh.Mesh is null) continue;

            for (var s = 0; s < mesh.Mesh.GetSurfaceCount(); s++)
            {
                if (texture is null || mesh.Mesh.SurfaceGetMaterial(s) is not BaseMaterial3D source || source.Duplicate() is not BaseMaterial3D copy)
                {
                    mesh.SetSurfaceOverrideMaterial(s, null);
                    continue;
                }

                copy.AlbedoTexture = texture;
                mesh.SetSurfaceOverrideMaterial(s, copy);
            }
        }
    }

    /// <summary>
    /// Puts the scene at <paramref name="wanted"/> in the weapon hand in place of
    /// <paramref name="blade"/>. False when it is already held or does not load, which keeps
    /// the blade there is.
    /// </summary>
    private bool Hold(ref MeshInstance3D? blade, ref string held, string wanted)
    {
        if (wanted.Length == 0 || wanted == held) return false;

        if (Named(Part(_model, wanted, WeaponHand)) is not { } fresh) return false;

        if (blade is not null && GodotObject.IsInstanceValid(blade))
        {
            // The whole scene that was put in the hand, not just its mesh.
            Node node = blade;
            while (node.GetParent() is { } parent && parent is not BoneAttachment3D) node = parent;

            node.GetParent()?.RemoveChild(node);
            node.QueueFree();
        }

        fresh.Visible = false;
        blade = fresh;
        held = wanted;
        return true;
    }

    /// <summary>
    /// A held blade's mesh named as a weapon, so what looks for the weapon by name — the Blade
    /// Aura's light — finds it whatever the model called it.
    /// </summary>
    private static MeshInstance3D? Named(MeshInstance3D? blade)
    {
        if (blade is not null && !blade.Name.ToString().Contains("sword") && !blade.Name.ToString().Contains("weapon"))
        {
            blade.Name = $"weapon_{blade.Name}";
        }

        return blade;
    }

    /// <summary>
    /// Hangs an armour's plates on the skeleton (2026-09-29): each piece called bone__&lt;bone&gt;
    /// goes on that bone, placed as it sat on the model at rest, so it follows every move.
    /// </summary>
    /// <remarks>
    /// The pieces were built round the model standing at rest, in the model's own space, so a
    /// piece's place on its bone is its place in the model undone by the bone's rest. Measured
    /// through the tree rather than in global space, so it holds before the model is shown.
    /// </remarks>
    private void DressPlates(string? path, List<MeshInstance3D> worn, ref string wornPath)
    {
        path ??= "";

        if (path == wornPath) return;

        wornPath = path;

        foreach (var plate in worn)
        {
            if (!GodotObject.IsInstanceValid(plate)) continue;

            plate.GetParent()?.RemoveChild(plate);
            plate.QueueFree();
        }

        worn.Clear();

        if (path.Length == 0) return;

        var skeleton = _model.FindChildren("*", "Skeleton3D", true, false).OfType<Skeleton3D>().FirstOrDefault();

        if (skeleton is null || ResourceLoader.Load<PackedScene>(path)?.Instantiate() is not Node3D suit)
        {
            GD.PushWarning($"[gear] armour '{path}' cannot be worn: no skeleton, or it did not load.");
            return;
        }

        var skeletonInModel = Within(skeleton, _model);

        foreach (var piece in suit.FindChildren("bone__*", "MeshInstance3D", true, false).OfType<MeshInstance3D>())
        {
            var bone = BoneOf(skeleton, piece.Name.ToString()["bone__".Length..]);

            if (bone < 0)
            {
                GD.PushWarning($"[gear] armour '{path}': no bone for '{piece.Name}'.");
                continue;
            }

            var place = Within(piece, suit);
            var rest = skeletonInModel * skeleton.GetBoneGlobalRest(bone);
            var slot = PlateSlot(skeleton, bone);

            piece.GetParent().RemoveChild(piece);
            piece.Owner = null;
            slot.AddChild(piece);
            piece.Transform = rest.AffineInverse() * place;
            worn.Add(piece);
        }

        suit.QueueFree();
    }

    /// <summary>
    /// Puts the unarmoured body of <see cref="GearPartsDef.BodyScene"/> on the model's skeleton
    /// in place of the model's own (2026-09-30). Both are the same rig, and the skins bind by
    /// bone name, so the grafted meshes follow every animation the model plays.
    /// </summary>
    private static void Graft(Node3D model, GearPartsDef parts)
    {
        if (parts.BodyScene.Length == 0) return;

        var skeleton = model.FindChildren("*", "Skeleton3D", true, false).OfType<Skeleton3D>().FirstOrDefault();

        if (skeleton is null || ResourceLoader.Load<PackedScene>(parts.BodyScene)?.Instantiate() is not Node3D donor)
        {
            GD.PushWarning($"[gear] body '{parts.BodyScene}' cannot be grafted: no skeleton, or it did not load.");
            return;
        }

        var donorSkeleton = donor.FindChildren("*", "Skeleton3D", true, false).OfType<Skeleton3D>().FirstOrDefault();

        foreach (var name in parts.BodyParts)
        {
            if (donor.FindChild(name, recursive: true, owned: false) is not MeshInstance3D mesh)
            {
                GD.PushWarning($"[gear] body '{parts.BodyScene}' has no part '{name}'.");
                continue;
            }

            var place = donorSkeleton is null ? mesh.Transform : Within(mesh, donorSkeleton);

            mesh.GetParent().RemoveChild(mesh);
            mesh.Owner = null;
            skeleton.AddChild(mesh);
            mesh.Transform = place;
            mesh.Skeleton = mesh.GetPathTo(skeleton);
        }

        donor.QueueFree();

        foreach (var name in parts.Replaces)
        {
            if (model.FindChild(name, recursive: true, owned: false) is not MeshInstance3D old) continue;

            old.GetParent().RemoveChild(old);
            old.QueueFree();
        }
    }

    /// <summary>A bone by its name as a node could carry it: "upperarm_l" is the bone "upperarm.l".</summary>
    private static int BoneOf(Skeleton3D skeleton, string name)
    {
        var bone = skeleton.FindBone(name);
        var dot = name.LastIndexOf('_');

        return bone >= 0 || dot < 0 ? bone : skeleton.FindBone(name[..dot] + "." + name[(dot + 1)..]);
    }

    private static BoneAttachment3D PlateSlot(Skeleton3D skeleton, int bone)
    {
        var name = $"Plates_{skeleton.GetBoneName(bone).Replace('.', '_')}";

        if (skeleton.GetNodeOrNull<BoneAttachment3D>(name) is { } slot) return slot;

        slot = new BoneAttachment3D { Name = name, BoneName = skeleton.GetBoneName(bone) };
        skeleton.AddChild(slot);
        return slot;
    }

    /// <summary>Where <paramref name="node"/> sits in <paramref name="ancestor"/>'s space, through the tree.</summary>
    private static Transform3D Within(Node3D node, Node ancestor)
    {
        var place = Transform3D.Identity;

        for (Node? n = node; n is not null && n != ancestor; n = n.GetParent())
        {
            if (n is Node3D spatial) place = spatial.Transform * place;
        }

        return place;
    }

    private IEnumerable<MeshInstance3D> All()
    {
        foreach (var part in new[] { _oneHand, _twoHand, _offHand, _cape })
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
