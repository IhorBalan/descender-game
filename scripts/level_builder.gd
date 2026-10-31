class_name LevelBuilder
extends Node2D

## Builds a level from a world export of the map tool (see WorldMap and
## scenes/world/world.tscn, or a world.json the editor exported).
##
## The export's layers become TileMapLayers, its terrain kinds become
## collision (solid runs, one-way ledges and hazards) and its lights become
## PointLight2Ds. Doors the export locks by an ability or a boss get a seal
## of light (DoorLock).
##
## Everything that stands in the world is a placement: the world scene's
## WorldPlacements, or, for an export, the placements its markers stand for
## (see WorldMap.placement_of_marker) and the "objects" of the
## placements.json next to it, which also names the dialogue file. Each
## has a type and the tile cell (x, y) whose bottom its feet are on:
##
##   start                where a new game puts the hero
##   save                 a checkpoint
##   ability              ability: the name of the feather's ability as the
##                        world's abilities list has it (the Player.Ability
##                        enum follows that order), for the abilities the
##                        hero can learn (LEARNABLE)
##   npc, item, trigger   a story object: name, character, tint, faces_left,
##                        dialogue and rotate, as the dialogue system wants;
##                        unless: a story flag that, once set, leaves it out;
##                        shop: a Shops id for an NPC that trades once its
##                        unique lines are spent
##   enemy                character: a walker body (Walker.CHARACTERS: imp,
##                        angel_guard, brute, hound, slug, hoarder, burned), a
##                        flyer body (Flyer.CHARACTERS: watcher, shade, siren),
##                        hanged (a Hanger: it hangs with its feet at the cell
##                        and drops on the hero), or a boss id (Boss.IDS) for a
##                        boss in its room; each body is a scene in
##                        scenes/enemies/ (see enemy_scene); which bodies stand
##                        in which region is in docs/design/enemies.md;
##                        barks: a Walker.BARKS set the enemy shouts; passive:
##                        true for a walker that never attacks; behaviour: a
##                        Walker.BEHAVIOURS habit (chase, ambush, pounce,
##                        hurl, slam, lunge) or "dive" for a flyer; requires:
##                        a story flag the enemy waits for, read at every spawn
##   upgrade              upgrade: heart (Kept Light), vial (Vial of the Basin) or iron
##                        (Church Iron, for Old Pell's forge)
##   power                power: halo, blade or zeal
##   barrier              a wall of angel light from (x, y) down `height` cells
##
## Tiles are 16x16.
##
## The tiles, their collision and the edge art are streamed: the grid is cut
## into chunks of CHUNK_TILES cells a side and only the chunks near the hero
## (and the camera) are built, see stream_around. Everything with gameplay
## state (the placements, the seals, the lights) is placed for the whole
## world at once, as before.

## The sky's rim light on the tiles: 0 turns it off.
const RIM_STRENGTH := 0.25
const RIM_SHADER := preload("res://godot_assets/shaders/rim.gdshader")
const TILE := 16
## How far from the hero or the camera an enemy keeps moving, in level
## pixels: the view (640 x 360) plus a wide margin, so nothing freezes in
## sight and enemies just off screen still walk in on their own.
const ACTIVE_RANGE := Vector2(800.0, 480.0)
const WORLD_PLACEMENTS := "placements.json"
## Streaming: the grid is built in square chunks this many tiles a side.
const CHUNK_TILES := 64
## Half the width and height of the box around each streamed point (the
## hero, the camera) whose chunks are built: the view (640 x 360) plus a
## screen and a half of margin, so nothing is seen being built.
const STREAM_IN := Vector2(960.0, 540.0)
## Chunks outside this box around every point are torn down. Wider than
## STREAM_IN, so a hero pacing a chunk's edge does not thrash it.
const STREAM_OUT := Vector2(1600.0, 960.0)
## How many cells of solid a secret passage may run through on each side
## of its door.
const SECRET_DEPTH := 12
## The abilities with gameplay behind them; the other feathers stay data.
const LEARNABLE: Array[int] = [
	Player.Ability.WINGS,
	Player.Ability.LIGHT_DASH,
	Player.Ability.WALL_GRIP,
	Player.Ability.HOLY_LIGHT,
	Player.Ability.GLIDE,
	Player.Ability.SMITE,
]
## How tall the one-way strip at the top of a ledge tile is.
const LEDGE_HEIGHT := 4.0
## Solid sprites (see _add_sprite_collision): strands of art thinner than
## this many level pixels carry no collision, and the traced outline may
## stray this far from the pixels.
const SPRITE_STRAND_PX := 6.0
const SPRITE_TRACE_PX := 1.0
## Pixels per level pixel the tracing works at.
const SPRITE_TRACE_SCALE := 2
## The traced outline is smoothed so it strays at most this many level
## pixels from the trace: a painted top that is nearly flat comes out of the
## tracing as a staircase of half-pixel steps, and every step would stop the
## hero like a wall. Within this, steps become gentle slopes.
const SPRITE_SMOOTH_PX := 1.5
## Lights are drawn with a radial gradient this many pixels across, scaled
## to each light's radius.
const LIGHT_TEXTURE_SIZE := 128
## The enemy bodies' scenes: scenes/enemies/<character>.tscn (see enemy_scene).
const ENEMY_SCENES_DIR := "res://scenes/enemies"
const HANGER_SCENE := preload("res://scenes/enemies/hanger.tscn")
const CHECKPOINT_SCENE := preload("res://scenes/checkpoint.tscn")
const GOAL_SCENE := preload("res://scenes/goal.tscn")
const HAZARD_SCENE := preload("res://scenes/hazard.tscn")
const PICKUP_SCENE := preload("res://scenes/ability_pickup.tscn")
const BOSS_SCENE := preload("res://scenes/enemies/boss.tscn")
## How many cells tall a seal across a sideways door can be.
const MAX_SEAL_HEIGHT := 16
const BARRIER_TEXTURE := preload("res://godot_assets/sprites/light_barrier.png")
const UPGRADE_KINDS := {
	"heart": Player.UpgradeKind.HEART,
	"vial": Player.UpgradeKind.VIAL,
	"iron": Player.UpgradeKind.IRON,
}
const POWER_KINDS := {
	"halo": Player.Power.HALO, "blade": Player.Power.BLADE, "zeal": Player.Power.ZEAL
}


## One streamed chunk of the grid: the cells it covers and the nodes that
## go with it, so it can be torn down again.
class Chunk:
	var cells: Rect2i
	var nodes: Array[Node] = []

	func _init(p_cells: Rect2i) -> void:
		cells = p_cells


class EnemySpawn:
	var scene: PackedScene
	var position: Vector2
	var character: String
	## For a boss: the room it guards, in level pixels.
	var arena := Rect2()
	## For a walker: the bark set it shouts from (see Walker.BARKS).
	var barks := ""
	## For a walker: never attacks (the resting army of the last camp).
	var passive := false
	## A walker's habit (see Walker.BEHAVIOURS) or a watcher's "dive".
	var behaviour := ""
	## A story flag the enemy waits for, checked whenever the enemies spawn.
	var requires := ""

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
## The rock over the secret passages the hero has not broken yet.
var secrets: Array[SecretWall] = []
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
## The rim light the sky throws on the lit tile layers (see
## godot_assets/shaders/rim.gdshader); null until the layers are built.
var rim: ShaderMaterial = null
## Whether the grid is streamed in chunks around stream_around's points
## (the game) or built whole by build() (tests and tools; see load_all).
var streaming := true
## The diffuse sheet as an image, fetched once per build for tracing solid
## sprites: get_image() copies the whole atlas back from the GPU, over
## 100 MB at 128-px art, so doing it per sprite would stall the start.
var _sheet_image: Image = null
## Chunk key (cell / CHUNK_TILES) -> Chunk, for the chunks built right now.
var _chunks: Dictionary = {}
## Terrain cells erased for good (opened secret passages): a chunk built
## again after an unload leaves them empty.
var _erased: Dictionary = {}
## The tile layers by name, once built.
var _layers: Dictionary = {}

var _enemy_spawns: Array[EnemySpawn] = []
## Where the power-ups lie; rebuilt with the enemies.
var _power_spawns: Array = []
## How many of each upgrade kind the sidecar has placed so far, for the flags.
var _upgrade_counts: Dictionary = {}
## Cells of the secret passages, carved out of the terrain's collision.
var _carved: Dictionary = {}
## Each secret door's id and the cells of its passage (see _find_secrets).
var _secret_specs: Array = []
var _tiles: Node2D
## Parallax sprites (see ParallaxSprite): those that lag behind the camera
## under the tile layers, those that run ahead in front of the hero.
var _parallax_back: Node2D
var _parallax_front: Node2D
## The pieces along the terrain's exposed edges (see EdgeDecor).
var _edges: EdgeDecor
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


## The scene of a walker or flyer body: scenes/enemies/<character>.tscn,
## which inherits walker.tscn or flyer.tscn and wears the body's profile
## and frames. Null for a name that has no scene.
static func enemy_scene(character: String) -> PackedScene:
	var path := "%s/%s.tscn" % [ENEMY_SCENES_DIR, character]
	if not ResourceLoader.exists(path):
		return null
	return load(path) as PackedScene


## Builds the world at `path`: a world export (world.json) with the
## placements its markers stand for and the objects of the placements.json
## next to it, if any, or a world scene (world.tscn, see SceneWorld) with
## the placements in its regions. An unreadable world leaves the level
## empty, with an error pushed.
func load_from_file(path: String) -> void:
	dialogue_path = ""
	var map: WorldMap
	if path.ends_with(".tscn"):
		map = SceneWorld.read(path)
		if map != null:
			dialogue_path = map.dialogue_path
	else:
		map = WorldMap.read(path)
		if map != null:
			_read_sidecar(map, map.dir() + WORLD_PLACEMENTS)
	if map == null:
		_make_containers()
		return
	build(map)


## Reads the dialogue file name and the objects from a sidecar, if any,
## the objects after the map's own placements.
func _read_sidecar(map: WorldMap, sidecar: String) -> void:
	if not FileAccess.file_exists(sidecar):
		return
	var parsed: Variant = JSON.parse_string(FileAccess.get_file_as_string(sidecar))
	if parsed is Dictionary:
		dialogue_path = str(parsed.get("dialogue", ""))
		if parsed.get("objects", []) is Array:
			map.objects.append_array(parsed["objects"])
	else:
		push_error("LevelBuilder: could not read %s" % sidecar)


## Empties the level and makes the nodes everything is built under.
func _make_containers() -> void:
	for child in get_children():
		remove_child(child)
		child.queue_free()
	_parallax_back = Node2D.new()
	_parallax_back.name = "ParallaxBack"
	add_child(_parallax_back)
	_tiles = Node2D.new()
	_tiles.name = "Tiles"
	add_child(_tiles)
	_parallax_front = Node2D.new()
	_parallax_front.name = "ParallaxFront"
	_parallax_front.z_index = 4
	add_child(_parallax_front)
	_edges = EdgeDecor.new()
	_edges.name = "Edges"
	add_child(_edges)
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
	secrets = []
	_carved = {}
	_secret_specs = []
	interactables = []
	upgrades = []
	_enemy_spawns = []
	_power_spawns = []
	_upgrade_counts = {}
	_chunks = {}
	_erased = {}
	_layers = {}


## Builds a world: one TileMapLayer per layer, collision merged from the
## terrain kinds, the lights, the seals and the placements.
func build(map: WorldMap) -> void:
	_make_containers()
	world = map
	width = map.width
	height = map.height
	size_px = map.size_px()
	var tile_set := _build_layers(map)
	_build_sprites(map, tile_set)
	_build_props(map)
	_edges.prepare(map)
	_find_secrets(map)
	if not streaming:
		load_all()
	_build_lights(map)
	_place_locks(map)
	_place_secrets()
	for spec in map.objects:
		if spec is Dictionary:
			_place_object(spec)
	# Saves index the checkpoints, so keep them in a stable order.
	checkpoints.sort_custom(_above_or_left)


func _build_layers(map: WorldMap) -> TileSet:
	var tile_set := map.build_tile_set()
	for layer_name in map.layer_names:
		var layer := TileMapLayer.new()
		layer.name = layer_name
		layer.tile_set = tile_set
		# Nearest up to 64-pixel art, which the window's whole-number scaling
		# lands pixel for pixel; finer art is sampled linear (see WorldMap).
		layer.texture_filter = map.texture_filter()
		layer.scale = Vector2.ONE * map.art_scale()
		layer.collision_enabled = false
		var source := 0
		if layer_name != WorldMap.LAYER_GLOW:
			# Lit layers: the sky's rim light on top of Godot's lighting.
			if rim == null:
				rim = ShaderMaterial.new()
				rim.shader = RIM_SHADER
				rim.set_shader_parameter("strength", RIM_STRENGTH)
			layer.material = rim
		else:
			# Emissive: unlit and added on top of what is under it.
			source = 1
			var material := CanvasItemMaterial.new()
			material.light_mode = CanvasItemMaterial.LIGHT_MODE_UNSHADED
			material.blend_mode = CanvasItemMaterial.BLEND_MODE_ADD
			layer.material = material
		_tiles.add_child(layer)
		_layers[layer_name] = layer
	return tile_set


# --- Streaming -----------------------------------------------------------


## Builds every chunk of the grid at once: the whole world, as the tests
## and the editor preview want it. Turns streaming off.
func load_all() -> void:
	streaming = false
	if world == null:
		return
	for key in _chunk_keys(Rect2i(0, 0, width, height)):
		_load_chunk(key)


## Builds the chunks within STREAM_IN of any of the points (the hero and the
## camera's centre, in level pixels) and tears down those outside STREAM_OUT
## of all of them. Called every physics tick by Main, and whenever the hero
## is put somewhere else (a checkpoint, fast travel), so he always lands on
## built ground: Main runs before its children, so the chunks under him are
## there before his step.
func stream_around(points: Array[Vector2]) -> void:
	if world == null or not streaming:
		return
	var wanted := {}
	for point in points:
		for key in _chunk_keys(_cells_around(point, STREAM_IN)):
			wanted[key] = true
	for key in _chunks.keys():
		var keep := false
		for point in points:
			if _cells_around(point, STREAM_OUT).intersects(_chunks[key].cells):
				keep = true
				break
		if not keep:
			_unload_chunk(key)
	for key in wanted:
		if not _chunks.has(key):
			_load_chunk(key)


## The cells of the chunks built right now.
func loaded_chunks() -> Array[Rect2i]:
	var rects: Array[Rect2i] = []
	for key in _chunks:
		rects.append(_chunks[key].cells)
	return rects


## Whether the cell's chunk is built.
func is_loaded(cell: Vector2i) -> bool:
	return _chunks.has(_chunk_key(cell))


## The grid cells within `half` pixels of the point, clamped to the grid.
func _cells_around(point: Vector2, half: Vector2) -> Rect2i:
	var top_left := Vector2i(((point - half) / TILE).floor())
	var bottom_right := Vector2i(((point + half) / TILE).ceil())
	return Rect2i(top_left, bottom_right - top_left).intersection(Rect2i(0, 0, width, height))


func _chunk_key(cell: Vector2i) -> Vector2i:
	return Vector2i(cell.x / CHUNK_TILES, cell.y / CHUNK_TILES)


## The keys of every chunk the cell rect touches.
func _chunk_keys(cells: Rect2i) -> Array[Vector2i]:
	var keys: Array[Vector2i] = []
	if not cells.has_area():
		return keys
	var first := _chunk_key(cells.position)
	var last := _chunk_key(cells.end - Vector2i.ONE)
	for cy in range(first.y, last.y + 1):
		for cx in range(first.x, last.x + 1):
			keys.append(Vector2i(cx, cy))
	return keys


## The cells of a chunk, clipped to the grid.
func _chunk_cells(key: Vector2i) -> Rect2i:
	var rect := Rect2i(key * CHUNK_TILES, Vector2i.ONE * CHUNK_TILES)
	return rect.intersection(Rect2i(0, 0, width, height))


## Builds one chunk: its cells on every tile layer, the collision of its
## terrain rows (runs are cut at the chunk's sides) and its edge art.
func _load_chunk(key: Vector2i) -> void:
	if _chunks.has(key) or world == null:
		return
	var chunk := Chunk.new(_chunk_cells(key))
	_chunks[key] = chunk
	var cells := chunk.cells
	for layer_name in world.layer_names:
		var layer: TileMapLayer = _layers.get(layer_name)
		if layer == null:
			continue
		var glow := layer_name == WorldMap.LAYER_GLOW
		var index := world.tile_index()
		var ids := world.cells(layer_name)
		var terrain := layer_name == WorldMap.LAYER_TERRAIN
		for y in range(cells.position.y, cells.end.y):
			var row := y * width
			for x in range(cells.position.x, cells.end.x):
				var id := ids[row + x]
				if id >= 0 and not (terrain and _erased.has(Vector2i(x, y))):
					var at := index.locate(id, glow)
					layer.set_cell(Vector2i(x, y), at[0], at[1])
	for y in range(cells.position.y, cells.end.y):
		_build_row(world, y, cells.position.x, cells.end.x, chunk.nodes)
	chunk.nodes.append_array(_edges.build_cells(world, cells))


## Tears a chunk down: its cells cleared, its collision and edge art freed.
func _unload_chunk(key: Vector2i) -> void:
	var chunk: Chunk = _chunks.get(key)
	if chunk == null:
		return
	_chunks.erase(key)
	for layer_name in _layers:
		var layer: TileMapLayer = _layers[layer_name]
		for y in range(chunk.cells.position.y, chunk.cells.end.y):
			for x in range(chunk.cells.position.x, chunk.cells.end.x):
				layer.erase_cell(Vector2i(x, y))
	for node in chunk.nodes:
		if is_instance_valid(node):
			node.get_parent().remove_child(node)
			node.queue_free()


## The export's free-placed decorations: each is a small TileMapLayer at a
## pixel position, drawn over the tile layers (a lit one also gets a glow
## layer) at its `scale` from its top-left corner, and a solid one collides
## at that scale too. A sprite with `parallax` goes under a ParallaxSprite
## instead, behind the level or in front of it, and does not collide.
func _build_sprites(map: WorldMap, tile_set: TileSet) -> void:
	for spec in map.sprites:
		var columns := int(spec.get("columns", 1))
		var rows := int(spec.get("rows", 1))
		var tiles := PackedInt32Array(spec.get("tiles", []))
		if columns <= 0 or tiles.size() != columns * rows:
			push_warning(
				(
					"LevelBuilder: sprite %s skipped, its tiles do not fill %d x %d"
					% [spec.get("name", "?"), columns, rows]
				)
			)
			continue
		var at := Vector2(float(spec.get("x", 0)), float(spec.get("y", 0)))
		var flip: bool = spec.get("flip", false)
		var copy_scale := float(spec.get("scale", 1.0))
		# A parallax sprite's layers sit at the origin of its ParallaxSprite,
		# which places them; it never collides.
		var parent: Node2D = _tiles
		var parallax: Variant = spec.get("parallax")
		if parallax is Dictionary:
			var art_size := Vector2(columns, rows) * TILE * copy_scale
			var holder := ParallaxSprite.from_spec(parallax, at, art_size)
			holder.name = "Sprite%dParallax" % int(spec.get("id", 0))
			(_parallax_front if holder.in_front() else _parallax_back).add_child(holder)
			parent = holder
			at = Vector2.ZERO
		var sources: Array[int] = [0]
		if spec.get("glow", false):
			sources.append(1)
		for source in sources:
			var layer := TileMapLayer.new()
			layer.name = "Sprite%d%s" % [int(spec.get("id", 0)), "Glow" if source == 1 else ""]
			layer.tile_set = tile_set
			layer.texture_filter = map.texture_filter()
			layer.position = at
			layer.scale = Vector2.ONE * map.art_scale() * copy_scale
			layer.collision_enabled = false
			if source == 1:
				var material := CanvasItemMaterial.new()
				material.light_mode = CanvasItemMaterial.LIGHT_MODE_UNSHADED
				material.blend_mode = CanvasItemMaterial.BLEND_MODE_ADD
				layer.material = material
			for i in tiles.size():
				var id := tiles[i]
				if id < 0:
					continue
				var cx := i % columns
				if flip:
					cx = columns - 1 - cx
				var alternative := TileSetAtlasSource.TRANSFORM_FLIP_H if flip else 0
				layer.set_cell(Vector2i(cx, i / columns), source, map.atlas_coords(id), alternative)
			parent.add_child(layer)
		if spec.get("solid", false) and parent == _tiles:
			_add_sprite_collision(spec, at, columns, rows, flip, tiles, map, tile_set, copy_scale)
	_sheet_image = null


## The pieces of the set placed as scenes (see WorldProp): each instanced
## where it was put, over the tile layers, its art lit and rimmed like them.
## A prop whose scene is missing is skipped with a warning.
func _build_props(map: WorldMap) -> void:
	if map.props.is_empty():
		return
	var holder := Node2D.new()
	holder.name = "Props"
	_tiles.add_child(holder)
	for spec in map.props:
		var path := str(spec.get("scene", ""))
		var packed := load(path) as PackedScene if ResourceLoader.exists(path) else null
		var prop := packed.instantiate() as WorldProp if packed else null
		if prop == null:
			push_warning("LevelBuilder: prop %s skipped, it is not a WorldProp scene" % path)
			continue
		prop.position = Vector2(float(spec.get("x", 0)), float(spec.get("y", 0)))
		var prop_scale: Variant = spec.get("scale")
		if prop_scale is Array and prop_scale.size() >= 2:
			prop.scale = Vector2(float(prop_scale[0]), float(prop_scale[1]))
		for sprite in prop.lit_sprites():
			sprite.material = rim
			sprite.texture_filter = map.texture_filter()
		holder.add_child(prop)


## Collision for a solid sprite: the walkable shape drawn in the editor
## (`shape`) when it has one, else polygons traced around the art's opaque
## pixels (alpha over a half), with strands thinner than a few level pixels
## (grass, hanging ivy) left out, so the hero stands on the stone itself.
## Without the sheet's image (a missing sheet), the export's hit box, or
## the whole tile box, stands in. All of it is scaled by `copy_scale`.
func _add_sprite_collision(
	spec: Dictionary,
	at: Vector2,
	columns: int,
	rows: int,
	flip: bool,
	tiles: PackedInt32Array,
	map: WorldMap,
	tile_set: TileSet,
	copy_scale := 1.0
) -> void:
	var polygons := drawn_shapes(spec.get("shape", []), columns * TILE, flip)
	var image := (
		_sprite_image(tiles, columns, rows, flip, map, tile_set) if polygons.is_empty() else null
	)
	if image:
		# Traced at a few pixels per level pixel: fine enough for the outline,
		# and small enough that the strand filter (a neighbourhood pass per
		# pixel) stays quick for 128-px art.
		var size := Vector2i(columns * SPRITE_TRACE_SCALE * TILE, rows * SPRITE_TRACE_SCALE * TILE)
		if image.get_size() != size:
			image.resize(size.x, size.y, Image.INTERPOLATE_BILINEAR)
		var mask := BitMap.new()
		mask.create_from_image_alpha(image, 0.5)
		var rect := Rect2i(Vector2i.ZERO, size)
		# Opening: shrink then grow back, which drops anything thinner than
		# the strand width and keeps the body's outline where it was.
		var margin := maxi(1, int(round(SPRITE_STRAND_PX * SPRITE_TRACE_SCALE / 2.0)))
		mask.grow_mask(-margin, rect)
		mask.grow_mask(margin, rect)
		for polygon in mask.opaque_to_polygons(rect, SPRITE_TRACE_PX * SPRITE_TRACE_SCALE):
			var points := PackedVector2Array()
			for point in polygon:
				points.append(point / float(SPRITE_TRACE_SCALE))
			points = smooth_outline(points, SPRITE_SMOOTH_PX)
			if points.size() >= 3:
				polygons.append(points)
	if polygons.is_empty():
		var hit: Array = spec.get("hit", [0, 0, columns * TILE, rows * TILE])
		var left := float(hit[0])
		if flip:
			left = columns * TILE - float(hit[0]) - float(hit[2])
		var size := Vector2(float(hit[2]), float(hit[3]))
		(
			polygons
			. append(
				PackedVector2Array(
					[
						Vector2(left, float(hit[1])),
						Vector2(left + size.x, float(hit[1])),
						Vector2(left + size.x, float(hit[1]) + size.y),
						Vector2(left, float(hit[1]) + size.y),
					]
				)
			)
		)
	for points in polygons:
		var shape := CollisionPolygon2D.new()
		# Outlines are in the texture's own level pixels; a resized copy scales them.
		shape.polygon = Transform2D().scaled(Vector2.ONE * copy_scale) * points
		shape.position = at
		_solids.add_child(shape)


## A sprite's drawn walkable shapes ([[x, y], ...] per shape, level px from
## its top-left, unmirrored) as polygons, mirrored inside `width` when the
## sprite is. Shapes with fewer than three points are left out.
static func drawn_shapes(shapes: Variant, width: float, flip: bool) -> Array[PackedVector2Array]:
	var out: Array[PackedVector2Array] = []
	if not shapes is Array:
		return out
	for shape: Variant in shapes:
		if not shape is Array:
			continue
		var points := PackedVector2Array()
		for point: Variant in shape:
			if point is Array and point.size() >= 2:
				var x := float(point[0])
				points.append(Vector2(width - x if flip else x, float(point[1])))
		if points.size() >= 3:
			out.append(points)
	return out


## A closed outline simplified (Ramer-Douglas-Peucker) so no point of the
## original lies further than `tolerance` from it: small steps merge into
## slopes, corners further out than that stay. The original comes back when
## the result would not make a valid polygon.
static func smooth_outline(points: PackedVector2Array, tolerance: float) -> PackedVector2Array:
	var n := points.size()
	if n < 4:
		return points
	# Split the loop at the point furthest from the first, so each half is an
	# open chain with well-defined ends.
	var far := 0
	for i in n:
		if points[i].distance_squared_to(points[0]) > points[far].distance_squared_to(points[0]):
			far = i
	if far == 0:
		return points
	var first := points.slice(0, far + 1)
	var second := points.slice(far)
	second.append(points[0])
	var result := _simplify_chain(first, tolerance)
	result.remove_at(result.size() - 1)
	var rest := _simplify_chain(second, tolerance)
	rest.remove_at(rest.size() - 1)
	result.append_array(rest)
	if result.size() < 3 or Geometry2D.triangulate_polygon(result).is_empty():
		return points
	return result


static func _simplify_chain(chain: PackedVector2Array, tolerance: float) -> PackedVector2Array:
	var keep := PackedByteArray()
	keep.resize(chain.size())
	keep[0] = 1
	keep[chain.size() - 1] = 1
	var stack: Array[Vector2i] = [Vector2i(0, chain.size() - 1)]
	while not stack.is_empty():
		var span: Vector2i = stack.pop_back()
		var worst := -1
		var worst_distance := tolerance
		for i in range(span.x + 1, span.y):
			var on := Geometry2D.get_closest_point_to_segment(
				chain[i], chain[span.x], chain[span.y]
			)
			var distance := chain[i].distance_to(on)
			if distance > worst_distance:
				worst = i
				worst_distance = distance
		if worst >= 0:
			keep[worst] = 1
			stack.append(Vector2i(span.x, worst))
			stack.append(Vector2i(worst, span.y))
	var out := PackedVector2Array()
	for i in chain.size():
		if keep[i]:
			out.append(chain[i])
	return out


## The sprite's art as one image: its atlas tiles laid out in order, flipped
## when the sprite is. Null without the diffuse sheet.
func _sprite_image(
	tiles: PackedInt32Array, columns: int, rows: int, flip: bool, map: WorldMap, tile_set: TileSet
) -> Image:
	if _sheet_image == null:
		var source := tile_set.get_source(0) as TileSetAtlasSource
		var texture := source.texture as CanvasTexture if source else null
		if texture and texture.diffuse_texture:
			_sheet_image = texture.diffuse_texture.get_image()
	var sheet := _sheet_image
	if sheet == null:
		return null
	var art := map.art_tile
	var image := Image.create(columns * art, rows * art, false, Image.FORMAT_RGBA8)
	for i in tiles.size():
		if tiles[i] < 0:
			continue
		var coords := map.atlas_coords(tiles[i])
		image.blit_rect(
			sheet,
			Rect2i(coords.x * art, coords.y * art, art, art),
			Vector2i((i % columns) * art, (i / columns) * art)
		)
	if flip:
		image.flip_x()
	return image


## Collision for the cells x0 to x1 of one row of terrain: runs of solid
## cells become one rectangle each, ledges a one-way strip and hazards a
## Hazard area, each appended to `nodes` (the chunk's). The tiles themselves
## are drawn by the layer.
func _build_row(map: WorldMap, y: int, x0: int, x1: int, nodes: Array[Node]) -> void:
	var x := x0
	while x < x1:
		var kind := _collision_kind(map, x, y)
		if kind == WorldMap.KIND_NONE:
			x += 1
			continue
		var run := 1
		while x + run < x1 and _collision_kind(map, x + run, y) == kind:
			run += 1
		match kind:
			WorldMap.KIND_SOLID:
				nodes.append(_add_solid_run(x, y, run))
			WorldMap.KIND_LEDGE:
				nodes.append(_add_ledge_run(x, y, run))
			WorldMap.KIND_HAZARD:
				nodes.append(_add_hazard_run(x, y, run))
		x += run


## Hidden collision is solid for the hero; a carved secret passage is air
## (its SecretWall is the solid until it breaks).
func _collision_kind(map: WorldMap, x: int, y: int) -> int:
	if _carved.has(Vector2i(x, y)):
		return WorldMap.KIND_NONE
	var kind := map.kind_at(x, y)
	return WorldMap.KIND_SOLID if kind == WorldMap.KIND_HIDDEN else kind


func _add_solid_run(x: int, y: int, run: int) -> Node:
	var shape := CollisionShape2D.new()
	var rect := RectangleShape2D.new()
	rect.size = Vector2(run * TILE, TILE)
	shape.shape = rect
	shape.position = Vector2(x * TILE + run * TILE / 2.0, y * TILE + TILE / 2.0)
	_solids.add_child(shape)
	return shape


func _add_ledge_run(x: int, y: int, run: int) -> Node:
	var shape := CollisionShape2D.new()
	var rect := RectangleShape2D.new()
	rect.size = Vector2(run * TILE, LEDGE_HEIGHT)
	shape.shape = rect
	shape.one_way_collision = true
	shape.position = Vector2(x * TILE + run * TILE / 2.0, y * TILE + LEDGE_HEIGHT / 2.0)
	_ledges.add_child(shape)
	return shape


func _add_hazard_run(x: int, y: int, run: int) -> Node:
	var hazard: Hazard = HAZARD_SCENE.instantiate()
	var shape: CollisionShape2D = hazard.get_node("CollisionShape2D")
	var rect := RectangleShape2D.new()
	# A bit narrower than the tiles and only the lower part, so brushing the
	# edge of a pit is forgiven.
	rect.size = Vector2(run * TILE - 4.0, 9.0)
	shape.shape = rect
	hazard.position = Vector2(x * TILE + run * TILE / 2.0, y * TILE + TILE - 4.5)
	_hazards.add_child(hazard)
	return hazard


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


## The secret doors of the export sit in the solid between two rooms. Each
## one's passage is the four-wide (or four-tall) channel from the air on
## one side to the air on the other, carved out of the collision here and
## filled by a SecretWall in _place_secrets. A door the tool left at the
## origin has no place in the grid and is skipped.
func _find_secrets(map: WorldMap) -> void:
	for door in map.doors:
		if not bool(door.get("secret", false)):
			continue
		var x := int(door.get("x", 0))
		var y := int(door.get("y", 0))
		if x == 0 and y == 0:
			continue
		var cells: Array[Vector2i] = []
		if str(door.get("dir", "v")) == "v":
			var top := _air_from(map, x, y - 1, Vector2i(0, -1))
			var bottom := _air_from(map, x, y + 1, Vector2i(0, 1))
			if top < 0 or bottom < 0:
				continue
			for row in range(top + 1, bottom):
				for column in range(x - 2, x + 2):
					cells.append(Vector2i(column, row))
		else:
			var left := _air_from(map, x - 1, y, Vector2i(-1, 0))
			var right := _air_from(map, x + 1, y, Vector2i(1, 0))
			if left < 0 or right < 0:
				continue
			for column in range(left + 1, right):
				for row in range(y - 3, y + 1):
					cells.append(Vector2i(column, row))
		if cells.is_empty():
			continue
		for cell in cells:
			_carved[cell] = true
		_secret_specs.append({"id": int(door.get("id", 0)), "cells": cells})


## The first cell of air from (x, y) along `step`, as its row (for a
## vertical step) or column, or -1 when the solid goes on too far.
func _air_from(map: WorldMap, x: int, y: int, step: Vector2i) -> int:
	var cell := Vector2i(x, y)
	for i in SECRET_DEPTH:
		if cell.x < 0 or cell.y < 0 or cell.x >= width or cell.y >= height:
			return -1
		if map.kind_at(cell.x, cell.y) == WorldMap.KIND_NONE:
			return cell.y if step.y != 0 else cell.x
		cell += step
	return -1


## A SecretWall over every passage the hero has not broken; a broken one
## is open already, its tiles gone.
func _place_secrets() -> void:
	for spec in _secret_specs:
		var cells: Array[Vector2i] = spec["cells"]
		if Story.has_flag(SecretWall.flag_for(int(spec["id"]))):
			_erase_cells(cells)
			continue
		var wall := SecretWall.new()
		wall.door_id = int(spec["id"])
		wall.cells = cells
		wall.name = "Secret%d" % wall.door_id
		wall.broken.connect(_on_secret_broken)
		_seals.add_child(wall)
		secrets.append(wall)


func _on_secret_broken(wall: SecretWall) -> void:
	secrets.erase(wall)
	_erase_cells(wall.cells)


## Takes the terrain tiles off the cells of an opened passage, for good: a
## chunk streamed in again later leaves them empty too.
func _erase_cells(cells: Array[Vector2i]) -> void:
	var terrain: TileMapLayer = _layers.get(WorldMap.LAYER_TERRAIN)
	for cell in cells:
		_erased[cell] = true
		if terrain != null:
			terrain.erase_cell(cell)


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


## The index of the ability `name` in the map's list, by its full name or
## its name without the note in brackets, or -1.
func _ability_index(map: WorldMap, name: String) -> int:
	var wanted := name.get_slice("(", 0).strip_edges().to_lower()
	for i in map.abilities.size():
		if map.abilities[i] == name or _ability_name(map, i).to_lower() == wanted:
			return i
	return -1


## The id of the boss standing in the region (an enemy placement with a
## boss's profile, see Boss.IDS), or "" when the region has none.
func _boss_in_region(map: WorldMap, region: int) -> String:
	if region < 0 or region >= map.regions.size():
		return ""
	var band: Dictionary = map.regions[region]
	for spec in map.objects:
		if not spec is Dictionary or str(spec.get("type", "")) != "enemy":
			continue
		var character := str(spec.get("character", ""))
		if not Boss.has_profile(character):
			continue
		var y := (int(spec.get("y", 0)) + 1) * map.tile
		if y > float(band.get("y0", 0)) and y <= float(band.get("y1", 0)):
			return character
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
		"start":
			player_start = at
		"save":
			var checkpoint: Checkpoint = CHECKPOINT_SCENE.instantiate()
			checkpoint.position = at
			_objects.add_child(checkpoint)
			checkpoints.append(checkpoint)
		"ability":
			var ability := _ability_for(_ability_index(world, str(spec.get("ability", ""))))
			if ability >= 0:
				_add_pickup(at, ability)
		"npc", "item", "trigger":
			_add_placement(at, spec)
		"enemy":
			var character := str(spec.get("character", "imp"))
			var spawn: EnemySpawn
			if character in Flyer.CHARACTERS:
				var flyer_scene := enemy_scene(character)
				spawn = EnemySpawn.new(flyer_scene, at - Vector2(0.0, TILE / 2.0), character)
				spawn.behaviour = str(spec.get("behaviour", ""))
			elif character == Hanger.CHARACTER:
				spawn = EnemySpawn.new(HANGER_SCENE, at)
			elif Boss.has_profile(character):
				if Story.has_flag(Boss.flag_for(character)):
					# A dead boss's unique loot lies where it fell until it
					# is taken, whatever happened to the hero since.
					var unique := Boss.unique_loot(character)
					if not unique.is_empty() and not Story.has_flag(Items.loot_flag(unique)):
						_add_loot(at, unique)
					return
				spawn = EnemySpawn.new(BOSS_SCENE, at, character)
				spawn.arena = room_rect(world, room_at(at))
			else:
				if character not in Walker.CHARACTERS:
					push_error("LevelBuilder: no enemy called '%s', placing an imp" % character)
					character = "imp"
				spawn = EnemySpawn.new(enemy_scene(character), at, character)
				spawn.barks = str(spec.get("barks", ""))
				spawn.passive = bool(spec.get("passive", false))
				spawn.behaviour = str(spec.get("behaviour", ""))
			spawn.requires = str(spec.get("requires", ""))
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
			npc.shop = str(spec.get("shop", ""))
			if not npc.shop.is_empty() and not Shops.has(npc.shop):
				push_warning("LevelBuilder: no shop called '%s'" % npc.shop)
				npc.shop = ""
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


## Lays an item on the floor (see LootDrop), for a boss's unique loot the
## hero has not picked up yet.
func _add_loot(at: Vector2, item_id: String) -> void:
	var drop := LootDrop.new()
	drop.item_id = item_id
	drop.name = "Loot%s" % item_id.capitalize().replace(" ", "")
	drop.position = at
	_objects.add_child(drop)


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


## Colours the rim light on the tiles (the region's light, see LookBlend).
func set_rim(color: Color) -> void:
	if rim != null:
		rim.set_shader_parameter("rim_color", color)


## (Re)creates every enemy from the map. Called at start and after the hero
## respawns, so a checkpoint run always faces the same fight.
func spawn_enemies() -> void:
	for child in _enemies.get_children():
		_enemies.remove_child(child)
		child.queue_free()
	for spawn in _enemy_spawns:
		if not spawn.requires.is_empty() and not Story.has_flag(spawn.requires):
			continue
		var enemy: Node2D = spawn.scene.instantiate()
		enemy.position = spawn.position
		enemy.drop_parent = _objects
		if enemy is Walker:
			var walker := enemy as Walker
			walker.barks = spawn.barks
			walker.passive = spawn.passive
			walker.behaviour = spawn.behaviour
			walker.death_y = size_px.y + 64.0
		elif enemy is Flyer:
			(enemy as Flyer).behaviour = spawn.behaviour
		elif enemy is Hanger:
			(enemy as Hanger).death_y = size_px.y + 64.0
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


## Puts the enemies far from all the points (the hero first, then the camera's
## centre) to sleep and wakes the ones near either. Every enemy in the world moving
## every tick cost more than a whole physics frame on its own, so the game
## fell behind, ran several ticks a frame to catch up and pinned the CPU.
## A boss also stays awake while the hero is in its arena (it watches for
## him there to start the fight) and once the fight has begun.
func update_active_enemies(points: Array[Vector2]) -> void:
	for enemy: Node2D in _enemies.get_children():
		var near := false
		if enemy is Boss:
			var boss := enemy as Boss
			near = boss.state != Boss.State.DORMANT or boss.arena.has_point(points[0])
		for point in points:
			var offset := (enemy.global_position - point).abs()
			if offset.x <= ACTIVE_RANGE.x and offset.y <= ACTIVE_RANGE.y:
				near = true
				break
		var mode := Node.PROCESS_MODE_INHERIT if near else Node.PROCESS_MODE_DISABLED
		if enemy.process_mode != mode:
			enemy.process_mode = mode


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
