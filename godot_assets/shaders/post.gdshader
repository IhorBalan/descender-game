// The post stack over the level, under the HUD (see PostFx): the region's
// colour grade (a tint the view is multiplied by, contrast around middle
// grey and saturation) and a soft vignette. Values of 1, white and 0
// leave the view as it is.
shader_type canvas_item;
render_mode unshaded;

uniform sampler2D screen : hint_screen_texture, filter_nearest;
uniform vec4 tint : source_color = vec4(1.0);
uniform float contrast : hint_range(0.0, 3.0) = 1.0;
uniform float saturation : hint_range(0.0, 3.0) = 1.0;
uniform float vignette : hint_range(0.0, 1.0) = 0.22;
uniform float vignette_start : hint_range(0.0, 1.5) = 0.6;

void fragment() {
	vec3 c = texture(screen, SCREEN_UV).rgb;
	c = (c - 0.5) * contrast + 0.5;
	float grey = dot(c, vec3(0.299, 0.587, 0.114));
	c = mix(vec3(grey), c, saturation);
	c *= tint.rgb;
	vec2 d = (SCREEN_UV - 0.5) * vec2(1.0, 0.8);
	float edge = smoothstep(vignette_start, 1.25, length(d) * 2.0);
	c *= 1.0 - vignette * edge;
	COLOR = vec4(max(c, vec3(0.0)), 1.0);
}
