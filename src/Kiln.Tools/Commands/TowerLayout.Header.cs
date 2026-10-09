namespace Kiln.Tools.Commands;

public static partial class TowerLayout
{
    // Everything above the floors: the resources, the flagstone shader, the light and the tower's root.
    private const string Header = """
[gd_scene load_steps=16 format=3]

; zone_catacombs — the Demon Tower (WLD-07; rebuilt 2026-09-23), band 19–23. The zone keeps its
; id so saves and the last quest keep working; everything the player sees calls it the tower.
;
; Nine floors, one footprint: a forty-metre hall at each level, stacked twenty-four metres apart,
; only the occupied one awake — and since 2026-10-09 each hall its own shape: pillared vestibule,
; basin, gallery, aisles, circle, choir, zigzag, cloister, throne. Red and dark-purple flagstones,
; dungeon walls with a tower at each corner, braziers burning red into violet. Zombies hold the
; lower floors and demons the upper; the dressing follows — coffins and bones below, chains and
; cages above, and the top floor is the Demon Lord's throne room in a ring of fire.
;
; What lives here is only what data cannot hold: where the player lands, where the stair is,
; where a target may stand and where a wave walks in. What each floor asks is in
; game/data/zones/act1.json. Written by `kiln scenes` from src/Kiln.Tools/Commands/TowerLayout.cs:
; change the layout there, never here.

[ext_resource type="Script" path="res://scripts/World/ZoneRoot.cs" id="1_zoneroot"]
[ext_resource type="Script" path="res://scripts/World/NavBaker.cs" id="2_navbaker"]
[ext_resource type="Script" path="res://scripts/World/KitPiece.cs" id="3_kit"]
[ext_resource type="Script" path="res://scripts/World/ShrineNode.cs" id="4_shrine"]
[ext_resource type="Script" path="res://scripts/World/ShardNode.cs" id="5_shard"]
[ext_resource type="Script" path="res://scripts/Visual/VisualRoot.cs" id="6_visualroot"]
[ext_resource type="Script" path="res://scripts/World/ZoneGate.cs" id="7_gate"]
[ext_resource type="Script" path="res://scripts/World/ZoneArrival.cs" id="8_arrival"]
[ext_resource type="Script" path="res://scripts/World/TowerNode.cs" id="9_tower"]
[ext_resource type="Script" path="res://scripts/World/TowerFloorNode.cs" id="10_floor"]
[ext_resource type="Script" path="res://scripts/World/FloorStair.cs" id="11_stair"]
[ext_resource type="Script" path="res://scripts/World/BenchNode.cs" id="12_bench"]
[ext_resource type="PackedScene" path="res://scenes/player.tscn" id="13_player"]
[ext_resource type="PackedScene" path="res://scenes/session.tscn" id="14_session"]

[sub_resource type="NavigationMesh" id="NavigationMesh_tower"]
vertices = PackedVector3Array()
cell_size = 0.15
cell_height = 0.15
agent_height = 1.8
agent_radius = 0.45
agent_max_climb = 0.3
agent_max_slope = 45.0
geometry_parsed_geometry_type = 1
geometry_collision_mask = 1
geometry_source_geometry_mode = 1
geometry_source_group_name = "navsource"

[sub_resource type="PlaneMesh" id="PlaneMesh_slab"]
size = Vector2(40, 40)

; Flagstones, two metres, rows offset: dark red and dark purple at random, grout between.
[sub_resource type="Shader" id="Shader_flags"]
code = "shader_type spatial;

uniform vec3 red : source_color = vec3(0.34, 0.07, 0.06);
uniform vec3 purple : source_color = vec3(0.17, 0.07, 0.22);
uniform vec3 grout : source_color = vec3(0.04, 0.02, 0.04);

varying vec3 wp;

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

void vertex() {
	wp = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	vec2 p = wp.xz / 2.0;
	float row = floor(p.y);
	p.x += mod(row, 2.0) * 0.5;
	vec2 cell = floor(p);
	vec2 f = fract(p);
	float h = hash(cell);
	vec3 stone = mix(red, purple, step(0.45, hash(cell + 7.3)));
	stone *= 0.7 + 0.55 * h;
	stone *= 0.88 + 0.24 * hash(floor(wp.xz * 5.0));
	float edge = min(min(f.x, 1.0 - f.x), min(f.y, 1.0 - f.y));
	float g = smoothstep(0.015, 0.045, edge);
	ALBEDO = mix(grout, stone, g);
	ROUGHNESS = 0.8 - 0.3 * h;
}"

[sub_resource type="ShaderMaterial" id="Material_slab"]
shader = SubResource("Shader_flags")

[sub_resource type="BoxShape3D" id="BoxShape3D_slab"]
size = Vector3(40, 1, 40)

[sub_resource type="Environment" id="Environment_tower"]
background_mode = 1
background_color = Color(0.04, 0.02, 0.04, 1)
ambient_light_source = 2
ambient_light_color = Color(0.42, 0.26, 0.36, 1)
ambient_light_energy = 0.45
fog_enabled = true
fog_light_color = Color(0.2, 0.06, 0.1, 1)
fog_density = 0.012

[node name="Catacombs" type="Node3D"]
script = ExtResource("1_zoneroot")
ZoneId = "zone_catacombs"

[node name="WorldEnvironment" type="WorldEnvironment" parent="."]
environment = SubResource("Environment_tower")

[node name="DirectionalLight3D" type="DirectionalLight3D" parent="."]
transform = Transform3D(0.8660254, -0.25, 0.4330127, 0, 0.8660254, 0.5, -0.5, -0.4330127, 0.75, 0, 14, 0)
light_color = Color(1, 0.7, 0.65, 1)
light_energy = 0.35
shadow_enabled = true

[node name="NavigationRegion3D" type="NavigationRegion3D" parent="."]
navigation_mesh = SubResource("NavigationMesh_tower")
script = ExtResource("2_navbaker")

[node name="Tower" type="Node3D" parent="NavigationRegion3D"]
script = ExtResource("9_tower")
""";
}
