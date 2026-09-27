using System.Collections.Generic;
using System.Linq;
using Godot;
using Kiln.Core.Foundation;
using Kiln.Core.World;
using Kiln.Data.Definitions;

namespace Kiln.Game.World;

/// <summary>
/// The mountains round a map (2026-09-27, at your call): no more edge of a green sheet over a
/// blue nothing, but hills, crags, mesas or snowy peaks as the map's theme says, a pass cut
/// through them wherever a gate leads out, and a wall at their foot the player cannot cross.
/// </summary>
/// <remarks>
/// A child of the navigation region, like the scatter nodes, so it is built before the region
/// bakes: the wall at the foot is baked in and nothing paths up a mountain. The zone's
/// <c>border</c> says what the mountains look like; <see cref="BorderTerrain"/> says where
/// they stand; this node turns that into a mesh, a wall, trees on the slopes and a wall across
/// each pass.
/// </remarks>
public partial class ZoneBorder : Node3D
{
    /// <summary>How far out the mountains go, in metres from the middle.</summary>
    private const float Extent = 260f;

    /// <summary>The size of one cell of the mountain mesh.</summary>
    private const float Cell = 5f;

    /// <summary>Cells per side of one mesh chunk, so a chunk off screen is not drawn.</summary>
    private const int Chunk = 26;

    /// <summary>
    /// The wall at the mountains' foot, round the map, for the map panel to draw the mountains
    /// beyond it. Empty on a map without a border.
    /// </summary>
    public static IReadOnlyList<Vector2> Outline { get; private set; } = [];

    /// <summary>The colour the map panel draws the mountains in.</summary>
    public static Color OutlineColour { get; private set; }

    private BorderTerrain? _terrain;
    private float _groundHalf = 90f;

    public override void _Ready()
    {
        Outline = [];

        if (Engine.IsEditorHint() || !GameContent.IsLoaded) return;

        if (Owner is not ZoneRoot root
            || !GameContent.Database.Zones.TryGetValue(root.ZoneId, out var zone)
            || zone.Border is not { } border)
        {
            return;
        }

        var start = Time.GetTicksUsec();

        var (groundColour, half) = Ground(root);
        _groundHalf = half;

        var shape = new BorderShape(border.Height[0], border.Height[1], border.Rise, border.Terrace, border.Seed);
        var (keep, passes) = Landmarks(root);

        _terrain = new BorderTerrain(shape, border.Inset, keep, passes);

        var grass = border.Grass.Length > 0 ? Color.FromHtml(border.Grass) : groundColour;
        var sky = Sky(root);

        BuildMountains(border, grass, sky);
        BuildFence();
        BuildCover(border);
        BuildWalls(border, passes);

        OutlineColour = Color.FromHtml(border.Rock);

        GD.Print($"[border] {root.ZoneId}: mountains in {(Time.GetTicksUsec() - start) / 1000.0:F0} ms");
    }

    /// <summary>A dungeon after a map with mountains has none: the map panel must not draw the last one's.</summary>
    public override void _ExitTree() => Outline = [];

    // ------------------------------------------------------------------ what is on the map

    /// <summary>The ground plane's colour and how far it reaches from the middle.</summary>
    private static (Color Colour, float Half) Ground(Node root)
    {
        var mesh = root.GetNodeOrNull<MeshInstance3D>("NavigationRegion3D/Ground/Mesh");
        var colour = (mesh?.GetSurfaceOverrideMaterial(0) ?? mesh?.MaterialOverride) is StandardMaterial3D m
            ? m.AlbedoColor
            : new Color(0.31f, 0.41f, 0.22f);
        var half = mesh?.Mesh is PlaneMesh plane ? Mathf.Min(plane.Size.X, plane.Size.Y) * 0.5f : 90f;

        return (colour, half);
    }

    private static Color Sky(Node root) =>
        root.GetNodeOrNull<WorldEnvironment>("WorldEnvironment")?.Environment is { } env
            ? env.BackgroundColor
            : new Color(0.5f, 0.6f, 0.7f);

    /// <summary>
    /// Everything the mountains must stay clear of, and the gates that cut passes through them.
    /// </summary>
    /// <remarks>
    /// Read from the scene as it was authored — kit pieces, gates, camps, villagers — so moving
    /// a camp towards the edge moves the mountains back from it with no second list to keep.
    /// Strewn woods do not count: a copse half up a slope is exactly right.
    /// </remarks>
    private static (List<(double X, double Z, double Clearance)> Keep, List<(double X, double Z)> Passes) Landmarks(Node root)
    {
        var keep = new List<(double, double, double)>();
        var passes = new List<(double, double)>();

        foreach (var node in root.FindChildren("*", "", true, true))
        {
            if (node is not Node3D spot || node is ScatterNode || node is ZoneBorder) continue;
            if (node.GetParent() is ScatterNode) continue;
            if (node is MeshInstance3D or CollisionShape3D or Light3D or WorldEnvironment) continue;

            var at = spot.GlobalPosition;

            if (Mathf.Max(Mathf.Abs(at.X), Mathf.Abs(at.Z)) < 55) continue;

            var clearance = node switch
            {
                ZoneGate gate => gate.Radius + 6,
                KitPiece piece when piece.Piece is { Size.Length: 3 } def => 3 + (Mathf.Max((float)def.Size[0], (float)def.Size[2]) * 0.5f),
                _ => 5,
            };

            keep.Add((at.X, at.Z, clearance));

            if (node is ZoneGate) passes.Add((at.X, at.Z));
        }

        return (keep, passes);
    }

    // ------------------------------------------------------------------ the mountains

    private void BuildMountains(BorderDef border, Color grass, Color sky)
    {
        var terrain = _terrain!;
        var cells = (int)(Extent * 2 / Cell);
        var heights = new float[cells + 1, cells + 1];

        for (var i = 0; i <= cells; i++)
        {
            for (var j = 0; j <= cells; j++)
            {
                var (x, z) = (-Extent + (i * Cell), -Extent + (j * Cell));
                var h = (float)terrain.Height(x, z);

                // Flat and on the map: tucked under the ground, so the two never fight over
                // which is on top. Off the map a flat stretch stays at ground level and carries on.
                var onGround = Mathf.Abs(x) < _groundHalf - 0.01f && Mathf.Abs(z) < _groundHalf - 0.01f;
                heights[i, j] = h < 0.05f && onGround ? -0.4f : h;
            }
        }

        var paint = new Paint(border, grass, sky);

        var material = new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            VertexColorIsSrgb = true,
            Roughness = 0.95f,
        };

        for (var ci = 0; ci < cells; ci += Chunk)
        {
            for (var cj = 0; cj < cells; cj += Chunk)
            {
                var mesh = ChunkMesh(heights, ci, cj, Mathf.Min(cells, ci + Chunk), Mathf.Min(cells, cj + Chunk), paint);

                if (mesh is null) continue;

                mesh.SurfaceSetMaterial(0, material);
                AddChild(new MeshInstance3D { Name = $"Mountains_{ci}_{cj}", Mesh = mesh });
            }
        }
    }

    /// <summary>
    /// One patch of the mesh: every face its own flat colour and flat normal, the low-poly
    /// look the rest of the kit has. Null when the whole patch lies under the map.
    /// </summary>
    private static ArrayMesh? ChunkMesh(float[,] heights, int i0, int j0, int i1, int j1, Paint paint)
    {
        var verts = new List<Vector3>();
        var normals = new List<Vector3>();
        var colours = new List<Color>();

        Vector3 V(int i, int j) => new(-Extent + (i * Cell), heights[i, j], -Extent + (j * Cell));

        void Face(Vector3 a, Vector3 b, Vector3 c)
        {
            if (a.Y < 0 && b.Y < 0 && c.Y < 0) return;

            var n = (c - a).Cross(b - a).Normalized();

            // Godot's front faces wind clockwise seen from outside: upwards, here.
            if (n.Y < 0)
            {
                (b, c) = (c, b);
                n = -n;
            }

            var colour = paint.Face((a + b + c) / 3f, n);

            verts.Add(a); verts.Add(b); verts.Add(c);
            normals.Add(n); normals.Add(n); normals.Add(n);
            colours.Add(colour); colours.Add(colour); colours.Add(colour);
        }

        for (var i = i0; i < i1; i++)
        {
            for (var j = j0; j < j1; j++)
            {
                var (a, b, c, d) = (V(i, j), V(i + 1, j), V(i + 1, j + 1), V(i, j + 1));

                // The diagonal alternates, so the faceting does not all lean one way.
                if (((i + j) & 1) == 0)
                {
                    Face(a, b, c);
                    Face(a, c, d);
                }
                else
                {
                    Face(a, b, d);
                    Face(b, c, d);
                }
            }
        }

        if (verts.Count == 0) return null;

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = verts.ToArray();
        arrays[(int)Mesh.ArrayType.Normal] = normals.ToArray();
        arrays[(int)Mesh.ArrayType.Color] = colours.ToArray();

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);

        return mesh;
    }

    /// <summary>What colour a face of the mountains is: grass low, rock above, snow on the tops, hazed with distance.</summary>
    private sealed class Paint(BorderDef border, Color grass, Color sky)
    {
        private readonly Color _rock = Color.FromHtml(border.Rock);
        private readonly Color _rockAlt = border.RockAlt.Length > 0 ? Color.FromHtml(border.RockAlt) : Color.FromHtml(border.Rock);
        private readonly Color? _cap = border.Cap.Length > 0 ? Color.FromHtml(border.Cap) : null;
        private readonly double _capLine = border.CapFrom * border.Height[1];
        private readonly double _band = border.Terrace > 0 ? border.Terrace : 7;

        public Color Face(Vector3 at, Vector3 normal)
        {
            var wobble = ValueNoise.Value(at.X / 11.0, at.Z / 11.0);
            Color colour;

            if (_cap is { } cap && at.Y > _capLine + ((wobble - 0.5) * 8) && normal.Y > 0.45f)
            {
                colour = cap;
            }
            else if (at.Y < border.GrassLine + (wobble * 4) && normal.Y > 0.5f)
            {
                colour = grass;
            }
            else
            {
                colour = (int)Mathf.Floor(at.Y / (float)_band) % 2 == 0 ? _rock : _rockAlt;

                // Steep faces a shade darker than the ledges.
                colour = colour.Darkened(Mathf.Clamp(0.55f - normal.Y, 0f, 0.5f) * 0.3f);
            }

            // Every face a little lighter or darker than its neighbours: the low-poly shimmer.
            var jitter = (float)ValueNoise.Hash((long)Mathf.Round(at.X * 3), (long)Mathf.Round(at.Z * 3));
            colour = colour.Lightened(jitter * 0.07f).Darkened((1 - jitter) * 0.05f);

            // Far faces fade towards the sky, so the ranges behind read as behind.
            var far = Mathf.Clamp((new Vector2(at.X, at.Z).Length() - 150f) / 260f, 0f, 1f);

            return colour.Lerp(sky, far * 0.4f);
        }
    }

    // ------------------------------------------------------------------ the wall at the foot

    /// <summary>
    /// A tall invisible wall along the mountains' foot: the player stops at the slope, the
    /// navigation mesh ends there, and the camera, pulled in by it, never ends up in the rock.
    /// </summary>
    private void BuildFence()
    {
        var line = _terrain!.Fence(_groundHalf - 1, 360).Select(p => new Vector2((float)p.X, (float)p.Z)).ToList();

        Outline = line;

        var body = new StaticBody3D
        {
            Name = "Fence",
            CollisionLayer = Foundation.Layers.World,
            CollisionMask = 0,
        };

        AddChild(body);

        for (var i = 0; i < line.Count; i++)
        {
            var a = line[i];
            var b = line[(i + 1) % line.Count];
            var span = b - a;
            var middle = (a + b) * 0.5f;

            body.AddChild(new CollisionShape3D
            {
                Shape = new BoxShape3D { Size = new Vector3(span.Length() + 0.6f, 30f, 1f) },
                Position = new Vector3(middle.X, 15f, middle.Y),
                Rotation = new Vector3(0, -Mathf.Atan2(span.Y, span.X), 0),
            });
        }

        body.AddToGroup("navsource");
    }

    // ------------------------------------------------------------------ what grows on the slopes

    /// <summary>
    /// Trees, rocks or cacti on the lower slopes, beyond the wall: they are scenery, so they
    /// have no collision, are not baked and are not on the map.
    /// </summary>
    private void BuildCover(BorderDef border)
    {
        if (border.Cover.Length == 0 || border.CoverCount <= 0) return;

        var terrain = _terrain!;
        var rng = new DeterministicRng((ulong)(uint)border.Seed).Fork("border-cover");
        var placed = new List<Vector2>();

        for (var attempt = 0; attempt < border.CoverCount * 20 && placed.Count < border.CoverCount; attempt++)
        {
            var angle = rng.NextDouble() * Mathf.Tau;
            var reach = terrain.FootAt(angle) + rng.NextDouble(1, 34);
            var dir = new Vector2(Mathf.Cos((float)angle), Mathf.Sin((float)angle));

            // Along the rounded square, not a circle: scaled back to the foot's own measure.
            var unit = BorderTerrain.Rounded(dir.X, dir.Y);
            var at = dir * (float)(reach / unit);

            var h = terrain.Height(at.X, at.Y);

            if (h < BorderTerrain.FenceHeight + 0.4 || h > border.CoverHeight) continue;
            if (terrain.OnAPassRoad(at.X, at.Y, 9)) continue;
            if (placed.Any(p => p.DistanceSquaredTo(at) < 16f)) continue;

            // Stood on the lowest ground under it, so no root hangs over a slope.
            var ground = Mathf.Min(
                Mathf.Min((float)terrain.Height(at.X + 1, at.Y), (float)terrain.Height(at.X - 1, at.Y)),
                Mathf.Min((float)terrain.Height(at.X, at.Y + 1), (float)terrain.Height(at.X, at.Y - 1)));

            if (Mathf.Abs((float)h - ground) > 1.6f) continue;

            var id = border.Cover[rng.NextInt(0, border.Cover.Length)];

            if (!GameContent.Database.KitPieces.TryGetValue(id, out var def)) continue;

            var height = def.Size.Length == 3 ? (float)def.Size[1] : 1f;
            var piece = new KitPiece
            {
                Name = $"Cover{placed.Count}",
                PieceId = id,
                Position = new Vector3(at.X, ground - 0.25f + (height * 0.5f), at.Y),
            };

            AddChild(piece);
            Scenery(piece);
            placed.Add(at);
        }
    }

    /// <summary>A kit piece that is only to be looked at: no collision, no bake, not on the map.</summary>
    private static void Scenery(KitPiece piece)
    {
        piece.CollisionLayer = 0;
        piece.RemoveFromGroup("navsource");
        piece.RemoveFromGroup("kit");

        foreach (var child in piece.GetChildren())
        {
            if (child is CollisionShape3D shape) shape.Disabled = true;
        }

        // No shadows: two hundred trees up a slope in every shadow cascade doubled the draw
        // calls, and a shadow that far out falls on more mountain, not on the map.
        foreach (var mesh in piece.FindChildren("*", "GeometryInstance3D", true, false))
        {
            ((GeometryInstance3D)mesh).CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        }
    }

    // ------------------------------------------------------------------ walls across the passes

    /// <summary>
    /// A wall either side of the road where each pass leaves the map, running from the road's
    /// edge into the slopes: the border is built on, not just walked through.
    /// </summary>
    private void BuildWalls(BorderDef border, List<(double X, double Z)> passes)
    {
        if (border.Wall.Length == 0 || !GameContent.Database.KitPieces.TryGetValue(border.Wall, out var def)) return;

        var length = def.Size.Length == 3 ? (float)def.Size[0] : 4f;
        var height = def.Size.Length == 3 ? (float)def.Size[1] : 3f;
        var n = 0;

        foreach (var (gx, gz) in passes)
        {
            var gate = new Vector2((float)gx, (float)gz);
            var along = gate.Normalized();
            var side = new Vector2(-along.Y, along.X);
            var line = gate + (along * 4.5f);

            foreach (var sign in new[] { -1f, 1f })
            {
                for (var k = 0; k < 3; k++)
                {
                    var middle = line + (side * sign * (5f + (length * (k + 0.5f))));

                    AddChild(new KitPiece
                    {
                        Name = $"PassWall{n++}",
                        PieceId = border.Wall,
                        Position = new Vector3(middle.X, height * 0.5f, middle.Y),
                        Rotation = new Vector3(0, -Mathf.Atan2(side.Y, side.X), 0),
                    });
                }
            }
        }
    }
}
