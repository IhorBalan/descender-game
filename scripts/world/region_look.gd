class_name RegionLook
extends Resource

## The look of a region as a resource (resources/regions/<key>.tres),
## exported on its WorldRegion as `look`: the tint on the screen while the
## hero is there, the sky's two colours when the region has its own (else
## the level's, see Levels.sky), whether it is a cave (flat dark streaks
## instead of clouds), the rim light the sky throws on the tiles, the
## colour grade and the music loop. Each is a typed field with a default
## that leaves the level alone, picked in the inspector.
##
## SceneWorld reads the resource into the fields a map's export carries for
## a region (`ambient`, `sky_top`, `sky_bottom`, `cave`, `light`, `grade`,
## `music`; see README, "The world"), so the game (LookBlend, Main) sees one
## kind of region whether it came from a scene or a download.

const DIR := "res://resources/regions"

## The colour that tints the screen while the hero is in the region.
@export var ambient := Color(0.76, 0.8, 0.9)
@export_group("Sky")
## Whether the region has sky colours of its own, below.
@export var own_sky := false
@export var sky_top := Color(0.4, 0.6, 0.9)
@export var sky_bottom := Color(0.8, 0.85, 0.95)
## Flat dark streaks instead of clouds and ridges.
@export var cave := false
@export_group("Light")
## The rim light the sky throws on the tiles; transparent for the sky top.
@export var light := Color(0.0, 0.0, 0.0, 0.0)
@export_group("Grade")
## A colour the view is multiplied by.
@export var tint := Color.WHITE
@export_range(0.0, 3.0) var contrast := 1.0
@export_range(0.0, 3.0) var saturation := 1.0
@export_group("Sound")
## The music loop (see tools/gen_music.py); empty for the region's key.
@export var music := ""


## The look as the fields of an exported region, only those that are set.
func to_spec() -> Dictionary:
	var spec := {"ambient": "#" + ambient.to_html(false)}
	if own_sky:
		spec["sky_top"] = "#" + sky_top.to_html(false)
		spec["sky_bottom"] = "#" + sky_bottom.to_html(false)
	if cave:
		spec["cave"] = true
	if light.a > 0.0:
		spec["light"] = "#" + light.to_html(false)
	var grade := {}
	if tint != Color.WHITE:
		grade["tint"] = "#" + tint.to_html(false)
	if not is_equal_approx(contrast, 1.0):
		grade["contrast"] = contrast
	if not is_equal_approx(saturation, 1.0):
		grade["saturation"] = saturation
	if not grade.is_empty():
		spec["grade"] = grade
	if not music.is_empty():
		spec["music"] = music
	return spec


## A look from an exported region's fields (see to_spec).
static func from_spec(spec: Dictionary) -> RegionLook:
	var look := RegionLook.new()
	look.ambient = _color(spec.get("ambient"), look.ambient)
	look.own_sky = spec.has("sky_top") or spec.has("sky_bottom")
	look.sky_top = _color(spec.get("sky_top"), look.sky_top)
	look.sky_bottom = _color(spec.get("sky_bottom"), look.sky_bottom)
	look.cave = bool(spec.get("cave", false))
	look.light = _color(spec.get("light"), look.light)
	var grade: Variant = spec.get("grade", {})
	if grade is Dictionary:
		look.tint = _color(grade.get("tint"), Color.WHITE)
		look.contrast = clampf(float(grade.get("contrast", 1.0)), 0.0, 3.0)
		look.saturation = clampf(float(grade.get("saturation", 1.0)), 0.0, 3.0)
	look.music = str(spec.get("music", ""))
	return look


## Saves `look` as the region's resource file, resources/regions/<key>.tres,
## and returns it with that path, or null when it could not be written.
static func write(key: String, look: RegionLook) -> RegionLook:
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(DIR))
	var path := "%s/%s.tres" % [DIR, key]
	look.take_over_path(path)
	if ResourceSaver.save(look, path) != OK:
		return null
	return look


static func _color(value: Variant, fallback: Color) -> Color:
	if value is String and Color.html_is_valid(value):
		return Color.html(value)
	return fallback
