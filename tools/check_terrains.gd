extends SceneTree

## Checks the world's terrains: how well the rules read off the map fit it,
## and how far Godot's terrain painting, given each region's ground cells,
## lands on tiles facing the same way the scenes paint them (the world's
## border and the rows where one region's ground meets the next are left
## out: the rules take the world's edge as ground, and a terrain does not
## join another region's):
##
##   godot --headless --path . --script tools/check_terrains.gd


func _initialize() -> void:
	var map := SceneWorld.read("res://scenes/world/world.tscn")
	if map == null:
		quit(1)
		return
	var rules := TerrainRules.infer(map)
	printerr(
		(
			"rules for %d tiles fit %.1f%% of the solid cells"
			% [rules.size(), 100.0 * TerrainRules.fit(map, rules)]
		)
	)
	var index := map.tile_index()
	var terrain := map.cells(WorldMap.LAYER_TERRAIN)
	var solid := PackedByteArray()
	solid.resize(terrain.size())
	for i in terrain.size():
		solid[i] = 1 if map.kinds.get(terrain[i], 0) == WorldMap.KIND_SOLID else 0
	var total := 0
	var same := 0
	for r in map.regions.size():
		var spec: Dictionary = map.regions[r]
		var top := int(floor(float(spec.get("y0", 0)) / map.tile))
		var bottom := mini(int(ceil(float(spec.get("y1", 0)) / map.tile)), map.height)
		var cells: Array[Vector2i] = []
		var expected := {}
		for y in range(top, bottom):
			for x in map.width:
				if solid[y * map.width + x] == 1:
					cells.append(Vector2i(x, y))
					expected[Vector2i(x, y)] = TerrainRules.mask_at(
						solid, map.width, map.height, x, y
					)
		if cells.is_empty():
			continue
		# The other regions' ground stands as the scenes paint it, so the
		# band's edges meet their neighbours; the world's own border rows and
		# columns are left out of the count, since the rules take the edge of
		# the world as ground and the painter has nothing there.
		var layer := TileMapLayer.new()
		layer.tile_set = map.tile_set
		for y in map.height:
			for x in map.width:
				var i := y * map.width + x
				if solid[i] == 1 and (y < top or y >= bottom):
					var at := index.locate(terrain[i])
					layer.set_cell(Vector2i(x, y), at[0], at[1])
		var terrain_index := -1
		for t in map.tile_set.get_terrains_count(0):
			if map.tile_set.get_terrain_name(0, t) == str(spec.get("name", "")):
				terrain_index = t
		if terrain_index < 0:
			printerr("%s: no terrain" % spec.get("name", "?"))
			continue
		layer.set_cells_terrain_connect(cells, 0, terrain_index, false)
		var hits := 0
		var misses := {}
		var counted := 0
		for cell in cells:
			# Nor the band's first and last rows: the ground across them is
			# another region's terrain, which terrains do not join.
			var border := cell.x == 0 or cell.x == map.width - 1
			var band_edge := cell.y == 0 or cell.y == map.height - 1
			if border or band_edge or cell.y == top or cell.y == bottom - 1:
				continue
			counted += 1
			var source := layer.get_cell_source_id(cell)
			var coords := layer.get_cell_atlas_coords(cell)
			if source < 0:
				continue
			var data: TileData = map.tile_set.get_source(source).get_tile_data(coords, 0)
			var got := TerrainRules.mask_of(data, terrain_index)
			if got == expected[cell]:
				hits += 1
			else:
				var pair := [expected[cell], got]
				misses[pair] = misses.get(pair, 0) + 1
		var worst := misses.keys()
		worst.sort_custom(func(a, b): return misses[a] > misses[b])
		var masks_present := {}
		for s_id in map.tile_set.get_source_count():
			var src := (
				map.tile_set.get_source(map.tile_set.get_source_id(s_id)) as TileSetAtlasSource
			)
			for t in src.get_tiles_count():
				var d := src.get_tile_data(src.get_tile_id(t), 0)
				if d.terrain == terrain_index:
					masks_present[TerrainRules.mask_of(d, terrain_index)] = true
		printerr("  masks with tiles: %s" % [masks_present.keys()])
		for pair in worst.slice(0, 6):
			printerr("  expected %2d got %2d: %d cells" % [pair[0], pair[1], misses[pair]])
		printerr(
			(
				"%-14s %6d ground cells, painted by terrain %5.1f%% face the same way"
				% [spec.get("name", "?"), counted, 100.0 * hits / maxi(counted, 1)]
			)
		)
		total += counted
		same += hits
		layer.free()
	printerr("all: %.1f%% of %d" % [100.0 * same / maxi(total, 1), total])
	quit(0)
