using System.Globalization;
using System.Text;
using Kiln.Data.Loading;

namespace Kiln.Tools.Commands;

/// <summary>
/// Writes act 2's map scenes from their layouts (A2-01): <c>game/scenes/&lt;name&gt;.tscn</c>, one for each
/// <see cref="Layout"/> in <see cref="Act2Layouts"/>.
/// </summary>
/// <remarks>
/// A map scene is coordinates and nothing else: every wall, tree and stone is a kit piece naming an id,
/// every camp of creatures a spawn field naming one, and what they are is data. Act 1's scenes were
/// written by a script that was never kept; this one is, so a map is changed by changing its layout
/// and running the command again — never by editing the scene.
/// </remarks>
public static class ScenesCommand
{
    public static int Run(string[] args)
    {
        var dataRoot = Program.ResolveDataRoot(args);
        var loaded = ContentLoader.LoadFromDirectory(dataRoot);

        if (loaded.Errors.Count > 0)
        {
            foreach (var error in loaded.Errors) Console.Error.WriteLine($"  {error}");
            return 1;
        }

        var scenes = Path.GetFullPath(Path.Combine(dataRoot, "..", "scenes"));
        var check = Program.HasFlag(args, "--check");
        var stale = 0;

        foreach (var layout in Act2Layouts.All())
        {
            var path = Path.Combine(scenes, layout.File + ".tscn");
            var text = layout.Write(loaded.Database);

            if (check)
            {
                var same = File.Exists(path) && File.ReadAllText(path).Replace("\r\n", "\n") == text;

                Console.WriteLine($"  {layout.File}.tscn {(same ? "current" : "STALE")}");
                if (!same) stale++;
                continue;
            }

            File.WriteAllText(path, text);
            Console.WriteLine($"  wrote {layout.File}.tscn — {layout.PieceCount} pieces, {layout.FieldCount} spawn fields");
        }

        // The Abyss Crypts (P5, 2026-10-07): a dungeon, laid out room by room rather than as a map.
        {
            var path = Path.Combine(scenes, CryptLayout.File + ".tscn");
            var text = CryptLayout.Write(loaded.Database);

            if (check)
            {
                var same = File.Exists(path) && File.ReadAllText(path).Replace("\r\n", "\n") == text;

                Console.WriteLine($"  {CryptLayout.File}.tscn {(same ? "current" : "STALE")}");
                if (!same) stale++;
            }
            else
            {
                File.WriteAllText(path, text);
                Console.WriteLine($"  wrote {CryptLayout.File}.tscn — seven floors");
            }
        }

        // The Demon Tower (moved here 2026-10-09 from a script kept outside the repository): nine floors, one footprint.
        {
            var path = Path.Combine(scenes, TowerLayout.File + ".tscn");
            var text = TowerLayout.Write(loaded.Database);

            if (check)
            {
                var same = File.Exists(path) && File.ReadAllText(path).Replace("\r\n", "\n") == text;

                Console.WriteLine($"  {TowerLayout.File}.tscn {(same ? "current" : "STALE")}");
                if (!same) stale++;
            }
            else
            {
                File.WriteAllText(path, text);
                Console.WriteLine($"  wrote {TowerLayout.File}.tscn — nine floors");
            }
        }

        if (stale > 0) Console.Error.WriteLine("  A scene is older than its layout: run `kiln scenes`.");

        return stale > 0 ? 1 : 0;
    }
}

/// <summary>One map, as the things on it and where they stand.</summary>
public sealed class Layout
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private sealed record Placed(string Name, string Kit, double X, double Z, double Yaw);

    private sealed record Section(string Name, string Comment, List<Placed> Pieces);

    private sealed record Scattered(string Name, string[] Kits, int Count, double Inner, double Outer, double Spacing, int Seed);

    private sealed record Field(string Name, string Id, double X, double Z);

    private sealed record Shrine(string Name, string Id, double X, double Z);

    private sealed record Person(string Name, string Id, double X, double Z);

    private sealed record Gate(string Name, string Zone, double X, double Z, double ArriveX, double ArriveZ);

    private readonly List<Section> _sections = [];
    private readonly List<Scattered> _scatters = [];
    private readonly List<Field> _fields = [];
    private readonly List<Shrine> _shrines = [];
    private readonly List<Gate> _gates = [];
    private readonly List<Person> _people = [];
    private readonly List<(double X, double Z, double R)> _clear = [];
    private readonly List<Placed> _camp = [];
    private readonly Dictionary<string, int> _names = [];
    private (string Id, double X, double Z)? _safe;
    private Section? _current;

    public required string File { get; init; }
    public required string Root { get; init; }
    public required string ZoneId { get; init; }
    public required string About { get; init; }

    /// <summary>The flat colour under the painted ground, the sky, the light of the place and its fog.</summary>
    public required string Ground { get; init; }
    public required string Sky { get; init; }
    public required string Ambient { get; init; }
    public required string Fog { get; init; }
    public required string Sun { get; init; }
    public double SunEnergy { get; init; } = 1.1;
    public double AmbientEnergy { get; init; } = 0.6;
    public double FogDensity { get; init; } = 0.005;

    public int PieceCount => _sections.Sum(s => s.Pieces.Count) + _camp.Count;
    public int FieldCount => _fields.Count;

    // ------------------------------------------------------------------ building

    private string Name(string wanted)
    {
        var n = _names[wanted] = _names.GetValueOrDefault(wanted) + 1;

        return n == 1 ? wanted : wanted + n.ToString(Inv);
    }

    public Layout Part(string name, string comment)
    {
        _current = new Section(Name(name), comment, []);
        _sections.Add(_current);

        return this;
    }

    /// <summary>A piece in the part being built, standing on the ground, turned <paramref name="yaw"/> degrees.</summary>
    public Layout Put(string kit, double x, double z, double yaw = 0, string? name = null)
    {
        _current!.Pieces.Add(new Placed(Name(name ?? Pretty(kit)), kit, x, z, yaw));

        return this;
    }

    private static string Pretty(string kit) =>
        string.Concat(kit.Split('_').Skip(2).DefaultIfEmpty("Piece").Select(w => char.ToUpperInvariant(w[0]) + w[1..]));

    /// <summary>Pieces round a centre, each turned to face it, leaving a gap between two bearings (degrees, 0 = +X).</summary>
    public Layout Ring(string kit, double cx, double cz, double radius, int count, double gapFrom = 0, double gapTo = 0)
    {
        for (var i = 0; i < count; i++)
        {
            var bearing = 360.0 * i / count;

            if (gapTo > gapFrom && bearing >= gapFrom && bearing <= gapTo) continue;

            var rad = bearing * Math.PI / 180;

            Put(kit, cx + (Math.Cos(rad) * radius), cz + (Math.Sin(rad) * radius), 90 - bearing);
        }

        return Clear(cx, cz, radius + 3);
    }

    /// <summary>A worn road through the points given: a tile every four metres, some of them gone.</summary>
    public Layout Road(string kit, int seed, params (double X, double Z)[] points)
    {
        Part("Trail", "The road, worn through in places");

        var rng = new Random(seed);

        for (var i = 0; i + 1 < points.Length; i++)
        {
            var (ax, az) = points[i];
            var (bx, bz) = points[i + 1];
            var length = Math.Sqrt(((bx - ax) * (bx - ax)) + ((bz - az) * (bz - az)));
            var yaw = -Math.Atan2(bz - az, bx - ax) * 180 / Math.PI;

            for (var d = 0.0; d < length; d += 4)
            {
                var (x, z) = (ax + ((bx - ax) * d / length), az + ((bz - az) * d / length));

                Clear(x, z, 5);

                if (rng.NextDouble() < 0.28) continue;

                Put(kit, x, z, yaw, "Trail");
            }
        }

        return this;
    }

    public Layout Clear(double x, double z, double radius)
    {
        _clear.Add((x, z, radius));

        return this;
    }

    /// <summary>
    /// A pool (water, lava) in the part being built, and the ground round it kept clear, so nothing scattered
    /// later stands in it.
    /// </summary>
    public Layout Pool(string kit, double x, double z, double radius, double yaw = 0) => Put(kit, x, z, yaw).Clear(x, z, radius + 1.5);

    /// <summary>
    /// <paramref name="count"/> pools strewn between two distances from the middle, by a seed, none on cleared ground
    /// (the road, the camps, the fields, each other) — what a scatter would do, but keeping their own ground clear.
    /// </summary>
    public Layout Pools(string name, string kit, int count, double inner, double outer, double radius, int seed)
    {
        Part(name, "Pools strewn by a seed, the ground round each kept clear");

        var rng = new Random(seed);

        for (var tries = 0; count > 0 && tries < 400; tries++)
        {
            var bearing = rng.NextDouble() * Math.PI * 2;
            var far = inner + (rng.NextDouble() * (outer - inner));
            var (x, z) = (Math.Round(Math.Cos(bearing) * far, 1), Math.Round(Math.Sin(bearing) * far, 1));

            if (Math.Abs(x) > 72 || Math.Abs(z) > 72) continue;
            if (_clear.Any(c => ((c.X - x) * (c.X - x)) + ((c.Z - z) * (c.Z - z)) < (c.R + radius) * (c.R + radius))) continue;

            Pool(kit, x, z, radius, rng.Next(0, 12) * 30);
            count--;
        }

        return this;
    }

    public Layout Scatter(string name, int count, double inner, double outer, double spacing, int seed, params string[] kits)
    {
        _scatters.Add(new Scattered(Name(name), kits, count, inner, outer, spacing, seed));

        return this;
    }

    public Layout Spawn(string name, string fieldId, double x, double z, double clear = 12)
    {
        _fields.Add(new Field(Name(name), fieldId, x, z));

        return Clear(x, z, clear);
    }

    public Layout ShrineAt(string name, string id, double x, double z)
    {
        _shrines.Add(new Shrine(Name(name), id, x, z));

        return Clear(x, z, 5);
    }

    /// <summary>The camp at the way in: safe ground, its shrine, a fire, a shelter, a banner and a bench.</summary>
    public Layout Camp(string safeId, string shrineId, double x, double z)
    {
        // Further into the corner, behind the fire: where a smith or a pedlar sets up.
        _camp.Add(new Placed("CampStall", "kit_camp_stall", x + 7, z + 4, 0));
        _safe = (safeId, x, z);
        _shrines.Add(new Shrine(Name("ShrineCamp"), shrineId, x - 4, z - 3));
        _camp.Add(new Placed("CampFire", "kit_camp_fire", x - 1, z + 4, 0));
        _camp.Add(new Placed("CampShelter", "kit_camp_shelter", x + 6, z - 7, 0));
        _camp.Add(new Placed("CampBanner", "kit_camp_banner", x - 7, z + 2, 0));
        _camp.Add(new Placed("CampBench", "kit_camp_bench", x + 4, z - 1, 0));

        return Clear(x, z, 14);
    }

    /// <summary>Somebody who stands in the camp: a smith, a merchant. Who they are is data (npcs).</summary>
    public Layout Npc(string name, string id, double x, double z)
    {
        _people.Add(new Person(Name(name), id, x, z));

        return Clear(x, z, 3);
    }

    public Layout Way(string name, string zone, double x, double z)
    {
        // The arrival stands twelve metres in from its gate, toward the middle of the map.
        var (ax, az) = (x, z - (Math.Sign(z) * 12));

        _gates.Add(new Gate(name, zone, x, z, ax, az));

        return Clear(x, z, 10).Clear(ax, az, 9);
    }

    // ------------------------------------------------------------------ writing

    private static string N(double value) => Math.Round(value, 3).ToString("0.###", Inv);

    private static string At(double x, double y, double z, double yaw = 0)
    {
        if (yaw == 0) return $"Transform3D(1, 0, 0, 0, 1, 0, 0, 0, 1, {N(x)}, {N(y)}, {N(z)})";

        var rad = yaw * Math.PI / 180;
        var (c, s) = (Math.Round(Math.Cos(rad), 6), Math.Round(Math.Sin(rad), 6));

        return string.Create(Inv, $"Transform3D({c:0.######}, 0, {s:0.######}, 0, 1, 0, {-s:0.######}, 0, {c:0.######}, {N(x)}, {N(y)}, {N(z)})");
    }

    private static string Rgb(string hex)
    {
        var value = Convert.ToInt32(hex.TrimStart('#'), 16);

        return string.Create(Inv, $"Color({((value >> 16) & 255) / 255.0:0.###}, {((value >> 8) & 255) / 255.0:0.###}, {(value & 255) / 255.0:0.###}, 1)");
    }

    public string Write(ContentDatabase db)
    {
        double Height(string kit) => db.KitPieces.TryGetValue(kit, out var def) && def.Size.Length == 3
            ? def.Size[1]
            : throw new InvalidOperationException($"{File}: no kit piece '{kit}'.");

        var zone = db.Zones.TryGetValue(ZoneId, out var found) ? found : throw new InvalidOperationException($"no zone '{ZoneId}'.");

        foreach (var field in _fields)
        {
            if (zone.SpawnFields.All(f => f.Id != field.Id)) throw new InvalidOperationException($"{File}: zone has no spawn field '{field.Id}'.");
        }

        foreach (var missing in zone.SpawnFields.Where(f => _fields.All(p => p.Id != f.Id)))
        {
            throw new InvalidOperationException($"{File}: spawn field '{missing.Id}' is placed nowhere.");
        }

        var b = new StringBuilder();

        void Line(string text = "") => b.Append(text).Append('\n');

        void Piece(Placed p, string parent)
        {
            Line($"[node name=\"{p.Name}\" type=\"StaticBody3D\" parent=\"{parent}\"]");
            Line($"transform = {At(p.X, Height(p.Kit) / 2, p.Z, p.Yaw)}");
            Line("script = ExtResource(\"3_kit\")");
            Line($"PieceId = \"{p.Kit}\"");
            Line();
        }

        Line("[gd_scene load_steps=19 format=3]");
        Line();
        foreach (var line in About.Split('\n')) Line($"; {line}".TrimEnd());
        Line(";");
        Line("; Everything the player walks on or into is a kit piece naming an id; woods and clutter are");
        Line("; ScatterNodes, seeded. Every creature is tuning in game/data/zones/act2.json. This file is");
        Line("; coordinates. Written by `kiln scenes` from src/Kiln.Tools/Commands/Act2Layouts.cs: change the");
        Line("; layout, not this file.");
        Line();
        Line("[ext_resource type=\"Script\" path=\"res://scripts/World/ZoneRoot.cs\" id=\"1_zoneroot\"]");
        Line("[ext_resource type=\"Script\" path=\"res://scripts/World/NavBaker.cs\" id=\"2_navbaker\"]");
        Line("[ext_resource type=\"Script\" path=\"res://scripts/World/KitPiece.cs\" id=\"3_kit\"]");
        Line("[ext_resource type=\"Script\" path=\"res://scripts/World/SpawnFieldNode.cs\" id=\"5_spawnfield\"]");
        Line("[ext_resource type=\"Script\" path=\"res://scripts/World/ShrineNode.cs\" id=\"6_shrine\"]");
        Line("[ext_resource type=\"Script\" path=\"res://scripts/Visual/VisualRoot.cs\" id=\"7_visualroot\"]");
        Line("[ext_resource type=\"PackedScene\" path=\"res://scenes/player.tscn\" id=\"8_player\"]");
        Line("[ext_resource type=\"PackedScene\" path=\"res://scenes/session.tscn\" id=\"9_session\"]");
        Line("[ext_resource type=\"Script\" path=\"res://scripts/World/ZoneGate.cs\" id=\"10_gate\"]");
        Line("[ext_resource type=\"Script\" path=\"res://scripts/World/ZoneArrival.cs\" id=\"11_arrival\"]");
        Line("[ext_resource type=\"Script\" path=\"res://scripts/World/NpcNode.cs\" id=\"12_npc\"]");
        Line("[ext_resource type=\"Script\" path=\"res://scripts/World/ScatterNode.cs\" id=\"13_scatter\"]");
        Line("[ext_resource type=\"Script\" path=\"res://scripts/World/ShardNode.cs\" id=\"14_shard\"]");
        Line("[ext_resource type=\"Script\" path=\"res://scripts/World/SafeZoneNode.cs\" id=\"16_safe\"]");
        Line("[ext_resource type=\"Script\" path=\"res://scripts/World/ZoneBorder.cs\" id=\"90_border\"]");
        Line();
        Line("[sub_resource type=\"NavigationMesh\" id=\"NavigationMesh_zone\"]");
        Line("vertices = PackedVector3Array()");
        Line("cell_size = 0.15");
        Line("cell_height = 0.15");
        Line("agent_height = 1.8");
        Line("agent_radius = 0.45");
        Line("agent_max_climb = 0.3");
        Line("agent_max_slope = 45.0");
        Line("geometry_parsed_geometry_type = 1");
        Line("geometry_collision_mask = 1");
        Line("geometry_source_geometry_mode = 1");
        Line("geometry_source_group_name = \"navsource\"");
        Line();
        Line("[sub_resource type=\"PlaneMesh\" id=\"PlaneMesh_ground\"]");
        Line("size = Vector2(180, 180)");
        Line();
        Line("[sub_resource type=\"StandardMaterial3D\" id=\"Material_ground\"]");
        Line($"albedo_color = {Rgb(Ground)}");
        Line("roughness = 1");
        Line();
        Line("[sub_resource type=\"BoxShape3D\" id=\"BoxShape3D_ground\"]");
        Line("size = Vector3(180, 1, 180)");
        Line();
        Line("[sub_resource type=\"Environment\" id=\"Environment_zone\"]");
        Line("background_mode = 1");
        Line($"background_color = {Rgb(Sky)}");
        Line("ambient_light_source = 2");
        Line($"ambient_light_color = {Rgb(Ambient)}");
        Line($"ambient_light_energy = {N(AmbientEnergy)}");
        Line("fog_enabled = true");
        Line($"fog_light_color = {Rgb(Fog)}");
        Line($"fog_density = {N(FogDensity)}");
        Line();
        Line($"[node name=\"{Root}\" type=\"Node3D\"]");
        Line("script = ExtResource(\"1_zoneroot\")");
        Line($"ZoneId = \"{ZoneId}\"");
        Line();
        Line("[node name=\"WorldEnvironment\" type=\"WorldEnvironment\" parent=\".\"]");
        Line("environment = SubResource(\"Environment_zone\")");
        Line();
        Line("[node name=\"DirectionalLight3D\" type=\"DirectionalLight3D\" parent=\".\"]");
        Line("transform = Transform3D(0.8660254, -0.3213938, 0.3830222, 0, 0.7660444, 0.6427876, -0.5, -0.5566704, 0.6634139, 0, 14, 0)");
        Line($"light_color = {Rgb(Sun)}");
        Line($"light_energy = {N(SunEnergy)}");
        Line("shadow_enabled = true");
        Line();
        Line("[node name=\"NavigationRegion3D\" type=\"NavigationRegion3D\" parent=\".\"]");
        Line("navigation_mesh = SubResource(\"NavigationMesh_zone\")");
        Line("script = ExtResource(\"2_navbaker\")");
        Line();
        Line("; The mountains round the map: their look is the zone's \"border\" in act2.json.");
        Line();
        Line("[node name=\"Border\" type=\"Node3D\" parent=\"NavigationRegion3D\"]");
        Line("script = ExtResource(\"90_border\")");
        Line();
        Line("[node name=\"Ground\" type=\"StaticBody3D\" parent=\"NavigationRegion3D\" groups=[\"navsource\"]]");
        Line("collision_layer = 1");
        Line("collision_mask = 0");
        Line();
        Line("[node name=\"Mesh\" type=\"MeshInstance3D\" parent=\"NavigationRegion3D/Ground\"]");
        Line("mesh = SubResource(\"PlaneMesh_ground\")");
        Line("surface_material_override/0 = SubResource(\"Material_ground\")");
        Line();
        Line("[node name=\"Collision\" type=\"CollisionShape3D\" parent=\"NavigationRegion3D/Ground\"]");
        Line("transform = Transform3D(1, 0, 0, 0, 1, 0, 0, 0, 1, 0, -0.5, 0)");
        Line("shape = SubResource(\"BoxShape3D_ground\")");
        Line();

        foreach (var section in _sections)
        {
            Line($"; -- {section.Comment} --");
            Line();
            Line($"[node name=\"{section.Name}\" type=\"Node3D\" parent=\"NavigationRegion3D\"]");
            Line();

            foreach (var piece in section.Pieces) Piece(piece, $"NavigationRegion3D/{section.Name}");
        }

        var clearings = string.Join(", ", _clear.Select(c => $"{N(c.X)}, {N(c.Z)}, {N(c.R)}"));

        Line("; -- What grows and lies about, seeded, clear of the road and of every camp --");
        Line();

        foreach (var scatter in _scatters)
        {
            Line($"[node name=\"{scatter.Name}\" type=\"Node3D\" parent=\"NavigationRegion3D\"]");
            Line("transform = Transform3D(1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0)");
            Line("script = ExtResource(\"13_scatter\")");
            Line($"Pieces = PackedStringArray({string.Join(", ", scatter.Kits.Select(k => $"\"{k}\""))})");
            Line($"Count = {scatter.Count}");
            Line($"InnerRadius = {N(scatter.Inner)}.0");
            Line($"OuterRadius = {N(scatter.Outer)}.0");
            Line($"Spacing = {N(scatter.Spacing)}");
            Line($"Seed = {scatter.Seed}");
            Line($"Clearings = PackedVector3Array({clearings})");
            Line();

            foreach (var kit in scatter.Kits) Height(kit);
        }

        var start = _gates.Count > 0 ? (_gates[0].ArriveX, _gates[0].ArriveZ) : (0.0, 0.0);

        Line("; -- Session, player and population --");
        Line();
        Line("[node name=\"Player\" parent=\".\" instance=ExtResource(\"8_player\")]");
        Line($"transform = {At(start.Item1, 0.1, start.Item2)}");
        Line();
        Line("[node name=\"Session\" parent=\".\" instance=ExtResource(\"9_session\")]");
        Line();
        Line("[node name=\"Enemies\" type=\"Node3D\" parent=\".\"]");
        Line();

        foreach (var shrine in _shrines)
        {
            if (zone.Shrines.All(s => s.Id != shrine.Id)) throw new InvalidOperationException($"{File}: zone has no shrine '{shrine.Id}'.");

            Line($"[node name=\"{shrine.Name}\" type=\"Area3D\" parent=\".\"]");
            Line($"transform = {At(shrine.X, 0.1, shrine.Z)}");
            Line("script = ExtResource(\"6_shrine\")");
            Line($"ShrineId = \"{shrine.Id}\"");
            Line();
            Line($"[node name=\"VisualRoot\" type=\"Node3D\" parent=\"{shrine.Name}\"]");
            Line("transform = Transform3D(1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0.85, 0)");
            Line("script = ExtResource(\"7_visualroot\")");
            Line("VisualId = \"mesh_placeholder_shrine\"");
            Line();
        }

        Line("; Camps of creatures. What is in each and how fast it comes back is data; these are the level design.");
        Line();

        foreach (var field in _fields)
        {
            Line($"[node name=\"{field.Name}\" type=\"Node3D\" parent=\".\"]");
            Line($"transform = {At(field.X, 0, field.Z)}");
            Line("script = ExtResource(\"5_spawnfield\")");
            Line($"FieldId = \"{field.Id}\"");
            Line();
        }

        Line("; The shards are not placed here: the zone stands them at random spots.");
        Line();

        if (_safe is { } safe)
        {
            Line("; -- The camp at the way in: safe ground, a shrine, a fire, a shelter and a banner --");
            Line();
            Line("[node name=\"SafeCamp\" type=\"Node3D\" parent=\".\"]");
            Line($"transform = {At(safe.X, 0, safe.Z)}");
            Line("script = ExtResource(\"16_safe\")");
            Line($"RegionId = \"{safe.Id}\"");
            Line();

            foreach (var piece in _camp) Piece(piece, ".");
        }

        foreach (var person in _people)
        {
            if (!db.Npcs.TryGetValue(person.Id, out var npc) || npc.Zone != ZoneId) throw new InvalidOperationException($"{File}: '{person.Id}' is not one of this zone's people.");

            Line($"[node name=\"{person.Name}\" type=\"Area3D\" parent=\".\"]");
            Line($"transform = {At(person.X, 0.1, person.Z)}");
            Line("script = ExtResource(\"12_npc\")");
            Line($"NpcId = \"{person.Id}\"");
            Line();
        }

        foreach (var gate in _gates)
        {
            Line($"[node name=\"GateTo{gate.Name}\" type=\"Area3D\" parent=\".\"]");
            Line($"transform = {At(gate.X, 0, gate.Z)}");
            Line("script = ExtResource(\"10_gate\")");
            Line($"ToZone = \"{gate.Zone}\"");
            Line();
            Line($"[node name=\"ArrivalFrom{gate.Name}\" type=\"Node3D\" parent=\".\"]");
            Line($"transform = {At(gate.ArriveX, 0.1, gate.ArriveZ)}");
            Line("script = ExtResource(\"11_arrival\")");
            Line($"FromZone = \"{gate.Zone}\"");
            Line();
        }

        return b.ToString().TrimEnd('\n') + "\n";
    }
}
