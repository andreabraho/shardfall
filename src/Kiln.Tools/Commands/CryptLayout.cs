using System.Globalization;
using System.Text;
using Kiln.Data.Loading;

namespace Kiln.Tools.Commands;

/// <summary>
/// Writes the Abyss Crypts' scene (P5, 2026-10-07): <c>game/scenes/crypts.tscn</c>, seven rooms stacked as the
/// Demon Tower's are, each laid out for its floor's task in <c>game/data/zones/crypts.json</c>.
/// </summary>
/// <remarks>
/// The tower's scene was written by a script that was never kept; this one is kept, beside act 2's maps, and
/// <c>kiln scenes</c> writes and checks it with them. Change the layout here, never the scene.
/// <para>
/// Every room is the tower's forty-metre hall in the crypts' stone, twenty-four metres under the one above. What
/// differs is what stands inside it: a statue in the ossuary, a walled court with four doorways, seven marks in a
/// ring, four halls with three gates at the end of each, and the three bosses' rooms.
/// </para>
/// </remarks>
public static class CryptLayout
{
    public const string File = "crypts";

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private const double Drop = 24;

    private sealed record Room(string Id, string Comment);

    private static readonly Room[] Rooms =
    [
        new("flr_crypt_ossuary", "The Ossuary: coffins and bones, a ring of pillars, the statue in the middle"),
        new("flr_crypt_doors", "The Sealed Doors: a walled court in the middle, a doorway on each side, the stair inside"),
        new("flr_crypt_judgement", "The Stones of Judgement: seven marks in a ring"),
        new("flr_crypt_maze", "The Portal Maze: four halls side by side, three gates at the east end of each"),
        new("flr_crypt_cell", "The Jailer's Cell: cages and chains"),
        new("flr_crypt_landing", "The Landing of Souls: obelisks and candles, a shrine and a bench"),
        new("flr_crypt_throne", "The Abyss Throne: a ring of fire round the throne, a shrine and a bench"),
    ];

    private static string N(double value) => Math.Round(value, 3).ToString("0.###", Inv);

    private static string At(double x, double y, double z, double yaw = 0)
    {
        if (yaw == 0) return $"Transform3D(1, 0, 0, 0, 1, 0, 0, 0, 1, {N(x)}, {N(y)}, {N(z)})";

        var rad = yaw * Math.PI / 180;
        var (c, s) = (Math.Round(Math.Cos(rad), 6), Math.Round(Math.Sin(rad), 6));

        return string.Create(Inv, $"Transform3D({c:0.######}, 0, {s:0.######}, 0, 1, 0, {-s:0.######}, 0, {c:0.######}, {N(x)}, {N(y)}, {N(z)})");
    }

    public static string Write(ContentDatabase db)
    {
        var zone = db.Zones.TryGetValue("zone_abyss_crypts", out var found) ? found : throw new InvalidOperationException("no zone 'zone_abyss_crypts'.");

        if (zone.Floors.Length != Rooms.Length || zone.Floors.Select(f => f.Id).Where((id, i) => id != Rooms[i].Id).Any())
        {
            throw new InvalidOperationException("crypts: the rooms here and the floors in crypts.json have drifted apart.");
        }

        double Height(string kit) => db.KitPieces.TryGetValue(kit, out var def) && def.Size.Length == 3
            ? def.Size[1]
            : throw new InvalidOperationException($"crypts: no kit piece '{kit}'.");

        var b = new StringBuilder();
        var names = new Dictionary<string, int>();

        void Line(string text = "") => b.Append(text).Append('\n');

        string Name(string parent, string wanted)
        {
            var key = parent + "/" + wanted;
            var n = names[key] = names.GetValueOrDefault(key) + 1;

            return n == 1 ? wanted : wanted + n.ToString(Inv);
        }

        void Piece(string parent, string kit, double x, double z, double yaw = 0, string? name = null)
        {
            var label = Name(parent, name ?? string.Concat(kit.Split('_').Skip(1).Select(w => char.ToUpperInvariant(w[0]) + w[1..])));

            Line($"[node name=\"{label}\" type=\"StaticBody3D\" parent=\"{parent}\"]");
            Line($"transform = {At(x, Height(kit) / 2, z, yaw)}");
            Line("script = ExtResource(\"3_kit\")");
            Line($"PieceId = \"{kit}\"");
            Line();
        }

        void Node(string parent, string name, double x, double z, double yaw = 0)
        {
            Line($"[node name=\"{name}\" type=\"Node3D\" parent=\"{parent}\"]");
            Line($"transform = {At(x, 0.1, z, yaw)}");
            Line();
        }

        void Group(string parent, string name)
        {
            Line($"[node name=\"{name}\" type=\"Node3D\" parent=\"{parent}\"]");
            Line();
        }

        void Marks(string floor, params (double X, double Z, double Yaw)[] marks)
        {
            Group(floor, "Marks");

            for (var i = 0; i < marks.Length; i++) Node($"{floor}/Marks", $"Mark{i + 1}", marks[i].X, marks[i].Z, marks[i].Yaw);
        }

        void Spawns(string floor, params (double X, double Z)[] spots)
        {
            Group(floor, "Spawns");

            for (var i = 0; i < spots.Length; i++) Node($"{floor}/Spawns", $"Spawn{i + 1}", spots[i].X, spots[i].Z);
        }

        void Stair(string floor, double x, double z)
        {
            Line($"[node name=\"Stair\" type=\"Area3D\" parent=\"{floor}\"]");
            Line($"transform = {At(x, 0.1, z)}");
            Line("script = ExtResource(\"11_stair\")");
            Line();
        }

        void Shrine(string floor, string id, double x, double z)
        {
            Line($"[node name=\"Shrine\" type=\"Area3D\" parent=\"{floor}\"]");
            Line($"transform = {At(x, 0.1, z)}");
            Line("script = ExtResource(\"4_shrine\")");
            Line($"ShrineId = \"{id}\"");
            Line();
            Line($"[node name=\"VisualRoot\" type=\"Node3D\" parent=\"{floor}/Shrine\"]");
            Line("transform = Transform3D(1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0.85, 0)");
            Line("script = ExtResource(\"6_visualroot\")");
            Line("VisualId = \"mesh_placeholder_shrine\"");
            Line();
        }

        void Bench(string floor, double x, double z)
        {
            Line($"[node name=\"Bench\" type=\"Area3D\" parent=\"{floor}\"]");
            Line($"transform = {At(x, 0.1, z)}");
            Line("script = ExtResource(\"12_bench\")");
            Line();
            Line($"[node name=\"Piece\" type=\"StaticBody3D\" parent=\"{floor}/Bench\"]");
            Line($"transform = {At(0, Height("kit_camp_anvil") / 2, 0)}");
            Line("script = ExtResource(\"3_kit\")");
            Line("PieceId = \"kit_camp_anvil\"");
            Line();
        }

        void Light(string floor, string name, double x, double y, double z, string colour, double energy, double range)
        {
            Line($"[node name=\"{name}\" type=\"OmniLight3D\" parent=\"{floor}\"]");
            Line($"transform = {At(x, y, z)}");
            Line($"light_color = {colour}");
            Line($"light_energy = {N(energy)}");
            Line($"omni_range = {N(range)}");
            Line();
        }

        // The hall every room is: the floor, its four walls of crypt stone, a column at each corner, and fires
        // along the walls.
        void Hall(string floor)
        {
            Line($"[node name=\"Slab\" type=\"StaticBody3D\" parent=\"{floor}\" groups=[\"navsource\"]]");
            Line("collision_layer = 1");
            Line("collision_mask = 0");
            Line();
            Line($"[node name=\"Mesh\" type=\"MeshInstance3D\" parent=\"{floor}/Slab\"]");
            Line("mesh = SubResource(\"PlaneMesh_slab\")");
            Line("surface_material_override/0 = SubResource(\"Material_slab\")");
            Line();
            Line($"[node name=\"Collision\" type=\"CollisionShape3D\" parent=\"{floor}/Slab\"]");
            Line("transform = Transform3D(1, 0, 0, 0, 1, 0, 0, 0, 1, 0, -0.5, 0)");
            Line("shape = SubResource(\"BoxShape3D_slab\")");
            Line();

            foreach (var along in new[] { -16.0, -8, 0, 8, 16 })
            {
                Piece(floor, "kit_cr_wall_8", along, -20.3, 0, "Wall");
                Piece(floor, "kit_cr_wall_8", along, 20.3, 180, "Wall");
                Piece(floor, "kit_cr_wall_8", -20.3, along, -90, "Wall");
                Piece(floor, "kit_cr_wall_8", 20.3, along, 90, "Wall");
            }

            foreach (var (x, z) in new[] { (-19.6, -19.6), (19.6, -19.6), (19.6, 19.6), (-19.6, 19.6) }) Piece(floor, "kit_cr_tower", x, z, 0, "Corner");

            foreach (var (x, z) in new[] { (-12.0, -18.6), (12.0, -18.6), (-12.0, 18.6), (12.0, 18.6), (-18.6, -12.0), (18.6, -12.0), (-18.6, 12.0), (18.6, 12.0) })
            {
                Piece(floor, "kit_demon_fire", x, z, 0, "Fire");
            }
        }

        // ------------------------------------------------------------------ the header

        Line("[gd_scene load_steps=16 format=3]");
        Line();
        Line("; zone_abyss_crypts — the Abyss Crypts (P5, 2026-10-07), band 40–42: the last dungeon, under the");
        Line("; Cinderlands, after the shape of the original's catacombs (docs/11-abyss-crypts.md).");
        Line(";");
        Line("; Seven rooms, one footprint, stacked twenty-four metres apart, only the occupied one awake.");
        Line("; Black and violet flagstones, walls of crypt stone, fires burning violet. What lives here is");
        Line("; only what data cannot hold: where the player lands, where the stair is, where a target may");
        Line("; stand, where a wave walks in, and the walls that make each room what it is. What each floor");
        Line("; asks is in game/data/zones/crypts.json. Written by `kiln scenes` from");
        Line("; src/Kiln.Tools/Commands/CryptLayout.cs: change the layout, not this file.");
        Line();
        Line("[ext_resource type=\"Script\" path=\"res://scripts/World/ZoneRoot.cs\" id=\"1_zoneroot\"]");
        Line("[ext_resource type=\"Script\" path=\"res://scripts/World/NavBaker.cs\" id=\"2_navbaker\"]");
        Line("[ext_resource type=\"Script\" path=\"res://scripts/World/KitPiece.cs\" id=\"3_kit\"]");
        Line("[ext_resource type=\"Script\" path=\"res://scripts/World/ShrineNode.cs\" id=\"4_shrine\"]");
        Line("[ext_resource type=\"Script\" path=\"res://scripts/Visual/VisualRoot.cs\" id=\"6_visualroot\"]");
        Line("[ext_resource type=\"Script\" path=\"res://scripts/World/ZoneGate.cs\" id=\"7_gate\"]");
        Line("[ext_resource type=\"Script\" path=\"res://scripts/World/ZoneArrival.cs\" id=\"8_arrival\"]");
        Line("[ext_resource type=\"Script\" path=\"res://scripts/World/TowerNode.cs\" id=\"9_tower\"]");
        Line("[ext_resource type=\"Script\" path=\"res://scripts/World/TowerFloorNode.cs\" id=\"10_floor\"]");
        Line("[ext_resource type=\"Script\" path=\"res://scripts/World/FloorStair.cs\" id=\"11_stair\"]");
        Line("[ext_resource type=\"Script\" path=\"res://scripts/World/BenchNode.cs\" id=\"12_bench\"]");
        Line("[ext_resource type=\"PackedScene\" path=\"res://scenes/player.tscn\" id=\"13_player\"]");
        Line("[ext_resource type=\"PackedScene\" path=\"res://scenes/session.tscn\" id=\"14_session\"]");
        Line();
        Line("[sub_resource type=\"NavigationMesh\" id=\"NavigationMesh_crypts\"]");
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
        Line("[sub_resource type=\"PlaneMesh\" id=\"PlaneMesh_slab\"]");
        Line("size = Vector2(40, 40)");
        Line();
        Line("; Flagstones, two metres, rows offset: black and deep violet at random, a violet seam between.");
        Line("[sub_resource type=\"Shader\" id=\"Shader_flags\"]");
        Line("code = \"shader_type spatial;");
        Line();
        Line("uniform vec3 black : source_color = vec3(0.07, 0.06, 0.09);");
        Line("uniform vec3 violet : source_color = vec3(0.16, 0.08, 0.24);");
        Line("uniform vec3 seam : source_color = vec3(0.16, 0.06, 0.24);");
        Line();
        Line("varying vec3 wp;");
        Line();
        Line("float hash(vec2 p) {");
        Line("\treturn fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);");
        Line("}");
        Line();
        Line("void vertex() {");
        Line("\twp = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;");
        Line("}");
        Line();
        Line("void fragment() {");
        Line("\tvec2 p = wp.xz / 2.0;");
        Line("\tfloat row = floor(p.y);");
        Line("\tp.x += mod(row, 2.0) * 0.5;");
        Line("\tvec2 cell = floor(p);");
        Line("\tvec2 f = fract(p);");
        Line("\tfloat h = hash(cell);");
        Line("\tvec3 stone = mix(black, violet, step(0.55, hash(cell + 7.3)));");
        Line("\tstone *= 0.7 + 0.55 * h;");
        Line("\tstone *= 0.88 + 0.24 * hash(floor(wp.xz * 5.0));");
        Line("\tfloat edge = min(min(f.x, 1.0 - f.x), min(f.y, 1.0 - f.y));");
        Line("\tfloat g = smoothstep(0.015, 0.045, edge);");
        Line("\tALBEDO = mix(seam, stone, g);");
        Line("\tEMISSION = seam * (1.0 - g) * 0.2;");
        Line("\tROUGHNESS = 0.8 - 0.3 * h;");
        Line("}\"");
        Line();
        Line("[sub_resource type=\"ShaderMaterial\" id=\"Material_slab\"]");
        Line("shader = SubResource(\"Shader_flags\")");
        Line();
        Line("[sub_resource type=\"BoxShape3D\" id=\"BoxShape3D_slab\"]");
        Line("size = Vector3(40, 1, 40)");
        Line();
        Line("[sub_resource type=\"Environment\" id=\"Environment_crypts\"]");
        Line("background_mode = 1");
        Line("background_color = Color(0.02, 0.01, 0.04, 1)");
        Line("ambient_light_source = 2");
        Line("ambient_light_color = Color(0.36, 0.28, 0.48, 1)");
        Line("ambient_light_energy = 0.5");
        Line("fog_enabled = true");
        Line("fog_light_color = Color(0.1, 0.05, 0.18, 1)");
        Line("fog_density = 0.012");
        Line();
        Line("[node name=\"Crypts\" type=\"Node3D\"]");
        Line("script = ExtResource(\"1_zoneroot\")");
        Line("ZoneId = \"zone_abyss_crypts\"");
        Line();
        Line("[node name=\"WorldEnvironment\" type=\"WorldEnvironment\" parent=\".\"]");
        Line("environment = SubResource(\"Environment_crypts\")");
        Line();
        Line("[node name=\"DirectionalLight3D\" type=\"DirectionalLight3D\" parent=\".\"]");
        Line("transform = Transform3D(0.8660254, -0.25, 0.4330127, 0, 0.8660254, 0.5, -0.5, -0.4330127, 0.75, 0, 14, 0)");
        Line("light_color = Color(0.8, 0.7, 1, 1)");
        Line("light_energy = 0.35");
        Line("shadow_enabled = true");
        Line();
        Line("[node name=\"NavigationRegion3D\" type=\"NavigationRegion3D\" parent=\".\"]");
        Line("navigation_mesh = SubResource(\"NavigationMesh_crypts\")");
        Line("script = ExtResource(\"2_navbaker\")");
        Line();
        Line("[node name=\"Tower\" type=\"Node3D\" parent=\"NavigationRegion3D\"]");
        Line("script = ExtResource(\"9_tower\")");
        Line();

        // ------------------------------------------------------------------ the rooms

        for (var i = 0; i < Rooms.Length; i++)
        {
            var room = Rooms[i];
            var floor = $"NavigationRegion3D/Tower/Floor{i + 1:00}";

            Line($"; -- Floor {i + 1} · {room.Id} -----------------------------------------------------------");
            Line($"; {room.Comment}");
            Line();
            Line($"[node name=\"Floor{i + 1:00}\" type=\"Node3D\" parent=\"NavigationRegion3D/Tower\"]");
            Line($"transform = {At(0, -Drop * i, 0)}");
            Line("script = ExtResource(\"10_floor\")");
            Line($"FloorId = \"{room.Id}\"");
            Line();

            Hall(floor);

            switch (room.Id)
            {
                case "flr_crypt_ossuary":
                    Node(floor, "Entry", -15, 0);
                    Stair(floor, 15, 0);
                    Marks(floor, (0, 0, 0));
                    Spawns(floor, (-16, -16), (16, -16), (16, 16), (0, -16), (16, 0), (6, 16));
                    // Nothing comes in by the way out, so the key never falls where picking it up walks the player out.
                    Shrine(floor, "shr_crypt_mouth", -15, 6);

                    for (var p = 0; p < 6; p++)
                    {
                        var a = (p * 60 + 30) * Math.PI / 180;

                        Piece(floor, "kit_cr_pillar", Math.Cos(a) * 8, Math.Sin(a) * 8, 0, "Pillar");
                    }

                    foreach (var (x, z, yaw) in new[] { (-10.0, -14.0, 0.0), (-4, -15, 20), (4, 15, 180), (10, 14, 160), (12, -12, 70), (-12, 12, 250) })
                    {
                        Piece(floor, "kit_hb_coffin", x, z, yaw, "Coffin");
                    }

                    foreach (var (x, z) in new[] { (-6.0, -10.0), (7, -9), (-8, 9), (6, 11), (14, -4), (13, 6), (-3, 13), (2, -13) }) Piece(floor, "kit_bones", x, z, 0, "Bones");
                    foreach (var (x, z) in new[] { (-2.5, -2.5), (2.5, 2.5), (-2.5, 2.5), (2.5, -2.5) }) Piece(floor, "kit_hb_candles", x * 1.6, z * 1.6, 0, "Candles");

                    Light(floor, "StatueLight", 0, 5, 0, "Color(0.7, 0.4, 1, 1)", 2.2, 14);
                    break;

                case "flr_crypt_doors":
                    // The court: twelve metres a side, a four-metre wall either side of a four-metre doorway.
                    // In the corner, out of reach of the doors' guards: they wake as the player comes to them, not on arrival.
                    Node(floor, "Entry", -16, -15);
                    Stair(floor, 0, 0);
                    Marks(floor, (0, -6, 0), (6, 0, 90), (0, 6, 180), (-6, 0, -90));
                    Spawns(floor, (16, -16), (16, 16), (-16, 16), (0, -16), (0, 16), (16, 0));

                    foreach (var side in new[] { -4.0, 4.0 })
                    {
                        Piece(floor, "kit_cr_wall_4", side, -6, 0, "Court");
                        Piece(floor, "kit_cr_wall_4", side, 6, 180, "Court");
                        Piece(floor, "kit_cr_wall_4", -6, side, -90, "Court");
                        Piece(floor, "kit_cr_wall_4", 6, side, 90, "Court");
                    }

                    foreach (var (x, z) in new[] { (-6.0, -6.0), (6, -6), (6, 6), (-6, 6) }) Piece(floor, "kit_cr_pillar", x, z, 0, "CourtCorner");
                    foreach (var (x, z) in new[] { (-2.2, -8.0), (2.2, -8), (8, -2.2), (8, 2.2), (2.2, 8), (-2.2, 8), (-8, 2.2), (-8, -2.2) }) Piece(floor, "kit_hb_candles", x, z, 0, "DoorCandles");
                    foreach (var (x, z) in new[] { (-14.0, -10.0), (12, -14), (14, 12), (-11, 14) }) Piece(floor, "kit_bones", x, z, 0, "Bones");

                    Light(floor, "CourtLight", 0, 6, 0, "Color(0.6, 0.35, 1, 1)", 2.0, 12);
                    break;

                case "flr_crypt_judgement":
                    Node(floor, "Entry", -15, 0);
                    Stair(floor, 15, 0);
                    Marks(floor, Enumerable.Range(0, 7).Select(k =>
                    {
                        var a = (k * 360.0 / 7 + 90) * Math.PI / 180;

                        return (Math.Round(Math.Cos(a) * 10, 2), Math.Round(Math.Sin(a) * 10, 2), 0.0);
                    }).ToArray());
                    Spawns(floor, (-16, -16), (16, -16), (16, 16), (-16, 16), (0, -17), (0, 17));

                    foreach (var (x, z) in new[] { (-15.0, -15.0), (15, -15), (15, 15), (-15, 15) }) Piece(floor, "kit_kn_obelisk", x, z, 0, "Obelisk");
                    foreach (var (x, z) in new[] { (-4.0, -16.0), (4, 16), (-16, 8), (16, -8) }) Piece(floor, "kit_hb_marker", x, z, 0, "Marker");

                    Light(floor, "RingLight", 0, 6, 0, "Color(0.75, 0.3, 1, 1)", 2.4, 16);
                    break;

                case "flr_crypt_maze":
                {
                    // Four halls ten metres wide, side by side north to south; walls between them run the whole
                    // width. Each hall is walked west to east; its three gates stand at the east end.
                    Node(floor, "Entry", -15, -15);

                    foreach (var z in new[] { -10.0, 0, 10 })
                    {
                        foreach (var along in new[] { -16.0, -8, 0, 8, 16 }) Piece(floor, "kit_cr_wall_8", along, z, 0, "Divide");
                    }

                    Group(floor, "Halls");

                    for (var h = 0; h < 4; h++)
                    {
                        var z = -15 + (10 * h);
                        var hall = $"{floor}/Halls/Hall{h + 1}";

                        Group($"{floor}/Halls", $"Hall{h + 1}");
                        Node(hall, "Start", -15, z);

                        if (h == 3) continue;

                        Group(hall, "Gates");

                        for (var g = 0; g < 3; g++) Node($"{hall}/Gates", $"Gate{g + 1}", 16, z - 3 + (3 * g), -90);
                    }

                    Stair(floor, 16, 15);
                    Marks(floor, (11, 15, 0));
                    Spawns(floor, (0, -15), (4, -15), (0, -5), (4, -5), (0, 5), (4, 5), (0, 15));

                    foreach (var (x, z) in new[] { (-8.0, -17.5), (6, -12.5), (-6, -7.5), (8, -2.5), (-8, 2.5), (6, 7.5), (-6, 12.5), (4, 17.5) }) Piece(floor, "kit_bones", x, z, 0, "Bones");

                    Light(floor, "GateLight1", 15, 4, -15, "Color(0.6, 0.3, 1, 1)", 1.4, 8);
                    Light(floor, "GateLight2", 15, 4, -5, "Color(0.6, 0.3, 1, 1)", 1.4, 8);
                    Light(floor, "GateLight3", 15, 4, 5, "Color(0.6, 0.3, 1, 1)", 1.4, 8);
                    Light(floor, "PoleLight", 12, 4, 15, "Color(1, 0.4, 0.6, 1)", 1.8, 9);
                    break;
                }

                case "flr_crypt_cell":
                    Node(floor, "Entry", -15, 0);
                    Stair(floor, 15, 0);
                    Spawns(floor, (-16, -16), (16, -16), (16, 16), (-16, 16));

                    foreach (var (x, z, yaw) in new[] { (-13.0, -13.0, 0.0), (13, -13, 90), (13, 13, 180), (-13, 13, 270), (0, -15, 0), (0, 15, 180) })
                    {
                        Piece(floor, "kit_camp_cage", x, z, yaw, "Cage");
                    }

                    foreach (var (x, z, yaw) in new[] { (-6.0, -8.0, 20.0), (7, -6, 110), (5, 8, 200), (-8, 5, 300), (-3, 12, 40), (10, 2, 160) })
                    {
                        Piece(floor, "kit_camp_chains", x, z, yaw, "Chains");
                    }

                    Light(floor, "CellLight", 0, 6, 0, "Color(0.55, 0.4, 0.9, 1)", 2.0, 16);
                    break;

                case "flr_crypt_landing":
                    Node(floor, "Entry", -15, 0);
                    Stair(floor, 15, 0);
                    Spawns(floor, (-16, -16), (16, -16), (16, 16), (-16, 16));
                    Shrine(floor, "shr_crypt_landing", -15, 6);
                    Bench(floor, -15, -6);

                    foreach (var (x, z) in new[] { (-9.0, -9.0), (9, -9), (9, 9), (-9, 9) }) Piece(floor, "kit_kn_obelisk", x, z, 0, "Obelisk");
                    foreach (var (x, z) in new[] { (-12.0, 0.0), (12, 0), (0, -12), (0, 12) }) Piece(floor, "kit_hb_candles", x, z, 0, "Candles");
                    foreach (var (x, z) in new[] { (-14.0, -12.0), (13, -14), (14, 13), (-12, 14) }) Piece(floor, "kit_hb_marker", x, z, 0, "Marker");

                    Light(floor, "LandingLight", 0, 6, 0, "Color(0.45, 0.35, 1, 1)", 2.2, 16);
                    break;

                case "flr_crypt_throne":
                    Node(floor, "Entry", -15, 0);
                    Stair(floor, 15, 0);
                    Spawns(floor, (-16, -16), (16, -16), (16, 16), (-16, 16));
                    Shrine(floor, "shr_crypt_throne", -15, 6);
                    Bench(floor, -15, -6);

                    Piece(floor, "kit_kn_altar", 0, 14, 180, "Throne");

                    for (var p = 0; p < 10; p++)
                    {
                        var a = p * 36 * Math.PI / 180;

                        Piece(floor, "kit_demon_fire", Math.Round(Math.Cos(a) * 11, 2), Math.Round(Math.Sin(a) * 11, 2), 0, "Ring");
                    }

                    foreach (var (x, z) in new[] { (-7.0, 16.0), (7, 16) }) Piece(floor, "kit_cr_pillar", x, z, 0, "ThronePillar");
                    foreach (var (x, z, yaw) in new[] { (-14.0, -10.0, 30.0), (12, -12, 120), (14, 9, 200), (-6, -15, 80) }) Piece(floor, "kit_camp_chains", x, z, yaw, "Chains");

                    Light(floor, "ThroneLight", 0, 6, 0, "Color(1, 0.3, 0.55, 1)", 2.6, 22);
                    break;
            }
        }

        // ------------------------------------------------------------------ the way back out

        Line("; -- Session, player and the way back out -----------------------------------------");
        Line("; The border stands on floor one beside the shrine: you leave the way you came in.");
        Line();
        Line("[node name=\"Player\" parent=\".\" instance=ExtResource(\"13_player\")]");
        Line($"transform = {At(-15, 0.1, 0)}");
        Line();
        Line("[node name=\"Session\" parent=\".\" instance=ExtResource(\"14_session\")]");
        Line();
        Line("[node name=\"Enemies\" type=\"Node3D\" parent=\".\"]");
        Line();
        Line("[node name=\"GateToCinderlands\" type=\"Area3D\" parent=\".\"]");
        Line($"transform = {At(-17, 0, 14)}");
        Line("script = ExtResource(\"7_gate\")");
        Line("ToZone = \"zone_cinderlands\"");
        Line("AlwaysSigned = true");
        Line();
        Line("[node name=\"ArrivalFromCinderlands\" type=\"Node3D\" parent=\".\"]");
        Line($"transform = {At(-15, 0.1, 0)}");
        Line("script = ExtResource(\"8_arrival\")");
        Line("FromZone = \"zone_cinderlands\"");

        return b.ToString();
    }
}
