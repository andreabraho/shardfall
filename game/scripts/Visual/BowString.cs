using Godot;

namespace Kiln.Game.Visual;

/// <summary>
/// The string of a bow in the hand, drawn every frame: from the top of the bow to the bottom, and while the
/// Archer draws, back to the right hand that pulls it (2026-10-07, at your call: "the string should be in the
/// other hand").
/// </summary>
/// <remarks>
/// The bow's model carries its string as a mesh of its own named <c>bowstring</c> (bowkit.py): straight, from tip
/// to tip. It is hidden here and its two ends are read from it, so a string drawn in Blender and the one in the
/// game always start and end in the same place, whatever the bow.
/// </remarks>
public partial class BowString : Node3D
{
    /// <summary>How quickly the string follows the hand back, and springs forward once loosed (shares a second).</summary>
    private const float Pull = 14f;
    private const float Spring = 30f;

    private MeshInstance3D _source = null!;
    private MeshInstance3D _upper = null!;
    private MeshInstance3D _lower = null!;
    private Vector3 _topLocal;
    private Vector3 _bottomLocal;
    private float _drawn;
    private Skeleton3D? _skeleton;
    private int _hand = -1;
    private ModelAnimator? _animator;
    private VisualRoot? _root;

    /// <summary>Puts a drawn string on the bow whose model holds <paramref name="source"/>.</summary>
    public static void On(Node3D bow, MeshInstance3D source)
    {
        if (bow.GetNodeOrNull<BowString>("BowString") is not null) return;

        var drawn = new BowString { Name = "BowString" };

        drawn._source = source;
        bow.AddChild(drawn);
    }

    public override void _Ready()
    {
        TopLevel = true;

        // The ends of the modelled string, in its own space: the long axis of its box.
        var box = _source.GetAabb();
        var centre = box.GetCenter();
        var axis = box.Size.X >= box.Size.Y && box.Size.X >= box.Size.Z ? Vector3.Right : box.Size.Y >= box.Size.Z ? Vector3.Up : Vector3.Back;
        var half = box.Size * axis.Abs() / 2;

        _topLocal = centre + half;
        _bottomLocal = centre - half;

        var radius = Mathf.Max(0.004f, Mathf.Min(box.Size.X, Mathf.Min(box.Size.Y, box.Size.Z)) / 2);
        var material = _source.GetActiveMaterial(0);

        _upper = Segment(radius, material);
        _lower = Segment(radius, material);
        _source.Visible = false;

        for (Node? node = GetParent(); node is not null; node = node.GetParent())
        {
            if (_skeleton is null && node is Skeleton3D skeleton) _skeleton = skeleton;
            if (node is VisualRoot root) { _root = root; break; }
        }

        _hand = _skeleton?.FindBone("handslot.r") ?? -1;
    }

    private MeshInstance3D Segment(float radius, Material? material)
    {
        var segment = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = radius, BottomRadius = radius, Height = 1f, RadialSegments = 6, Rings = 1 },
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };

        AddChild(segment);
        return segment;
    }

    public override void _Process(double delta)
    {
        if (!IsInstanceValid(_source)) return;

        var visible = _source.IsVisibleInTree() || (_source.GetParent() is Node3D { Visible: true } && GetParent() is Node3D { Visible: true });

        _upper.Visible = _lower.Visible = GetParent() is Node3D held && held.IsVisibleInTree();

        var frame = _source.GlobalTransform;
        var top = frame * _topLocal;
        var bottom = frame * _bottomLocal;
        var rest = (top + bottom) / 2;

        // Asked for each frame: the bow is put in the hand before the body has its animator.
        _animator ??= _root?.Animator;

        var drawing = _animator?.DrawingBow == true && _skeleton is not null && _hand >= 0;
        _drawn = Mathf.MoveToward(_drawn, drawing ? 1f : 0f, (float)delta * (drawing ? Pull : Spring));

        var nock = rest;

        if (_drawn > 0 && _skeleton is not null && _hand >= 0)
        {
            var hand = _skeleton.GlobalTransform * _skeleton.GetBoneGlobalPose(_hand).Origin;
            nock = rest.Lerp(hand, _drawn);
        }

        Place(_upper, top, nock);
        Place(_lower, nock, bottom);
    }

    /// <summary>Stretches a unit cylinder from one point to another.</summary>
    private static void Place(MeshInstance3D segment, Vector3 from, Vector3 to)
    {
        var length = from.DistanceTo(to);

        if (length < 0.0001f) return;

        var up = (to - from) / length;
        var side = Mathf.Abs(up.Dot(Vector3.Up)) > 0.95f ? Vector3.Right : Vector3.Up;
        var x = side.Cross(up).Normalized();
        var z = x.Cross(up).Normalized();

        segment.GlobalTransform = new Transform3D(new Basis(x, up * length, z), (from + to) / 2);
    }
}
