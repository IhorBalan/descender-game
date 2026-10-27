class_name LookBlend
extends RefCounted

## The look of the region the hero is in, eased towards over FADE seconds
## so crossing a region line reads as a change of weather, not a cut: the
## procedural sky's two colours and cave flag, the band the sky gradient
## spans, the rim light the sky throws on the tiles and the colour grade.
##
## The export's regions carry them (see the map editor, and RegionLook for
## a region of the game's own world): `sky_top` and `sky_bottom` colours,
## `cave`, `light` (the rim colour), `ambient`, a `grade` of tint, contrast
## and saturation and the `music` loop. Each is optional; a region without
## sky colours uses the level's own (see Levels.sky).

const FADE := 2.5
## How far a derived ambient tint sits below the sky colour, so a region
## with no ambient of its own is lit by its sky without glowing.
const DERIVED_AMBIENT := 0.6

var top := Color.BLACK
var bottom := Color.BLACK
var cave := 0.0
var band := Vector2(0.0, 1.0)
var rim := Color.WHITE
var tint := Color.WHITE
var contrast := 1.0
var saturation := 1.0

var _target := {}
var _from := {}
var _t := FADE


## The sky colours of a region: its own, else `fallback` (top, bottom).
static func sky_of(region: Dictionary, fallback: Array[Color]) -> Array[Color]:
	var top_fallback: Color = fallback[0] if fallback.size() > 0 else Color.BLACK
	var bottom_fallback: Color = (
		fallback[fallback.size() - 1] if fallback.size() > 0 else Color.BLACK
	)
	return [
		_color(region.get("sky_top"), top_fallback),
		_color(region.get("sky_bottom"), bottom_fallback),
	]


## The region's rim-light colour: its light tint, else its sky top.
static func rim_of(region: Dictionary, fallback: Array[Color]) -> Color:
	return _color(region.get("light"), sky_of(region, fallback)[0])


## The region's ambient tint: its own, else one derived from its sky.
static func ambient_of(region: Dictionary, fallback: Array[Color]) -> Color:
	if region.get("ambient") is String and Color.html_is_valid(str(region["ambient"])):
		return Color.html(str(region["ambient"]))
	var sky := sky_of(region, fallback)
	var mix := sky[1].lerp(sky[0], 0.5)
	mix.a = 1.0
	return mix.darkened(1.0 - DERIVED_AMBIENT)


## The region's music loop: its own, else its key (see EdgeDecor.region_key).
static func music_of(region: Dictionary) -> String:
	var own := str(region.get("music", ""))
	return own if not own.is_empty() else EdgeDecor.region_key(str(region.get("name", "")))


## The region's colour grade, every part optional: tint (a colour the view
## is multiplied by), contrast and saturation (1 leaves the view alone).
static func grade_of(region: Dictionary) -> Dictionary:
	var grade: Variant = region.get("grade", {})
	if not grade is Dictionary:
		grade = {}
	return {
		"tint": _color(grade.get("tint"), Color.WHITE),
		"contrast": clampf(float(grade.get("contrast", 1.0)), 0.0, 3.0),
		"saturation": clampf(float(grade.get("saturation", 1.0)), 0.0, 3.0),
	}


## How bright the world is at depth `y` of a level `height` pixels tall
## with the export's `descent.dark` (0 leaves it as it is, 1 goes black at
## the bottom): 1 at the top, falling evenly.
static func descent_factor(dark: float, y: float, height: float) -> float:
	if dark <= 0.0 or height <= 0.0:
		return 1.0
	return 1.0 - clampf(dark, 0.0, 1.0) * clampf(y / height, 0.0, 1.0)


## Jumps to a region's look at once (the first region of a level).
func reset(region: Dictionary, fallback: Array[Color]) -> void:
	_target = _look(region, fallback)
	_apply(_target)
	_from = _target
	_t = FADE


## Starts easing towards a region's look.
func retarget(region: Dictionary, fallback: Array[Color]) -> void:
	var next := _look(region, fallback)
	if next == _target:
		return
	_from = _snapshot()
	_target = next
	_t = 0.0


## Advances the easing; returns whether anything is still changing.
func step(delta: float) -> bool:
	if _t >= FADE:
		return false
	_t = minf(_t + delta, FADE)
	var k := smoothstep(0.0, 1.0, _t / FADE)
	var mixed := {}
	for key in _target:
		var a: Variant = _from.get(key, _target[key])
		var b: Variant = _target[key]
		mixed[key] = lerp(a, b, k)
	_apply(mixed)
	return true


func _look(region: Dictionary, fallback: Array[Color]) -> Dictionary:
	var sky := sky_of(region, fallback)
	var grade := grade_of(region)
	return {
		"top": sky[0],
		"bottom": sky[1],
		"cave": 1.0 if bool(region.get("cave", false)) else 0.0,
		"band": Vector2(float(region.get("y0", 0.0)), float(region.get("y1", 1.0))),
		"rim": rim_of(region, fallback),
		"tint": grade["tint"],
		"contrast": grade["contrast"],
		"saturation": grade["saturation"],
	}


func _snapshot() -> Dictionary:
	return {
		"top": top,
		"bottom": bottom,
		"cave": cave,
		"band": band,
		"rim": rim,
		"tint": tint,
		"contrast": contrast,
		"saturation": saturation,
	}


func _apply(look: Dictionary) -> void:
	top = look["top"]
	bottom = look["bottom"]
	cave = look["cave"]
	band = look["band"]
	rim = look["rim"]
	tint = look["tint"]
	contrast = look["contrast"]
	saturation = look["saturation"]


static func _color(value: Variant, fallback: Color) -> Color:
	if value is String and Color.html_is_valid(value):
		return Color.html(value)
	return fallback
