using System.Collections.Generic;
using Godot;
using Kiln.Data.Definitions;

namespace Kiln.Game.World;

/// <summary>
/// One piece of world geometry, built from data (WLD-04).
/// </summary>
/// <remarks>
/// The ENG-07 boundary applied to level design. A scene node says <c>kit_wall_4</c> and
/// nothing else; the mesh, the collision shape, the colour and whether it is baked into the
/// navigation mesh all come from <c>game/data/tables/world_kit.json</c>. Replacing the grey
/// boxes with modelled walls is then an edit to that file, not a pass over every scene that
/// used one — which matters most for the villages, since they are almost entirely kit.
/// <para>
/// Meshes, materials and collision shapes are shared per piece id. A village perimeter is
/// two hundred wall segments; giving each its own material would cost two hundred draw calls
/// for one wall.
/// </para>
/// </remarks>
[Tool]
public partial class KitPiece : StaticBody3D
{
    private static readonly Dictionary<string, Mesh> Meshes = [];
    private static readonly Dictionary<string, Material> Materials = [];
    private static readonly Dictionary<string, Shape3D> Shapes = [];

    private string _pieceId = "";
    private string _tint = "";

    [Export]
    public string PieceId
    {
        get => _pieceId;
        set { _pieceId = value; Rebuild(); }
    }

    /// <summary>This piece's tuning, or null while content is not loaded or the id is unknown.</summary>
    public KitPieceDef? Piece =>
        GameContent.IsLoaded && GameContent.Database.KitPieces.TryGetValue(_pieceId, out var def) ? def : null;

    /// <summary>Overrides the piece's colour, for marking a route or a faction's wall.</summary>
    [Export]
    public string TintOverride
    {
        get => _tint;
        set { _tint = value; Rebuild(); }
    }

    public override void _Ready()
    {
        Apply();

        // A fire crackles where it burns (REF-20). Not in the editor, and not rebuilt with
        // the geometry: a piece only changes there.
        if (!Engine.IsEditorHint() && Piece is { Sound.Length: > 0 } piece) Audio.AudioDirector.Emitter(this, piece.Sound);
    }

    /// <summary>
    /// Builds, or rebuilds, the piece's geometry.
    /// </summary>
    /// <remarks>
    /// Called from the property setters as well as <c>_Ready</c> so that changing the piece id
    /// in the inspector shows the new piece immediately — which is the entire point of the
    /// editor preview, and the reason the generated nodes get no <c>Owner</c>: without one
    /// Godot leaves them out of the saved scene, so a <c>.tscn</c> stays a list of piece ids
    /// rather than a copy of the meshes they happened to produce.
    /// </remarks>
    private void Rebuild()
    {
        // The setters run while the scene is still being deserialised, before the node is in
        // the tree and before its siblings exist. _Ready does the first build.
        if (!IsNodeReady()) return;

        Apply();
    }

    private void Apply()
    {
        Clear();

        if (_pieceId.Length == 0) return;

        if (!GameContent.IsLoaded && !GameContent.EnsureLoadedForEditor())
        {
            if (!Engine.IsEditorHint()) GD.PushError($"[kit] '{_pieceId}' loaded before content.");
            return;
        }

        if (!GameContent.Database.KitPieces.TryGetValue(_pieceId, out var def))
        {
            GD.PushError($"[kit] no piece '{_pieceId}' in the kit — this node will be invisible.");
            return;
        }

        Build(def);
    }

    private void Clear()
    {
        foreach (var child in GetChildren())
        {
            // Only what a previous build added. Anything the designer parented to the piece —
            // a light on a brazier, a marker on a gate — has an owner and is left alone.
            if (child.Owner is not null) continue;

            RemoveChild(child);
            child.QueueFree();
        }
    }

    private void Build(KitPieceDef def)
    {
        var size = Size(def);

        if (def.Shape == "model" && ModelFor(def) is { Length: > 0 } path)
        {
            BuildModel(def, path, size);
        }
        else
        {
            AddChild(new MeshInstance3D
            {
                Name = "Mesh",
                Mesh = MeshFor(def, size),
                MaterialOverride = MaterialFor(string.IsNullOrEmpty(_tint) ? def.Color : _tint),

                // An arch is two legs and a lintel, so its centre is empty; a solid shadow
                // caster would fill the gateway with darkness the player can walk through.
                CastShadow = GeometryInstance3D.ShadowCastingSetting.On,
            });
        }

        if (def.Solid)
        {
            CollisionLayer = Foundation.Layers.World;
            CollisionMask = 0;

            // A tree is stopped by its trunk, not its crown: the collider can be smaller than
            // what is drawn, and is then centred at the foot of the piece rather than its middle.
            var collider = def.CollisionSize.Length == 3
                ? new Vector3((float)def.CollisionSize[0], (float)def.CollisionSize[1], (float)def.CollisionSize[2])
                : size;

            foreach (var shape in CollisionFor(def, collider))
            {
                if (def.CollisionSize.Length == 3) shape.Position += new Vector3(0, (collider.Y - size.Y) * 0.5f, 0);

                AddChild(shape);
            }
        }
        else
        {
            CollisionLayer = 0;
            CollisionMask = 0;
        }

        // The navigation bake reads this group, so a piece opts in by data rather than by
        // somebody remembering to tick a box in the editor.
        if (def.Navigation) AddToGroup("navsource");

        // Every piece, navigable or not, so the map (WLD-08) can draw the zone from the same
        // nodes the zone is built from. A map drawn from a second description of the level
        // is a map that goes stale the first time somebody moves a wall.
        AddToGroup("kit");
    }

    /// <summary>
    /// A number that belongs to where this piece stands, the same on every load and every
    /// machine. Picks the variant and the turn; .NET's own string hashes are salted per run
    /// and would give a different wood every time the scene opened.
    /// </summary>
    private int Placement()
    {
        var at = Position;
        var x = (int)Mathf.Round(at.X * 10f);
        var z = (int)Mathf.Round(at.Z * 10f);

        unchecked
        {
            var h = (x * 73856093) ^ (z * 19349663) ^ ((int)Mathf.Round(at.Y * 10f) * 83492791);
            return h & 0x7fffffff;
        }
    }

    private string ModelFor(KitPieceDef def)
    {
        if (def.Models.Length == 0) return def.ModelPath ?? "";

        return def.Models[Placement() % def.Models.Length];
    }

    /// <summary>
    /// Puts the model in the box: stretched to fill it, tiled along it, or stood up to its
    /// height in proportion — whichever the piece asks for.
    /// </summary>
    private void BuildModel(KitPieceDef def, string path, Vector3 size)
    {
        var scene = ResourceLoader.Load<PackedScene>(path);

        if (scene is null)
        {
            GD.PushWarning($"[kit] model '{path}' for '{def.Id}' failed to load.");
            return;
        }

        var copies = System.Math.Max(1, def.Tile);
        var step = size.X / copies;

        for (var i = 0; i < copies; i++)
        {
            var instance = scene.Instantiate<Node3D>();

            if (def.RandomYaw)
            {
                // Quarter-turns and the angles between, but never the same for two neighbours.
                instance.Rotation = new Vector3(0, ((Placement() + (i * 97)) % 360) * Mathf.Pi / 180f, 0);
            }

            AddChild(instance);

            if (def.Fit == "height")
            {
                Visual.ModelFit.ByHeight(instance, size.Y);
            }
            else
            {
                Visual.ModelFit.Stretch(instance, new Vector3(step, size.Y, size.Z));
            }

            // Side by side along the piece's length, the run centred on the node like every
            // other piece.
            if (copies > 1) instance.Position += new Vector3((-size.X * 0.5f) + (step * (i + 0.5f)), 0, 0);
        }
    }

    private static Vector3 Size(KitPieceDef def) => def.Size.Length == 3
        ? new Vector3((float)def.Size[0], (float)def.Size[1], (float)def.Size[2])
        : Vector3.One;

    private static Mesh MeshFor(KitPieceDef def, Vector3 size)
    {
        var key = $"{def.Id}:mesh";

        if (Meshes.TryGetValue(key, out var cached)) return cached;

        Mesh mesh = def.Shape switch
        {
            // Godot's PrismMesh is a wedge: a ramp with no extra geometry.
            "ramp" => new PrismMesh { Size = size },
            "cylinder" => new CylinderMesh
            {
                TopRadius = size.X * 0.5f,
                BottomRadius = size.X * 0.5f,
                Height = size.Y,
            },
            "sphere" => new SphereMesh { Radius = size.X * 0.5f, Height = size.Y },

            // An arch is drawn as its lintel only; the legs are separate children below, so
            // the opening is genuinely open rather than a box with a doorway painted on.
            "arch" => new BoxMesh { Size = new Vector3(size.X, size.Y * 0.22f, size.Z) },
            _ => new BoxMesh { Size = size },
        };

        Meshes[key] = mesh;

        return mesh;
    }

    private static Material MaterialFor(string colour)
    {
        if (Materials.TryGetValue(colour, out var cached)) return cached;

        // A tint typed by hand in the inspector is half-finished for as long as it takes to
        // type it, so an unparseable one is a normal intermediate state, not an error.
        var material = new StandardMaterial3D
        {
            AlbedoColor = Color.HtmlIsValid(colour.TrimStart('#')) ? new Color(colour) : Colors.Magenta,
            Roughness = 0.9f,
        };

        Materials[colour] = material;

        return material;
    }

    /// <summary>The shape that stops you, which is not always the shape you see.</summary>
    private static string CollisionShapeOf(KitPieceDef def) =>
        def.Collision.Length > 0 ? def.Collision : def.Shape;

    private static List<CollisionShape3D> CollisionFor(KitPieceDef def, Vector3 size)
    {
        var shapes = new List<CollisionShape3D>();
        var form = CollisionShapeOf(def);

        if (form == "arch")
        {
            // Two legs and a lintel. Walking through a gateway is the whole point of a gate,
            // and a single box would make it a wall.
            var legWidth = size.X * 0.18f;
            var inset = (size.X - legWidth) * 0.5f;

            shapes.Add(Box(def.Id + ":leg", new Vector3(legWidth, size.Y, size.Z),
                new Vector3(-inset, size.Y * 0.5f, 0)));
            shapes.Add(Box(def.Id + ":leg", new Vector3(legWidth, size.Y, size.Z),
                new Vector3(inset, size.Y * 0.5f, 0)));

            return shapes;
        }

        var key = $"{def.Id}:shape";

        if (!Shapes.TryGetValue(key, out var shape))
        {
            shape = form switch
            {
                "cylinder" => new CylinderShape3D { Radius = size.X * 0.5f, Height = size.Y },
                "sphere" => new SphereShape3D { Radius = size.X * 0.5f },

                // A wedge approximated by its bounding box: the player walks up the visible
                // slope, and the navmesh bake decides what is climbable from the geometry.
                _ => new BoxShape3D { Size = size },
            };

            Shapes[key] = shape;
        }

        shapes.Add(new CollisionShape3D { Name = "Collision", Shape = shape });

        return shapes;
    }

    private static CollisionShape3D Box(string name, Vector3 size, Vector3 at) => new()
    {
        Name = name,
        Shape = new BoxShape3D { Size = size },
        Position = at,
    };

    /// <summary>
    /// Drops the shared caches. Called when content reloads in the editor, so an edited piece
    /// does not keep rendering at its old size.
    /// </summary>
    public static void ClearCaches()
    {
        Meshes.Clear();
        Materials.Clear();
        Shapes.Clear();
    }
}
