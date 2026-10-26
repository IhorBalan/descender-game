extends SceneTree

## Renders the developer console and the `show_boxes` overlay and saves
## screenshots. Needs a display (or a virtual one):
##
##   xvfb-run -a godot --rendering-driver opengl3 --path . \
##       --script tools/console_screenshot.gd -- /tmp/shots
##
## The hero is put by the Lit Blade, where a guard waits ahead and the
## power-up floats over the floor, so one view holds every colour: the
## console is opened and `help` typed for the first shot, `show_boxes on`
## for the second, and the third shows the boxes with the console closed
## and the hero walking on.

const SPOT := Vector2(157 * 16 + 8.0, 15 * 16.0)

var _ticks := 0
var _out_dir := "/tmp"
var _main: Node
## The DevConsole autoload, looked up by name: a --script run compiles
## before the autoloads are known.
var _console: Node


func _initialize() -> void:
	var args := OS.get_cmdline_user_args()
	if args.size() > 0:
		_out_dir = args[0]
	DirAccess.make_dir_recursive_absolute(_out_dir)
	_main = load("res://scenes/main.tscn").instantiate()
	root.add_child(_main)
	_console = root.get_node("DevConsole")


func _physics_process(_delta: float) -> bool:
	_ticks += 1
	match _ticks:
		2:
			Story.set_flag("opening_seen")
		20:
			_main.player.global_position = SPOT
			_main.player.velocity = Vector2.ZERO
			_main.camera.snap_to_target()
		40:
			_console.open()
			_type("help")
		50:
			_save("console_help.png")
		52:
			_type("show_boxes on")
		62:
			_save("console_show_boxes.png")
		64:
			_console.close()
			Input.action_press("move_right")
		84:
			_save("show_boxes.png")
		86:
			Input.action_release("move_right")
			_type_closed("show_boxes off")
		96:
			_save("show_boxes_off.png")
		100:
			quit()
			return true
	return false


## Types a line into the open console and submits it.
func _type(line: String) -> void:
	_console.input.text = line
	_console.input.text_submitted.emit(line)


func _type_closed(line: String) -> void:
	_console.open()
	_type(line)
	_console.close()


func _save(name: String) -> void:
	await process_frame
	var image := root.get_viewport().get_texture().get_image()
	var path := _out_dir.path_join(name)
	image.save_png(path)
	print("saved ", path, " ", image.get_size())
