extends SceneTree

## Renders the title screen, the credits, the four pages of the settings
## screen and the pause menu and saves screenshots. Needs a display (or a virtual one):
##
##   xvfb-run -a godot --rendering-driver opengl3 --path . \
##       --script tools/menu_screenshot.gd -- /tmp/shots
##
## The steps are ten ticks apart: a slow software renderer runs several
## physics ticks per frame, and a step has to be drawn before the next.

var _ticks := 0
var _out_dir := "/tmp"
var _title: Node
var _main: Node


func _initialize() -> void:
	var args := OS.get_cmdline_user_args()
	if args.size() > 0:
		_out_dir = args[0]
	DirAccess.make_dir_recursive_absolute(_out_dir)
	_title = load("res://scenes/title.tscn").instantiate()
	root.add_child(_title)


func _physics_process(_delta: float) -> bool:
	_ticks += 1
	if _ticks == 10:
		_save("title.png")
	if _ticks == 12:
		_title.get_node("Menu/Credits").pressed.emit()
	if _ticks == 22:
		_save("credits.png")
	if _ticks == 24:
		_title.get_node("Credits/Back").pressed.emit()
		_title.get_node("Menu/Settings").pressed.emit()
	if _ticks == 34:
		_save("settings_audio.png")
	if _ticks == 36:
		_settings_menu().get_node("Tabs/Display").pressed.emit()
	if _ticks == 46:
		_save("settings_display.png")
	if _ticks == 48:
		_settings_menu().get_node("Tabs/Controls").pressed.emit()
	if _ticks == 58:
		_save("settings_controls.png")
	if _ticks == 60:
		_settings_menu().get_node("Tabs/Game").pressed.emit()
	if _ticks == 70:
		_save("settings_game.png")
	if _ticks == 72:
		# The pause menu, over the start of the world.
		root.remove_child(_title)
		_title.free()
		_main = load("res://scenes/main.tscn").instantiate()
		root.add_child(_main)
	if _ticks == 120:
		_main.pause_menu.open()
	if _ticks == 130:
		_save("pause.png")
	if _ticks > 136:
		quit()
		return true
	return false


## Typed as a Node: naming the SettingsMenu class here would compile the
## game scripts before the autoloads they use exist.
func _settings_menu() -> Node:
	return _title.get_node("SettingsMenu")


func _save(name: String) -> void:
	await process_frame
	var image := root.get_viewport().get_texture().get_image()
	var path := _out_dir.path_join(name)
	image.save_png(path)
	print("saved ", path, " ", image.get_size())
