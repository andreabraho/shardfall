using System.Globalization;
using Kiln.Data.Loading;

namespace Kiln.Tools.Commands;

/// <summary>
/// Writes the Demon Tower's scene: <c>game/scenes/catacombs.tscn</c>, nine floors on one footprint, each hall
/// its own shape (2026-10-09). <c>kiln scenes</c> writes and checks it with act 2's maps and the crypts.
/// </summary>
/// <remarks>
/// Moved here from a script kept outside the repository, line for line: the same pieces in the same order, the
/// same seeded dressing, so the scene it writes is the scene that was there. Change the layout here, never the
/// scene.
/// </remarks>
public static partial class TowerLayout
{
    public const string File = "catacombs";

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private sealed record Floor(string Id, string Shrine, bool Bench, bool Refuge, string Dressing, string Layout);

    private static readonly Floor[] Floors =
    [
        new("flr_the_mouth", "shr_catacombs_mouth", false, false, "crypt", "vestibule"),
        new("flr_the_cistern", "", false, false, "crypt", "basin"),
        new("flr_the_gallery", "", false, false, "arena", "gallery"),
        new("flr_the_lantern_walk", "shr_catacombs_gallery", true, false, "crypt", "aisles"),
        new("flr_the_sealed_crossing", "", false, true, "hall", "circle"),
        new("flr_the_choir_vault", "", false, false, "arena", "choir"),
        new("flr_the_falling_stair", "shr_catacombs_vault", true, false, "hall", "zigzag"),
        new("flr_the_black_cloister", "", false, false, "hall", "cloister"),
        new("flr_the_heart", "", true, false, "throne", "throne"),
    ];

    private static double Fixed(double value, int places) => Math.Round(value, places, MidpointRounding.AwayFromZero);

    /// <summary>A number as the old script wrote it: rounded to so many places, no trailing zeros, never "-0".</summary>
    private static string Num(double value, int places)
    {
        var rounded = Fixed(value, places);

        return rounded == 0 ? "0" : rounded.ToString("0." + new string('#', places), Inv);
    }

    private static string At(double yaw, double x, double y, double z)
    {
        var r = yaw * Math.PI / 180;
        var c = Fixed(Math.Cos(r), 6);
        var s = Fixed(Math.Sin(r), 6);

        return $"Transform3D({Num(c, 6)}, 0, {Num(s, 6)}, 0, 1, 0, {Num(-s, 6)}, 0, {Num(c, 6)}, {Num(x, 3)}, {Num(y, 3)}, {Num(z, 3)})";
    }

    /// <summary>The old script's generator, so the dressing falls where it fell.</summary>
    private sealed class Lcg(uint seed)
    {
        private uint _s = seed;

        public double Next()
        {
            unchecked { _s = (_s * 1664525u) + 1013904223u; }

            return _s / 4294967296.0;
        }
    }

    public static string Write(ContentDatabase db)
    {
        var zone = db.Zones.TryGetValue("zone_catacombs", out var found) ? found : throw new InvalidOperationException("no zone 'zone_catacombs'.");

        if (zone.Floors.Length != Floors.Length || zone.Floors.Select(f => f.Id).Where((id, i) => id != Floors[i].Id).Any())
        {
            throw new InvalidOperationException("tower: the floors here and the floors in act1.json have drifted apart.");
        }

        double Half(string kit) => db.KitPieces.TryGetValue(kit, out var def) && def.Size.Length == 3
            ? def.Size[1] / 2
            : throw new InvalidOperationException($"tower: no kit piece '{kit}'.");

        var blocks = new List<string> { Header.TrimEnd('\n') };
        var n = 0;

        void Comment(string text) => blocks.Add(string.Join("\n", text.Split('\n').Select(l => l.Length > 0 ? "; " + l : ";")));

        void Piece(string parent, string id, double x, double z, double yaw = 0, double? y = null)
        {
            var name = string.Concat(id["kit_".Length..].Split('_').Select(w => char.ToUpperInvariant(w[0]) + w[1..]));

            blocks.Add($"[node name=\"{name}{++n}\" type=\"StaticBody3D\" parent=\"{parent}\"]\n" +
                $"transform = {At(yaw, x, y ?? Half(id), z)}\nscript = ExtResource(\"3_kit\")\nPieceId = \"{id}\"");
        }

        for (var i = 0; i < Floors.Length; i++)
        {
            var (id, shrine, bench, refuge, dressing, layout) = Floors[i];
            var depth = i + 1;
            var pad = depth.ToString("00", Inv);
            var at = $"NavigationRegion3D/Tower/Floor{pad}";
            var rand = new Lcg((uint)(100 + depth));

            Comment($"-- Floor {depth} · {id} -----------------------------------------------------------");
            blocks.Add($"[node name=\"Floor{pad}\" type=\"Node3D\" parent=\"NavigationRegion3D/Tower\"]\ntransform = {At(0, 0, -24 * i, 0)}\nscript = ExtResource(\"10_floor\")\nFloorId = \"{id}\"");
            blocks.Add($"[node name=\"Slab\" type=\"StaticBody3D\" parent=\"{at}\" groups=[\"navsource\"]]\ncollision_layer = 1\ncollision_mask = 0\n\n" +
                $"[node name=\"Mesh\" type=\"MeshInstance3D\" parent=\"{at}/Slab\"]\nmesh = SubResource(\"PlaneMesh_slab\")\nsurface_material_override/0 = SubResource(\"Material_slab\")\n\n" +
                $"[node name=\"Collision\" type=\"CollisionShape3D\" parent=\"{at}/Slab\"]\ntransform = {At(0, 0, -0.5, 0)}\nshape = SubResource(\"BoxShape3D_slab\")");

            // Walls, eight metres a piece, and a tower at each corner. Nothing leaves but by the stair.
            foreach (var c in new[] { -16, -8, 0, 8, 16 })
            {
                Piece(at, "kit_wall_8", c, -20.3, 0);
                Piece(at, "kit_wall_8", c, 20.3, 180);
                Piece(at, "kit_wall_8", -20.3, c, -90);
                Piece(at, "kit_wall_8", 20.3, c, 90);
            }

            foreach (var (x, z) in new[] { (-19.6, -19.6), (19.6, -19.6), (19.6, 19.6), (-19.6, 19.6) }) Piece(at, "kit_tower", x, z);

            blocks.Add($"[node name=\"Entry\" type=\"Node3D\" parent=\"{at}\"]\ntransform = {At(0, -15, 0.1, 0)}\n\n" +
                $"[node name=\"Stair\" type=\"Area3D\" parent=\"{at}\"]\ntransform = {At(0, 15, 0.1, 0)}\nscript = ExtResource(\"11_stair\")");

            blocks.Add($"[node name=\"Marks\" type=\"Node3D\" parent=\"{at}\"]");
            var marks = new (double X, double Z)[] { (-8, -10), (0, -12), (8, -10), (11, 0), (8, 10), (0, 12), (-8, 10), (-11, 0) };
            for (var m = 0; m < marks.Length; m++) blocks.Add($"[node name=\"Mark{m + 1}\" type=\"Node3D\" parent=\"{at}/Marks\"]\ntransform = {At(0, marks[m].X, 0.1, marks[m].Z)}");

            blocks.Add($"[node name=\"Spawns\" type=\"Node3D\" parent=\"{at}\"]");
            var spawns = new (double X, double Z)[] { (-16, -16), (16, -16), (16, 16), (-16, 16), (0, -17), (0, 17) };
            for (var m = 0; m < spawns.Length; m++) blocks.Add($"[node name=\"Spawn{m + 1}\" type=\"Node3D\" parent=\"{at}/Spawns\"]\ntransform = {At(0, spawns[m].X, 0.1, spawns[m].Z)}");

            // The hall's bones: its shape (below), fires at the entry and the stair, banners on the walls.
            var keep = new List<(double X, double Z, double R)>();

            void Wall(double x, double z, double yaw, string kit = "kit_wall_8")
            {
                Piece(at, kit, x, z, yaw);
                keep.Add((x, z, kit == "kit_wall_8" ? 5 : 3));
            }

            void Pillar(double x, double z)
            {
                Piece(at, "kit_pillar", x, z);
                keep.Add((x, z, 2));
            }

            switch (layout)
            {
                case "vestibule":
                    // Two rows of pillars down the middle, broken walls in the corners.
                    foreach (var x in new[] { -6, 0, 6 }) { Pillar(x, -8); Pillar(x, 8); }

                    foreach (var (x, z, y) in new[] { (-13.0, -15.0, 30.0), (13, -15, -30), (-13, 15, -30), (13, 15, 30) })
                    {
                        Piece(at, "kit_ruin_wall", x, z, y);
                        keep.Add((x, z, 3));
                    }

                    break;
                case "basin":
                    // A low-walled basin in the middle, a gap on each side; the old four pillars round it.
                    foreach (var (x, z, y) in new[] { (-4.0, -6.0, 0.0), (4, -6, 0), (-4, 6, 0), (4, 6, 0), (-6, -4, 90), (-6, 4, 90), (6, -4, 90), (6, 4, 90) }) Wall(x, z, y, "kit_wall_low_4");
                    foreach (var (x, z) in new[] { (-10, -10), (10, -10), (10, 10), (-10, 10) }) Pillar(x, z);
                    break;
                case "gallery":
                    // A long gallery: walls down both sides, open at the ends where the waves come round.
                    foreach (var x in new[] { -8, 0, 8 }) { Wall(x, -14.5, 0); Wall(x, 14.5, 0); }
                    break;
                case "aisles":
                    // Aisles: four wall runs either side of the way through, a crossing in the middle.
                    foreach (var (x, z) in new[] { (-8, -5), (8, -5), (-8, 5), (8, 5) }) Wall(x, z, 0);
                    break;
                case "circle":
                    // A circle of eight pillars round the crossing.
                    for (var k = 0; k < 8; k++)
                    {
                        var a = (k + 0.5) * Math.PI / 4;
                        Pillar(Fixed(Math.Cos(a) * 6, 2), Fixed(Math.Sin(a) * 6, 2));
                    }

                    break;
                case "choir":
                    // Choir stalls: two rows of pillars along the hall.
                    foreach (var x in new[] { -12, -4, 4, 12 }) { Pillar(x, -7); Pillar(x, 7); }
                    break;
                case "zigzag":
                    // The falling stair: two walls across the hall, the gaps at opposite ends — the way through zigzags.
                    foreach (var z in new[] { -16, -8, 0, 6 }) Wall(-5, z, 90);
                    foreach (var z in new[] { 16, 8, 0, -6 }) Wall(5, z, 90);
                    break;
                case "cloister":
                    // A cloister: a square colonnade round an open court.
                    foreach (var c in new[] { -8, -4, 0, 4, 8 }) { Pillar(c, -8); Pillar(c, 8); }
                    foreach (var c in new[] { -4, 0, 4 }) { Pillar(-8, c); Pillar(8, c); }
                    break;
                case "throne":
                    foreach (var (x, z) in new[] { (-10, -10), (10, -10), (10, 10), (-10, 10) }) Pillar(x, z);
                    break;
            }

            Piece(at, "kit_demon_fire", -17.5, -3);
            Piece(at, "kit_demon_fire", -17.5, 3);
            Piece(at, "kit_demon_fire", 17.5, -3.5);
            Piece(at, "kit_demon_fire", 17.5, 3.5);

            foreach (var x in new[] { -12, -4, 4, 12 })
            {
                Piece(at, "kit_banner", x, -19.8, 90, 2.4);
                Piece(at, "kit_banner", x, 19.8, 90, 2.4);
            }

            var clearOf = new List<(double X, double Z, double R)> { (-15, 0, 5), (15, 0, 5), (-15, 6, 3), (-15, -6, 3), (14, 14, 5), (-17, 14, 3) };
            clearOf.AddRange(keep);

            void Scatter(string[] ids, int count)
            {
                for (var k = 0; k < count; k++)
                {
                    var kit = ids[(int)Math.Floor(rand.Next() * ids.Length)];
                    var edge = rand.Next() < 0.5;
                    var x = edge ? (rand.Next() < 0.5 ? -17 : 17) + ((rand.Next() - 0.5) * 2) : (rand.Next() - 0.5) * 34;
                    var z = edge ? (rand.Next() - 0.5) * 34 : (rand.Next() < 0.5 ? -17 : 17) + ((rand.Next() - 0.5) * 2);

                    if (clearOf.Any(c => Math.Sqrt(((x - c.X) * (x - c.X)) + ((z - c.Z) * (z - c.Z))) < c.R)) continue;

                    Piece(at, kit, x, z, rand.Next() * 360);
                }
            }

            switch (dressing)
            {
                case "crypt":
                    Scatter(["kit_hb_coffin", "kit_bones", "kit_bones", "kit_hb_candles", "kit_hb_marker"], 16);
                    break;
                case "hall":
                    Scatter(["kit_camp_chains", "kit_camp_cage", "kit_bones", "kit_hb_candles", "kit_camp_chains"], 14);
                    break;
                case "arena":
                    foreach (var (x, z) in new[] { (-13, -13), (13, -13), (13, 13), (-13, 13) }) Piece(at, "kit_demon_fire", x, z);
                    Scatter(["kit_bones", "kit_bones", "kit_hb_candles"], 10);
                    break;
                case "throne":
                    for (var k = 0; k < 10; k++)
                    {
                        var a = (k / 10.0 * Math.PI * 2) + 0.3;
                        Piece(at, "kit_demon_fire", Math.Cos(a) * 13.5, Math.Sin(a) * 13.5);
                    }

                    Piece(at, "kit_hb_arch", 0, -18.6, 0);
                    Piece(at, "kit_kn_obelisk", -4, -17.5);
                    Piece(at, "kit_kn_obelisk", 4, -17.5);
                    Piece(at, "kit_kn_altar", 0, -16);
                    Piece(at, "kit_camp_chains", -2, -12, 30);
                    Piece(at, "kit_camp_chains", 3, -13, 120);
                    blocks.Add($"[node name=\"ThroneLight\" type=\"OmniLight3D\" parent=\"{at}\"]\ntransform = {At(0, 0, 6, 0)}\nlight_color = Color(1, 0.3, 0.2, 1)\nlight_energy = 2.5\nomni_range = 22.0");
                    Scatter(["kit_bones", "kit_bones", "kit_camp_chains"], 10);
                    break;
            }

            if (shrine.Length > 0)
            {
                blocks.Add($"[node name=\"Shrine\" type=\"Area3D\" parent=\"{at}\"]\ntransform = {At(0, -15, 0.1, 6)}\nscript = ExtResource(\"4_shrine\")\nShrineId = \"{shrine}\"\n\n" +
                    $"[node name=\"VisualRoot\" type=\"Node3D\" parent=\"{at}/Shrine\"]\ntransform = {At(0, 0, 0.85, 0)}\nscript = ExtResource(\"6_visualroot\")\nVisualId = \"mesh_placeholder_shrine\"");
            }

            if (bench)
            {
                blocks.Add($"[node name=\"Bench\" type=\"Area3D\" parent=\"{at}\"]\ntransform = {At(0, -15, 0.1, -6)}\nscript = ExtResource(\"12_bench\")\n\n" +
                    $"[node name=\"Piece\" type=\"StaticBody3D\" parent=\"{at}/Bench\"]\ntransform = {At(0, 0, Half("kit_camp_anvil"), 0)}\nscript = ExtResource(\"3_kit\")\nPieceId = \"kit_camp_anvil\"");
            }

            if (refuge)
            {
                Comment("The refuge (FR-7.18): a walled corner nothing walks into, somewhere to drink and go back in —\nand, optional, a demon stone for a player who wants more than the task.");
                Piece(at, "kit_wall_low_4", 14, -12);
                Piece(at, "kit_wall_low_4", 12, -14, -90);
                Piece(at, "kit_demon_fire", 16.5, -16.5);
                blocks.Add($"[node name=\"Shard\" type=\"StaticBody3D\" parent=\"{at}\"]\ntransform = {At(0, 14, 0.1, 14)}\nscript = ExtResource(\"5_shard\")\nShardId = \"shd_demon_stone\"\n\n" +
                    $"[node name=\"VisualRoot\" type=\"Node3D\" parent=\"{at}/Shard\"]\ntransform = {At(0, 0, 2.15, 0)}\nscript = ExtResource(\"6_visualroot\")\nVisualId = \"mesh_shard_demon\"");
            }
        }

        Comment("-- Session, player and the way back out -----------------------------------------\nThe border stands on floor one beside the gate shrine: you leave the way you came in.");
        blocks.Add($"[node name=\"Player\" parent=\".\" instance=ExtResource(\"13_player\")]\ntransform = {At(0, -15, 0.1, 0)}\n\n" +
            "[node name=\"Session\" parent=\".\" instance=ExtResource(\"14_session\")]\n\n" +
            "[node name=\"Enemies\" type=\"Node3D\" parent=\".\"]\n\n" +
            $"[node name=\"GateToBrokenGate\" type=\"Area3D\" parent=\".\"]\ntransform = {At(0, -17, 0, 14)}\nscript = ExtResource(\"7_gate\")\nToZone = \"zone_broken_gate\"\nAlwaysSigned = true\n\n" +
            $"[node name=\"ArrivalFromBrokenGate\" type=\"Node3D\" parent=\".\"]\ntransform = {At(0, -15, 0.1, 0)}\nscript = ExtResource(\"8_arrival\")\nFromZone = \"zone_broken_gate\"");

        return string.Join("\n\n", blocks) + "\n";
    }
}
