extends Node

## Game feel: hit-stop, screen shake, hit flashes and sparks.
##
## Registered as the Feedback autoload so enemies, the hero and pickups can
## ask for feedback in one line. The camera registers itself in the
## "game_camera" group to receive shakes.

const FLASH_SHADER := preload("res://assets/shaders/hit_flash.gdshader")
const SPARK_SCENE := preload("res://scenes/hit_spark.tscn")
const HIT_STOP_SCALE := 0.05

## Real-time stamp (msec) at which the current hit-stop ends.
var _hit_stop_until := 0


func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS


func _process(_delta: float) -> void:
	if _hit_stop_until > 0 and Time.get_ticks_msec() >= _hit_stop_until:
		_hit_stop_until = 0
		Engine.time_scale = 1.0


## Freezes the game for a moment of real time; a longer stop replaces a
## shorter one still running.
func hit_stop(seconds: float) -> void:
	var until := Time.get_ticks_msec() + int(seconds * 1000.0)
	if until > _hit_stop_until:
		_hit_stop_until = until
	Engine.time_scale = HIT_STOP_SCALE


func is_hit_stopped() -> bool:
	return _hit_stop_until > 0


## Shakes the camera; `amount` is 0 to 1 and adds up across hits.
func shake(amount: float) -> void:
	for camera in get_tree().get_nodes_in_group("game_camera"):
		camera.add_trauma(amount)


## Flashes a sprite white, fading back over the duration. The first flash
## gives the sprite its own copy of the flash shader.
func flash(item: CanvasItem, duration := 0.1) -> void:
	if not is_instance_valid(item):
		return
	var material := item.material as ShaderMaterial
	if material == null or material.shader != FLASH_SHADER:
		material = ShaderMaterial.new()
		material.shader = FLASH_SHADER
		item.material = material
	material.set_shader_parameter("flash", 1.0)
	var tween := item.create_tween()
	tween.tween_property(material, "shader_parameter/flash", 0.0, duration)


## Bursts sparks at a world position. The spark frees itself when done.
func spark(world_position: Vector2, color := Color.WHITE) -> void:
	var spark := SPARK_SCENE.instantiate()
	spark.global_position = world_position
	spark.color = color
	get_tree().root.add_child(spark)


func spark_count() -> int:
	return get_tree().get_nodes_in_group("hit_spark").size()
