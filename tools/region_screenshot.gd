extends SceneTree

## Renders every region scene under scenes/world/ (see RegionPreview) to a
## PNG, each framed to fit the window, the way the editor shows it. Needs a
## display (or a virtual one):
##
##   xvfb-run -a godot --rendering-driver opengl3 --resolution 1920x600 \
##       --path . --script tools/region_screenshot.gd -- /tmp/regions
##
## A region with several areas also gets one shot per area, closer in.
## A second argument narrows it to one scene file name (`03_limbo.tscn`).

const SCENES_DIR := "res://scenes/world"
const FRAMES_TO_SETTLE := 6

var _out_dir := "/tmp"
var _only := ""


func _initialize() -> void:
	var args := OS.get_cmdline_user_args()
	if args.size() > 0:
		_out_dir = args[0]
	if args.size() > 1:
		_only = args[1]
	DirAccess.make_dir_recursive_absolute(_out_dir)
	# The window as it is, not the game's 320 x 180 canvas scaled up.
	root.content_scale_mode = Window.CONTENT_SCALE_MODE_DISABLED
	RenderingServer.set_default_clear_color(Color(0.05, 0.05, 0.09))
	_run()


func _run() -> void:
	var names: Array[String] = []
	for file in DirAccess.get_files_at(SCENES_DIR):
		if file.ends_with(".tscn") and (_only.is_empty() or file == _only):
			names.append(file)
	names.sort()
	for file in names:
		await _shoot(file)
	quit()


func _shoot(file: String) -> void:
	var scene: Node2D = load(SCENES_DIR.path_join(file)).instantiate()
	root.add_child(scene)
	# The scene is readied, and the preview built, once the tree runs.
	await process_frame
	var preview := scene as RegionPreview
	var camera := Camera2D.new()
	scene.add_child(camera)
	camera.make_current()
	await _save(preview, camera, file.get_basename() + ".png")
	if preview and preview.map:
		var areas := {}
		for room in preview.rooms_shown():
			areas[int(room.get("area", 0))] = true
		if areas.size() > 1:
			for area in areas:
				preview.area = area
				await _save(preview, camera, "%s_area%d.png" % [file.get_basename(), area])
	root.remove_child(scene)
	scene.queue_free()


## Frames the cells on show and saves the window.
func _save(preview: RegionPreview, camera: Camera2D, name: String) -> void:
	var size := Vector2(root.size)
	if preview and preview.map:
		var shown := Vector2(preview.cells.size) * RegionPreview.TILE
		var fit := minf(size.x / shown.x, size.y / (shown.y + 40.0))
		camera.zoom = Vector2.ONE * fit
		camera.position = shown / 2.0 - Vector2(0.0, 20.0)
		camera.reset_smoothing()
	for i in FRAMES_TO_SETTLE:
		await process_frame
	var image := root.get_viewport().get_texture().get_image()
	var path := _out_dir.path_join(name)
	image.save_png(path)
	print("saved ", path, " ", image.get_size())
