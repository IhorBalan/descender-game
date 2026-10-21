class_name LevelBuilder
extends Node2D

## Builds a level from a world export of the map tool (see WorldMap and
## levels/world/world.json).
##
## The export's layers become TileMapLayers, its terrain kinds become
## collision (solid runs, one-way ledges and hazards), its lights become
## PointLight2Ds and its markers place the hero (start), the checkpoints
## (save), the feathers (ability) and the bosses: a Boss for every boss
## marker with a profile, a placeholder angel guard for the rest. Doors the
## export locks by an ability or a boss get a seal of light (DoorLock).
##
## A placements.json next to the export names the dialogue file and lists
## the rest of what stands in the world under "objects", each with a type
## and the tile cell (x, y) whose bottom its feet are on:
##
##   npc, item, trigger   a story object: name, character, tint, faces_left,
##                        dialogue and rotate, as the dialogue system wants;
##                        unless: a story flag that, once set, leaves it out
##   enemy                character: imp, angel_guard, brute (walkers) or
##                        watcher; barks: a Walker.BARKS set the enemy shouts
##   upgrade              upgrade: heart (Kept Light), vial (Vial of the Basin) or iron
##                        (Church Iron, for Old Pell's forge)
##   power                power: halo, blade or zeal
##   barrier              a wall of angel light from (x, y) down `height` cells
##
## Tiles are 16x16.

const TILE := 16
const WORLD_PLACEMENTS := "placements.json"
## The abilities with gameplay behind them; the other markers stay data.
const LEARNABLE: Array[int] = [
	Player.Ability.WINGS,
	Player.Ability.LIGHT_DASH,
	Player.Ability.WALL_GRIP,
	Player.Ability.HOLY_LIGHT,
	Player.Ability.GLIDE,
	Player.Ability.SMITE,
]
## Boss markers get this enemy until there are bosses.
const BOSS_PLACEHOLDER := "angel_guard"
## How tall the one-way strip at the top of a ledge tile is.
const LEDGE_HEIGHT := 4.0
## Lights are drawn with a radial gradient this many pixels across, scaled
## to each light's radius.
const LIGHT_TEXTURE_SIZE := 128
const WALKER_SCENE := preload("res://scenes/enemies/walker.tscn")
const FLYER_SCENE := preload("res://scenes/enemies/flyer.tscn")
const CHECKPOINT_SCENE := preload("res://scenes/checkpoint.tscn")
const GOAL_SCENE := preload("res://scenes/goal.tscn")
const HAZARD_SCENE := preload("res://scenes/hazard.tscn")
const PICKUP_SCENE := preload("res://scenes/ability_pickup.tscn")
const BOSS_SCENE := preload("res://scenes/enemies/boss.tscn")
## How many cells tall a seal across a sideways door can be.
const MAX_SEAL_HEIGHT := 16
const BARRIER_TEXTURE := preload("res://assets/sprites/light_barrier.png")
const UPGRADE_KINDS := {
	"heart": Player.UpgradeKind.HEART,
	"vial": Player.UpgradeKind.VIAL,
	"iron": Player.UpgradeKind.IRON,
}
const POWER_KINDS := {
	"halo": Player.Power.HALO, "blade": Player.Power.BLADE, "zeal": Player.Power.ZEAL
}


class EnemySpawn:
	var scene: PackedScene
	var position: Vector2
	var character: String
	## For a boss: the room it guards, in level pixels.
	var arena := Rect2()
	## For a walker: the bark set it shouts from (see Walker.BARKS).
	var barks := ""

	func _init(p_scene: PackedScene, p_position: Vector2, p_character: String = "") -> void:
		scene = p_scene
		position = p_position
		character = p_character


var width := 0
var height := 0
var size_px := Vector2.ZERO
var player_start := Vector2.ZERO
## Top to bottom, left to right, so a save's checkpoint index is stable.
var checkpoints: Array[Checkpoint] = []
## The gate that ends the level; the world has none yet.
var goal: Goal = null
var pickups: Array[AbilityPickup] = []
## The seals on the export's locked doors that are still shut.
var locks: Array[DoorLock] = []
## Permanent upgrades still lying in the level (ones the save holds are
## never built).
var upgrades: Array[Upgrade] = []
## Names the upgrade flags: set by main.gd before the level is loaded.
var level_id := ""
## NPCs, relics and triggers from the sidecar, in sidecar order.
var interactables: Array[Interactable] = []
## The dialogue file the sidecar names, or "" when the level has none.
var dialogue_path := ""
## The export the level was built from, or null before a level is loaded.
var world: WorldMap = null
## Tints the whole level (hero included) with the region's ambient colour.
var ambient: CanvasModulate = null

var _enemy_spawns: Array[EnemySpawn] = []
## Where the power-ups lie; rebuilt with the enemies.
var _power_spawns: Array = []
## How many of each upgrade kind the sidecar has placed so far, for the flags.
var _upgrade_counts: Dictionary = {}
## The sidecar's "objects" list.
var _objects_spec: Array = []
var _tiles: Node2D
var _solids: StaticBody2D
## One-way platforms.
var _ledges: StaticBody2D
var _hazards: Node2D
var _barriers: LightBarrier
var _objects: Node2D
var _enemies: Node2D
var _seals: Node2D
var _power_ups: Node2D
var _lights: Node2D


## Builds the world export at `path`, with the objects of the
## placements.json next to it, if any. An unreadable export leaves the
## level empty, with an error pushed.
func load_from_file(path: String) -> void:
	dialogue_path = ""
	_objects_spec = []
	var map := WorldMap.read(path)
	if map == null:
		_make_containers()
		return
	_read_sidecar(map.dir() + WORLD_PLACEMENTS)
	build(map)


## Reads the dialogue file name and the objects from a sidecar, if any.
func _read_sidecar(sidecar: String) -> void:
	if not FileAccess.file_exists(sidecar):
		return
	var parsed: Variant = JSON.parse_string(FileAccess.get_file_as_string(sidecar))
	if parsed is Dictionary:
		dialogue_path = str(parsed.get("dialogue", ""))
		if parsed.get("objects", []) is Array:
			_objects_spec = parsed["objects"]
	else:
		push_error("LevelBuilder: could not read %s" % sidecar)


## Empties the level and makes the nodes everything is built under.
func _make_containers() -> void:
	for child in get_children():
		remove_child(child)
		child.queue_free()
	_tiles = Node2D.new()
	_tiles.name = "Tiles"
	add_child(_tiles)
	_solids = StaticBody2D.new()
	_solids.name = "Solids"
	_solids.collision_layer = 1
	_solids.collision_mask = 0
	add_child(_solids)
	_ledges = StaticBody2D.new()
	_ledges.name = "Ledges"
	_ledges.collision_layer = Player.LEDGE_LAYER_BIT
	_ledges.collision_mask = 0
	add_child(_ledges)
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
	_seals = Node2D.new()
	_seals.name = "Seals"
	add_child(_seals)
	_power_ups = Node2D.new()
	_power_ups.name = "PowerUps"
	add_child(_power_ups)
	_lights = Node2D.new()
	_lights.name = "Lights"
	add_child(_lights)
	ambient = CanvasModulate.new()
	ambient.name = "Ambient"
	ambient.color = Color.WHITE
	add_child(ambient)

	world = null
	width = 0
	height = 0
	size_px = Vector2.ZERO
	player_start = Vector2.ZERO
	checkpoints = []
	goal = null
	pickups = []
	locks = []
	interactables = []
	upgrades = []
	_enemy_spawns = []
	_power_spawns = []
	_upgrade_counts = {}


## Builds a world export: one TileMapLayer per layer, collision merged from
## the terrain kinds, the lights, the markers and the sidecar's objects.
func build(map: WorldMap) -> void:
	_make_containers()
	world = map
	width = map.width
	height = map.height
	size_px = map.size_px()
	_build_layers(map)
	for y in height:
		_build_row(map, y)
	_build_lights(map)
	_place_markers(map)
	_place_locks(map)
	for spec in _objects_spec:
		if spec is Dictionary:
			_place_object(spec)
	# Saves index the checkpoints, so keep them in a stable order.
	checkpoints.sort_custom(_above_or_left)


func _build_layers(map: WorldMap) -> void:
	var tile_set := map.build_tile_set()
	for layer_name in map.layer_names:
		var layer := TileMapLayer.new()
		layer.name = layer_name
		layer.tile_set = tile_set
		layer.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
		layer.collision_enabled = false
		var source := 0
		if layer_name == WorldMap.LAYER_GLOW:
			# Emissive: unlit and added on top of what is under it.
			source = 1
			var material := CanvasItemMaterial.new()
			material.light_mode = CanvasItemMaterial.LIGHT_MODE_UNSHADED
			material.blend_mode = CanvasItemMaterial.BLEND_MODE_ADD
			layer.material = material
		var cells := map.cells(layer_name)
		for i in cells.size():
			var id := cells[i]
			if id >= 0:
				layer.set_cell(Vector2i(i % width, i / width), source, map.atlas_coords(id))
		_tiles.add_child(layer)


## Collision for one row of terrain: runs of solid cells become one
## rectangle each, ledges a one-way strip and hazards a Hazard area. The
## tiles themselves are drawn by the layer.
func _build_row(map: WorldMap, y: int) -> void:
	var x := 0
	while x < width:
		var kind := _collision_kind(map.kind_at(x, y))
		if kind == WorldMap.KIND_NONE:
			x += 1
			continue
		var run := 1
		while x + run < width and _collision_kind(map.kind_at(x + run, y)) == kind:
			run += 1
		match kind:
			WorldMap.KIND_SOLID:
				_add_solid_run(x, y, run)
			WorldMap.KIND_LEDGE:
				_add_ledge_run(x, y, run)
			WorldMap.KIND_HAZARD:
				_add_hazard_run(x, y, run)
		x += run


## Hidden collision is solid for the hero.
func _collision_kind(kind: int) -> int:
	return WorldMap.KIND_SOLID if kind == WorldMap.KIND_HIDDEN else kind


func _add_solid_run(x: int, y: int, run: int) -> void:
	var shape := CollisionShape2D.new()
	var rect := RectangleShape2D.new()
	rect.size = Vector2(run * TILE, TILE)
	shape.shape = rect
	shape.position = Vector2(x * TILE + run * TILE / 2.0, y * TILE + TILE / 2.0)
	_solids.add_child(shape)


func _add_ledge_run(x: int, y: int, run: int) -> void:
	var shape := CollisionShape2D.new()
	var rect := RectangleShape2D.new()
	rect.size = Vector2(run * TILE, LEDGE_HEIGHT)
	shape.shape = rect
	shape.one_way_collision = true
	shape.position = Vector2(x * TILE + run * TILE / 2.0, y * TILE + LEDGE_HEIGHT / 2.0)
	_ledges.add_child(shape)


func _add_hazard_run(x: int, y: int, run: int) -> void:
	var hazard: Hazard = HAZARD_SCENE.instantiate()
	var shape: CollisionShape2D = hazard.get_node("CollisionShape2D")
	var rect := RectangleShape2D.new()
	# A bit narrower than the tiles and only the lower part, so brushing the
	# edge of a pit is forgiven.
	rect.size = Vector2(run * TILE - 4.0, 9.0)
	shape.shape = rect
	hazard.position = Vector2(x * TILE + run * TILE / 2.0, y * TILE + TILE - 4.5)
	_hazards.add_child(hazard)


func _build_lights(map: WorldMap) -> void:
	if map.lights.is_empty():
		return
	var texture := _light_texture()
	for spec in map.lights:
		var light := PointLight2D.new()
		light.texture = texture
		light.position = Vector2(float(spec.get("x", 0)), float(spec.get("y", 0)))
		light.height = float(spec.get("z", 0))
		light.color = Color.from_string(str(spec.get("color", "#ffffff")), Color.WHITE)
		light.energy = float(spec.get("energy", 1.0))
		light.texture_scale = float(spec.get("radius", 64)) * 2.0 / LIGHT_TEXTURE_SIZE
		# Shadows would need occluders on every solid tile; too dear for a
		# world this size, so the light passes through walls for now.
		light.shadow_enabled = false
		_lights.add_child(light)


## A white disc fading to nothing at the edge, shared by every light.
func _light_texture() -> ImageTexture:
	var image := Image.create(LIGHT_TEXTURE_SIZE, LIGHT_TEXTURE_SIZE, false, Image.FORMAT_RGBA8)
	var centre := Vector2(LIGHT_TEXTURE_SIZE / 2.0, LIGHT_TEXTURE_SIZE / 2.0)
	for y in LIGHT_TEXTURE_SIZE:
		for x in LIGHT_TEXTURE_SIZE:
			var distance := Vector2(x + 0.5, y + 0.5).distance_to(centre) / centre.x
			image.set_pixel(x, y, Color(1.0, 1.0, 1.0, clampf(1.0 - distance, 0.0, 1.0)))
	return ImageTexture.create_from_image(image)


## The export's markers: start, save (a checkpoint), ability (a feather,
## for the abilities the hero can learn) and boss (a placeholder enemy).
## Secrets, shops, gates and the clock are data only until there is
## something for them to do.
func _place_markers(map: WorldMap) -> void:
	for marker in map.markers:
		var at := map.marker_position(marker)
		match str(marker.get("type", "")):
			"start":
				player_start = at
			"save":
				var checkpoint: Checkpoint = CHECKPOINT_SCENE.instantiate()
				checkpoint.position = at
				_objects.add_child(checkpoint)
				checkpoints.append(checkpoint)
			"ability":
				var ability := _ability_for(map.abilities.find(str(marker.get("label", ""))))
				if ability >= 0:
					_add_pickup(at, ability)
			"boss":
				_place_boss(map, marker, at)


## A boss marker: a Boss with the room as its arena when its label has a
## profile and the save does not already hold its death, else the
## placeholder enemy.
func _place_boss(map: WorldMap, marker: Dictionary, at: Vector2) -> void:
	var id := Boss.id_for(str(marker.get("label", "")))
	if not Boss.has_profile(id):
		_enemy_spawns.append(EnemySpawn.new(WALKER_SCENE, at, BOSS_PLACEHOLDER))
		return
	if Story.has_flag(Boss.flag_for(id)):
		return
	var spawn := EnemySpawn.new(BOSS_SCENE, at, id)
	spawn.arena = room_rect(map, int(marker.get("room", -1)))
	_enemy_spawns.append(spawn)


## The rect of a room of the export in level pixels, or an empty rect.
func room_rect(map: WorldMap, room_id: int) -> Rect2:
	for room in map.rooms:
		if int(room.get("id", -1)) == room_id:
			var rect: Array = room.get("rect", [])
			if rect.size() == 4:
				return Rect2(float(rect[0]), float(rect[1]), float(rect[2]), float(rect[3]))
	return Rect2()


## A seal of light on every door the export locks, unless the hero may
## already pass it. Secret passages have no place in the grid and get none.
func _place_locks(map: WorldMap) -> void:
	for door in map.doors:
		var lock: Variant = door.get("lock")
		if not lock is Dictionary or bool(door.get("secret", false)):
			continue
		var seal := DoorLock.new()
		seal.door_id = int(door.get("id", 0))
		match str(lock.get("kind", "")):
			"ability":
				var index := int(lock.get("ability", -1))
				seal.kind = DoorLock.Kind.ABILITY
				seal.ability = _ability_for(index)
				seal.ability_name = _ability_name(map, index)
			"boss":
				var boss_id := _boss_in_region(map, int(lock.get("region", -1)))
				seal.kind = DoorLock.Kind.BOSS
				seal.boss_flag = Boss.flag_for(boss_id)
				seal.boss_name = boss_id.capitalize()
				if Story.has_flag(seal.boss_flag):
					continue
			_:
				continue
		seal.cells = _door_cells(map, door)
		if seal.cells.is_empty():
			continue
		seal.name = "Seal%d" % seal.door_id
		_seals.add_child(seal)
		locks.append(seal)


## The cells a seal across a door covers. A door down (dir "v") is a gap
## in a ledge four cells wide; the seal is the row of air just under the
## ledge. A door sideways (dir "h") is an opening in the air at the room's
## edge; the seal is the column from the door's floor up to the ceiling.
func _door_cells(map: WorldMap, door: Dictionary) -> Array[Vector2i]:
	var cells: Array[Vector2i] = []
	var x := int(door.get("x", 0))
	var y := int(door.get("y", 0))
	if str(door.get("dir", "")) == "v":
		var row := y
		while row > 0 and _row_is_air(map, x - 2, row - 1, 4):
			row -= 1
		for i in 4:
			cells.append(Vector2i(x - 2 + i, row))
	else:
		var row := y
		while row >= 0 and map.kind_at(x, row) == WorldMap.KIND_NONE:
			cells.append(Vector2i(x, row))
			row -= 1
			if cells.size() >= MAX_SEAL_HEIGHT:
				break
	return cells


func _row_is_air(map: WorldMap, x: int, y: int, count: int) -> bool:
	for i in count:
		if map.kind_at(x + i, y) != WorldMap.KIND_NONE:
			return false
	return true


## The map's name for an ability, without the note in brackets.
func _ability_name(map: WorldMap, index: int) -> String:
	if index < 0 or index >= map.abilities.size():
		return "unknown ability"
	return map.abilities[index].get_slice("(", 0).strip_edges()


## The id of the boss whose room is in the region (see Boss.id_for), or
## "" when the region has none.
func _boss_in_region(map: WorldMap, region: int) -> String:
	for marker in map.markers_of("boss"):
		var rect_room := int(marker.get("room", -1))
		for room in map.rooms:
			if int(room.get("id", -1)) == rect_room and int(room.get("region", -1)) == region:
				return Boss.id_for(str(marker.get("label", "")))
	return ""


## Breaks every seal the hero may now pass: called when he learns an
## ability or kills a boss, and quietly once a save has been applied.
func open_locks(player: Player, quiet := false) -> void:
	for seal in locks.duplicate():
		if not is_instance_valid(seal):
			locks.erase(seal)
		elif seal.satisfied(player):
			locks.erase(seal)
			seal.open(quiet)


## Seals every door of a room for a boss fight. Returns the seals, which
## `unseal` breaks when the fight is over.
func seal_room(room_id: int) -> Array[DoorLock]:
	var seals: Array[DoorLock] = []
	if world == null:
		return seals
	for door in world.doors:
		if int(door.get("a", -1)) != room_id and int(door.get("b", -1)) != room_id:
			continue
		if bool(door.get("secret", false)):
			continue
		var cells := _door_cells(world, door)
		if cells.is_empty() or _sealed(cells[0]):
			continue
		var seal := DoorLock.new()
		seal.kind = DoorLock.Kind.ARENA
		seal.door_id = int(door.get("id", 0))
		seal.cells = cells
		seal.name = "Arena%d" % seal.door_id
		_seals.add_child(seal)
		seals.append(seal)
	return seals


func unseal(seals: Array[DoorLock]) -> void:
	for seal in seals:
		if is_instance_valid(seal):
			seal.open()


## Whether a lock already covers the cell.
func _sealed(cell: Vector2i) -> bool:
	for seal in locks:
		if is_instance_valid(seal) and cell in seal.cells:
			return true
	return false


## The id of the room the pixel position falls in, or -1.
func room_at(position: Vector2) -> int:
	if world == null:
		return -1
	for room in world.rooms:
		var rect: Array = room.get("rect", [])
		if rect.size() == 4:
			var r := Rect2(float(rect[0]), float(rect[1]), float(rect[2]), float(rect[3]))
			if r.has_point(position):
				return int(room.get("id", -1))
	return -1


## The hero's ability for an index into the export's ability list (the
## Player.Ability enum follows the tool's order), or -1 for one he cannot
## learn yet.
func _ability_for(index: int) -> int:
	if index < 0 or index >= Player.Ability.size():
		return -1
	if index in LEARNABLE:
		return index
	return -1


## One entry of the sidecar's "objects" (see the top of the file).
func _place_object(spec: Dictionary) -> void:
	var x := int(spec.get("x", 0))
	var y := int(spec.get("y", 0))
	var at := Vector2(x * TILE + TILE / 2.0, (y + 1) * TILE)
	var type := str(spec.get("type", ""))
	match type:
		"npc", "item", "trigger":
			_add_placement(at, spec)
		"enemy":
			var character := str(spec.get("character", "imp"))
			if character == "watcher":
				_enemy_spawns.append(EnemySpawn.new(FLYER_SCENE, at - Vector2(0.0, TILE / 2.0)))
			else:
				var spawn := EnemySpawn.new(WALKER_SCENE, at, character)
				spawn.barks = str(spec.get("barks", ""))
				_enemy_spawns.append(spawn)
		"upgrade":
			var kind := str(spec.get("upgrade", ""))
			if UPGRADE_KINDS.has(kind):
				_add_upgrade(at, UPGRADE_KINDS[kind])
			else:
				push_warning("LevelBuilder: no upgrade called '%s'" % kind)
		"power":
			var kind := str(spec.get("power", ""))
			if POWER_KINDS.has(kind):
				_power_spawns.append([POWER_KINDS[kind], at])
			else:
				push_warning("LevelBuilder: no power-up called '%s'" % kind)
		"barrier":
			for i in maxi(int(spec.get("height", 1)), 1):
				_add_barrier_cell(x, y + i)
		_:
			push_warning("LevelBuilder: object type '%s' is not known" % type)


## Places a story object. A relic the save already holds is left out, and
## so is anything whose `unless` flag is set (someone who has died).
func _add_placement(at: Vector2, spec: Dictionary) -> void:
	var node: Interactable
	var type := str(spec.get("type", ""))
	if Story.has_flag(str(spec.get("unless", ""))):
		return
	match type:
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
			var item_id := str(spec.get("item", ""))
			if Story.has_flag(LoreItem.flag_for(item_id)):
				return
			var item := LoreItem.new()
			item.item_id = item_id
			node = item
		"trigger":
			node = StoryTrigger.new()
		_:
			return
	node.name = "%s%d" % [type.capitalize(), interactables.size() + 1]
	node.dialogue_ids = spec.get("dialogue", [])
	node.rotate_ids = spec.get("rotate", [])
	node.position = at
	_objects.add_child(node)
	interactables.append(node)


## Places a permanent upgrade, unless the save already holds it. Its flag
## numbers the upgrades of that kind in sidecar order, taken ones included,
## so an upgrade keeps its identity as long as that order does not change.
func _add_upgrade(at: Vector2, kind: Player.UpgradeKind) -> void:
	var index: int = _upgrade_counts.get(kind, 0)
	_upgrade_counts[kind] = index + 1
	var flag := Upgrade.flag_for(kind, level_id, index)
	if Story.has_flag(flag):
		return
	var upgrade := Upgrade.new()
	upgrade.kind = kind
	upgrade.flag = flag
	upgrade.name = "Upgrade%s%d" % [Upgrade.FLAG_NAMES[kind].capitalize(), index]
	upgrade.position = at
	_objects.add_child(upgrade)
	upgrades.append(upgrade)


func _add_barrier_cell(x: int, y: int) -> void:
	var centre := Vector2(x * TILE + TILE / 2.0, y * TILE + TILE / 2.0)
	var shape := CollisionShape2D.new()
	var rect := RectangleShape2D.new()
	rect.size = Vector2(TILE, TILE)
	shape.shape = rect
	shape.position = centre
	_barriers.add_child(shape)
	var sprite := Sprite2D.new()
	sprite.texture = BARRIER_TEXTURE
	sprite.position = centre
	_barriers.add_child(sprite)


func _add_pickup(at: Vector2, ability: Player.Ability) -> void:
	var pickup: AbilityPickup = PICKUP_SCENE.instantiate()
	pickup.ability = ability
	pickup.position = at
	_objects.add_child(pickup)
	pickups.append(pickup)


func _above_or_left(a: Node2D, b: Node2D) -> bool:
	if a.position.y != b.position.y:
		return a.position.y < b.position.y
	return a.position.x < b.position.x


## The region of the export around the pixel row `y`.
func region_at(y: float) -> Dictionary:
	return world.region_at(y) if world != null else {}


## Tints the level with a region's ambient colour.
func set_ambient(color: Color) -> void:
	if ambient != null:
		ambient.color = color


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
			walker.barks = spawn.barks
			walker.death_y = size_px.y + 64.0
		elif enemy is Boss:
			var boss := enemy as Boss
			boss.boss_id = spawn.character
			boss.arena = spawn.arena
		_enemies.add_child(enemy)


## (Re)creates every power-up from the map, alongside the enemies, so a
## checkpoint run has the same tools for the same fight.
func spawn_power_ups() -> void:
	for child in _power_ups.get_children():
		_power_ups.remove_child(child)
		child.queue_free()
	for spawn in _power_spawns:
		var power_up := PowerUp.new()
		power_up.power = spawn[0]
		power_up.position = spawn[1]
		_power_ups.add_child(power_up)


## Power-ups still lying in the level (taken ones are freed).
func power_up_count() -> int:
	var count := 0
	for power_up in _power_ups.get_children():
		if not power_up.is_queued_for_deletion():
			count += 1
	return count


## Permanent upgrades still lying in the level (taken ones are freed, ones
## the save holds were never built).
func upgrade_count() -> int:
	var count := 0
	for upgrade in upgrades:
		if is_instance_valid(upgrade) and not upgrade.is_queued_for_deletion():
			count += 1
	return count


## The bosses standing in the level, dead ones included until they fade.
func bosses() -> Array[Boss]:
	var found: Array[Boss] = []
	for enemy in _enemies.get_children():
		if enemy is Boss:
			found.append(enemy)
	return found


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
