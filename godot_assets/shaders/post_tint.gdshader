// The post stack's cheap path (see PostFx), for a grade with contrast and
// saturation at 1: the tint and the vignette multiply what is under them
// through the blend, so the screen is not copied to be read back. The
// result matches post.gdshader with those values.
shader_type canvas_item;
render_mode unshaded, blend_mul;

uniform vec4 tint : source_color = vec4(1.0);
uniform float vignette : hint_range(0.0, 1.0) = 0.22;
uniform float vignette_start : hint_range(0.0, 1.5) = 0.6;

void fragment() {
	vec2 d = (SCREEN_UV - 0.5) * vec2(1.0, 0.8);
	float edge = smoothstep(vignette_start, 1.25, length(d) * 2.0);
	COLOR = vec4(tint.rgb * (1.0 - vignette * edge), 1.0);
}
