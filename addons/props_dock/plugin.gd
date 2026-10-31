@tool
extends EditorPlugin

## The Props dock: a gallery of the props of the region whose scene is open
## (scenes/props/<key>/, see WorldProp and PropScene), with a search box,
## each prop shown as its art and the picked one shown large. Click a prop,
## then click in the 2D view to drop an instance under the region's `Props`
## node at the mouse (a ghost follows the mouse; Shift keeps placing, Esc
## or a right click stops). The dock also flips the next placement, makes
## a new prop from a PNG in godot_assets/props/<key>/ and opens a prop's scene.
##
## Props are placed in a region's own scene; world.tscn only shows them.

const DOCK := preload("res://addons/props_dock/dock.gd")

var _dock: Control


func _enter_tree() -> void:
	_dock = DOCK.new()
	_dock.name = "Props"
	_dock.plugin = self
	add_control_to_dock(DOCK_SLOT_RIGHT_UL, _dock)
	scene_changed.connect(_dock.on_scene_changed)
	_dock.on_scene_changed(EditorInterface.get_edited_scene_root())


func _exit_tree() -> void:
	if scene_changed.is_connected(_dock.on_scene_changed):
		scene_changed.disconnect(_dock.on_scene_changed)
	remove_control_from_docks(_dock)
	_dock.queue_free()
	_dock = null


## The dock places into any node of a region scene, so a click lands while
## the region, its Props holder or any prop is selected.
func _handles(object: Object) -> bool:
	return object is WorldRegion or (object is Node and WorldRegion.of(object) != null)


func _forward_canvas_gui_input(event: InputEvent) -> bool:
	return _dock != null and _dock.canvas_input(event)


func _forward_canvas_draw_over_viewport(overlay: Control) -> void:
	if _dock:
		_dock.draw_ghost(overlay)
