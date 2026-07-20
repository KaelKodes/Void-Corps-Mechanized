shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, depth_test_disabled, cull_disabled, shadows_disabled;

uniform vec4 silhouette_color : source_color = vec4(0.3, 0.85, 1.0, 0.9);
uniform sampler2D depth_texture : hint_depth_texture, repeat_disable, filter_nearest;
// Linear-depth tolerance in view-space meters.
uniform float depth_bias = 0.08;

void fragment() {
	// Built-ins are only valid inside fragment(); pass them into locals for the depth reconstruct.
	mat4 inv_proj = INV_PROJECTION_MATRIX;
	vec2 screen_uv = SCREEN_UV;

	vec4 scene_view = inv_proj * vec4(screen_uv * 2.0 - 1.0, texture(depth_texture, screen_uv).r, 1.0);
	float scene_z = abs(scene_view.z / scene_view.w);

	vec4 my_view = inv_proj * vec4(screen_uv * 2.0 - 1.0, FRAGCOORD.z, 1.0);
	float my_z = abs(my_view.z / my_view.w);

	// Only draw where closer geometry already won the depth buffer.
	if (scene_z + depth_bias >= my_z) {
		discard;
	}

	ALBEDO = silhouette_color.rgb;
	ALPHA = silhouette_color.a;
}
