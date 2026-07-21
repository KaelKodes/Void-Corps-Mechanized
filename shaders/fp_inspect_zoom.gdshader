shader_type canvas_item;

// Full-screen inspect overlay: edge blur + soft vignette while Alt-zoom is active.
uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_linear_mipmap;
uniform float strength : hint_range(0.0, 1.0) = 0.0;

void fragment() {
	vec2 uv = SCREEN_UV;
	vec2 center = vec2(0.5);
	vec2 from_center = uv - center;
	float dist = length(from_center);
	// Keep the center sharp; soft blur only toward the frame.
	float edge = smoothstep(0.22, 0.82, dist) * strength;

	vec3 color = textureLod(screen_texture, uv, 0.0).rgb;
	if (edge > 0.002) {
		vec3 acc = color;
		float weight = 1.0;
		vec2 dir = length(from_center) > 0.0001
			? normalize(from_center)
			: vec2(0.0);
		for (int i = 1; i <= 7; i++) {
			float t = float(i) / 7.0;
			float sample_w = 1.0 - t * 0.6;
			vec2 offset = dir * edge * 0.055 * t;
			acc += textureLod(screen_texture, uv - offset, edge * 3.0).rgb * sample_w;
			weight += sample_w;
		}
		color = acc / weight;
		color *= 1.0 - edge * 0.4;
	}

	COLOR = vec4(color, 1.0);
}
