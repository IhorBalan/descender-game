class_name UiComponent
extends Control

## A UI component made in the map editor's Components page (health bars,
## counters, panels), drawn from its component.json and textures (see
## ComponentStore). Parts are drawn back to front like the editor draws
## them: colour boxes, images (stretched, tiled or nine-sliced, tinted by
## their colour), fill bars bound to props (clipped or squeezed, with an
## optional lag that trails behind drops) and text with {prop}
## placeholders. The game sets props with set_prop; the component is drawn
## at `display_width` level pixels wide, its height following its shape.

const Backdrop := preload("res://scripts/backdrop.gd")

## How wide the component is drawn, in level pixels; 0 draws it at its own
## size.
@export var display_width := 0.0:
	set(value):
		display_width = value
		_resize()

## The component's file, parsed; empty until load_from succeeds.
var spec: Dictionary = {}
## Where it was loaded from.
var source_dir := ""
## Prop values by name, starting at the component's defaults.
var values: Dictionary = {}

var _textures: Dictionary = {}
## Lagging fills' shown ratios, by part id.
var _shown: Dictionary = {}


func _init() -> void:
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	texture_filter = CanvasItem.TEXTURE_FILTER_LINEAR_WITH_MIPMAPS


## Loads the component in `dir` (component.json and textures/). Returns
## whether it could be read; the values keep any prop already set.
func load_from(dir: String) -> bool:
	var text := FileAccess.get_file_as_string(dir.path_join("component.json"))
	var parsed: Variant = JSON.parse_string(text) if not text.is_empty() else null
	if not parsed is Dictionary or str(parsed.get("format", "")) != "lit-pixel-component":
		return false
	spec = parsed
	source_dir = dir
	_textures.clear()
	for part: Variant in spec.get("parts", []):
		if part is Dictionary and part.has("texture"):
			var path := dir.path_join(str(part["texture"]))
			if not _textures.has(path):
				_textures[path] = Backdrop.load_texture(path, true)
	var kept := values
	values = {}
	for prop: Variant in spec.get("props", []):
		if prop is Dictionary:
			var name := str(prop.get("name", ""))
			values[name] = kept.get(name, prop.get("default"))
	_shown.clear()
	_resize()
	queue_redraw()
	return true


## Sets a prop; unknown names are ignored. Returns whether the component
## has the prop.
func set_prop(name: String, value: Variant) -> bool:
	if not values.has(name):
		return false
	if values[name] != value:
		values[name] = value
		queue_redraw()
	return true


func has_prop(name: String) -> bool:
	return values.has(name)


## Level pixels per component pixel.
func view_scale() -> float:
	var w := float(spec.get("w", 0))
	if w <= 0.0:
		return 1.0
	return display_width / w if display_width > 0.0 else 1.0


func _resize() -> void:
	var s := view_scale()
	custom_minimum_size = Vector2(float(spec.get("w", 0)), float(spec.get("h", 0))) * s
	size = custom_minimum_size


func _process(delta: float) -> void:
	# Lagging fills fall behind their value at a full bar per `lag` seconds
	# and jump up at once, as in the editor.
	for part: Variant in spec.get("parts", []):
		if not part is Dictionary or str(part.get("kind")) != "fill":
			continue
		var lag := float(part.get("lag", 0.0))
		var id := str(part.get("id", ""))
		var target := fill_ratio(part)
		if lag <= 0.0:
			_shown.erase(id)
			continue
		var current: float = _shown.get(id, target)
		var next := target if target >= current else maxf(target, current - delta / lag)
		if next != current or not _shown.has(id):
			_shown[id] = next
			queue_redraw()


func _draw() -> void:
	var s := view_scale()
	for part: Variant in spec.get("parts", []):
		if not part is Dictionary or not is_visible_part(part):
			continue
		var opacity := clampf(float(part.get("opacity", 1.0)), 0.0, 1.0)
		if opacity <= 0.0:
			continue
		var color := part_color(part)
		color.a = opacity
		var box := _box(part)
		var kind := str(part.get("kind", "rect"))
		var texture: Texture2D = _textures.get(source_dir.path_join(str(part.get("texture", ""))))
		draw_set_transform(Vector2.ZERO, 0.0, Vector2(s, s))
		match kind:
			"rect":
				draw_rect(box, color)
			"image":
				if texture:
					_draw_texture(texture, part, box, Rect2(box), color)
			"fill":
				var id := str(part.get("id", ""))
				var ratio: float = _shown.get(id, fill_ratio(part))
				var filled := fill_rect(part, ratio)
				if texture == null:
					draw_rect(filled, color)
				elif bool(part.get("clip", true)):
					_draw_texture(texture, part, box, filled, color)
				else:
					_draw_texture(texture, part, filled, filled, color)
			"text":
				_draw_text(part, box, color, s)
	draw_set_transform(Vector2.ZERO, 0.0, Vector2.ONE)


func _box(part: Dictionary) -> Rect2:
	return Rect2(
		float(part.get("x", 0)),
		float(part.get("y", 0)),
		maxf(1.0, float(part.get("w", 1))),
		maxf(1.0, float(part.get("h", 1)))
	)


## Draws a texture into `box` by the part's mode, keeping only what falls
## inside `clip`.
func _draw_texture(
	texture: Texture2D, part: Dictionary, box: Rect2, clip: Rect2, color: Color
) -> void:
	var tw := float(part.get("textureW", texture.get_width()))
	var th := float(part.get("textureH", texture.get_height()))
	# The PNG may have been downscaled or kept at the editor's size; draw
	# from it in its own pixels.
	var kx := texture.get_width() / tw if tw > 0.0 else 1.0
	var ky := texture.get_height() / th if th > 0.0 else 1.0
	for piece in pieces(str(part.get("mode", "stretch")), tw, th, box, part.get("slice", [])):
		var cut := clip_piece(piece[0], piece[1], clip)
		if cut.is_empty():
			continue
		var src: Rect2 = cut[0]
		draw_texture_rect_region(
			texture,
			cut[1],
			Rect2(src.position.x * kx, src.position.y * ky, src.size.x * kx, src.size.y * ky),
			color
		)


func _draw_text(part: Dictionary, box: Rect2, color: Color, s: float) -> void:
	# Text is drawn at the screen's size, not scaled as a picture, so it
	# stays sharp; it keeps its share of the component.
	draw_set_transform(Vector2.ZERO, 0.0, Vector2.ONE)
	var font := ThemeDB.fallback_font
	var font_size := maxi(1, roundi(float(part.get("size", 8)) * s))
	var text := format_text(str(part.get("text", "")))
	var align := HORIZONTAL_ALIGNMENT_LEFT
	match str(part.get("align", "left")):
		"center":
			align = HORIZONTAL_ALIGNMENT_CENTER
		"right":
			align = HORIZONTAL_ALIGNMENT_RIGHT
	var at := Vector2(box.position.x * s, box.position.y * s + font.get_ascent(font_size))
	var width := box.size.x * s
	if bool(part.get("shadow", true)):
		draw_string(
			font,
			at + Vector2.ONE,
			text,
			align,
			width,
			font_size,
			Color(0.0, 0.0, 0.0, 0.8 * color.a)
		)
	draw_string(font, at, text, align, width, font_size, color)


# ---------- the editor's rules (src/component.ts) ----------


## A fill's 0..1 ratio: its value prop over its max prop, or over the value
## prop's own min..max range; 1 when it is bound to nothing.
func fill_ratio(part: Dictionary) -> float:
	var bind: Dictionary = part.get("bind", {})
	var value_name := str(bind.get("value", ""))
	if value_name.is_empty():
		return 1.0
	var def := _prop(value_name)
	var v := float(values.get(value_name, def.get("default", 0.0)))
	var max_name := str(bind.get("max", ""))
	if not max_name.is_empty():
		var top := float(values.get(max_name, 0.0))
		return clampf(v / top, 0.0, 1.0) if top > 0.0 else 0.0
	var low := float(def.get("min", 0.0))
	var high := float(def.get("max", 1.0))
	return 0.0 if high == low else clampf((v - low) / (high - low), 0.0, 1.0)


func is_visible_part(part: Dictionary) -> bool:
	var name := str(part.get("bind", {}).get("visible", ""))
	if name.is_empty():
		return true
	var v: Variant = values.get(name)
	if v is float or v is int:
		return v > 0
	if v is String:
		return not v.is_empty()
	return bool(v)


func part_color(part: Dictionary) -> Color:
	var name := str(part.get("bind", {}).get("color", ""))
	var bound: Variant = values.get(name) if not name.is_empty() else null
	if bound is String and Color.html_is_valid(bound):
		return Color.html(bound)
	return Color.from_string(str(part.get("color", "#ffffff")), Color.WHITE)


## Text with {prop} placeholders filled in; numbers keep at most two
## decimals and whole numbers show none.
func format_text(text: String) -> String:
	var re := RegEx.create_from_string("\\{([A-Za-z_][A-Za-z0-9_]*)\\}")
	var out := ""
	var at := 0
	for m in re.search_all(text):
		out += text.substr(at, m.get_start() - at)
		var v: Variant = values.get(m.get_string(1))
		if v is float or v is int:
			var rounded := roundf(float(v) * 100.0) / 100.0
			out += str(int(rounded)) if rounded == floorf(rounded) else str(rounded)
		elif v != null:
			out += str(v)
		at = m.get_end()
	return out + text.substr(at)


## The filled part of a fill's box for a ratio, growing from the side
## opposite to its direction.
static func fill_rect(part: Dictionary, ratio: float) -> Rect2:
	var r := clampf(ratio, 0.0, 1.0)
	var x := float(part.get("x", 0))
	var y := float(part.get("y", 0))
	var w := float(part.get("w", 1))
	var h := float(part.get("h", 1))
	match str(part.get("dir", "right")):
		"left":
			return Rect2(x + w - roundf(w * r), y, roundf(w * r), h)
		"up":
			return Rect2(x, y + h - roundf(h * r), w, roundf(h * r))
		"down":
			return Rect2(x, y, w, roundf(h * r))
	return Rect2(x, y, roundf(w * r), h)


## The [source, destination] rectangles that draw a tw x th texture into a
## box: one stretched piece, nine slices with fixed margins (which shrink
## when the box is smaller than both together), or repeated tiles.
static func pieces(mode: String, tw: float, th: float, box: Rect2, slice: Variant) -> Array:
	var out: Array = []
	if box.size.x <= 0.0 or box.size.y <= 0.0 or tw <= 0.0 or th <= 0.0:
		return out
	if mode == "nine" and slice is Array and slice.size() == 4:
		var l := minf(maxf(0.0, roundf(float(slice[0]))), tw)
		var t := minf(maxf(0.0, roundf(float(slice[1]))), th)
		var r := minf(maxf(0.0, roundf(float(slice[2]))), tw - l)
		var b := minf(maxf(0.0, roundf(float(slice[3]))), th - t)
		var fit_x := box.size.x / (l + r) if l + r > box.size.x else 1.0
		var fit_y := box.size.y / (t + b) if t + b > box.size.y else 1.0
		var dl := floorf(l * fit_x)
		var dr := floorf(r * fit_x)
		var dt := floorf(t * fit_y)
		var db := floorf(b * fit_y)
		var cols := [
			[0.0, l, 0.0, dl],
			[l, tw - l - r, dl, box.size.x - dl - dr],
			[tw - r, r, box.size.x - dr, dr]
		]
		var rows := [
			[0.0, t, 0.0, dt],
			[t, th - t - b, dt, box.size.y - dt - db],
			[th - b, b, box.size.y - db, db]
		]
		for row: Array in rows:
			for col: Array in cols:
				if col[1] <= 0.0 or row[1] <= 0.0 or col[3] <= 0.0 or row[3] <= 0.0:
					continue
				out.append(
					[
						Rect2(col[0], row[0], col[1], row[1]),
						Rect2(box.position.x + col[2], box.position.y + row[2], col[3], row[3])
					]
				)
		return out
	if mode == "tile":
		var y := 0.0
		while y < box.size.y:
			var hh := minf(th, box.size.y - y)
			var x := 0.0
			while x < box.size.x:
				var ww := minf(tw, box.size.x - x)
				out.append(
					[Rect2(0, 0, ww, hh), Rect2(box.position + Vector2(x, y), Vector2(ww, hh))]
				)
				x += tw
			y += th
		return out
	out.append([Rect2(0, 0, tw, th), box])
	return out


## A piece cut to `clip`: the destination intersected, the source cut by the
## same share. Empty when nothing is left.
static func clip_piece(src: Rect2, dst: Rect2, clip: Rect2) -> Array:
	var cut := dst.intersection(clip)
	if cut.size.x <= 0.0 or cut.size.y <= 0.0:
		return []
	var kx := src.size.x / dst.size.x
	var ky := src.size.y / dst.size.y
	var s := Rect2(
		src.position.x + (cut.position.x - dst.position.x) * kx,
		src.position.y + (cut.position.y - dst.position.y) * ky,
		cut.size.x * kx,
		cut.size.y * ky
	)
	return [s, cut]


func _prop(name: String) -> Dictionary:
	for prop: Variant in spec.get("props", []):
		if prop is Dictionary and str(prop.get("name", "")) == name:
			return prop
	return {}
