namespace Kiln.Tools.Commands;

/// <summary>
/// Act 2's three maps (A2-01, 2026-10-02), as layouts <see cref="ScenesCommand"/> writes into scenes.
/// </summary>
/// <remarks>
/// A map is 180 m square, its corners 74 m out. Each is entered at one corner, by a camp, and crossed by a
/// road to the far one; the creatures' camps lie either side of the road, the boss in a ring of its own off
/// the far end of it, behind a guard.
/// </remarks>
public static class Act2Layouts
{
    public static IEnumerable<Layout> All() => [Stonewood(), DrownedMire(), Cinderlands()];

    /// <summary>A handful of pieces thrown round a point, by a seed — a camp's clutter.</summary>
    private static Layout Strew(this Layout map, int seed, double x, double z, double reach, params string[] kits)
    {
        var rng = new Random(seed);

        foreach (var kit in kits)
        {
            var bearing = rng.NextDouble() * Math.PI * 2;
            var far = reach * (0.45 + (rng.NextDouble() * 0.55));

            map.Put(kit, Math.Round(x + (Math.Cos(bearing) * far), 1), Math.Round(z + (Math.Sin(bearing) * far), 1), rng.Next(0, 12) * 30);
        }

        return map;
    }

    // ------------------------------------------------------------------ the Stonewood

    private static Layout Stonewood()
    {
        var map = new Layout
        {
            File = "stonewood",
            Root = "Stonewood",
            ZoneId = "zone_stonewood",
            About = "zone_stonewood — the forest turned to stone (act 2, map one; A2-01, 2026-10-02), band 27–31.\n\n"
                + "Past the Broken Gate the wood stands as it stood the day it stopped: trunks, leaves, deer and wolves,\n"
                + "all grey stone, and crystal growing out of the ground between them. A road crosses it from the\n"
                + "south-east corner to the north-west. Slate wolves hunt by the way in, shard spiders nest in the east,\n"
                + "adders lie in the still grove; the golems keep a ring of crystal in the middle, the lichen caps a\n"
                + "glade that is still green in the west, and the geodes a field of their own in the north. The Stone\n"
                + "Stag holds a ring of crags in the south-west, one way in, behind its guard.",
            Ground = "#5c6658",
            Sky = "#8fa6b6",
            Ambient = "#8a989c",
            Fog = "#9aacb6",
            Sun = "#f2f4ee",
            SunEnergy = 1.1,
            AmbientEnergy = 0.55,
            FogDensity = 0.0035,
        };

        map.Road("kit_path_4", 61, (74, 62), (56, 48), (30, 30), (6, 6), (-20, -12), (-44, -40), (-66, -62), (-74, -62));

        map.Part("Outpost", "The outpost at the way in: the smith's anvil and his fire")
            .Put("kit_camp_anvil", 59.6, 66.2, 20).Put("kit_brazier", 60, 68).Put("kit_camp_weapons", 62.5, 68.2, 160).Put("kit_camp_crate", 58.6, 64.4);

        map.Part("Wolves", "The wolves' run by the way in: fallen trunks and the deer they were hunting, stone as they are")
            .Put("kit_st_deer", 26, 61, 40).Put("kit_st_deer", 29, 63, 70).Put("kit_st_stag", 19, 53, 200)
            .Strew(11, 16, 56, 9, "kit_st_log", "kit_st_log", "kit_st_stump", "kit_st_tree_dead", "kit_st_rock", "kit_bones", "kit_bones");

        map.Part("Webs", "The spiders' hollow in the east: webs strung between stone trees, eggs, crystal")
            .Put("kit_web", 50, 4, 20).Put("kit_web", 55, 10, 110).Put("kit_web", 46, 12, 70).Put("kit_web", 53, 15, 160)
            .Put("kit_spider_eggs", 52, 8).Put("kit_spider_eggs", 48, 5).Put("kit_spider_eggs", 56, 13)
            .Strew(13, 51, 9, 10, "kit_st_tree_dead", "kit_st_tree_dead", "kit_st_tree_dead", "kit_st_crystal", "kit_st_crystal", "kit_st_rock");

        map.Part("Grove", "The still grove: a herd caught as it ran, and the adders that lie among it")
            .Put("kit_st_deer", 8, 38, 300).Put("kit_st_deer", 12, 40, 320).Put("kit_st_deer", 14, 35, 280).Put("kit_st_stag", 6, 33, 310)
            .Put("kit_st_wolf", 18, 42, 300).Put("kit_st_wolf", 20, 38, 290)
            .Strew(17, 11, 37, 10, "kit_st_tree", "kit_st_tree", "kit_st_pine", "kit_st_fern", "kit_st_fern", "kit_st_bush");

        map.Part("Circle", "The golems' ring in the middle: twelve crystals standing, an altar among them")
            .Ring("kit_st_crystal", -4, -28, 9, 12, 20, 70)
            .Put("kit_kn_altar", -4, -28).Put("kit_pillar", -9, -23).Put("kit_pillar", 1, -33).Put("kit_ruin_rubble", -8, -32).Put("kit_ruin_rubble", 2, -24);

        map.Part("Glade", "The glade in the west that never turned: green, wet, and the lichen caps' own")
            .Put("kit_nat_tree", -44, 6).Put("kit_nat_tree", -36, 15).Put("kit_nat_tree", -46, 16).Put("kit_nat_tree", -35, 4)
            .Strew(19, -40, 10, 10, "kit_nat_fern", "kit_nat_fern", "kit_nat_fern", "kit_nat_bush", "kit_nat_bush", "kit_nat_flowers", "kit_nat_flowers", "kit_nat_grass", "kit_nat_grass", "kit_nat_rock");

        map.Part("Geodes", "The geode field in the north: split stones, violet inside")
            .Strew(23, -14, -56, 11, "kit_st_geode", "kit_st_geode", "kit_st_geode", "kit_st_geode", "kit_st_geode", "kit_st_geode", "kit_st_geode", "kit_st_crag", "kit_st_rock", "kit_st_rock", "kit_st_crystal");

        map.Part("Lair", "The Stone Stag's ring: crags all round, open to the north-east, crystal at its heart")
            .Ring("kit_st_crag", -56, 50, 15, 13, 290, 340)
            .Put("kit_st_crystal", -62, 55).Put("kit_st_crystal", -60, 58, 40).Put("kit_st_crystal", -64, 51, 80)
            .Put("kit_st_wolf", -50, 47, 120).Put("kit_st_wolf", -53, 56, 60).Put("kit_bones", -57, 46).Put("kit_bones", -52, 52)
            .Clear(-44, 36, 9);

        map.Scatter("StoneTrees", 170, 8, 78, 6, 61, "kit_st_pine", "kit_st_tree_dead", "kit_st_pine", "kit_st_tree_dead", "kit_st_pine", "kit_st_tree")
            .Scatter("Rocks", 46, 8, 78, 9, 62, "kit_st_rock", "kit_st_rock", "kit_st_crag")
            .Scatter("Undergrowth", 130, 6, 76, 3, 63, "kit_st_fern", "kit_st_bush", "kit_st_stump", "kit_st_log", "kit_st_fern")
            .Scatter("Crystals", 26, 12, 74, 11, 64, "kit_st_crystal")
            .Scatter("Moss", 120, 6, 76, 4, 65, "kit_nat_grass", "kit_nat_grass", "kit_nat_fern", "kit_nat_bush")
            .Scatter("Flowers", 30, 10, 74, 8, 66, "kit_nat_flowers");

        map.Camp("saf_stonewood_camp", "shr_stonewood_camp", 66, 62)
            .Npc("SmithMarek", "npc_marek", 61, 66)
            .Npc("MerchantSella", "npc_sella", 71.5, 65)
            .ShrineAt("ShrineGrove", "shr_stonewood_grove", 2, 12)
            .ShrineAt("ShrinePass", "shr_stonewood_pass", -60, -52)
            .Spawn("WolfRun", "spf_stonewood_wolves", 16, 56)
            .Spawn("SpiderHollow", "spf_stonewood_webs", 51, 9)
            .Spawn("StillGrove", "spf_stonewood_grove", 11, 37)
            .Spawn("GolemRing", "spf_stonewood_circle", -4, -28)
            .Spawn("GreenGlade", "spf_stonewood_glade", -40, 10)
            .Spawn("GeodeField", "spf_stonewood_geodes", -14, -56)
            .Spawn("StagGuard", "spf_stonewood_guard", -44, 36, 8)
            .Spawn("Stag", "spf_stonewood_stag", -56, 50, 6)
            .Way("Gate", "zone_broken_gate", 74, 74)
            .Way("Mire", "zone_drowned_mire", -74, -74);

        return map;
    }

    // ------------------------------------------------------------------ the Drowned Mire

    private static Layout DrownedMire()
    {
        var map = new Layout
        {
            File = "drowned_mire",
            Root = "DrownedMire",
            ZoneId = "zone_drowned_mire",
            About = "zone_drowned_mire — the drowned village (act 2, map two; A2-01, 2026-10-02), band 31–35.\n\n"
                + "The stone gives way to black water and reeds. A causeway crosses the marsh from the south-west\n"
                + "corner to the north-east. Crawlers come out of the shallows by the way in, the drowned still walk\n"
                + "the streets of their village, serpents keep the reed beds, spitters the graveyard, the witches and\n"
                + "the brutes the sunken chapel. The Drowned King sits in what is left of his hall in the south-east,\n"
                + "walls on three sides, behind his guard.",
            Ground = "#3a4630",
            Sky = "#56645c",
            Ambient = "#7c8c80",
            Fog = "#62746a",
            Sun = "#cfe0d0",
            SunEnergy = 0.85,
            AmbientEnergy = 0.7,
            FogDensity = 0.008,
        };

        map.Road("kit_path_4", 67, (-74, 62), (-52, 50), (-28, 34), (-4, 10), (20, -10), (44, -38), (66, -62), (74, -62));

        map.Part("Shallows", "The shallows by the way in: black pools, reeds, what the water gave back")
            .Pool("kit_sw_pool", -22, 64, 5).Pool("kit_sw_pool", -14, 56, 5, 40)
            .Strew(31, -19, 60, 10, "kit_sw_reeds", "kit_sw_reeds", "kit_sw_reeds", "kit_sw_reeds", "kit_sw_reeds", "kit_bones", "kit_bones", "kit_un_log", "kit_hb_coffin");

        map.Part("Village", "The village: houses fallen in, fences, a cart nobody came back for")
            .Put("kit_ruin_house_small", -36, 4, 20).Put("kit_ruin_house_long", -24, 2, 95).Put("kit_ruin_house_cottage", -30, 16, 200)
            .Put("kit_ruin_house_small", -40, 14, 250).Put("kit_vil_wagon", -29, 9, 30).Put("kit_vil_fence_4", -22, 12, 10).Put("kit_vil_fence_4", -34, -4, 100)
            .Put("kit_ruin_pile", -27, 6).Put("kit_ruin_pile", -37, 10).Put("kit_ruin_beam", -32, 3, 60);

        map.Part("Reeds", "The reed beds: water to the knee, and the serpents in it")
            .Pool("kit_sw_pool", 6, 38, 5).Pool("kit_sw_pool", 14, 32, 5, 60).Pool("kit_sw_pool", 2, 30, 5, 20)
            .Strew(37, 8, 35, 11, "kit_sw_reeds", "kit_sw_reeds", "kit_sw_reeds", "kit_sw_reeds", "kit_sw_reeds", "kit_sw_reeds", "kit_sw_reeds", "kit_sw_reeds", "kit_un_log", "kit_sw_rock");

        map.Part("Graves", "The graveyard: stones leaning in the mud, coffins come up, a lamp still lit")
            .Put("kit_kn_gravestone", 0, -26).Put("kit_kn_gravestone", 3, -30).Put("kit_kn_gravestone", 7, -27).Put("kit_kn_gravestone", -2, -32)
            .Put("kit_kn_gravestone", 9, -32).Put("kit_kn_gravestone", 5, -23).Put("kit_hb_grave", 2, -35).Put("kit_hb_grave", 8, -36)
            .Put("kit_hb_coffin", -1, -22).Put("kit_hb_coffin", 10, -25).Put("kit_kn_crypt", -6, -30, 90).Put("kit_kn_lamp", 4, -27)
            .Put("kit_kn_fence_broken", -3, -18, 0).Put("kit_kn_fence_broken", 12, -20, 60).Put("kit_ghost_fire", 6, -31);

        map.Part("Chapel", "The sunken chapel: an arch, an altar, candles the witches keep")
            .Put("kit_kn_crypt_large", 44, -4, 90).Put("kit_hb_arch", 36, -6, 90).Put("kit_kn_altar", 40, -6, 90)
            .Put("kit_hb_candles", 38, -2).Put("kit_hb_candles", 38, -10).Put("kit_ghost_fire", 34, 0).Put("kit_ghost_fire", 34, -12)
            .Put("kit_kn_obelisk", 42, 4).Put("kit_kn_obelisk", 42, -14).Pool("kit_sw_pool", 32, -6, 5);

        map.Part("Causeway", "The causeway's far end: the road on stilts of stone, the flies thick over it")
            .Put("kit_kn_stone_wall_broken", 26, -52, 30).Put("kit_kn_stone_wall_broken", 34, -58, 30).Pool("kit_sw_pool", 24, -58, 5).Pool("kit_sw_pool", 36, -50, 5, 50)
            .Strew(41, 30, -54, 10, "kit_sw_reeds", "kit_sw_reeds", "kit_sw_reeds", "kit_sw_reeds", "kit_bones", "kit_hb_coffin", "kit_ghost_fire");

        map.Part("Hall", "The Drowned King's hall: three walls standing, open to the west, his throne of stone")
            .Ring("kit_kn_stone_wall_broken", 56, 46, 14, 20, 150, 210)
            .Put("kit_kn_crypt", 63, 46, 90).Put("kit_pillar", 52, 40).Put("kit_pillar", 52, 52).Put("kit_pillar", 60, 38).Put("kit_pillar", 60, 54)
            .Put("kit_ghost_fire", 58, 42).Put("kit_ghost_fire", 58, 50).Pool("kit_sw_pool", 54, 46, 5)
            .Clear(38, 44, 9);

        map.Scatter("MarshTrees", 190, 8, 78, 6, 67, "kit_sw_tree", "kit_hb_dead_tree", "kit_un_dead_tree", "kit_hb_dead_tree", "kit_sw_tree", "kit_un_dead_tree")
            .Scatter("ReedBeds", 170, 6, 76, 3, 69, "kit_sw_reeds", "kit_sw_reeds", "kit_un_grass")
            .Scatter("Wrack", 60, 8, 76, 7, 70, "kit_sw_rock", "kit_un_log", "kit_un_stump", "kit_bones");

        map.Camp("saf_mire_camp", "shr_mire_camp", -66, 62)
            .Npc("PedlarOswin", "npc_oswin", -60.5, 65)
            .ShrineAt("ShrineChapel", "shr_mire_chapel", 26, -2)
            .ShrineAt("ShrineCauseway", "shr_mire_causeway", 56, -56)
            .Spawn("Shallows", "spf_mire_shallows", -15, 60)
            .Spawn("VillageStreets", "spf_mire_village", -31, 9, 14)
            .Spawn("ReedBed", "spf_mire_reeds", 8, 35)
            .Spawn("Graveyard", "spf_mire_graves", 4, -28)
            .Spawn("SunkenChapel", "spf_mire_chapel", 40, -6)
            .Spawn("CausewayEnd", "spf_mire_causeway", 30, -54)
            .Spawn("KingsGuard", "spf_mire_guard", 38, 44, 8)
            .Spawn("King", "spf_mire_king", 56, 46, 6)
            .Way("Stonewood", "zone_stonewood", -74, 74)
            .Way("Cinderlands", "zone_cinderlands", 74, -74);

        map.Pools("Water", "kit_sw_pool", 14, 14, 72, 5, 68);

        return map;
    }

    // ------------------------------------------------------------------ the Cinderlands

    private static Layout Cinderlands()
    {
        var map = new Layout
        {
            File = "cinderlands",
            Root = "Cinderlands",
            ZoneId = "zone_cinderlands",
            About = "zone_cinderlands — the land of fire (act 2, map three; A2-01, 2026-10-02), band 35–40.\n\n"
                + "Past the marsh the ground is black glass and the air is ash. A road crosses it from the south-east\n"
                + "corner toward the north-west and stops at the forge. Ember hounds run by the way in, the orc clans\n"
                + "hold two camps east of the road, salamanders and wasps the rift of open fire, the shamans a ring of\n"
                + "burning stones, and the flats west of the road belong to whatever is fastest. The Burning Warlord\n"
                + "holds the forge camp in the north-west, spikes all round, behind his guard.",
            Ground = "#34292a",
            Sky = "#5a3428",
            Ambient = "#9a7262",
            Fog = "#6a3a28",
            Sun = "#ffd2a8",
            SunEnergy = 1.1,
            AmbientEnergy = 0.65,
            FogDensity = 0.0055,
        };

        map.Road("kit_path_4", 71, (74, 62), (52, 50), (26, 36), (0, 12), (-24, -10), (-42, -32), (-50, -44));

        map.Part("Hounds", "The hounds' ground by the way in: bones, burnt trunks")
            .Strew(51, 16, 58, 9, "kit_bones", "kit_bones", "kit_bones", "kit_em_tree", "kit_em_tree", "kit_em_rock", "kit_un_log");

        map.Part("SouthCamp", "The first clan's camp: huts, a totem, spikes toward the road")
            .Put("kit_orc_hut", 62, 9, 200).Put("kit_orc_hut", 56, 1, 250).Put("kit_orc_totem", 58, 9).Put("kit_orc_spikes", 52, 11, 40)
            .Put("kit_orc_spikes", 64, 15, 0).Put("kit_camp_fire", 58, 6).Put("kit_skull_post", 54, 5).Put("kit_camp_weapons", 61, 4, 30).Put("kit_brazier", 63, 2);

        map.Part("Rift", "The rift: open fire in the ground, and what was born in it")
            .Pool("kit_em_lava", -8, 44, 3.5).Pool("kit_em_lava", -16, 50, 3.5, 30).Pool("kit_em_lava", -2, 52, 3.5, 60)
            .Put("kit_em_shard", -10, 38).Put("kit_em_shard", -3, 46, 40).Put("kit_em_shard", -14, 44, 80).Put("kit_em_crag", -20, 42, 20)
            .Strew(53, -8, 46, 11, "kit_em_rock", "kit_em_rock", "kit_em_shard", "kit_em_tree");

        map.Part("EastCamp", "The second clan's camp: throwers on the rocks above the road")
            .Put("kit_orc_hut", 31, -25, 120).Put("kit_orc_hut", 25, -19, 60).Put("kit_orc_totem", 27, -25).Put("kit_orc_spikes", 21, -27, 110)
            .Put("kit_orc_spikes", 33, -17, 20).Put("kit_camp_fire", 28, -22).Put("kit_skull_post", 23, -23).Put("kit_camp_cage", 32, -21).Put("kit_em_crag", 35, -29, 50);

        map.Part("BurningStones", "The shamans' ring: nine stones that burn, a fire in the middle")
            .Ring("kit_em_shard", -10, -40, 8, 9, 60, 110)
            .Put("kit_camp_fire", -10, -40).Put("kit_orc_totem", -14, -44).Put("kit_orc_totem", -6, -36).Put("kit_skull_post", -16, -38).Put("kit_skull_post", -4, -44);

        map.Part("Flats", "The flats west of the road: ash, glass, nothing standing")
            .Pool("kit_em_lava", -46, 10, 3.5, 20)
            .Strew(57, -44, 6, 11, "kit_em_rock", "kit_em_rock", "kit_em_tree", "kit_bones", "kit_bones", "kit_em_shard");

        map.Part("Forge", "The forge camp: spikes all round, open to the south-east, the anvils and the fire")
            .Ring("kit_orc_spikes", -58, -56, 15, 20, 20, 70)
            .Put("kit_orc_hut", -64, -62, 45).Put("kit_orc_hut", -66, -52, 100).Put("kit_orc_totem", -60, -64).Put("kit_orc_totem", -52, -62)
            .Put("kit_camp_anvil", -60, -58, 30).Put("kit_camp_anvil", -57, -60, 100).Put("kit_brazier", -62, -55).Put("kit_brazier", -55, -52)
            .Pool("kit_em_lava", -51, -62, 3.5, 10).Put("kit_camp_weapons", -63, -57, 60)
            .Clear(-46, -42, 9);

        map.Scatter("BlackRock", 70, 8, 78, 8, 71, "kit_em_rock", "kit_em_rock", "kit_em_rock", "kit_em_crag")
            .Scatter("BurntTrees", 130, 8, 78, 6, 72, "kit_em_tree")
            .Scatter("Embers", 26, 10, 74, 11, 74, "kit_em_shard")
            .Scatter("Ash", 180, 6, 76, 3, 75, "kit_em_grass", "kit_em_grass", "kit_em_grass", "kit_bones");

        map.Camp("saf_cinder_camp", "shr_cinder_camp", 66, 62)
            .Npc("PedlarKetta", "npc_ketta", 71.5, 65)
            .ShrineAt("ShrineRift", "shr_cinder_rift", 6, 22)
            .ShrineAt("ShrineForge", "shr_cinder_forge", -34, -24)
            .Spawn("HoundGround", "spf_cinder_hounds", 16, 58)
            .Spawn("SouthClan", "spf_cinder_camp_south", 58, 6)
            .Spawn("FireRift", "spf_cinder_rift", -8, 46)
            .Spawn("EastClan", "spf_cinder_camp_east", 28, -22)
            .Spawn("ShamanRing", "spf_cinder_shrine", -10, -40)
            .Spawn("AshFlats", "spf_cinder_flats", -44, 6)
            .Spawn("WarlordGuard", "spf_cinder_guard", -46, -42, 8)
            .Spawn("Warlord", "spf_cinder_warlord", -58, -56, 6)
            .Way("Mire", "zone_drowned_mire", 74, 74);

        map.Pools("Fire", "kit_em_lava", 12, 16, 72, 3.5, 73);

        return map;
    }
}
