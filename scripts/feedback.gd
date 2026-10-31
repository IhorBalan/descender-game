extends Node

## Game feel: hit-stop, screen shake, hit flashes, sparks, death puffs and
## landing dust.
##
## Registered as the Feedback autoload so enemies, the hero and pickups can
## ask for feedback in one line. The camera registers itself in the
## "game_camera" group to receive shakes.

const FLASH_SHADER := preload("res://godot_assets/shaders/hit_flash.gdshader")
const SPARK_SCENE := preload("res://scenes/hit_spark.tscn")
const PUFF_SCENE := preload("res://scenes/death_puff.tscn")
const HIT_STOP_SCALE := 0.05
## How long a hit rumbles the gamepad; the shake amount sets how hard.
const RUMBLE_TIME := 0.15
const DUST_COLOR := Color(0.85, 0.8, 0.7, 0.75)
const DUST_LIFETIME := 0.35

## How many landings have raised dust, for the test.
var dusts_raised := 0

## Real-time stamp (msec) at which the current hit-stop ends.
var _hit_stop_until := 0


func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS


func _process(_delta: float) -> void:
	if _hit_stop_until > 0 and Time.get_ticks_msec() >= _hit_stop_until:
		_hit_stop_until = 0
		Engine.time_scale = 1.0


## Freezes the game for a moment of real time; a longer stop replaces a
## shorter one still running. Does nothing with hit stop turned off in the
## settings.
func hit_stop(seconds: float) -> void:
	if not Settings.hit_stop:
		return
	var until := Time.get_ticks_msec() + int(seconds * 1000.0)
	if until > _hit_stop_until:
		_hit_stop_until = until
	Engine.time_scale = HIT_STOP_SCALE


func is_hit_stopped() -> bool:
	return _hit_stop_until > 0


## Shakes the camera and rumbles the gamepads; `amount` is 0 to 1 and
## adds up across hits. The screen shake setting scales the shake, down to
## nothing when turned off, and the rumble setting turns the rumble off.
func shake(amount: float) -> void:
	for camera in get_tree().get_nodes_in_group("game_camera"):
		camera.add_trauma(amount * Settings.screen_shake)
	if Settings.rumble and amount > 0.0:
		for device in Input.get_connected_joypads():
			Input.start_joy_vibration(device, amount * 0.5, amount, RUMBLE_TIME)


## Flashes a sprite white, fading back over the duration. The first flash
## gives the sprite its own copy of the flash shader. Does nothing with
## hit flashes turned off in the settings.
func flash(item: CanvasItem, duration := 0.1) -> void:
	if not is_instance_valid(item) or not Settings.hit_flash:
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


## A slow puff of dust (or light) at a world position, for a death. The
## puff frees itself when done.
func puff(world_position: Vector2, color := Color.WHITE) -> void:
	var puff := PUFF_SCENE.instantiate()
	puff.global_position = world_position
	puff.color = color
	get_tree().root.add_child(puff)


## A small, quick puff of dust at the hero's feet, for a hard landing: the
## death puff's scene, smaller and shorter, in its own group.
func dust(world_position: Vector2) -> void:
	var dust := PUFF_SCENE.instantiate() as CPUParticles2D
	dust.global_position = world_position
	dust.color = DUST_COLOR
	dust.amount = 5
	dust.lifetime = DUST_LIFETIME
	dust.emission_sphere_radius = 5.0
	dust.initial_velocity_min = 10.0
	dust.initial_velocity_max = 30.0
	dust.scale_amount_min = 1.0
	dust.scale_amount_max = 2.0
	dust.spread = 80.0
	get_tree().root.add_child(dust)
	dust.remove_from_group("death_puff")
	dust.add_to_group("landing_dust")
	dusts_raised += 1


func dust_count() -> int:
	return get_tree().get_nodes_in_group("landing_dust").size()


func puff_count() -> int:
	return get_tree().get_nodes_in_group("death_puff").size()
