class_name TileIndex
extends RefCounted

## Where the tiles of the editor's sheet live in a TileSet.
##
## The map editor numbers tiles across one wide sheet: id = row * columns +
## column (64 columns). The TileSet built straight from that sheet keeps the
## same grid (source 0 the diffuse sheet, source 1 the glow sheet, a tile's
## atlas coords its column and row), which is what a downloaded map plays
## with. The world's own TileSet is cut up for the editor instead: one
## atlas source per region (see WorldMigration.build_tile_set), holding a
## few rows of the sheet reflowed ATLAS_COLUMNS wide, with the region's glow
## tiles under its diffuse ones in the same source. Each such source
## records the sheet rows it holds, in order, as the metadata SHEET_ROWS,
## and this index translates both ways between ids and (source, coords).

## Metadata on a TileSetAtlasSource: the sheet rows it holds, in order.
const SHEET_ROWS := "sheet_rows"
## How wide a per-region atlas is, in tiles.
const ATLAS_COLUMNS := 16

## The sheet's columns.
var columns := 64
## True for a TileSet with the sheet's own grid (no SHEET_ROWS anywhere).
var plain := true

## Sheet row -> [source id, the row's order within the source].
var _row_place := {}
## Source id -> {"rows": PackedInt32Array, "width": int, "glow_row": int}.
var _sources := {}


## The index of `tile_set`, whose sheet has `columns` columns.
static func of(tile_set: TileSet, sheet_columns: int) -> TileIndex:
	var index := TileIndex.new()
	index.columns = sheet_columns
	if tile_set == null:
		return index
	for i in tile_set.get_source_count():
		var source_id := tile_set.get_source_id(i)
		var source := tile_set.get_source(source_id) as TileSetAtlasSource
		if source == null or not source.has_meta(SHEET_ROWS):
			continue
		var rows := PackedInt32Array(source.get_meta(SHEET_ROWS))
		index.add_source(source_id, rows)
	return index


## Registers a source holding the sheet `rows`, in that order.
func add_source(source_id: int, rows: PackedInt32Array) -> void:
	plain = false
	var width := atlas_width()
	var tiles := rows.size() * columns
	_sources[source_id] = {
		"rows": rows, "width": width, "glow_row": int(ceil(float(tiles) / width))
	}
	for order in rows.size():
		_row_place[rows[order]] = [source_id, order]


## The width of a per-region atlas: ATLAS_COLUMNS, or the sheet's own
## width when that is narrower.
func atlas_width() -> int:
	return mini(ATLAS_COLUMNS, columns)


## Where a tile id is drawn from: [source id, atlas coords]; the glow
## sheet's copy of it with `glow`. [-1, Vector2i(-1, -1)] for an id no
## source holds.
func locate(id: int, glow := false) -> Array:
	if id < 0:
		return [-1, Vector2i(-1, -1)]
	var column := id % columns
	var row := id / columns
	if plain:
		return [1 if glow else 0, Vector2i(column, row)]
	var place: Array = _row_place.get(row, [])
	if place.is_empty():
		return [-1, Vector2i(-1, -1)]
	var source: Dictionary = _sources[place[0]]
	var local: int = place[1] * columns + column
	var width: int = source["width"]
	var coords := Vector2i(local % width, local / width)
	if glow:
		coords.y += source["glow_row"]
	return [place[0], coords]


## The tile id drawn at `coords` of a source, glow or not; -1 for an
## unknown source.
func id_of(source_id: int, coords: Vector2i) -> int:
	if plain:
		return coords.y * columns + coords.x if source_id in [0, 1] else -1
	if not _sources.has(source_id):
		return -1
	var source: Dictionary = _sources[source_id]
	var y: int = coords.y
	if y >= source["glow_row"]:
		y -= source["glow_row"]
	var local: int = y * source["width"] + coords.x
	var order: int = local / columns
	var rows: PackedInt32Array = source["rows"]
	if order >= rows.size():
		return -1
	return rows[order] * columns + local % columns


## Whether `coords` of a source is in its glow part.
func is_glow(source_id: int, coords: Vector2i) -> bool:
	if plain:
		return source_id == 1
	var source: Dictionary = _sources.get(source_id, {})
	return not source.is_empty() and coords.y >= source["glow_row"]


## The rows a source holds, in order.
func rows_of(source_id: int) -> PackedInt32Array:
	var source: Dictionary = _sources.get(source_id, {})
	return source.get("rows", PackedInt32Array())


## The ids of the sources this index knows, in order.
func source_ids() -> Array:
	return _sources.keys()
