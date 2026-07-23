shader_type spatial;
// depth_draw_never: walls stay see-through to exterior even when ALPHA≈0.
// depth_draw_opaque was writing Z and occluding everything past the barrier.
render_mode blend_add, depth_draw_never, cull_disabled;

// --- PARAMETERS:
uniform vec4 shield_color : source_color = vec4(0.1, 0.6, 1.0, 1.0);
uniform sampler2D noise_texture;
uniform float wave_height : hint_range(0.0, 2.0) = 0.2;
uniform vec2 wave_speed = vec2(0.1, 0.05);
uniform float fresnel_power : hint_range(0.5, 10.0) = 3.0;
uniform float center_visibility : hint_range(0.0, 1.0) = 0.0;
/// Overall near-wall strength (0 far → 1 close), multiplied by a local blob.
uniform float proximity_visibility : hint_range(0.0, 1.0) = 0.0;
/// World-space center of the idle glow (mech projected onto this face).
uniform vec3 proximity_point_world = vec3(0.0);
uniform float proximity_radius = 7.0;

uniform vec3 hit_position = vec3(0.0, 1.0, 0.0);
uniform float hit_progress : hint_range(0.0, 1.0) = 0.2;
uniform vec4 hit_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform float hit_radius = 3.0;
uniform float hit_ring_width : hint_range(0.0, 1.0, 0.1) = 0.3;
uniform float hit_push_force = 0.5;
/// World hit for planar walls (pad barriers). Zero length → fall back to sphere hit_position.
uniform vec3 hit_point_world = vec3(0.0);
uniform float hit_world_radius = 6.0;

varying float v_noise;
varying float v_hit_intensity;
varying vec3 v_world_pos;

void vertex() {
	v_world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;

	float u = atan(NORMAL.z, NORMAL.x) / TAU + 0.5;
	float v = asin(clamp(NORMAL.y, -1.0, 1.0)) / PI + 0.5;
	vec2 noise_uv = vec2(u, v) + TIME * wave_speed;

	float n = textureLod(noise_texture, noise_uv, 0.0).r;
	v_noise = n;
	float base_displacement = (n - 0.5) * 2.0 * wave_height;

	// Sphere-style hit (mech shields). Pad walls prefer hit_point_world in fragment.
	vec3 hit_dir = length(hit_position) > 0.001 ? normalize(hit_position) : vec3(0.0, 1.0, 0.0);
	float impact_dist = acos(clamp(dot(NORMAL, hit_dir), -1.0, 1.0));
	float current_radius = hit_progress * hit_radius;
	float dist_to_ring = abs(impact_dist - current_radius);
	float hit_intensity = smoothstep(hit_ring_width, 0.0, dist_to_ring);
	hit_intensity *= (1.0 - hit_progress);
	v_hit_intensity = hit_intensity;

	float total_displacement = base_displacement + (hit_intensity * hit_push_force);
	VERTEX += NORMAL * total_displacement;
}

void fragment() {
	float fresnel = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), fresnel_power);
	float visibility_mask = mix(center_visibility, 1.0, fresnel);

	ALBEDO = shield_color.rgb;

	float dynamic_glow = v_noise * 2.0 * visibility_mask;

	// Local blob around the mech's projection on this face.
	float dist_blob = distance(v_world_pos, proximity_point_world);
	float blob = 1.0 - smoothstep(proximity_radius * 0.35, proximity_radius, dist_blob);
	float near = proximity_visibility * blob;

	vec3 base_emission = shield_color.rgb * (fresnel * 2.5 + dynamic_glow) * near;

	// Planar hit ripple for pad walls; sphere varying for mech bubbles.
	float hit_i = v_hit_intensity;
	if (length(hit_point_world) > 0.001) {
		float hd = distance(v_world_pos, hit_point_world);
		float ring_r = hit_progress * hit_world_radius;
		float ring = 1.0 - smoothstep(0.0, hit_ring_width * hit_world_radius + 0.35, abs(hd - ring_r));
		hit_i = ring * (1.0 - hit_progress);
	}

	vec3 hit_emission = hit_color.rgb * hit_i * 5.0;
	EMISSION = base_emission + hit_emission;

	float base_alpha = clamp(fresnel + dynamic_glow, 0.0, 1.0) * shield_color.a * near;
	ALPHA = clamp(base_alpha + hit_i, 0.0, 1.0);
}
