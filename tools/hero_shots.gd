extends SceneTree

## Films the hero's moves with his collision box drawn over him, one strip
## per move, to check the animation against the physics. Needs a display
## (or a virtual one):
##
##   xvfb-run -a godot --rendering-driver opengl3 --path . \
##       --script tools/hero_shots.gd -- /tmp/hero
##
## Every ability is unlocked. Each move is set up at a tick (the hero is put
## somewhere, keys are pressed) and then filmed: a crop around the hero every
## few ticks, pasted into <move>.png in rows of FRAMES_PER_ROW, so a strip
## reads as time left to right. The green box is the hero's body shape, the
## red one the attack hitbox while it is live (drawn by BoxDraw below, not by
## Godot's collision debug drawing, which fills the shapes and hides the
## sprite). The hero is reached untyped: a --script compiles before the
## autoloads exist, so naming the Player class here would fail to compile.

## Level pixels around the hero's feet in each crop (left, up, right, down).
const CROP := Rect2i(-24, -42, 48, 48)
const FRAMES_PER_ROW := 8
## A spot on the start room's floor with clear air above it.
const AIR_START := Vector2(1064.0, 240.0)
## In the air beside the wall at the Warden's room's edge.
const WALL_START := Vector2(327.0 * 16.0 + 8.0, 44.0 * 16.0)

var _ticks := 0
var _main: Node
var _out_dir := "/tmp/hero"
## The strips being filmed: name -> {next, every, count, taken, frames}.
var _films: Dictionary = {}
var _done := false


func _initialize() -> void:
	var args := OS.get_cmdline_user_args()
	if args.size() > 0:
		_out_dir = args[0]
	DirAccess.make_dir_recursive_absolute(_out_dir)
	# One physics tick per drawn frame: a slow (software) renderer would
	# otherwise run several ticks between frames and film the same image
	# twice. The game runs slower for it, which the film does not show.
	Engine.max_physics_steps_per_frame = 1
	_main = load("res://scenes/main.tscn").instantiate()
	root.add_child(_main)


func _physics_process(_delta: float) -> bool:
	# The level (and the hero with it) comes a few ticks after the scene.
	if _main.player == null:
		return false
	_ticks += 1
	var player: Node = _main.player
	# A line spoken where the hero lands would hold him still.
	while _main.dialogue_box.is_open():
		_main.dialogue_box.advance()
	match _ticks:
		2:
			Story.set_flag("opening_seen")
			# Every ability (Player.Ability, in order).
			for ability in 6:
				player.unlock(ability)
			player.add_child(BoxDraw.new())
			_place(AIR_START)
		# Idle, breathing.
		10:
			_place(AIR_START)
			_film("idle", 20, 5, 8)
		# Run: up to speed, then a cycle.
		60:
			Input.action_press("move_right")
			_film("run", 30, 2, 12)
		# Skid: running right, the stick goes left.
		120:
			Input.action_release("move_right")
			Input.action_press("move_left")
			_film("skid", 0, 2, 10)
		145:
			Input.action_release("move_left")
			_place(AIR_START)
		165:
			# The scene itself, for scale.
			_save_scene("scene.png")
		# Jump, held: launch, rise, apex, fall, landing.
		170:
			Input.action_press("jump")
			_film("jump", 0, 3, 22)
		185:
			Input.action_release("jump")
		# Wings: a second jump near the top of the first.
		250:
			Input.action_press("jump")
			_film("double_jump", 0, 3, 24)
		258:
			Input.action_release("jump")
		274:
			Input.action_press("jump")
		290:
			Input.action_release("jump")
		# Dash on the ground.
		340:
			Input.action_press("dash")
			_film("dash", 0, 1, 14)
		342:
			Input.action_release("dash")
		# The sword.
		380:
			Input.action_press("attack")
			_film("attack", 0, 1, 18)
		382:
			Input.action_release("attack")
		# Holy Light.
		420:
			Input.action_press("cast")
			_film("cast", 0, 1, 15)
			_place(AIR_START)
		422:
			Input.action_release("cast")
		# Smite: jump, then down and attack.
		460:
			Input.action_press("jump")
			_film("slam", 10, 2, 22)
		470:
			Input.action_release("jump")
			Input.action_press("move_down")
			Input.action_press("attack")
		472:
			Input.action_release("attack")
		490:
			Input.action_release("move_down")
		# Wall grip and the jump off the wall.
		520:
			_place(WALL_START)
			Input.action_press("move_right")
			_film("wall", 6, 3, 20)
		560:
			Input.action_press("jump")
		563:
			Input.action_release("jump")
			Input.action_release("move_right")
		# Glide: dropped from high up, jump pressed on the way down.
		600:
			_place(AIR_START + Vector2(0.0, -160.0))
			_film("glide", 8, 3, 20)
		610:
			Input.action_press("jump")
		670:
			Input.action_release("jump")
			_place(AIR_START)
		# Hurt, then the healing.
		700:
			player.take_damage(1, player.global_position + Vector2(20.0, 0.0))
			_film("hurt", 0, 2, 12)
		760:
			Input.action_press("heal")
			_film("heal", 0, 3, 16)
		762:
			Input.action_release("heal")
		# Death.
		830:
			player.take_damage(99, player.global_position + Vector2(20.0, 0.0))
			_film("death", 0, 3, 24)
	_record()
	if _ticks > 910:
		quit()
		return true
	return false


func _place(spot: Vector2) -> void:
	var player: Node = _main.player
	player.global_position = spot
	player.velocity = Vector2.ZERO
	player.reset_physics_interpolation()
	_main.camera.snap_to_target()


## Films `count` crops of the hero every `every` ticks, starting `delay`
## ticks from now.
func _film(film_name: String, delay: int, every: int, count: int) -> void:
	_films[film_name] = {
		"next": _ticks + delay, "every": every, "count": count, "taken": 0, "frames": []
	}


func _record() -> void:
	for film_name: String in _films.keys():
		var film: Dictionary = _films[film_name]
		if int(film["taken"]) >= int(film["count"]) or _ticks < int(film["next"]):
			continue
		# Booked before the crop, which waits for a drawn frame: the render
		# can lag the physics, so the next tick may come first.
		film["next"] = int(film["next"]) + int(film["every"])
		film["taken"] = int(film["taken"]) + 1
		var frames: Array = film["frames"]
		frames.append(await _crop())
		if frames.size() == int(film["count"]):
			_save_film(film_name, frames)


## The viewport around the hero's feet, at the window's scale.
func _crop() -> Image:
	await process_frame
	var image := root.get_viewport().get_texture().get_image()
	var player: Node = _main.player
	var scale := float(image.get_width()) / root.get_visible_rect().size.x
	var feet: Vector2 = player.get_global_transform_with_canvas().origin * scale
	var rect := Rect2i(
		Vector2i(feet) + Vector2i(CROP.position) * int(scale), Vector2i(CROP.size) * int(scale)
	)
	var crop := Image.create(rect.size.x, rect.size.y, false, image.get_format())
	crop.fill(Color.BLACK)
	var src := rect.intersection(Rect2i(Vector2i.ZERO, image.get_size()))
	crop.blit_rect(image, src, src.position - rect.position)
	return crop


func _save_film(film_name: String, frames: Array) -> void:
	var w: int = frames[0].get_width()
	var h: int = frames[0].get_height()
	var gap := 4
	var columns := mini(frames.size(), FRAMES_PER_ROW)
	var rows := ceili(float(frames.size()) / FRAMES_PER_ROW)
	var strip := Image.create(
		(w + gap) * columns - gap, (h + gap) * rows - gap, false, frames[0].get_format()
	)
	strip.fill(Color(0.16, 0.17, 0.2))
	for i in frames.size():
		var at := Vector2i((i % FRAMES_PER_ROW) * (w + gap), (i / FRAMES_PER_ROW) * (h + gap))
		strip.blit_rect(frames[i], Rect2i(0, 0, w, h), at)
	var path := _out_dir.path_join(film_name + ".png")
	strip.save_png(path)
	print("saved ", path, " ", strip.get_size())


func _save_scene(file_name: String) -> void:
	await process_frame
	var image := root.get_viewport().get_texture().get_image()
	var path := _out_dir.path_join(file_name)
	image.save_png(path)
	print("saved ", path, " ", image.get_size())


## Draws the hero's shapes over him: the body in green, the attack hitbox
## in red while it is enabled.
class BoxDraw:
	extends Node2D

	func _ready() -> void:
		z_index = 100

	func _process(_delta: float) -> void:
		queue_redraw()

	func _draw() -> void:
		for shape: CollisionShape2D in [
			get_parent().get_node("CollisionShape2D"),
			get_parent().get_node("AttackHitbox/CollisionShape2D"),
		]:
			if shape.disabled:
				continue
			var rect := shape.shape as RectangleShape2D
			var at := shape.global_position - global_position
			var color := Color.RED if shape.get_parent() is Area2D else Color.GREEN
			draw_rect(Rect2(at - rect.size / 2.0, rect.size), color, false, 1.0)
