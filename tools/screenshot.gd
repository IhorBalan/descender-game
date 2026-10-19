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
	if _ticks == 5:
		Input.action_press("move_right")
	for shot in _shots:
		if _ticks == shot["tick"] - 30 and shot["x"] >= 0.0:
			_main.player.global_position = Vector2(shot["x"], shot["y"])
			_main.camera.global_position = _main.player.global_position
			_main.camera.reset_smoothing()
		if _ticks == shot["tick"]:
			_save(shot["name"])
	if _ticks > _shots[-1]["tick"] + 5:
		quit()
		return true
	return false


func _save(name: String) -> void:
	await process_frame
	var image := root.get_viewport().get_texture().get_image()
	var path := _out_dir.path_join(name)
	image.save_png(path)
	print("saved ", path, " ", image.get_size())
