class_name GameCamera
extends Camera2D

## Follows the hero and stays inside the level bounds. The hero is spawned at
## runtime, so main.gd assigns `target`; the camera runs its smoothing in the
## physics step (process_callback in main.tscn) to match the follow below.

var target: Node2D


func _physics_process(_delta: float) -> void:
	if is_instance_valid(target):
		global_position = target.global_position


func set_limits(level_size: Vector2) -> void:
	limit_left = 0
	limit_top = 0
	limit_right = int(level_size.x)
	limit_bottom = int(level_size.y)
