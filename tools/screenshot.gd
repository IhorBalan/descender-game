extends SceneTree

## Renders a few frames of the level and saves screenshots. Needs a display
## (or a virtual one):
##
##   xvfb-run -a godot --rendering-driver opengl3 --path . \
##       --script tools/screenshot.gd -- /tmp/shots
##
## The hero is teleported to a few spots so each shot shows a different
## part of the world, one per region on the way down and the story beats
## of Paradise and the hub.

var _ticks := 0
var _main: Node
var _out_dir := "/tmp"
var _shots := [
	{"tick": 40, "name": "start.png", "x": -1.0},
	# Next to Mereth, with her first line typed out: the hero is put there
	# early and stopped, interact is pressed and the typing is given time.
	{"tick": 190, "name": "dialogue.png", "x": 84 * 16 - 14.0, "y": 15 * 16.0, "lead": 120},
	# The Lit Blade, with the guard ahead.
	{"tick": 240, "name": "power_up.png", "x": 157 * 16 + 8.0, "y": 15 * 16.0, "walk": 20},
	{"tick": 290, "name": "barrier.png", "x": 186 * 16 + 8.0, "y": 15 * 16.0, "walk": 20},
	{"tick": 340, "name": "checkpoint.png", "x": 240 * 16 + 8.0, "y": 49 * 16.0},
	# The Kept Light in the secret room: the hero walks into it and its
	# description opens with the sixth heart in the HUD.
	{"tick": 440, "name": "upgrade.png", "x": 279 * 16 + 8.0, "y": 15 * 16.0, "walk": 70},
	{"tick": 490, "name": "wings.png", "x": 400 * 16 + 8.0, "y": 49 * 16.0, "walk": 20},
	{"tick": 640, "name": "hub_pell.png", "x": 322 * 16 - 14.0, "y": 66 * 16.0, "lead": 120},
	{"tick": 690, "name": "hub_camp.png", "x": 300 * 16 + 8.0, "y": 83 * 16.0},
	{"tick": 740, "name": "purgatory.png", "x": 3352.0, "y": 1872.0},
	{"tick": 790, "name": "limbo.png", "x": 4072.0, "y": 2688.0},
	{"tick": 840, "name": "wrath.png", "x": 4552.0, "y": 4864.0},
	{"tick": 890, "name": "treachery.png", "x": 3592.0, "y": 7040.0},
	{"tick": 940, "name": "lust.png", "x": 2872.0, "y": 3232.0},
	{"tick": 990, "name": "gluttony.png", "x": 6984.0, "y": 3776.0},
	{"tick": 1040, "name": "greed.png", "x": 2152.0, "y": 4208.0},
	{"tick": 1090, "name": "heresy.png", "x": 5512.0, "y": 5408.0},
	{"tick": 1140, "name": "violence.png", "x": 3128.0, "y": 5952.0},
	{"tick": 1190, "name": "fraud.png", "x": 2152.0, "y": 6496.0},
]


func _initialize() -> void:
	var args := OS.get_cmdline_user_args()
	if args.size() > 0:
		_out_dir = args[0]
	DirAccess.make_dir_recursive_absolute(_out_dir)
	_main = load("res://scenes/main.tscn").instantiate()
	root.add_child(_main)


func _physics_process(_delta: float) -> bool:
	_ticks += 1
	if _ticks == 2:
		# Skip the opening dialogue, which would stop the hero at the start.
		Story.set_flag("opening_seen")
	if _ticks == 5:
		Input.action_press("move_right")
	for shot in _shots:
		# "lead" is a talk shot (stop, press interact, let the text type);
		# "walk" only gives the hero longer to walk before the shot.
		var lead: int = shot.get("lead", shot.get("walk", 30))
		if _ticks == shot["tick"] - lead and shot["x"] >= 0.0:
			# A box left open by the previous shot would hold the hero still.
			while _main.dialogue_box.is_open():
				_main.dialogue_box.advance()
			_main.player.global_position = Vector2(shot["x"], shot["y"])
			_main.player.velocity = Vector2.ZERO
			_main.camera.snap_to_target()
			# A talk shot stops the hero; a walk shot keeps him going (or
			# starts him again after a talk shot).
			if shot.has("lead"):
				Input.action_release("move_right")
			else:
				Input.action_press("move_right")
		if _ticks == shot["tick"] - lead + 30 and shot.has("lead"):
			_press("interact", true)
		if _ticks == shot["tick"] - lead + 32 and shot.has("lead"):
			_press("interact", false)
		if _ticks == shot["tick"]:
			_save(shot["name"])
	if _ticks > _shots[-1]["tick"] + 5:
		quit()
		return true
	return false


## Interact is read from input events, not from the action state, so it is
## sent as an event.
func _press(action: String, pressed: bool) -> void:
	var event := InputEventAction.new()
	event.action = action
	event.pressed = pressed
	Input.parse_input_event(event)


func _save(name: String) -> void:
	await process_frame
	var image := root.get_viewport().get_texture().get_image()
	var path := _out_dir.path_join(name)
	image.save_png(path)
	print("saved ", path, " ", image.get_size())
