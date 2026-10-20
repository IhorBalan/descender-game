extends SceneTree

## Renders a few frames of the level and saves screenshots. Needs a display
## (or a virtual one):
##
##   xvfb-run -a godot --rendering-driver opengl3 --path . \
##       --script tools/screenshot.gd -- /tmp/shots [level id]
##
## The hero is teleported to a few spots so each shot shows a different
## part of the level. Without a level id the shots are of Heaven 1; with
## `earth_hub` they are of the Earth hub.

var _ticks := 0
var _main: Node
var _out_dir := "/tmp"
var _shots: Array = []
var _heaven_shots := [
	{"tick": 40, "name": "start.png", "x": -1.0},
	{"tick": 90, "name": "spikes.png", "x": 36 * 16 + 8.0, "y": 12 * 16.0},
	{"tick": 140, "name": "checkpoint.png", "x": 57 * 16 + 8.0, "y": 12 * 16.0},
	{"tick": 190, "name": "goal.png", "x": 88 * 16 + 8.0, "y": 11 * 16.0},
	# Next to Mereth, with her first line typed out: the hero is put there
	# early and stopped, interact is pressed and the typing is given time.
	{"tick": 340, "name": "dialogue.png", "x": 13 * 16 - 14.0, "y": 12 * 16.0, "lead": 120},
]
var _hub_shots := [
	{"tick": 40, "name": "hub_start.png", "x": -1.0},
	{"tick": 90, "name": "hub_barricade.png", "x": 41 * 16 + 8.0, "y": 11 * 16.0},
	{"tick": 140, "name": "hub_church.png", "x": 76 * 16 + 8.0, "y": 11 * 16.0},
	{"tick": 290, "name": "hub_pell.png", "x": 11 * 16 - 14.0, "y": 10 * 16.0, "lead": 120},
]


func _initialize() -> void:
	var args := OS.get_cmdline_user_args()
	if args.size() > 0:
		_out_dir = args[0]
	_shots = _heaven_shots
	if args.size() > 1:
		_shots = _hub_shots if args[1] == "earth_hub" else _heaven_shots
		root.get_node("SaveManager").pending = {"level": args[1], "flags": []}
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
			_main.player.global_position = Vector2(shot["x"], shot["y"])
			_main.player.velocity = Vector2.ZERO
			_main.camera.snap_to_target()
			if shot.has("lead"):
				Input.action_release("move_right")
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
