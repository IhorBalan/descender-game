@tool
extends VBoxContainer

## The Props dock's panel (see plugin.gd): the gallery, the preview, the
## placing state and the new-prop dialog.

const THUMB := 64
const PREVIEW := 160
const GHOST_ALPHA := 0.6
## How the gallery's cells are sized: icon plus a line of name.
const CELL := Vector2(THUMB + 24, THUMB + 28)

var plugin: EditorPlugin

var _key := ""
var _names := PackedStringArray()
var _armed := ""
var _flip := false
var _mouse := Vector2.ZERO
var _in_view := false
var _thumbs := {}

var _regions: OptionButton
var _search: LineEdit
var _list: ItemList
var _preview: TextureRect
var _preview_name: Label
var _hint: Label
var _flip_button: CheckButton
var _new_dialog: EditorFileDialog


func _init() -> void:
	custom_minimum_size = Vector2(220, 0)
	var top := HBoxContainer.new()
	add_child(top)
	_regions = OptionButton.new()
	_regions.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	_regions.tooltip_text = "The region whose props are shown (the open scene's by itself)"
	_regions.item_selected.connect(_on_region_picked)
	top.add_child(_regions)
	var rescan := Button.new()
	rescan.text = "Rescan"
	rescan.tooltip_text = "Read the props folders again"
	rescan.pressed.connect(_rescan)
	top.add_child(rescan)
	_search = LineEdit.new()
	_search.placeholder_text = "Search props"
	_search.clear_button_enabled = true
	_search.text_changed.connect(func(_text: String) -> void: _fill())
	add_child(_search)
	_list = ItemList.new()
	_list.icon_mode = ItemList.ICON_MODE_TOP
	_list.max_columns = 0
	_list.same_column_width = true
	_list.fixed_column_width = int(CELL.x)
	_list.fixed_icon_size = Vector2i(THUMB, THUMB)
	_list.auto_height = false
	_list.size_flags_vertical = Control.SIZE_EXPAND_FILL
	_list.custom_minimum_size = Vector2(0, CELL.y * 2)
	_list.item_selected.connect(_on_item_selected)
	_list.item_activated.connect(_on_item_activated)
	_list.empty_clicked.connect(func(_at: Vector2, _button: int) -> void: _disarm())
	add_child(_list)
	var preview_box := CenterContainer.new()
	preview_box.custom_minimum_size = Vector2(0, PREVIEW + 8)
	add_child(preview_box)
	_preview = TextureRect.new()
	_preview.custom_minimum_size = Vector2(PREVIEW, PREVIEW)
	_preview.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
	_preview.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	_preview.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	preview_box.add_child(_preview)
	_preview_name = Label.new()
	_preview_name.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	add_child(_preview_name)
	var buttons := HBoxContainer.new()
	add_child(buttons)
	_flip_button = CheckButton.new()
	_flip_button.text = "Flip"
	_flip_button.tooltip_text = "Place the prop mirrored"
	_flip_button.toggled.connect(func(on: bool) -> void: _flip = on)
	buttons.add_child(_flip_button)
	var spacer := Control.new()
	spacer.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	buttons.add_child(spacer)
	var open := Button.new()
	open.text = "Open"
	open.tooltip_text = "Open the picked prop's scene to edit every copy"
	open.pressed.connect(_open_picked)
	buttons.add_child(open)
	var new_prop := Button.new()
	new_prop.text = "New from PNG"
	new_prop.tooltip_text = "Make a prop scene from a PNG in the region's assets/props folder"
	new_prop.pressed.connect(_pick_png)
	buttons.add_child(new_prop)
	_hint = Label.new()
	_hint.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	_hint.add_theme_color_override("font_color", Color(1, 1, 1, 0.6))
	add_child(_hint)
	_new_dialog = EditorFileDialog.new()
	_new_dialog.file_mode = EditorFileDialog.FILE_MODE_OPEN_FILE
	_new_dialog.access = EditorFileDialog.ACCESS_RESOURCES
	_new_dialog.filters = PackedStringArray(["*.png ; PNG images"])
	_new_dialog.title = "New prop from a PNG"
	_new_dialog.file_selected.connect(_make_prop)
	add_child(_new_dialog)
	_rescan()


## Follows the editor to the region whose scene is open.
func on_scene_changed(root: Node) -> void:
	_disarm()
	var region := root as WorldRegion
	if region == null and root != null:
		region = WorldRegion.of(root)
	if region:
		_pick_region(EdgeDecor.region_key(region.region_name))
	elif root is WorldScene:
		_set_hint("Open a region's own scene to place props; world.tscn only shows them.")
	_update_hint()


## A click in the 2D view with a prop armed drops one at the mouse.
func canvas_input(event: InputEvent) -> bool:
	if event is InputEventMouseMotion:
		_mouse = event.position
		_in_view = true
		if not _armed.is_empty():
			plugin.update_overlays()
		return false
	if (
		event is InputEventKey
		and event.pressed
		and event.keycode == KEY_ESCAPE
		and not _armed.is_empty()
	):
		_disarm()
		return true
	var click := event as InputEventMouseButton
	if click == null or not click.pressed or _armed.is_empty():
		return false
	if click.button_index == MOUSE_BUTTON_RIGHT:
		_disarm()
		return true
	if click.button_index != MOUSE_BUTTON_LEFT:
		return false
	_mouse = click.position
	if _place(click.position) and not click.shift_pressed:
		_disarm()
	return true


## The armed prop's art under the mouse, as it will land.
func draw_ghost(overlay: Control) -> void:
	if _armed.is_empty() or not _in_view:
		return
	var texture := _thumb(_armed, 0)
	var region := _region()
	if texture == null or region == null:
		return
	var world := region.get_parent() as WorldScene
	var tile := world.tile if world else 16
	var art := world.art_tile if world else 64
	var transform := EditorInterface.get_editor_viewport_2d().global_canvas_transform
	var zoom := transform.get_scale().x
	var size := texture.get_size() * (float(tile) / float(art)) * zoom
	var at := _snap_view(_mouse, transform)
	# A negative width draws the art mirrored, as a scale of -1 does.
	var rect := Rect2(at, Vector2(-size.x if _flip else size.x, size.y))
	overlay.draw_texture_rect(texture, rect, false, Color(1, 1, 1, GHOST_ALPHA))
	overlay.draw_rect(rect.abs(), Color(1, 1, 1, 0.5), false, 1.0)


func _rescan() -> void:
	var keys := PropScene.regions()
	var was := _key
	_regions.clear()
	for key in keys:
		_regions.add_item(key.capitalize())
		_regions.set_item_metadata(_regions.item_count - 1, key)
	_thumbs.clear()
	if was in keys:
		_pick_region(was)
	elif not keys.is_empty():
		_pick_region(keys[0])
	else:
		_key = ""
		_names = PackedStringArray()
		_fill()


func _pick_region(key: String) -> void:
	_key = key
	for i in _regions.item_count:
		if _regions.get_item_metadata(i) == key:
			_regions.select(i)
	_names = PropScene.names(key)
	_fill()


func _on_region_picked(index: int) -> void:
	_disarm()
	_pick_region(str(_regions.get_item_metadata(index)))


## The gallery: every prop of the region whose name holds the search text.
func _fill() -> void:
	_list.clear()
	var needle := _search.text.strip_edges().to_lower()
	for name in _names:
		if not needle.is_empty() and not name.to_lower().contains(needle):
			continue
		_list.add_item(name.capitalize(), _thumb(name, THUMB))
		_list.set_item_metadata(_list.item_count - 1, name)
		_list.set_item_tooltip(_list.item_count - 1, PropScene.scene_path(_key, name))
	if _armed.is_empty() or _armed not in _names:
		_show(_armed if _armed in _names else "")


## A prop's art scaled to fit `size` (0 for the art as it is), cached.
func _thumb(name: String, size: int) -> Texture2D:
	var cache_key := "%s/%s@%d" % [_key, name, size]
	if not _thumbs.has(cache_key):
		_thumbs[cache_key] = PropScene.thumbnail(_key, name, size if size > 0 else 1 << 14)
	return _thumbs[cache_key]


func _on_item_selected(index: int) -> void:
	_armed = str(_list.get_item_metadata(index))
	_show(_armed)
	_update_hint()
	plugin.update_overlays()


func _on_item_activated(index: int) -> void:
	_armed = str(_list.get_item_metadata(index))
	_open_picked()


func _show(name: String) -> void:
	_preview.texture = _thumb(name, PREVIEW) if not name.is_empty() else null
	_preview_name.text = name.capitalize() if not name.is_empty() else ""


func _disarm() -> void:
	_armed = ""
	_list.deselect_all()
	_update_hint()
	if plugin:
		plugin.update_overlays()


func _update_hint() -> void:
	if _region() == null:
		_set_hint("Open a region scene (scenes/world/) to place props.")
	elif _armed.is_empty():
		_set_hint("Pick a prop, then click in the 2D view to place it under Props.")
	else:
		_set_hint(
			(
				"Click to place %s. Shift keeps placing; Esc or right click stops."
				% _armed.capitalize()
			)
		)


func _set_hint(text: String) -> void:
	_hint.text = text


## The region of the scene being edited, or null.
func _region() -> WorldRegion:
	var root := EditorInterface.get_edited_scene_root()
	if root is WorldRegion:
		return root
	return WorldRegion.of(root) if root else null


## The mouse in the 2D view snapped to a whole level pixel, back in the view.
func _snap_view(at: Vector2, transform: Transform2D) -> Vector2:
	var world := (transform.affine_inverse() * at).round()
	return transform * world


## Drops an instance of the armed prop under the region's Props node at the
## view position `at`, through undo/redo, and selects it.
func _place(at: Vector2) -> bool:
	var region := _region()
	if region == null:
		return false
	var scene := load(PropScene.scene_path(_key, _armed)) as PackedScene
	if scene == null:
		return false
	var root := EditorInterface.get_edited_scene_root()
	var transform := EditorInterface.get_editor_viewport_2d().global_canvas_transform
	var world := (transform.affine_inverse() * at).round()
	var holder := region.get_node_or_null("Props")
	var undo := plugin.get_undo_redo()
	undo.create_action("Place prop %s" % _armed.capitalize())
	if holder == null:
		holder = Node2D.new()
		holder.name = "Props"
		undo.add_do_method(region, "add_child", holder)
		undo.add_do_method(region, "move_child", holder, _props_index(region))
		undo.add_do_property(holder, "owner", root)
		undo.add_do_reference(holder)
		undo.add_undo_method(region, "remove_child", holder)
	var prop := scene.instantiate()
	prop.name = _free_name(holder, _armed.to_pascal_case())
	prop.position = region.to_local(world)
	if _flip:
		prop.scale = Vector2(-1.0, 1.0)
	undo.add_do_method(holder, "add_child", prop, true)
	undo.add_do_property(prop, "owner", root)
	undo.add_do_reference(prop)
	undo.add_undo_method(holder, "remove_child", prop)
	undo.add_do_method(self, "_select", prop)
	undo.add_undo_method(self, "_select", holder)
	undo.commit_action()
	return true


## Where a new Props holder goes: after Tiles, before Fringes and the rest.
static func _props_index(region: WorldRegion) -> int:
	var tiles := region.get_node_or_null("Tiles")
	return tiles.get_index() + 1 if tiles else region.get_child_count()


static func _free_name(holder: Node, base: String) -> String:
	var name := base
	var n := 2
	while holder and holder.has_node(name):
		name = "%s%d" % [base, n]
		n += 1
	return name


func _select(node: Node) -> void:
	if node == null or not node.is_inside_tree():
		return
	var selection := EditorInterface.get_selection()
	selection.clear()
	selection.add_node(node)
	EditorInterface.mark_scene_as_unsaved()


func _open_picked() -> void:
	if _armed.is_empty():
		return
	EditorInterface.open_scene_from_path(PropScene.scene_path(_key, _armed))


func _pick_png() -> void:
	if _key.is_empty():
		return
	_new_dialog.current_dir = PropScene.art_dir(_key)
	_new_dialog.popup_centered_ratio(0.6)


## Writes a prop scene for a PNG of the region's props folder and shows it.
func _make_prop(path: String) -> void:
	var dir := PropScene.art_dir(_key)
	if path.get_base_dir() != dir:
		_set_hint("Put the PNG in %s first (with <name>_n.png and <name>_glow.png if any)." % dir)
		return
	var name := path.get_file().get_basename()
	if name.ends_with("_n") or name.ends_with("_glow"):
		_set_hint("Pick the prop's own PNG, not its normal map or glow.")
		return
	var region := _region()
	var world := region.get_parent() as WorldScene if region else null
	var art := world.art_tile if world else 64
	var tile := world.tile if world else 16
	var written := PropScene.write(_key, name, art, tile)
	if written.is_empty():
		_set_hint("%s could not be written; is the PNG imported?" % name)
		return
	EditorInterface.get_resource_filesystem().scan()
	_thumbs.clear()
	_names = PropScene.names(_key)
	_armed = name
	_fill()
	for i in _list.item_count:
		if _list.get_item_metadata(i) == name:
			_list.select(i)
	_show(name)
	_update_hint()
