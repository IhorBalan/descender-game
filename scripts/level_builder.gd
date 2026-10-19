class_name LevelBuilder
extends Node2D

## Builds a level from an ASCII map (see levels/level_01.txt).
##
##   #  solid tile      ^  spikes           P  hero start
##   w  imp (walker)    g  angel guard      f  watcher (flyer)
##   C  checkpoint      G  goal gate        .  empty
##
## Tiles are 16x16. Rows may have different lengths; missing cells are empty.

const TILE := 16
const TILES_TEXTURE := preload("res://assets/sprites/tiles.png")
const WALKER_SCENE := preload("res://scenes/enemies/walker.tscn")
const FLYER_SCENE := preload("res://scenes/enemies/flyer.tscn")
const CHECKPOINT_SCENE := preload("res://scenes/checkpoint.tscn")
const GOAL_SCENE := preload("res://scenes/goal.tscn")
const HAZARD_SCENE := preload("res://scenes/hazard.tscn")

const TILE_FILL := 0
const TILE_TOP := 1
const TILE_SPIKE := 2

var rows: PackedStringArray = []
var width := 0
var height := 0
var size_px := Vector2.ZERO
var player_start := Vector2.ZERO
var checkpoints: Array[Checkpoint] = []
var goal: Goal = null

var _enemy_spawns: Array[Dictionary] = []
var _tiles: Node2D
var _solids: StaticBody2D
var _hazards: Node2D
var _objects: Node2D
var _enemies: Node2D


func load_from_file(path: String) -> void:
	build(FileAccess.get_file_as_string(path))


func build(text: String) -> void:
	for child in get_children():
		child.queue_free()
	rows = []
	for line in text.split("\n"):
		line = line.trim_suffix("\r")
		if line.begins_with(";"):
			continue
		rows.append(line)
	while rows.size() > 0 and rows[rows.size() - 1].strip_edges().is_empty():
		rows.remove_at(rows.size() - 1)
	height = rows.size()
	width = 0
	for row in rows:
		width = maxi(width, row.length())
	size_px = Vector2(width * TILE, height * TILE)

	_tiles = Node2D.new()
	_tiles.name = "Tiles"
	add_child(_tiles)
	_solids = StaticBody2D.new()
	_solids.name = "Solids"
	_solids.collision_layer = 1
	_solids.collision_mask = 0
	add_child(_solids)
	_hazards = Node2D.new()
	_hazards.name = "Hazards"
	add_child(_hazards)
	_objects = Node2D.new()
	_objects.name = "Objects"
	add_child(_objects)
	_enemies = Node2D.new()
	_enemies.name = "Enemies"
	add_child(_enemies)

	checkpoints = []
	goal = null
	_enemy_spawns = []
	for y in height:
		_build_row(y)


func _cell(x: int, y: int) -> String:
	if y < 0 or y >= height or x < 0 or x >= rows[y].length():
		return "."
	return rows[y][x]


func _cell_center(x: int, y: int) -> Vector2:
	return Vector2(x * TILE + TILE / 2.0, y * TILE + TILE / 2.0)


func _cell_bottom(x: int, y: int) -> Vector2:
	return Vector2(x * TILE + TILE / 2.0, (y + 1) * TILE)


func _build_row(y: int) -> void:
	var x := 0
	while x < width:
		var c := _cell(x, y)
		match c:
			"#":
				var run := _run_length(x, y, "#")
				_add_solid_run(x, y, run)
				x += run
			"^":
				var run := _run_length(x, y, "^")
				_add_spike_run(x, y, run)
				x += run
			"P":
				# The hero's origin is at his feet, on the bottom of the cell.
				player_start = _cell_bottom(x, y)
				x += 1
			"w":
				_enemy_spawns.append({"scene": WALKER_SCENE, "position": _cell_bottom(x, y), "character": "imp"})
				x += 1
			"g":
				_enemy_spawns.append({"scene": WALKER_SCENE, "position": _cell_bottom(x, y), "character": "angel_guard"})
				x += 1
			"f":
				_enemy_spawns.append({"scene": FLYER_SCENE, "position": _cell_center(x, y)})
				x += 1
			"C":
				var checkpoint: Checkpoint = CHECKPOINT_SCENE.instantiate()
				checkpoint.position = _cell_center(x, y)
				_objects.add_child(checkpoint)
				checkpoints.append(checkpoint)
				x += 1
			"G":
				goal = GOAL_SCENE.instantiate()
				goal.position = _cell_center(x, y)
				_objects.add_child(goal)
				x += 1
			_:
				x += 1


func _run_length(x: int, y: int, c: String) -> int:
	var run := 0
	while _cell(x + run, y) == c:
		run += 1
	return run


func _add_solid_run(x: int, y: int, run: int) -> void:
	for i in run:
		var tile := TILE_TOP if _cell(x + i, y - 1) != "#" else TILE_FILL
		_add_tile_sprite(x + i, y, tile, _tiles)
	var shape := CollisionShape2D.new()
	var rect := RectangleShape2D.new()
	rect.size = Vector2(run * TILE, TILE)
	shape.shape = rect
	shape.position = Vector2(x * TILE + run * TILE / 2.0, y * TILE + TILE / 2.0)
	_solids.add_child(shape)


func _add_spike_run(x: int, y: int, run: int) -> void:
	var hazard: Hazard = HAZARD_SCENE.instantiate()
	var shape: CollisionShape2D = hazard.get_node("CollisionShape2D")
	var rect := RectangleShape2D.new()
	# A bit narrower than the tiles and only the lower part, so brushing the
	# edge of a pit is forgiven.
	rect.size = Vector2(run * TILE - 4.0, 9.0)
	shape.shape = rect
	hazard.position = Vector2(x * TILE + run * TILE / 2.0, y * TILE + TILE - 4.5)
	_hazards.add_child(hazard)
	for i in run:
		_add_tile_sprite(x + i, y, TILE_SPIKE, hazard)


func _add_tile_sprite(x: int, y: int, tile: int, parent: Node2D) -> void:
	var sprite := Sprite2D.new()
	sprite.texture = TILES_TEXTURE
	sprite.region_enabled = true
	sprite.region_rect = Rect2(tile * TILE, 0, TILE, TILE)
	sprite.position = _cell_center(x, y) - parent.position
	parent.add_child(sprite)


## (Re)creates every enemy from the map. Called at start and after the hero
## respawns, so a checkpoint run always faces the same fight.
func spawn_enemies() -> void:
	for child in _enemies.get_children():
		_enemies.remove_child(child)
		child.queue_free()
	for spawn in _enemy_spawns:
		var enemy: Node2D = spawn["scene"].instantiate()
		enemy.position = spawn["position"]
		if spawn.has("character"):
			enemy.character = spawn["character"]
		_enemies.add_child(enemy)


func enemy_count() -> int:
	return _enemies.get_child_count()
