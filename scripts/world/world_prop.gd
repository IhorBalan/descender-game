@tool
class_name WorldProp
extends Node2D

## A piece of a region's set: a lantern, a pillar, a floating ledge, a
## statue, as its own scene under scenes/props/<region key>/, instanced in
## the region's scene under `Props/*` (see WorldRegion). The scene is the
## root with this script and, under it, an `Art` Sprite2D drawing the piece's
## PNG (a CanvasTexture of assets/props/<key>/<name>.png and <name>_n.png,
## its normal map) at tile / art_tile of its size from its top-left corner,
## and, for a piece that shines, a `Glow` Sprite2D of <name>_glow.png added
## unlit on top. Drag an instance to place it, scale it by -1 to flip it,
## and edit the scene once to change every copy. tools/extract_props.gd
## wrote the first set from the pieces painted as tiles.
##
## The game instances the same scene (LevelBuilder) where SceneWorld read
## it, lighting its art the way it lights the tile layers.

## Pixels a tile the PNGs were drawn at (the sheet's art scale).
@export var art_tile := 64


## The sprites the level's lights and the sky's rim fall on: every Sprite2D
## under the prop without a material of its own (the glow has one).
func lit_sprites() -> Array[Sprite2D]:
	var found: Array[Sprite2D] = []
	_collect_lit(self, found)
	return found


## The prop as the spec SceneWorld hands LevelBuilder: its scene, its
## position in level pixels (given the region's `offset`) and its scale.
func to_spec(offset: Vector2) -> Dictionary:
	var at := position + offset
	var spec := {"scene": scene_file_path, "x": at.x, "y": at.y}
	if scale != Vector2.ONE:
		spec["scale"] = [scale.x, scale.y]
	return spec


func _collect_lit(node: Node, found: Array[Sprite2D]) -> void:
	for child in node.get_children():
		if child is Sprite2D and (child as Sprite2D).material == null:
			found.append(child)
		_collect_lit(child, found)
