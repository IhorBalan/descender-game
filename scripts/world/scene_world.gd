class_name SceneWorld
extends RefCounted

## Reads the game's world from its scenes (scenes/world/world.tscn, a
## WorldScene with a WorldRegion per region) into a WorldMap, the same
## thing a map editor export gives, so LevelBuilder and everything after
## it see one kind of world. The regions' tile layers become the grid, the
## TileSet's custom data the terrain kinds, and the regions' lights, rooms,
## doors, markers and placements the lists the export would hold, in level
## pixels of the whole world. Nothing is added to the tree: the scene is
## instanced, read and freed.

## The room grid the map page draws on, in tiles.
const ROOM_COLUMNS := 30
const ROOM_ROWS := 17


## Reads the world scene at `path`; null, with an error pushed, when it is
## missing or not a WorldScene.
static func read(path: String) -> WorldMap:
	var packed := load(path) as PackedScene
	if packed == null:
		push_error("SceneWorld: no scene at %s" % path)
		return null
	var root := packed.instantiate()
	if not root is WorldScene:
		push_error("SceneWorld: %s is not a WorldScene" % path)
		root.free()
		return null
	var map := from_scene(root)
	map.path = path
	root.free()
	return map


## A WorldMap of a WorldScene node (not freed here).
static func from_scene(world: WorldScene) -> WorldMap:
	var map := WorldMap.new()
	map.width = world.width
	map.tile = world.tile
	map.art_tile = world.art_tile
	map.columns = world.columns
	map.tile_set = world.tile_set
	map.abilities = world.abilities
	map.descent_dark = world.descent_dark
	map.dialogue_path = world.dialogue
	var regions := world.regions()
	for region in regions:
		map.height = maxi(map.height, _top_row(region, map.tile) + region.rows)
	map.layer_names = PackedStringArray(WorldRegion.LAYER_NAMES)
	for layer_name in map.layer_names:
		var cells := PackedInt32Array()
		cells.resize(map.width * map.height)
		cells.fill(-1)
		map.layers[layer_name] = cells
	var index := TileIndex.of(world.tile_set, world.columns)
	map.kinds = _kinds(world.tile_set, index)
	for i in regions.size():
		_read_region(map, regions[i], i, index)
	var seen := {}
	for layer_name in map.layer_names:
		for id in map.layers[layer_name]:
			if id >= 0:
				seen[id] = true
	map.used_tiles = PackedInt32Array(seen.keys())
	map.used_tiles.sort()
	return map


## The row of the grid the region's band starts on.
static func _top_row(region: WorldRegion, tile: int) -> int:
	return int(round(region.position.y / tile))


## Tile id -> kind, from the TileSet's custom data "kind" on the tiles of
## its sources (see TileIndex for which tile is which).
static func _kinds(tile_set: TileSet, index: TileIndex) -> Dictionary:
	var kinds := {}
	if tile_set == null:
		return kinds
	var layer := tile_set.get_custom_data_layer_by_name("kind")
	if layer < 0:
		return kinds
	for i in tile_set.get_source_count():
		var source_id := tile_set.get_source_id(i)
		var source := tile_set.get_source(source_id) as TileSetAtlasSource
		if source == null:
			continue
		for j in source.get_tiles_count():
			var coords := source.get_tile_id(j)
			if index.is_glow(source_id, coords):
				continue
			var kind := int(source.get_tile_data(coords, 0).get_custom_data_by_layer_id(layer))
			var id := index.id_of(source_id, coords)
			if kind != WorldMap.KIND_NONE and id >= 0:
				kinds[id] = kind
	return kinds


static func _read_region(map: WorldMap, region: WorldRegion, index: int, tiles: TileIndex) -> void:
	var tile := map.tile
	var top := _top_row(region, tile)
	var offset := Vector2(0.0, top * tile)
	var spec := {
		"name": region.region_name,
		"subtitle": region.subtitle,
		"ambient": "#" + region.ambient.to_html(false),
		"y0": top * tile,
		"y1": (top + region.rows) * tile,
	}
	for key in region.look:
		spec[key] = region.look[key]
	map.regions.append(spec)
	for layer_name in map.layer_names:
		var layer := region.layer(layer_name)
		if layer == null:
			continue
		var cells: PackedInt32Array = map.layers[layer_name]
		for cell in layer.get_used_cells():
			var x: int = cell.x
			var y: int = cell.y + top
			if x < 0 or y < 0 or x >= map.width or y >= map.height:
				continue
			var id := tiles.id_of(layer.get_cell_source_id(cell), layer.get_cell_atlas_coords(cell))
			if id >= 0:
				cells[y * map.width + x] = id
	for light in region.lights():
		var at := light.position + offset
		(
			map
			. lights
			. append(
				{
					"x": at.x,
					"y": at.y,
					"z": light.height,
					"radius": light.radius(),
					"color": "#" + light.color.to_html(false),
					"energy": light.energy,
					"flicker": light.flicker,
				}
			)
		)
	var room_cell := Vector2(ROOM_COLUMNS * tile, ROOM_ROWS * tile)
	for room in region.rooms():
		var rect := Rect2(room.position + offset, room.size)
		var room_spec := {
			"id": room.id,
			"region": index,
			"cx": int(floor(rect.position.x / room_cell.x)),
			"cy": int(floor(rect.position.y / room_cell.y)),
			"cw": int(ceil(rect.size.x / room_cell.x)),
			"ch": int(ceil(rect.size.y / room_cell.y)),
			"kind": room.kind,
			"area": room.area,
			"depth": room.depth,
			"rect": [rect.position.x, rect.position.y, rect.size.x, rect.size.y],
		}
		if not room.room_name.is_empty():
			room_spec["name"] = room.room_name
		if not room.ability.is_empty():
			room_spec["ability"] = room.ability
		map.rooms.append(room_spec)
	for door in region.doors():
		var cell := Vector2i(((door.position + offset) / tile).floor())
		var door_spec := {
			"id": door.id,
			"a": door.room_a,
			"b": door.room_b,
			"dir": door.dir,
			"x": cell.x,
			"y": cell.y,
			"lock": door.lock(),
		}
		if door.secret:
			door_spec["secret"] = true
		map.doors.append(door_spec)
	for marker in region.markers():
		var feet := marker.position + offset
		var cell := feet_cell(feet, tile)
		(
			map
			. markers
			. append(
				{
					"id": marker.id,
					"type": marker.type,
					"x": cell.x,
					"y": cell.y,
					"label": marker.label,
					"room": marker.room,
					"px": feet.x,
					"py": feet.y,
				}
			)
		)
	for placement in region.placements():
		map.objects.append(placement.to_spec(feet_cell(placement.position + offset, tile)))


## The cell whose bottom a thing with its feet at `feet` stands on.
static func feet_cell(feet: Vector2, tile: int) -> Vector2i:
	return Vector2i(int(floor(feet.x / tile)), int(round(feet.y / tile)) - 1)


## Where a thing standing on the cell has its feet: the bottom centre.
static func feet_of(cell: Vector2i, tile: int) -> Vector2:
	return Vector2(cell.x * tile + tile / 2.0, (cell.y + 1) * tile)
