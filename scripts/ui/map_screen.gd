class_name MapScreen
extends Control

## The map page of the pause menu: the rooms of the world the hero has
## walked through, drawn on the room grid of the export, with the hero,
## the checkpoints he can come back to, the boss rooms and the doors he
## has not been through yet. A room is found the first time the hero
## stands in it (main.gd sets its flag, see room_flag), so the map fills
## in as he goes and is kept with the save like any other story flag.
##
## The whole world fits the page: the export's room grid is 16 by 27
## cells of 30 x 17 tiles, drawn at CELL pixels each.

signal closed

## The flag prefix a found room is kept under in the story flags.
const ROOM_FLAG_PREFIX := "room:"
## One room cell of the export, on screen.
const CELL := Vector2(10.0, 5.0)
## Where the grid's top left corner sits on the page.
const ORIGIN := Vector2(16.0, 26.0)
## How far the room fill is pulled towards white from the region's
## ambient, so dark regions still read on the dark page.
const FILL_LIFT := 0.35
const BORDER_COLOR := Color(0.05, 0.03, 0.12)
const HERE_COLOR := Color(1.0, 1.0, 1.0)
const HERO_COLOR := Color(1.0, 1.0, 1.0)
const REST_COLOR := Color(1.0, 0.86, 0.32)
const REST_DIM_COLOR := Color(0.75, 0.62, 0.3)
const BOSS_COLOR := Color(0.85, 0.25, 0.25)
const BOSS_DONE_COLOR := Color(0.15, 0.1, 0.2)
const EXIT_COLOR := Color(0.55, 0.52, 0.7)
const LOCK_COLOR := Color(1.0, 0.86, 0.32)
const GRID_COLOR := Color(0.12, 0.09, 0.22)
const REGION_NAME_COLOR := Color(0.7, 0.68, 0.85)
const REGION_UNKNOWN_COLOR := Color(0.3, 0.28, 0.42)
const BLINK_TIME := 0.35
## How many of the walls of a found room its door tick breaks.
const DOOR_TICK := 2.0

var world: WorldMap
## The hero's feet, in level pixels, when the page opened.
var hero_position := Vector2.ZERO
## The checkpoints of the level, and which one the hero last rested at.
var checkpoints: Array[Checkpoint] = []
## Pixels per room cell in the export, read from the rooms themselves.
var _cell_px := Vector2(480.0, 272.0)
var _blink_timer := 0.0
var _blink_on := true
var _region_labels: Array[Label] = []

@onready var heading: Label = $Heading
@onready var found_label: Label = $Found
@onready var legend_label: Label = $Legend
@onready var back_button: Button = $Back


## The story flag a found room is kept under.
static func room_flag(room_id: int) -> String:
	return "%s%d" % [ROOM_FLAG_PREFIX, room_id]


## Whether the hero has stood in a room of the export.
static func is_found(room: Dictionary) -> bool:
	return Story.has_flag(room_flag(int(room.get("id", -1))))


## How many rooms of a world the hero has found.
static func rooms_found(map: WorldMap) -> int:
	var count := 0
	for room in map.rooms:
		if is_found(room):
			count += 1
	return count


func _ready() -> void:
	visible = false
	back_button.pressed.connect(close)


func _unhandled_input(event: InputEvent) -> void:
	if not visible:
		return
	if event.is_action_pressed("ui_cancel") or event.is_action_pressed("pause"):
		close()
		get_viewport().set_input_as_handled()


func _process(delta: float) -> void:
	if not visible:
		return
	_blink_timer += delta
	if _blink_timer >= BLINK_TIME:
		_blink_timer = 0.0
		_blink_on = not _blink_on
		queue_redraw()


## Shows the map of `map` with the hero at `hero` (level pixels) and the
## level's checkpoints (the active one is the hero's way back).
func open(map: WorldMap, hero: Vector2, level_checkpoints: Array[Checkpoint]) -> void:
	world = map
	hero_position = hero
	checkpoints = level_checkpoints
	refresh()
	visible = true
	_blink_on = true
	_blink_timer = 0.0
	back_button.grab_focus()


func close() -> void:
	visible = false
	closed.emit()


## Rebuilds the labels from the world and the story flags; the rooms
## themselves are drawn in _draw.
func refresh() -> void:
	for label in _region_labels:
		label.queue_free()
	_region_labels = []
	if world == null:
		found_label.text = ""
		queue_redraw()
		return
	_read_cell_size()
	found_label.text = "%d of %d rooms found" % [rooms_found(world), world.rooms.size()]
	var known := _regions_found()
	for i in world.regions.size():
		var region: Dictionary = world.regions[i]
		var y0 := _map_y(float(region.get("y0", 0.0)))
		var y1 := _map_y(float(region.get("y1", 0.0)))
		var label := Label.new()
		label.text = str(region.get("name", "")) if known.has(i) else "?"
		label.add_theme_color_override(
			"font_color", REGION_NAME_COLOR if known.has(i) else REGION_UNKNOWN_COLOR
		)
		label.position = Vector2(ORIGIN.x + _grid_size().x + 8.0, (y0 + y1) / 2.0 - 5.0)
		label.size = Vector2(100.0, 10.0)
		label.mouse_filter = Control.MOUSE_FILTER_IGNORE
		add_child(label)
		_region_labels.append(label)
	queue_redraw()


## The region indices with a found room in them.
func _regions_found() -> Dictionary:
	var known := {}
	for room in world.rooms:
		if is_found(room):
			known[int(room.get("region", -1))] = true
	return known


## The pixel size of one room cell, from the first room with a rect.
func _read_cell_size() -> void:
	for room in world.rooms:
		var rect: Array = room.get("rect", [])
		var cw := int(room.get("cw", 0))
		var ch := int(room.get("ch", 0))
		if rect.size() == 4 and cw > 0 and ch > 0:
			_cell_px = Vector2(float(rect[2]) / cw, float(rect[3]) / ch)
			return


## The grid in screen pixels, from the rooms' far edges.
func _grid_size() -> Vector2:
	var cells := Vector2.ZERO
	for room in world.rooms:
		cells.x = maxf(cells.x, int(room.get("cx", 0)) + int(room.get("cw", 0)))
		cells.y = maxf(cells.y, int(room.get("cy", 0)) + int(room.get("ch", 0)))
	return cells * CELL


func _map_x(x_px: float) -> float:
	return ORIGIN.x + x_px / _cell_px.x * CELL.x


func _map_y(y_px: float) -> float:
	return ORIGIN.y + y_px / _cell_px.y * CELL.y


func _map_point(point: Vector2) -> Vector2:
	return Vector2(_map_x(point.x), _map_y(point.y)).floor()


func _room_rect(room: Dictionary) -> Rect2:
	var at := Vector2(int(room.get("cx", 0)), int(room.get("cy", 0))) * CELL
	var size := Vector2(int(room.get("cw", 1)), int(room.get("ch", 1))) * CELL
	return Rect2(ORIGIN + at, size)


func _room_by_id(room_id: int) -> Dictionary:
	for room in world.rooms:
		if int(room.get("id", -1)) == room_id:
			return room
	return {}


func _region_color(room: Dictionary) -> Color:
	var index := int(room.get("region", -1))
	if index < 0 or index >= world.regions.size():
		return Color(0.4, 0.4, 0.5)
	var ambient := str(world.regions[index].get("ambient", "#666666"))
	return Color.from_string(ambient, Color(0.4, 0.4, 0.5)).lerp(Color.WHITE, FILL_LIFT)


func _draw() -> void:
	if world == null:
		return
	var grid := Rect2(ORIGIN, _grid_size())
	draw_rect(grid.grow(2.0), GRID_COLOR)
	var here := _hero_room()
	for room in world.rooms:
		if not is_found(room):
			continue
		var rect := _room_rect(room)
		var color := _region_color(room)
		var kind := str(room.get("kind", ""))
		if kind == "boss":
			var id := Boss.id_for(str(room.get("name", "")))
			color = BOSS_DONE_COLOR if Story.has_flag(Boss.flag_for(id)) else BOSS_COLOR
		draw_rect(rect, color)
		draw_rect(rect, BORDER_COLOR, false, 1.0)
		if int(room.get("id", -1)) == here:
			draw_rect(rect.grow(-1.0), HERE_COLOR, false, 1.0)
	for door in world.doors:
		_draw_door(door)
	for checkpoint in checkpoints:
		var room_id := _room_at(checkpoint.global_position)
		if room_id < 0 or not is_found(_room_by_id(room_id)):
			continue
		var at := _map_point(checkpoint.global_position) - Vector2(1.0, 2.0)
		draw_rect(Rect2(at, Vector2(2.0, 2.0)), REST_COLOR if checkpoint.active else REST_DIM_COLOR)
	if _blink_on:
		var at := _map_point(hero_position) - Vector2(1.0, 2.0)
		draw_rect(Rect2(at, Vector2(3.0, 3.0)), HERO_COLOR)


## A door between two found rooms is a gap in the wall between them; one
## out of a found room into one not yet found is a tick on the far side,
## the way not taken, gold when a seal still holds it.
func _draw_door(door: Dictionary) -> void:
	var a := _room_by_id(int(door.get("a", -1)))
	var b := _room_by_id(int(door.get("b", -1)))
	if a.is_empty() or b.is_empty():
		return
	var found_a := is_found(a)
	var found_b := is_found(b)
	if not found_a and not found_b:
		return
	# A door the editor has not placed yet sits at the origin.
	if int(door.get("x", 0)) == 0 and int(door.get("y", 0)) == 0:
		return
	# A secret passage is only drawn once both its rooms are known.
	if bool(door.get("secret", false)) and not (found_a and found_b):
		return
	var tile := float(world.tile)
	var at := _map_point(Vector2(int(door.get("x", 0)) * tile, int(door.get("y", 0)) * tile))
	var vertical := str(door.get("dir", "h")) == "v"
	var size := Vector2(1.0, DOOR_TICK) if not vertical else Vector2(DOOR_TICK, 1.0)
	var rect := Rect2(at - size / 2.0, size).abs()
	rect.position = rect.position.floor()
	var color: Color
	if found_a and found_b:
		color = _region_color(a if _room_rect(a).has_point(rect.position) else b)
	elif door.get("lock") != null and not _lock_open(door):
		color = LOCK_COLOR
	else:
		color = EXIT_COLOR
	draw_rect(rect, color)


## Whether the hero may already pass a locked door: the ability's flag or
## the boss's death. Mirrors LevelBuilder's locks without needing them.
func _lock_open(door: Dictionary) -> bool:
	var lock: Dictionary = door.get("lock", {})
	match str(lock.get("kind", "")):
		"ability":
			var ability := int(lock.get("ability", -1))
			var player := get_tree().get_first_node_in_group("player") as Player
			return (
				player != null
				and ability >= 0
				and ability < Player.Ability.size()
				and player.has_ability(ability as Player.Ability)
			)
		"boss":
			var region := int(lock.get("region", -1))
			for room in world.rooms:
				if str(room.get("kind", "")) == "boss" and int(room.get("region", -1)) == region:
					var id := Boss.id_for(str(room.get("name", "")))
					return Story.has_flag(Boss.flag_for(id))
	return false


func _room_at(point: Vector2) -> int:
	for room in world.rooms:
		var rect: Array = room.get("rect", [])
		if rect.size() == 4:
			var r := Rect2(float(rect[0]), float(rect[1]), float(rect[2]), float(rect[3]))
			if r.has_point(point):
				return int(room.get("id", -1))
	return -1


func _hero_room() -> int:
	return _room_at(hero_position)


## The names shown down the side, for the test: a found region's name, or
## "?" for one the hero has not reached.
func region_names() -> Array[String]:
	var names: Array[String] = []
	for label in _region_labels:
		names.append(label.text)
	return names
