class_name TerrainRules
extends RefCounted

## Which way each ground tile faces, read off the map it is painted in, for
## the TileSet's terrains (autotiling).
##
## The map editor's export does not say which of a region's ground tiles
## is a left edge, a top edge or an inside piece; it only places them. But
## it places them by a rule: in the world as painted, a tile's four side
## neighbours on the Terrain layer are solid or not in the same pattern
## almost everywhere it stands (99 of 100 cells), and the corner neighbours
## do not matter to it. So each solid tile's pattern is the most common
## one around it, as a mask of the four sides, and the TileSet gives the
## tile that mask as its terrain peering bits in a "match sides" terrain
## set (see WorldMigration.build_tile_set). Painting ground with Godot's
## terrain tools then picks the edge pieces the editor would have.
##
## The editor's sheet lays every region's ground row out the same way, so
## a tile never painted takes the pattern of its column across the other
## regions, and a tile with no such column stays without a terrain.

## The side bits of a mask.
const TOP := 1
const LEFT := 2
const RIGHT := 4
const BOTTOM := 8
## Each side bit with the cell neighbour it stands for.
const SIDES := {
	TOP: TileSet.CELL_NEIGHBOR_TOP_SIDE,
	LEFT: TileSet.CELL_NEIGHBOR_LEFT_SIDE,
	RIGHT: TileSet.CELL_NEIGHBOR_RIGHT_SIDE,
	BOTTOM: TileSet.CELL_NEIGHBOR_BOTTOM_SIDE,
}
const OFFSETS := {
	TOP: Vector2i(0, -1), LEFT: Vector2i(-1, 0), RIGHT: Vector2i(1, 0), BOTTOM: Vector2i(0, 1)
}


## Tile id -> side mask, for every solid tile the map knows: its own most
## common pattern, else its column's.
static func infer(map: WorldMap) -> Dictionary:
	var counts := {}
	var terrain := map.cells(WorldMap.LAYER_TERRAIN)
	if terrain.is_empty():
		return {}
	var width := map.width
	var solid := PackedByteArray()
	solid.resize(terrain.size())
	for i in terrain.size():
		solid[i] = 1 if map.kinds.get(terrain[i], 0) == WorldMap.KIND_SOLID else 0
	for y in map.height:
		for x in width:
			var i := y * width + x
			if solid[i] == 0:
				continue
			var mask := mask_at(solid, width, map.height, x, y)
			var id := terrain[i]
			if not counts.has(id):
				counts[id] = {}
			counts[id][mask] = counts[id].get(mask, 0) + 1
	var by_column := {}
	var rules := {}
	for id in counts:
		var mask := _mode(counts[id])
		rules[id] = mask
		var column: int = id % map.columns
		if not by_column.has(column):
			by_column[column] = {}
		for m in counts[id]:
			by_column[column][m] = by_column[column].get(m, 0) + counts[id][m]
	for id in map.kinds:
		if map.kinds[id] != WorldMap.KIND_SOLID or rules.has(id):
			continue
		var column := int(id) % map.columns
		if by_column.has(column):
			rules[id] = _mode(by_column[column])
	return rules


## The side mask around cell (x, y): a bit for each side whose neighbour is
## solid (outside the grid counts as solid, so the world's edge is ground).
static func mask_at(solid: PackedByteArray, width: int, height: int, x: int, y: int) -> int:
	var mask := 0
	for bit in OFFSETS:
		var at: Vector2i = Vector2i(x, y) + OFFSETS[bit]
		var inside := at.x >= 0 and at.y >= 0 and at.x < width and at.y < height
		if not inside or solid[at.y * width + at.x] == 1:
			mask |= bit
	return mask


## The side mask a TileSet tile declares, from its peering bits.
static func mask_of(data: TileData, terrain: int) -> int:
	var mask := 0
	for bit in SIDES:
		if data.get_terrain_peering_bit(SIDES[bit]) == terrain:
			mask |= bit
	return mask


## How well the rules fit the map: the share of solid cells whose tile has
## the mask the cell is in, in [0, 1].
static func fit(map: WorldMap, rules: Dictionary) -> float:
	var terrain := map.cells(WorldMap.LAYER_TERRAIN)
	var solid := PackedByteArray()
	solid.resize(terrain.size())
	for i in terrain.size():
		solid[i] = 1 if map.kinds.get(terrain[i], 0) == WorldMap.KIND_SOLID else 0
	var cells := 0
	var hits := 0
	for y in map.height:
		for x in map.width:
			var i := y * map.width + x
			if solid[i] == 0 or not rules.has(terrain[i]):
				continue
			cells += 1
			if rules[terrain[i]] == mask_at(solid, map.width, map.height, x, y):
				hits += 1
	return float(hits) / cells if cells > 0 else 1.0


static func _mode(histogram: Dictionary) -> int:
	var best := -1
	var best_count := -1
	for mask in histogram:
		if histogram[mask] > best_count or (histogram[mask] == best_count and mask < best):
			best = mask
			best_count = histogram[mask]
	return best
