using System.Linq;
using Godot;
using Kiln.Game.World;

namespace Kiln.Game.UI;

/// <summary>
/// Draws the current zone: the ground you have walked, the shape of what is on it, and the
/// handful of things worth walking to (WLD-08).
/// </summary>
/// <remarks>
/// Drawn from the live scene rather than from a hand-authored map image or a second
/// description of the level. Every wall on this map is a <see cref="KitPiece"/> that is
/// actually standing in the world, every camp is a spawn field that will actually spawn, and
/// every border is a gate that will actually take you somewhere. A map maintained separately
/// from the level is a map that goes stale the first time somebody moves a wall, and a
/// greybox gets its walls moved constantly.
/// <para>
/// A schematic rather than a rendered top-down view. A second camera would cost a full extra
/// render of the scene to produce a picture of grey boxes seen from above — which is what
/// the player is already looking at. Shapes and colours taken from the kit data read better
/// and cost a few hundred polygons.
/// </para>
/// </remarks>
public partial class MapView : Control
{
    /// <summary>Metres per side of one square of memory. Coarse enough that walking a road reveals its width.</summary>
    public const float CellSize = 6.0f;

    /// <summary>How far the character notices ground, in metres. Roughly what a creature can see of them.</summary>
    public const float RevealRadius = 26.0f;

    private static readonly Color Background = new(0.07f, 0.075f, 0.09f);
    private static readonly Color Explored = new(0.16f, 0.17f, 0.19f);
    private static readonly Color SafeTint = new(0.36f, 0.72f, 0.42f);
    private static readonly Color CampTint = new(0.85f, 0.38f, 0.34f);
    private static readonly Color ShrineTint = new(0.45f, 0.82f, 0.86f);
    private static readonly Color VillagerTint = new(0.9f, 0.84f, 0.66f);
    private static readonly Color ShardTint = new(0.82f, 0.52f, 0.95f);
    private static readonly Color GateTint = new(0.62f, 0.82f, 1f);
    private static readonly Color PlayerTint = new(0.98f, 0.86f, 0.42f);
    private static readonly Color QuestTint = new(1f, 0.74f, 0.2f);
    private static readonly Color Faint = new(0.48f, 0.53f, 0.58f);

    private float _scale = 1f;
    private Vector2 _centre;

    public override void _Draw()
    {
        var area = new Rect2(Vector2.Zero, Size);

        DrawRect(area, Background);

        if (!GameWorld.IsLoaded) return;

        Frame();

        var seen = Memory();

        DrawExplored(seen);
        DrawTerrain(seen);
        DrawRegions(seen);
        DrawMarkers(seen);
        DrawQuest();
        DrawPlayer();
    }

    // ------------------------------------------------------------------ framing

    /// <summary>
    /// Fits the whole zone in the panel.
    /// </summary>
    /// <remarks>
    /// Measured from what is in the scene rather than from the ground plane's size, because
    /// the ground is deliberately larger than the playable area on every map — framing to it
    /// would put the content in the middle of a wide empty border.
    /// </remarks>
    private void Frame()
    {
        var min = new Vector2(float.MaxValue, float.MaxValue);
        var max = new Vector2(float.MinValue, float.MinValue);
        var found = false;

        void Include(Vector3 at)
        {
            var p = new Vector2(at.X, at.Z);
            min = new Vector2(Mathf.Min(min.X, p.X), Mathf.Min(min.Y, p.Y));
            max = new Vector2(Mathf.Max(max.X, p.X), Mathf.Max(max.Y, p.Y));
            found = true;
        }

        foreach (var node in GetTree().GetNodesInGroup("kit"))
        {
            if (node is Node3D piece) Include(piece.GlobalPosition);
        }

        foreach (var group in new[] { "zone_gates", "shrines", "shards", "spawn_fields", "safe_zones" })
        {
            foreach (var node in GetTree().GetNodesInGroup(group))
            {
                if (node is Node3D marker) Include(marker.GlobalPosition);
            }
        }

        if (!found)
        {
            _scale = 2f;
            _centre = Vector2.Zero;
            return;
        }

        const float pad = 10f;

        min -= new Vector2(pad, pad);
        max += new Vector2(pad, pad);

        var span = max - min;

        _centre = (min + max) * 0.5f;
        _scale = Mathf.Min(Size.X / Mathf.Max(span.X, 1f), Size.Y / Mathf.Max(span.Y, 1f));
    }

    /// <summary>World metres to panel pixels. North is up, which is world -Z.</summary>
    private Vector2 At(Vector3 world) => At(new Vector2(world.X, world.Z));

    private Vector2 At(Vector2 world) => (world - _centre) * _scale + Size * 0.5f;

    // ------------------------------------------------------------------ memory

    private System.Collections.Generic.HashSet<long> Memory() =>
        PlayerProfile.Explored.TryGetValue(GameWorld.CurrentZoneId, out var seen) ? seen : [];

    /// <summary>Packs a cell into one key. The offset keeps negative coordinates out of the sign bit.</summary>
    public static long Key(int cx, int cz) => ((long)(cx + 8192) << 20) | (uint)(cz + 8192);

    public static long CellOf(Vector3 at) =>
        Key(Mathf.FloorToInt(at.X / CellSize), Mathf.FloorToInt(at.Z / CellSize));

    private static bool Known(System.Collections.Generic.HashSet<long> seen, Vector3 at) =>
        seen.Contains(CellOf(at));

    private void DrawExplored(System.Collections.Generic.HashSet<long> seen)
    {
        var side = CellSize * _scale;

        foreach (var key in seen)
        {
            var cx = (int)(key >> 20) - 8192;
            var cz = (int)(key & 0xFFFFF) - 8192;
            var corner = At(new Vector2(cx * CellSize, cz * CellSize));

            // A pixel of overlap, so the grid reads as one lit region rather than as tiles.
            DrawRect(new Rect2(corner, new Vector2(side + 1, side + 1)), Explored);
        }
    }

    // ------------------------------------------------------------------ the zone

    private void DrawTerrain(System.Collections.Generic.HashSet<long> seen)
    {
        foreach (var node in GetTree().GetNodesInGroup("kit"))
        {
            if (node is not KitPiece piece || piece.Piece is not { } def) continue;

            // Only what stops you. A shrub and a banner are things to look at, not things to
            // plan around, and a map that draws them is a map you have to read past.
            if (!def.Solid || !Known(seen, piece.GlobalPosition)) continue;

            var colour = Color.HtmlIsValid(def.Color.TrimStart('#'))
                ? new Color(def.Color) with { A = 0.9f }
                : Faint;

            var size = def.Size.Length == 3
                ? new Vector3((float)def.Size[0], (float)def.Size[1], (float)def.Size[2])
                : Vector3.One;

            // By the collision shape, not the visual one. Since the kit became models every
            // piece's shape reads "model", and the map drew the village gate as a solid wall —
            // the same coupling that once sealed the gate in the world.
            var form = def.Collision.Length > 0 ? def.Collision : def.Shape;

            if (form is "cylinder" or "sphere")
            {
                DrawCircle(At(piece.GlobalPosition), Mathf.Max(size.X * 0.5f * _scale, 1.5f), colour);
                continue;
            }

            if (form == "arch")
            {
                // Its two legs, not its outline. An arch drawn as a block is a wall on the
                // map and a doorway in the world, which is the one thing a map must not do.
                var leg = size.X * 0.18f;
                var inset = (size.X - leg) * 0.5f;

                Footprint(piece, new Vector3(leg, 0, size.Z), new Vector3(-inset, 0, 0), colour);
                Footprint(piece, new Vector3(leg, 0, size.Z), new Vector3(inset, 0, 0), colour);
                continue;
            }

            Footprint(piece, size, Vector3.Zero, colour);
        }
    }

    /// <summary>Draws a box piece as the quadrilateral it actually covers, rotation included.</summary>
    private void Footprint(Node3D piece, Vector3 size, Vector3 offset, Color colour)
    {
        var basis = piece.GlobalBasis;
        var centre = piece.GlobalPosition + basis * offset;
        var x = basis.X * (size.X * 0.5f);
        var z = basis.Z * (size.Z * 0.5f);

        DrawColoredPolygon(
        [
            At(centre - x - z),
            At(centre + x - z),
            At(centre + x + z),
            At(centre - x + z),
        ], colour);
    }

    private void DrawRegions(System.Collections.Generic.HashSet<long> seen)
    {
        foreach (var node in GetTree().GetNodesInGroup("safe_zones"))
        {
            if (node is not SafeZoneNode marker) continue;

            var region = GameWorld.Graph.SafeRegion(marker.RegionId);

            if (region is null || !Known(seen, marker.GlobalPosition)) continue;

            Ring(marker.GlobalPosition, (float)region.Radius, SafeTint, 0.14f);
        }

        foreach (var node in GetTree().GetNodesInGroup("spawn_fields"))
        {
            if (node is not SpawnFieldNode field || field.Def is not { } def) continue;

            if (!Known(seen, field.GlobalPosition)) continue;

            // The camp's own footprint, not its creatures' reach. What the player wants from
            // this circle is "there is a camp here", and drawing the leash would paint most
            // of the map red.
            Ring(field.GlobalPosition, (float)def.Radius, CampTint, 0.12f);
        }
    }

    private void Ring(Vector3 at, float radius, Color tint, float fill)
    {
        var centre = At(at);
        var r = Mathf.Max(radius * _scale, 3f);

        DrawCircle(centre, r, tint with { A = fill });
        DrawArc(centre, r, 0, Mathf.Tau, 32, tint with { A = 0.7f }, 1.5f);
    }

    private void DrawMarkers(System.Collections.Generic.HashSet<long> seen)
    {
        var font = GetThemeDefaultFont();
        var fontSize = GetThemeDefaultFontSize();

        foreach (var node in GetTree().GetNodesInGroup("shrines"))
        {
            if (node is not ShrineNode shrine || !Known(seen, shrine.GlobalPosition)) continue;

            Diamond(At(shrine.GlobalPosition), 5f, ShrineTint);
            Caption(font, fontSize, At(shrine.GlobalPosition), NameOfShrine(shrine.ShrineId), ShrineTint);
        }

        // Villagers by what they do, not who they are: "where do I sell this" is the question.
        foreach (var node in GetTree().GetNodesInGroup("npcs"))
        {
            if (node is not NpcNode npc || npc.Def is null || !Known(seen, npc.GlobalPosition)) continue;

            DrawCircle(At(npc.GlobalPosition), 3.5f, VillagerTint);
            Caption(font, fontSize, At(npc.GlobalPosition), npc.Def.Title.Length > 0 ? npc.Def.Title : npc.DisplayName, VillagerTint);
        }

        foreach (var node in GetTree().GetNodesInGroup("shards"))
        {
            if (node is not ShardNode shard || !Known(seen, shard.GlobalPosition)) continue;

            Diamond(At(shard.GlobalPosition), 7f, ShardTint);
            Caption(font, fontSize, At(shard.GlobalPosition), NameOfShard(shard.ShardId), ShardTint);
        }

        // Borders last, so their labels sit on top of everything else. A map is mostly read
        // to answer "which way out", and that answer should never be half-hidden by a camp.
        foreach (var node in GetTree().GetNodesInGroup("zone_gates"))
        {
            if (node is not ZoneGate gate || !Known(seen, gate.GlobalPosition)) continue;

            var at = At(gate.GlobalPosition);

            DrawRect(new Rect2(at - new Vector2(4, 4), new Vector2(8, 8)), GateTint);
            Caption(font, fontSize, at, NameOfZone(gate.ToZone), GateTint);
        }
    }


    // ------------------------------------------------------------------ the quest

    /// <summary>
    /// Where the active quest wants the player (FR-8.3).
    /// </summary>
    /// <remarks>
    /// Two cases. If the goal can be done in this map, every place it can be done is marked:
    /// each camp with the creature in it, or the shard itself. If it cannot, the border to walk
    /// through is marked instead, on the shortest road to a map where it can.
    /// <para>
    /// Drawn through the fog. Every other marker waits until the player has been there, and
    /// that is right for them — but a quest marker the player cannot see until they have
    /// already found the place is not a marker.
    /// </para>
    /// </remarks>
    private void DrawQuest()
    {
        if (Quests.QuestPlaces.CurrentGoal() is not { } goal) return;

        var font = GetThemeDefaultFont();
        var fontSize = GetThemeDefaultFontSize();
        var here = GameWorld.CurrentZoneId;
        var zones = Quests.QuestPlaces.ZonesFor(goal);

        if (zones.Contains(here))
        {
            if (goal.Type == Kiln.Core.Foundation.ObjectiveType.Kill)
            {
                var label = Items.GameItems.NameOfEnemy(goal.Target);

                foreach (var node in GetTree().GetNodesInGroup("spawn_fields"))
                {
                    if (node is not SpawnFieldNode field || field.Def is not { } def) continue;
                    if (!def.Entries.Any(e => e.EnemyId == goal.Target)) continue;

                    QuestMark(font, fontSize, field.GlobalPosition, (float)def.Radius, label);
                }
            }
            else if (goal.Type == Kiln.Core.Foundation.ObjectiveType.Shard)
            {
                foreach (var node in GetTree().GetNodesInGroup("shards"))
                {
                    if (node is ShardNode shard && shard.ShardId == goal.Target)
                    {
                        QuestMark(font, fontSize, shard.GlobalPosition, 4f, NameOfShard(shard.ShardId));
                    }
                }
            }

            return;
        }

        var step = Quests.QuestPlaces.NextStep(here, zones);

        if (step is null) return;

        foreach (var node in GetTree().GetNodesInGroup("zone_gates"))
        {
            if (node is ZoneGate gate && gate.ToZone == step)
            {
                QuestMark(font, fontSize, gate.GlobalPosition, 4f, $"Quest: this way ({NameOfZone(step)})");
            }
        }
    }

    /// <summary>A pulsing ring and an exclamation mark, in a colour nothing else on the map uses.</summary>
    private void QuestMark(Font font, int fontSize, Vector3 world, float radius, string text)
    {
        var at = At(world);
        var pulse = 0.5f + (0.5f * Mathf.Sin((float)Time.GetTicksMsec() / 1000f * 4f));
        var r = Mathf.Max(radius * _scale, 7f) + (pulse * 4f);

        DrawArc(at, r, 0, Mathf.Tau, 40, QuestTint with { A = 0.55f + (0.4f * pulse) }, 2.5f);
        DrawCircle(at + new Vector2(0, -r - 10), 8f, QuestTint);
        DrawString(font, at + new Vector2(-2.5f, -r - 5), "!", HorizontalAlignment.Left, -1, 14, new Color(0.1f, 0.07f, 0.02f));

        Caption(font, fontSize, at + new Vector2(r, -4), text, QuestTint);
    }

    private static string NameOfZone(string id) =>
        GameWorld.Graph[id] is { } zone ? Items.GameItems.Localise(zone.Name) : id;

    private static string NameOfShrine(string id) =>
        GameWorld.Graph.Shrine(id) is { } shrine ? Items.GameItems.Localise(shrine.Name) : id;

    private static string NameOfShard(string id) =>
        GameContent.IsLoaded && GameContent.Database.Shards.TryGetValue(id, out var def)
            ? Items.GameItems.Localise(def.Name)
            : id;

    private void Caption(Font font, int fontSize, Vector2 at, string text, Color tint)
    {
        var offset = at + new Vector2(8, 4);

        // Drawn twice, dark underneath. The map's background is whatever the terrain happens
        // to be at that point, and a label is worthless if it lands on a light wall.
        DrawString(font, offset + Vector2.One, text, HorizontalAlignment.Left, -1, fontSize,
            new Color(0, 0, 0, 0.8f));
        DrawString(font, offset, text, HorizontalAlignment.Left, -1, fontSize, tint);
    }

    private void Diamond(Vector2 at, float radius, Color tint) => DrawColoredPolygon(
    [
        at + new Vector2(0, -radius),
        at + new Vector2(radius, 0),
        at + new Vector2(0, radius),
        at + new Vector2(-radius, 0),
    ], tint);

    private void DrawPlayer()
    {
        if (GetTree().GetFirstNodeInGroup("player") is not Node3D player) return;

        var at = At(player.GlobalPosition);
        // The model's facing, not the body's: the body never turns, only the model on it does,
        // so reading the body pointed this arrow the same way for the whole game.
        var forward = player is Player.PlayerMotor motor ? motor.Facing : -player.GlobalBasis.Z;
        var heading = new Vector2(forward.X, forward.Z);

        heading = heading.LengthSquared() < 0.0001f ? Vector2.Up : heading.Normalized();

        var side = new Vector2(-heading.Y, heading.X);

        DrawColoredPolygon(
        [
            at + heading * 9f,
            at - heading * 5f + side * 5f,
            at - heading * 5f - side * 5f,
        ], PlayerTint);
    }
}
