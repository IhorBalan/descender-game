extends SceneTree

## Renders every region scene under scenes/world/ (see WorldRegion) to a
## PNG, each framed to fit the window, the way the editor shows it, with
## the rooms, doors, markers and placements drawn over the tiles. A region
## with several areas also gets one shot per area, closer in. Needs a
## display (or a virtual one):
##
##   xvfb-run -a -s "-screen 0 2560x1440x24" godot --rendering-driver opengl3 \
##       --path . --script tools/region_screenshot.gd -- /tmp/regions
##
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
	# The window as it is, not the game's 320 x 180 canvas scaled up, and
	# the overlays the editor draws.
	root.content_scale_mode = Window.CONTENT_SCALE_MODE_DISABLED
	RenderingServer.set_default_clear_color(Color(0.05, 0.05, 0.09))
	WorldRegion.show_overlays = true
	_run()


func _run() -> void:
	var names: Array[String] = []
	for file in DirAccess.get_files_at(SCENES_DIR):
		if file.ends_with(".tscn") and file != "world.tscn" and (_only.is_empty() or file == _only):
			names.append(file)
	names.sort()
	for file in names:
		await _shoot(file)
	quit()


func _shoot(file: String) -> void:
	var region: WorldRegion = load(SCENES_DIR.path_join(file)).instantiate()
	# The scene stands on its own here, at the origin.
	region.position = Vector2.ZERO
	root.add_child(region)
	await process_frame
	var camera := Camera2D.new()
	region.add_child(camera)
	camera.make_current()
	var tile := 16
	var whole := Rect2(Vector2.ZERO, Vector2(480, region.rows) * tile)
	await _save(camera, whole, file.get_basename() + ".png")
	var areas := {}
	for room in region.rooms():
		var rect := room.rect()
		areas[room.area] = rect if not areas.has(room.area) else areas[room.area].merge(rect)
	if areas.size() > 1:
		for area in areas:
			await _save(camera, areas[area], "%s_area%d.png" % [file.get_basename(), area])
	root.remove_child(region)
	region.queue_free()


## Frames the rect and saves the window.
func _save(camera: Camera2D, rect: Rect2, name: String) -> void:
	var size := Vector2(root.size)
	var fit := minf(size.x / rect.size.x, size.y / (rect.size.y + 40.0))
	camera.zoom = Vector2.ONE * fit
	camera.position = rect.get_center() - Vector2(0.0, 20.0)
	camera.reset_smoothing()
	for i in FRAMES_TO_SETTLE:
		await process_frame
	var image := root.get_viewport().get_texture().get_image()
	var path := _out_dir.path_join(name)
	image.save_png(path)
	print("saved ", path, " ", image.get_size())
