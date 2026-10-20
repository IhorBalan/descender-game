class_name LevelBuilder
extends Node2D

## Builds a level from an ASCII map (see levels/level_01.txt).
##
##   #  solid tile      ^  spikes           P  hero start
##   w  imp (walker)    g  angel guard      f  watcher (flyer)
##   C  checkpoint      G  goal gate        .  empty
##   W  feather that unlocks the wings (double jump)
##   L  feather that unlocks the light dash    |  light barrier
##   1-9  a story object (NPC, relic or trigger) described in the level's
##        .json sidecar under "placements" (see levels/level_01.json)
##
## Tiles are 16x16. Rows may have different lengths; missing cells are empty.

const TILE := 16
const DEFAULT_TILES_TEXTURE := preload("res://assets/sprites/tiles.png")
const WALKER_SCENE := preload("res://scenes/enemies/walker.tscn")
const FLYER_SCENE := preload("res://scenes/enemies/flyer.tscn")
const CHECKPOINT_SCENE := preload("res://scenes/checkpoint.tscn")
const GOAL_SCENE := preload("res://scenes/goal.tscn")
const HAZARD_SCENE := preload("res://scenes/hazard.tscn")
const PICKUP_SCENE := preload("res://scenes/ability_pickup.tscn")
const BARRIER_TEXTURE := preload("res://assets/sprites/light_barrier.png")


class EnemySpawn:
	var scene: PackedScene
	var position: Vector2
	var character: String

	func _init(p_scene: PackedScene, p_position: Vector2, p_character: String = "") -> void:
		scene = p_scene
		position = p_position
		character = p_character


const TILE_FILL := 0
const TILE_TOP := 1
const TILE_SPIKE := 2

## The tile sheet in use: fill, top and spikes, 16x16 each. Set before
## loading a level to give it its own ground (see Levels.tiles).
var tiles_texture: Texture2D = DEFAULT_TILES_TEXTURE
var rows: PackedStringArray = []
var width := 0
var height := 0
var size_px := Vector2.ZERO
var player_start := Vector2.ZERO
var checkpoints: Array[Checkpoint] = []
var goal: Goal = null
var pickups: Array[AbilityPickup] = []
## NPCs, relics and triggers from the sidecar, in map order.
var interactables: Array[Interactable] = []
## The dialogue file the sidecar names, or "" when the level has none.
var dialogue_path := ""
## Digit -> story object description, from the sidecar.
var placements: Dictionary = {}

var _enemy_spawns: Array[EnemySpawn] = []
var _tiles: Node2D
var _solids: StaticBody2D
var _hazards: Node2D
var _barriers: LightBarrier
var _objects: Node2D
var _enemies: Node2D


## Builds the map at `path`; a .json file next to it, if any, places the
## story objects and names the dialogue file.
func load_from_file(path: String) -> void:
	placements = {}
	dialogue_path = ""
	var sidecar := path.get_basename() + ".json"
	if FileAccess.file_exists(sidecar):
		var parsed: Variant = JSON.parse_string(FileAccess.get_file_as_string(sidecar))
		if parsed is Dictionary:
			dialogue_path = str(parsed.get("dialogue", ""))
			if parsed.get("placements", {}) is Dictionary:
				placements = parsed["placements"]
		else:
			push_error("LevelBuilder: could not read %s" % sidecar)
	build(FileAccess.get_file_as_string(path))


func build(text: String) -> void:
	for child in get_children():
		remove_child(child)
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
	_barriers = LightBarrier.new()
	_barriers.name = "LightBarriers"
	_barriers.collision_layer = LightBarrier.LAYER_BIT
	_barriers.collision_mask = 0
	add_child(_barriers)
	_objects = Node2D.new()
	_objects.name = "Objects"
	add_child(_objects)
	_enemies = Node2D.new()
	_enemies.name = "Enemies"
	add_child(_enemies)

	checkpoints = []
	goal = null
	pickups = []
	interactables = []
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
				_enemy_spawns.append(EnemySpawn.new(WALKER_SCENE, _cell_bottom(x, y), "imp"))
				x += 1
			"g":
				_enemy_spawns.append(
					EnemySpawn.new(WALKER_SCENE, _cell_bottom(x, y), "angel_guard")
				)
				x += 1
			"f":
				_enemy_spawns.append(EnemySpawn.new(FLYER_SCENE, _cell_center(x, y)))
				x += 1
			"C":
				var checkpoint: Checkpoint = CHECKPOINT_SCENE.instantiate()
				checkpoint.position = _cell_bottom(x, y)
				_objects.add_child(checkpoint)
				checkpoints.append(checkpoint)
				x += 1
			"G":
				goal = GOAL_SCENE.instantiate()
				goal.position = _cell_center(x, y)
				_objects.add_child(goal)
				x += 1
			"W":
				_add_pickup(x, y, Player.Ability.WINGS)
				x += 1
			"L":
				_add_pickup(x, y, Player.Ability.LIGHT_DASH)
				x += 1
			"|":
				_add_barrier_cell(x, y)
				x += 1
			"1", "2", "3", "4", "5", "6", "7", "8", "9":
				_add_placement(x, y, c)
				x += 1
			_:
				x += 1


## Places the story object the sidecar describes under `key`. A relic the
## save already holds is left out.
func _add_placement(x: int, y: int, key: String) -> void:
	var spec: Variant = placements.get(key)
	if not spec is Dictionary:
		push_warning("LevelBuilder: no placement for '%s' in the level's .json" % key)
		return
	var node: Interactable
	match str(spec.get("type", "")):
		"npc":
			var npc := Npc.new()
			npc.npc_name = str(spec.get("name", ""))
			npc.character = str(spec.get("character", "hero"))
			npc.faces_left = bool(spec.get("faces_left", false))
			var tint: Variant = spec.get("tint")
			if tint is Array and tint.size() >= 3:
				npc.tint = Color(float(tint[0]), float(tint[1]), float(tint[2]))
			node = npc
		"item":
			var item_id := str(spec.get("item", key))
			if Story.has_flag(LoreItem.flag_for(item_id)):
				return
			var item := LoreItem.new()
			item.item_id = item_id
			node = item
		"trigger":
			node = StoryTrigger.new()
		_:
			push_warning("LevelBuilder: placement '%s' has no known type" % key)
			return
	node.name = "%s%s" % [str(spec.get("type", "")).capitalize(), key]
	node.dialogue_ids = spec.get("dialogue", [])
	node.rotate_ids = spec.get("rotate", [])
	node.position = _cell_bottom(x, y)
	_objects.add_child(node)
	interactables.append(node)


func _add_barrier_cell(x: int, y: int) -> void:
	var shape := CollisionShape2D.new()
	var rect := RectangleShape2D.new()
	rect.size = Vector2(TILE, TILE)
	shape.shape = rect
	shape.position = _cell_center(x, y)
	_barriers.add_child(shape)
	var sprite := Sprite2D.new()
	sprite.texture = BARRIER_TEXTURE
	sprite.position = _cell_center(x, y)
	_barriers.add_child(sprite)


func _add_pickup(x: int, y: int, ability: Player.Ability) -> void:
	var pickup: AbilityPickup = PICKUP_SCENE.instantiate()
	pickup.ability = ability
	pickup.position = _cell_bottom(x, y)
	_objects.add_child(pickup)
	pickups.append(pickup)


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
	sprite.texture = tiles_texture
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
		var enemy: Node2D = spawn.scene.instantiate()
		enemy.position = spawn.position
		enemy.drop_parent = _objects
		if enemy is Walker:
			var walker := enemy as Walker
			walker.character = spawn.character
			walker.death_y = size_px.y + 64.0
		_enemies.add_child(enemy)


## Live enemies: the dying ones still fading out are not counted.
func enemy_count() -> int:
	var count := 0
	for enemy in _enemies.get_children():
		if not enemy.is_queued_for_deletion() and not enemy.get("_dead"):
			count += 1
	return count


## The bounds of every light barrier cell, in level pixels (empty when the
## level has none).
func barrier_rect() -> Rect2:
	var rect := Rect2()
	var first := true
	for child in _barriers.get_children():
		if child is CollisionShape2D:
			var cell := Rect2(child.position - Vector2(TILE, TILE) / 2.0, Vector2(TILE, TILE))
			rect = cell if first else rect.merge(cell)
			first = false
	return rect


## Drops the feathers for abilities the hero already has, for a loaded save.
func remove_pickups(abilities: Array[Player.Ability]) -> void:
	for pickup in pickups.duplicate():
		if pickup.ability in abilities:
			pickups.erase(pickup)
			_objects.remove_child(pickup)
			pickup.queue_free()


## Relics still waiting in the level (examined ones are freed).
func item_count() -> int:
	var count := 0
	for node in interactables:
		if is_instance_valid(node) and not node.is_queued_for_deletion() and node is LoreItem:
			count += 1
	return count


func npc_count() -> int:
	var count := 0
	for node in interactables:
		if node is Npc:
			count += 1
	return count


## Pickups still waiting in the level (collected ones are freed).
func pickup_count() -> int:
	var count := 0
	for pickup in pickups:
		if is_instance_valid(pickup) and not pickup.is_queued_for_deletion():
			count += 1
	return count
