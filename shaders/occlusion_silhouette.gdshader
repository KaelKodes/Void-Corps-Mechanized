shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, depth_test_disabled, cull_disabled, shadows_disabled;

uniform vec4 silhouette_color : source_color = vec4(0.3, 0.85, 1.0, 0.9);
uniform sampler2D depth_texture : hint_depth_texture, repeat_disable, filter_nearest;
// Linear-depth tolerance in view-space meters.
uniform float depth_bias = 0.08;

float linear_depth(float raw_depth) {
	vec4 view = INV_PROJECTION_MATRIX * vec4(SCREEN_UV * 2.0 - 1.0, raw_depth, 1.0);
	return abs(view.z / view.w);
}

void fragment() {
	float scene_z = linear_depth(texture(depth_texture, SCREEN_UV).r);
	float my_z = linear_depth(FRAGCOORD.z);

	// Only draw where closer geometry already won the depth buffer.
	if (scene_z + depth_bias >= my_z) {
		discard;
	}

	ALBEDO = silhouette_color.rgb;
	ALPHA = silhouette_color.a;
}
