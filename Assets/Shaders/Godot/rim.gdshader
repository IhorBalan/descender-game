// Rim light from the sky on the lit tile layers (see LevelBuilder): where
// a tile's normal map faces up, the region's light colour is added, so
// ledges and the tops of walls catch the sky like the hero's silhouette
// does. `strength` 0 turns it off; the rest of the lighting is Godot's.
shader_type canvas_item;

uniform vec4 rim_color : source_color = vec4(1.0);
uniform float strength : hint_range(0.0, 1.0) = 0.25;
uniform float sharpness : hint_range(0.5, 8.0) = 2.0;

void fragment() {
	vec3 n = texture(NORMAL_TEXTURE, UV).rgb * 2.0 - 1.0;
	float up = pow(max(n.y, 0.0), sharpness);
	COLOR.rgb += rim_color.rgb * strength * up * COLOR.a;
}
