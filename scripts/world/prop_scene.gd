class_name PropScene
extends RefCounted

## Writes a prop's scene from its PNGs (see WorldProp): scenes/props/<key>/
## <name>.tscn from godot_assets/props/<key>/<name>.png, with <name>_n.png as its
## normal map when there is one and <name>_glow.png as an unlit additive
## Glow sprite when there is one. The extractor (tools/extract_props.gd)
## and the editor's Props dock (addons/props_dock) both write props this
## way, so every prop scene has the same shape.

const SCENES_DIR := "res://scenes/props"
const ART_DIR := "res://godot_assets/props"
const WORLD_PROP := "res://scripts/world/world_prop.gd"


## The folder of a region's prop scenes.
static func scenes_dir(key: String) -> String:
	return SCENES_DIR.path_join(key)


## The folder of a region's prop PNGs.
static func art_dir(key: String) -> String:
	return ART_DIR.path_join(key)


## The scene of a region's prop by name, whether or not it exists yet.
static func scene_path(key: String, name: String) -> String:
	return scenes_dir(key).path_join(name + ".tscn")


## The names of a region's props: the scenes under its folder, sorted.
static func names(key: String) -> PackedStringArray:
	var found := PackedStringArray()
	var dir := scenes_dir(key)
	if DirAccess.dir_exists_absolute(dir):
		for file in DirAccess.get_files_at(dir):
			if file.ends_with(".tscn"):
				found.append(file.get_basename())
	found.sort()
	return found


## The region keys that have props.
static func regions() -> PackedStringArray:
	var found := PackedStringArray()
	if DirAccess.dir_exists_absolute(SCENES_DIR):
		found = DirAccess.get_directories_at(SCENES_DIR)
	found.sort()
	return found


## Writes the scene of prop `name` of region `key` from its PNGs, drawn at
## `art` pixels a tile onto a `tile`-pixel grid, to `path` (the prop's own
## scene path when empty). Returns the path written, or "" with the reason
## printed when the diffuse PNG is not an imported texture or the scene
## could not be saved.
static func write(key: String, name: String, art: int, tile: int, path := "") -> String:
	var dir := art_dir(key)
	var diffuse := load(dir.path_join(name + ".png")) as Texture2D
	if diffuse == null:
		printerr("%s/%s.png is not an imported texture" % [dir, name])
		return ""
	var normal_path := dir.path_join(name + "_n.png")
	var normal := load(normal_path) as Texture2D if ResourceLoader.exists(normal_path) else null
	var root := Node2D.new()
	root.name = name.to_pascal_case()
	root.set_script(load(WORLD_PROP))
	root.set("art_tile", art)
	var sprite := Sprite2D.new()
	sprite.name = "Art"
	if normal:
		var canvas := CanvasTexture.new()
		canvas.diffuse_texture = diffuse
		canvas.normal_texture = normal
		sprite.texture = canvas
	else:
		sprite.texture = diffuse
	sprite.centered = false
	sprite.scale = Vector2.ONE * float(tile) / float(art)
	sprite.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
	root.add_child(sprite)
	sprite.owner = root
	var glow_path := dir.path_join(name + "_glow.png")
	if ResourceLoader.exists(glow_path):
		var glow := Sprite2D.new()
		glow.name = "Glow"
		glow.texture = load(glow_path)
		glow.centered = false
		glow.scale = sprite.scale
		glow.texture_filter = CanvasItem.TEXTURE_FILTER_NEAREST
		var material := CanvasItemMaterial.new()
		material.light_mode = CanvasItemMaterial.LIGHT_MODE_UNSHADED
		material.blend_mode = CanvasItemMaterial.BLEND_MODE_ADD
		glow.material = material
		root.add_child(glow)
		glow.owner = root
	if path.is_empty():
		path = scene_path(key, name)
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(path.get_base_dir()))
	var packed := PackedScene.new()
	var error := packed.pack(root)
	if error == OK:
		error = ResourceSaver.save(packed, path)
	root.free()
	if error != OK:
		printerr("%s: could not be written (%s)" % [path, error_string(error)])
		return ""
	return path


## A prop's art as a thumbnail that fits `size` pixels, for a gallery;
## null when the PNG cannot be read.
static func thumbnail(key: String, name: String, size: int) -> ImageTexture:
	var texture := load(art_dir(key).path_join(name + ".png")) as Texture2D
	var image := texture.get_image() if texture else null
	if image == null or image.is_empty():
		return null
	if image.is_compressed():
		image.decompress()
	var scale_factor := minf(float(size) / image.get_width(), float(size) / image.get_height())
	if scale_factor < 1.0:
		image.resize(
			maxi(int(image.get_width() * scale_factor), 1),
			maxi(int(image.get_height() * scale_factor), 1),
			Image.INTERPOLATE_LANCZOS
		)
	return ImageTexture.create_from_image(image)
