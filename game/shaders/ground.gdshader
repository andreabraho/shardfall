// The ground of a map (2026-09-30): one colour everywhere read as a plastic sheet. This paints
// it from one noise, sampled in world space at a few scales:
//   - broad swathes, sunlit and shaded, a few tens of metres across;
//   - patches of something else — bare earth, ash, gravel, stone — with ragged edges;
//   - a fine grain, so it does not go flat up close;
//   - scattered specks — flowers, pebbles, embers, glints of ice.
// The colours are the map's (zones "ground", ZoneGround.cs); the shapes are the same everywhere.
shader_type spatial;

uniform vec3 base_colour : source_color = vec3(0.31, 0.41, 0.22);
uniform vec3 light_colour : source_color = vec3(0.42, 0.52, 0.25);
uniform vec3 dark_colour : source_color = vec3(0.20, 0.31, 0.16);
uniform vec3 patch_colour : source_color = vec3(0.36, 0.29, 0.19);
uniform float patch_amount : hint_range(0.0, 1.0) = 0.2;
uniform vec3 speck_colour : source_color = vec3(0.85, 0.80, 0.45);
uniform float speck_amount : hint_range(0.0, 0.3) = 0.03;
uniform float grain : hint_range(0.0, 0.5) = 0.16;
uniform float roughness_value : hint_range(0.0, 1.0) = 0.95;
uniform sampler2D noise : repeat_enable, filter_linear_mipmap;

varying vec3 world;

void vertex() {
	world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

// The noise is smooth and sits mostly near the middle; stretched, its highs and lows show.
float stretched(float v, float by) {
	return clamp((v - 0.5) * by + 0.5, 0.0, 1.0);
}

// The value of the noise that only `share` of the ground lies above. The noise falls in a
// bell round 0.49, 0.18 wide (measured, 2026-09-30); this is the bell's tail, approximated.
float above(float share) {
	float t = sqrt(-2.0 * log(clamp(share, 0.0005, 0.5)));
	float z = t - (2.515517 + 0.802853 * t + 0.010328 * t * t) / (1.0 + 1.432788 * t + 0.189269 * t * t + 0.001308 * t * t * t);
	return 0.49 + 0.18 * z;
}

float at(vec2 p, float scale, vec2 shift) {
	return texture(noise, p * scale + shift).r;
}

void fragment() {
	vec2 p = world.xz;

	// Swathes: two octaves, so their edges wander.
	float swathe = at(p, 0.012, vec2(0.0)) * 0.65 + at(p, 0.037, vec2(0.37, 0.71)) * 0.35;
	vec3 colour = mix(dark_colour, light_colour, smoothstep(0.32, 0.68, swathe));
	colour = mix(colour, base_colour, 0.35);

	// Patches, their edges roughened by a finer noise.
	float rough_edge = stretched(at(p, 0.21, vec2(0.52, 0.13)), 2.0) - 0.5;
	float patchy = at(p, 0.028, vec2(0.13, 0.52)) + rough_edge * 0.18;
	float threshold = above(patch_amount);
	float patch_mask = smoothstep(threshold - 0.05, threshold + 0.05, patchy) * step(0.001, patch_amount) * 0.85;
	colour = mix(colour, patch_colour, patch_mask);

	// Grain: a little lighter and darker, finely.
	float fine = stretched(at(p, 0.9, vec2(0.61, 0.29)) * 0.5 + at(p, 3.1, vec2(0.83, 0.47)) * 0.5, 3.0);
	colour *= 1.0 - grain + fine * grain * 2.0;

	// Specks, where a fine noise peaks, and not on the patches.
	float s = at(p, 1.7, vec2(0.21, 0.83));
	float from = above(speck_amount);
	float speck = smoothstep(from, from + 0.03, s) * (1.0 - patch_mask);
	colour = mix(colour, speck_colour, speck);

	ALBEDO = colour;
	ROUGHNESS = roughness_value;
}
