// The procedural sky behind a level, drawn over the whole view (see Main):
// a gradient from `top` to `bottom` across the region's band, with the
// map editor's weather on it. Open regions get distant ridges, a sea of
// clouds low in the band and puffs higher up; a cave (`cave` = 1) gets
// flat dark streaks. Pixel art (`art_tile` of 16) is stepped and dithered
// like the editor's; painted maps get a smooth sky. `cave` is a mix so a
// region change can cross-fade, and `dark` is the descent darkening.
shader_type canvas_item;
render_mode unshaded;

uniform vec3 top : source_color = vec3(0.05, 0.05, 0.09);
uniform vec3 bottom : source_color = vec3(0.1, 0.1, 0.15);
uniform float cave : hint_range(0.0, 1.0) = 0.0;
uniform vec2 band = vec2(0.0, 1.0);
uniform vec2 cam_origin = vec2(0.0);
uniform vec2 view_size = vec2(320.0, 180.0);
uniform float art_tile = 16.0;
uniform float dark : hint_range(0.0, 1.0) = 1.0;

float h21(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float n1(float x) {
	float i = floor(x);
	float f = fract(x);
	f = f * f * (3.0 - 2.0 * f);
	return mix(h21(vec2(i, 0.0)), h21(vec2(i + 1.0, 0.0)), f);
}

float n2(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(
		mix(h21(i), h21(i + vec2(1.0, 0.0)), f.x),
		mix(h21(i + vec2(0.0, 1.0)), h21(i + vec2(1.0, 1.0)), f.x),
		f.y
	);
}

float fbm(vec2 p) {
	return n2(p) * 0.55 + n2(p * 2.1) * 0.3 + n2(p * 4.3) * 0.15;
}

float bayer(vec2 p) {
	const float m[16] = float[16](
		0., 8., 2., 10., 12., 4., 14., 6., 3., 11., 1., 9., 15., 7., 13., 5.
	);
	ivec2 q = ivec2(int(mod(p.x, 4.0)), int(mod(p.y, 4.0)));
	return (m[q.x + q.y * 4] + 0.5) / 16.0;
}

// The open sky of a painted map: smooth, with pale distant shapes and a
// soft sea of clouds low in the band.
vec3 painted_sky(vec2 wp, float tt, float t) {
	vec3 c = mix(top, bottom, smoothstep(0.0, 1.0, tt));
	float xf = wp.x;
	float y = wp.y;
	vec3 pale = mix(bottom, vec3(1.0), 0.55);
	vec3 dim = mix(bottom, top, 0.3);
	float ridge = 0.4 + 0.16 * n1(xf / 260.0) + 0.08 * n1(xf / 70.0 + 5.0);
	float sil = smoothstep(ridge - 0.012, ridge + 0.012, tt);
	c = mix(c, mix(c, dim, 0.3), sil);
	float f1 = fbm(vec2(xf / 360.0 + t * 0.002, y / 150.0));
	float sea = smoothstep(0.46, 0.68, f1) * smoothstep(0.5, 0.95, tt);
	c = mix(c, pale, sea * 0.85);
	float f2 = fbm(vec2(xf / 220.0 + 13.0 + t * 0.004, y / 100.0));
	float sea2 = smoothstep(0.5, 0.72, f2) * smoothstep(0.72, 1.0, tt);
	c = mix(c, mix(pale, dim, 0.25), sea2 * 0.9);
	float f3 = fbm(vec2(xf / 200.0 + t * 0.003, y / 80.0 + 3.0));
	float puff = smoothstep(0.62, 0.8, f3) * (1.0 - smoothstep(0.3, 0.6, tt));
	return mix(c, pale, puff * 0.6);
}

// The open sky of a pixel-art map: stepped bands, two ridge lines and
// hard-edged clouds.
vec3 pixel_sky(vec2 wp, float t, float time) {
	vec3 c = mix(top, bottom, t);
	float y = wp.y;
	float bh = band.y - band.x;
	float xf = floor(wp.x / 6.0) * 6.0;
	float far = band.y - bh * (0.18 + 0.32 * n1(xf / 140.0) + 0.12 * step(0.7, n1(xf / 30.0)));
	float xn = floor(wp.x / 4.0) * 4.0;
	float near = band.y
		- bh * (0.06 + 0.22 * n1(xn / 90.0 + 17.0) + 0.1 * step(0.75, n1(xn / 18.0 + 3.0)));
	if (y > far) {
		c = mix(c, mix(top, bottom, 0.35) * 0.7, 0.55);
	}
	if (y > near) {
		c = mix(c, top * 0.35, 0.75);
	}
	float cl = fbm(vec2(wp.x / 90.0 + time * 0.004, y / 26.0));
	float cloud = step(0.64, cl) * (1.0 - step(near, y));
	return mix(c, mix(bottom, vec3(1.0), 0.6), cloud * 0.85);
}

// A cave: the gradient under flat dark streaks.
vec3 cave_sky(vec2 wp, float t) {
	vec3 c = mix(top, bottom, t);
	float streak = n2(vec2(wp.x / 12.0, wp.y / 80.0));
	return c * (0.55 + 0.25 * floor(streak * 4.0) / 4.0);
}

void fragment() {
	vec2 wp = floor(cam_origin + UV * view_size);
	float span = max(band.y - band.x, 1.0);
	float tt = clamp((wp.y - band.x) / span, 0.0, 1.0);
	float steps = 12.0;
	float t = floor(tt * steps + bayer(floor(wp / 2.0))) / steps;
	vec3 open = art_tile > 16.0 ? painted_sky(wp, tt, TIME) : pixel_sky(wp, t, TIME);
	vec3 c = mix(open, cave_sky(wp, t), cave);
	COLOR = vec4(c * dark, 1.0);
}
