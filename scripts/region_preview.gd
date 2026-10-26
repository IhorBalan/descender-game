@tool
class_name RegionPreview
extends Node2D

## One region (level) of the world export on its own, to look at in the
## editor: its tile layers, its lights, and over them the rooms, doors and
## markers of the export. The whole world is one 480 x 459 grid in
## levels/world/world.json, too much to open at once; scenes/world/ holds a
## scene per region, each a RegionPreview with its region set
## (tools/gen_region_scenes.py writes them from the export). Set `area` to
## one of the region's areas to narrow the view to its rooms; Paradise,
## Ruined Earth and Purgatory have several. Nothing is saved into the scene:
## everything is built from the export when the scene opens, so a live map
## pull (see LiveMap) shows up on the next open. The region's top-left cell
## sits at the origin; the label in the corner gives its place in the world.
## Run on its own (F6) the scene builds the same, with no hero.

## Rooms are outlined in this and labelled; boss rooms in the second.
const ROOM_COLOR := Color(1.0, 1.0, 1.0, 0.35)
const BOSS_COLOR := Color(1.0, 0.5, 0.3, 0.6)
const DOOR_COLOR := Color(0.4, 0.9, 1.0, 0.8)
const MARKER_COLOR := Color(1.0, 0.9, 0.3, 0.9)
const LABEL_COLOR := Color(1.0, 1.0, 1.0, 0.9)
const TILE := 16
const LIGHT_TEXTURE_SIZE := 64

## The export to read.
@export_file("*.json") var world_path := "res://levels/world/world.json":
	set(value):
		world_path = value
		_rebuild_if_ready()
## Which region of the export, top to bottom from 0 (Paradise).
@export var region := 0:
	set(value):
		region = value
		_rebuild_if_ready()
## An area of the region to show on its own, or -1 for the whole region.
@export var area := -1:
	set(value):
		area = value
		_rebuild_if_ready()
## Outline and name the rooms, mark the doors.
@export var show_rooms := true:
	set(value):
		show_rooms = value
		_rebuild_if_ready()
## Label the markers (saves, bosses, abilities, secrets...).
@export var show_markers := true:
	set(value):
		show_markers = value
		_rebuild_if_ready()
## Place the region's point lights.
@export var show_lights := true:
	set(value):
		show_lights = value
		_rebuild_if_ready()

## The export, once read.
var map: WorldMap
## The cells of the grid on show.
var cells := Rect2i()
## The region shown, as the export describes it.
var region_spec: Dictionary = {}


func _ready() -> void:
	rebuild()


func _rebuild_if_ready() -> void:
	if is_inside_tree():
		rebuild()


## Reads the export and builds the view again from scratch. Only what the
## last build made goes: anything else put under the node stays.
func rebuild() -> void:
	var old := get_node_or_null("Content")
	if old != null:
		remove_child(old)
		old.queue_free()
	map = WorldMap.read(world_path)
	if map == null:
		return
	if region < 0 or region >= map.regions.size():
		push_warning("RegionPreview: %s has no region %d" % [world_path, region])
		return
	region_spec = map.regions[region]
	cells = _region_cells()
	if area >= 0:
		var area_cells := _area_cells()
		if area_cells.has_area():
			cells = area_cells
		else:
			push_warning("RegionPreview: region %d has no area %d" % [region, area])
	# Everything hangs under one node moved so the view starts at the origin.
	var content := Node2D.new()
	content.name = "Content"
	content.position = -Vector2(cells.position) * TILE
	add_child(content)
	_build_layers(content)
	if show_lights:
		_build_lights(content)
	var overlay := Overlay.new()
	overlay.name = "Overlay"
	overlay.preview = self
	content.add_child(overlay)


## The rows of the region, across the whole grid.
func _region_cells() -> Rect2i:
	var y0 := clampi(int(floor(float(region_spec.get("y0", 0)) / TILE)), 0, map.height)
	var y1 := clampi(int(ceil(float(region_spec.get("y1", 0)) / TILE)), 0, map.height)
	return Rect2i(0, y0, map.width, y1 - y0)


## The cells the area's rooms cover, within the region's rows.
func _area_cells() -> Rect2i:
	var bounds := Rect2i()
	var first := true
	for room in rooms_shown(false):
		if int(room.get("area", -1)) != area:
			continue
		var rect := _room_rect(room)
		var room_cells := Rect2i(
			Vector2i((rect.position / TILE).floor()), Vector2i((rect.size / TILE).ceil())
		)
		bounds = room_cells if first else bounds.merge(room_cells)
		first = false
	return bounds.intersection(_region_cells()) if not first else Rect2i()


## The rooms of the region (all of its areas, or with `narrow` only the
## area on show).
func rooms_shown(narrow := true) -> Array[Dictionary]:
	var found: Array[Dictionary] = []
	for room in map.rooms:
		if int(room.get("region", -1)) != region:
			continue
		if narrow and area >= 0 and int(room.get("area", -1)) != area:
			continue
		found.append(room)
	return found


func _room_rect(room: Dictionary) -> Rect2:
	var rect: Array = room.get("rect", [])
	if rect.size() != 4:
		return Rect2()
	return Rect2(float(rect[0]), float(rect[1]), float(rect[2]), float(rect[3]))


## The cells on show, in level pixels.
func shown_rect() -> Rect2:
	return Rect2(Vector2(cells.position) * TILE, Vector2(cells.size) * TILE)


## One TileMapLayer per layer of the export, with the cells on show; the
## glow layer added on top, as in the game.
func _build_layers(parent: Node2D) -> void:
	var tile_set := map.build_tile_set()
	for layer_name in map.layer_names:
		var layer := TileMapLayer.new()
		layer.name = layer_name
		layer.tile_set = tile_set
		layer.texture_filter = map.texture_filter()
		layer.scale = Vector2.ONE * map.art_scale()
		layer.collision_enabled = false
		var source := 0
		if layer_name == WorldMap.LAYER_GLOW:
			source = 1
			var material := CanvasItemMaterial.new()
			material.light_mode = CanvasItemMaterial.LIGHT_MODE_UNSHADED
			material.blend_mode = CanvasItemMaterial.BLEND_MODE_ADD
			layer.material = material
		var ids := map.cells(layer_name)
		for y in range(cells.position.y, cells.end.y):
			for x in range(cells.position.x, cells.end.x):
				var id := ids[y * map.width + x]
				if id >= 0:
					layer.set_cell(Vector2i(x, y), source, map.atlas_coords(id))
		parent.add_child(layer)


## The export's point lights within the view, as the game places them.
func _build_lights(parent: Node2D) -> void:
	var shown := shown_rect()
	var texture := _light_texture()
	var holder := Node2D.new()
	holder.name = "Lights"
	parent.add_child(holder)
	for spec in map.lights:
		var at := Vector2(float(spec.get("x", 0)), float(spec.get("y", 0)))
		if not shown.has_point(at):
			continue
		var light := PointLight2D.new()
		light.texture = texture
		light.position = at
		light.height = float(spec.get("z", 0))
		light.color = Color.from_string(str(spec.get("color", "#ffffff")), Color.WHITE)
		light.energy = float(spec.get("energy", 1.0))
		light.texture_scale = float(spec.get("radius", 64)) * 2.0 / LIGHT_TEXTURE_SIZE
		light.shadow_enabled = false
		holder.add_child(light)


func _light_texture() -> ImageTexture:
	var image := Image.create(LIGHT_TEXTURE_SIZE, LIGHT_TEXTURE_SIZE, false, Image.FORMAT_RGBA8)
	var centre := Vector2.ONE * LIGHT_TEXTURE_SIZE / 2.0
	for y in LIGHT_TEXTURE_SIZE:
		for x in LIGHT_TEXTURE_SIZE:
			var distance := Vector2(x + 0.5, y + 0.5).distance_to(centre) / centre.x
			image.set_pixel(x, y, Color(1.0, 1.0, 1.0, clampf(1.0 - distance, 0.0, 1.0)))
	return ImageTexture.create_from_image(image)


## The rooms, doors and markers of the export drawn over the tiles, with the
## region's name and its place in the world in the corner.
class Overlay:
	extends Node2D
	var preview: RegionPreview

	func _ready() -> void:
		z_index = 10

	func _draw() -> void:
		if preview == null or preview.map == null:
			return
		var font := ThemeDB.fallback_font
		var shown := preview.shown_rect()
		var map := preview.map
		if preview.show_rooms:
			for room in preview.rooms_shown():
				var rect := preview._room_rect(room)
				var boss := str(room.get("kind", "")) == "boss"
				draw_rect(rect, BOSS_COLOR if boss else ROOM_COLOR, false, 2.0)
				var label := "room %d" % int(room.get("id", 0))
				var name := str(room.get("name", "")) if room.get("name") != null else ""
				if not name.is_empty():
					label += ": " + name
				draw_string(font, rect.position + Vector2(6.0, 18.0), label, 0, -1, 14, LABEL_COLOR)
			for door in map.doors:
				var at := Vector2(int(door.get("x", 0)) + 0.5, int(door.get("y", 0)) + 0.5) * TILE
				if shown.has_point(at):
					draw_circle(at, 5.0, DOOR_COLOR)
		if preview.show_markers:
			for marker in map.markers:
				var at := map.marker_position(marker)
				if not shown.has_point(at):
					continue
				draw_rect(Rect2(at - Vector2(5.0, 10.0), Vector2(10.0, 10.0)), MARKER_COLOR)
				var text := str(marker.get("type", ""))
				var label := str(marker.get("label", "")) if marker.get("label") != null else ""
				if not label.is_empty():
					text += ": " + label
				draw_string(font, at + Vector2(8.0, -2.0), text, 0, -1, 12, MARKER_COLOR)
		var title := "%d. %s" % [preview.region, str(preview.region_spec.get("name", ""))]
		if preview.area >= 0:
			title += "  (area %d)" % preview.area
		title += "   world cells %s to %s" % [preview.cells.position, preview.cells.end]
		draw_string(font, shown.position + Vector2(8.0, -8.0), title, 0, -1, 20, LABEL_COLOR)
