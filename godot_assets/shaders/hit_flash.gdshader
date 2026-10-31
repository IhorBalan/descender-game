// Whitens a sprite by `flash` (0 = normal, 1 = solid white) for hit feedback.
shader_type canvas_item;

uniform float flash : hint_range(0.0, 1.0) = 0.0;

void fragment() {
	COLOR.rgb = mix(COLOR.rgb, vec3(1.0), flash);
}
