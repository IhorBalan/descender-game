class_name SecretWall
extends StaticBody2D

## The rock that hides a secret passage of the world. The map tool marks a
## secret door between two rooms; the level builder carves the passage
## through the solid between them and fills it with one of these, drawn by
## the terrain tiles that were there. A Smite landing on or beside it
## breaks it (flag `secret:<door id>`, saved), and the builder erases the
## tiles. Cells are added by the builder; the node's origin is the level's.

## Fired once the wall breaks, with the cells to clear.
signal broken(wall: SecretWall)

const TILE := 16
const SPARK := Color(0.75, 0.7, 0.65)
## How far past its cells a slam still counts, in pixels.
const SLACK := TILE / 2.0

## The door the wall hides (its id in the export).
var door_id := 0
## The cells the wall fills, in tiles.
var cells: Array[Vector2i] = []

var _broken := false


static func flag_for(id: int) -> String:
	return "secret:%d" % id


func _ready() -> void:
	add_to_group("secret_wall")
	collision_layer = 1
	collision_mask = 0
	for cell in cells:
		var shape := CollisionShape2D.new()
		var rect := RectangleShape2D.new()
		rect.size = Vector2(TILE, TILE)
		shape.shape = rect
		shape.position = Vector2(cell.x * TILE + TILE / 2.0, cell.y * TILE + TILE / 2.0)
		add_child(shape)


## The bounds of the wall in level pixels.
func rect() -> Rect2:
	var result := Rect2()
	for i in cells.size():
		var cell := Rect2(Vector2(cells[i]) * TILE, Vector2(TILE, TILE))
		result = cell if i == 0 else result.merge(cell)
	return result


## A Smite landed with its feet at `at`, hurting `reach` around them.
func smite(at: Vector2, reach: Vector2) -> void:
	var blow := Rect2(at - Vector2(reach.x / 2.0, reach.y), reach)
	if not _broken and rect().grow(SLACK).intersects(blow):
		open()


## Breaks the wall: the flag, the sparks, and the builder clears the tiles.
func open(quiet := false) -> void:
	if _broken:
		return
	_broken = true
	Story.set_flag(flag_for(door_id))
	if not quiet:
		for cell in cells:
			Feedback.spark(Vector2(cell.x * TILE + TILE / 2.0, cell.y * TILE + TILE / 2.0), SPARK)
		Feedback.shake(0.4)
		Audio.play("land", -4.0)
	set_deferred("collision_layer", 0)
	broken.emit(self)
	queue_free()
