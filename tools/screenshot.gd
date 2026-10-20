extends SceneTree

## Renders a few frames of the level and saves screenshots. Needs a display
## (or a virtual one):
##
##   xvfb-run -a godot --rendering-driver opengl3 --path . \
##       --script tools/screenshot.gd -- /tmp/shots
##
## The hero is teleported to a few spots so each shot shows a different
## part of the level.

var _ticks := 0
var _main: Node
var _out_dir := "/tmp"
var _shots := [
	{"tick": 40, "name": "start.png", "x": -1.0},
	{"tick": 90, "name": "spikes.png", "x": 36 * 16 + 8.0, "y": 12 * 16.0},
	{"tick": 140, "name": "checkpoint.png", "x": 57 * 16 + 8.0, "y": 12 * 16.0},
	{"tick": 190, "name": "goal.png", "x": 88 * 16 + 8.0, "y": 11 * 16.0},
	# Next to Mereth, with her first line typed out: the hero is put there
	# early, interact is pressed on arrival and the typing is given time.
	{
		"tick": 340,
		"name": "dialogue.png",
		"x": 13 * 16 - 20.0,
		"y": 12 * 16.0,
		"lead": 120,
		"talk": true
	},
	# On the ledge left of the Kept Light: the hero walks into it and its
	# description opens with the sixth heart in the HUD.
	{"tick": 440, "name": "upgrade.png", "x": 1 * 16 + 8.0, "y": 6 * 16.0, "lead": 70},
	# Taking the Lit Blade, with the guard and the wall of light ahead.
	{"tick": 490, "name": "power_up.png", "x": 70 * 16 + 8.0, "y": 12 * 16.0, "lead": 20},
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
		var lead: int = shot.get("lead", 30)
		if _ticks == shot["tick"] - lead and shot["x"] >= 0.0:
			# A box left open by the previous shot would hold the hero still.
			while _main.dialogue_box.is_open():
				_main.dialogue_box.advance()
			_main.player.global_position = Vector2(shot["x"], shot["y"])
			_main.camera.snap_to_target()
		if _ticks == shot["tick"] - lead + 30 and shot.has("talk"):
			Input.action_release("move_right")
			_press("interact", true)
		if _ticks == shot["tick"] - lead + 32 and shot.has("talk"):
			_press("interact", false)
		if _ticks == shot["tick"] - lead and shot["name"] == "upgrade.png":
			# The dialogue shot let go of the stick; walk again.
			Input.action_press("move_right")
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
