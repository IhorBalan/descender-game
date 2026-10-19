extends Camera2D

## Follows the hero and stays inside the level bounds.

var target: Node2D


func _physics_process(_delta: float) -> void:
	if is_instance_valid(target):
		global_position = target.global_position


func set_limits(level_size: Vector2) -> void:
	limit_left = 0
	limit_top = 0
	limit_right = int(level_size.x)
	limit_bottom = int(level_size.y)
