using System.Collections.Generic;
using Godot;
using Kiln.Core.Foundation;

namespace Kiln.Game.World;

/// <summary>
/// Strews kit pieces over an area: a wood around a village, grass across a meadow, rocks on a
/// slope (map themes, 2026-09-23).
/// </summary>
/// <remarks>
/// A wood is a hundred and fifty trees. Placed one by one they would bury a scene file under
/// coordinates nobody reads and nobody would move; described as "this many of these, in this
/// ring, keeping clear of these places" they are four lines, and moving the wood is changing a
/// radius.
/// <para>
/// Deterministic, from a seed: the same scene is the same wood on every load and every
/// machine. A wood that rearranged itself between sessions would move the path the player
/// learnt, and the navigation mesh baked around it.
/// </para>
/// <para>
/// It places ordinary <see cref="KitPiece"/>s, so everything a piece says about itself still
/// holds — what is solid, what the navigation bake carves around, what the map draws. Placed
/// under the navigation region it runs before the bake, because a parent's ready waits for its
/// children's.
/// </para>
/// </remarks>
public partial class ScatterNode : Node3D
{
    /// <summary>The pieces to strew, picked evenly.</summary>
    [Export] public string[] Pieces { get; set; } = [];

    /// <summary>How many to try to place. Fewer land when the area is too crowded for the spacing.</summary>
    [Export] public int Count { get; set; } = 40;

    /// <summary>The ring the pieces land in, around this node. An inner radius of zero is a disc.</summary>
    [Export] public float InnerRadius { get; set; }

    [Export] public float OuterRadius { get; set; } = 20f;

    /// <summary>Closest two pieces may stand, centre to centre.</summary>
    [Export] public float Spacing { get; set; } = 3f;

    [Export] public int Seed { get; set; } = 1;

    /// <summary>
    /// Places to keep clear, in world metres: x and z of the centre, and the radius in the
    /// third component — (x, z, radius). Roads, camps, gates.
    /// </summary>
    [Export] public Vector3[] Clearings { get; set; } = [];

    public override void _Ready()
    {
        if (Pieces.Length == 0 || Count <= 0 || OuterRadius <= InnerRadius) return;

        if (!GameContent.IsLoaded) return;

        var rng = new DeterministicRng((ulong)Seed).Fork($"scatter:{Name}");
        var placed = new List<Vector2>();
        var origin = new Vector2(GlobalPosition.X, GlobalPosition.Z);

        // Rejection sampling, with a ceiling: a crowded area yields fewer pieces rather than
        // an endless loop hunting for room that is not there.
        for (var attempt = 0; attempt < Count * 12 && placed.Count < Count; attempt++)
        {
            // Uniform over the ring's area, not its radius — sampled on the radius, a ring
            // piles its pieces up against the inner edge.
            var angle = rng.NextDouble() * Mathf.Tau;
            var inner = InnerRadius * InnerRadius;
            var outer = OuterRadius * OuterRadius;
            var radius = Mathf.Sqrt((float)(inner + (rng.NextDouble() * (outer - inner))));
            var local = new Vector2(Mathf.Cos((float)angle), Mathf.Sin((float)angle)) * radius;
            var world = origin + local;

            if (Blocked(world, placed)) continue;

            var id = Pieces[rng.NextInt(0, Pieces.Length)];

            if (!GameContent.Database.KitPieces.TryGetValue(id, out var def))
            {
                GD.PushError($"[scatter] '{Name}' names piece '{id}', which is not in the kit.");
                return;
            }

            var height = def.Size.Length == 3 ? (float)def.Size[1] : 1f;

            AddChild(new KitPiece
            {
                Name = $"{id}_{placed.Count}",
                PieceId = id,

                // Pieces are centred on their node, so standing one on the ground is half its
                // height up.
                Position = new Vector3(local.X, height * 0.5f, local.Y),
            });

            placed.Add(world);
        }
    }

    private bool Blocked(Vector2 at, List<Vector2> placed)
    {
        foreach (var clearing in Clearings)
        {
            if (at.DistanceTo(new Vector2(clearing.X, clearing.Y)) < clearing.Z) return true;
        }

        foreach (var other in placed)
        {
            if (at.DistanceTo(other) < Spacing) return true;
        }

        return false;
    }
}
