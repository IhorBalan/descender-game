class_name AnimLoader
extends RefCounted

## Builds SpriteFrames from the animation strips under assets/sprites/<name>/
## using assets/sprites/sprites.json (frame size, fps, loop, feet origin).

const SPEC_PATH := "res://assets/sprites/sprites.json"
const SPRITES_DIR := "res://assets/sprites"

static var _spec: Dictionary = {}
static var _cache: Dictionary = {}


## The sprite spec for one character. A missing file, bad JSON or unknown
## character is reported once and yields an empty spec, so the game keeps
## running with an invisible sprite instead of crashing on a lookup.
static func spec(character: String) -> Dictionary:
	if _spec.is_empty():
		var parsed: Variant = JSON.parse_string(FileAccess.get_file_as_string(SPEC_PATH))
		if parsed is Dictionary:
			_spec = parsed
		else:
			push_error("AnimLoader: could not read %s" % SPEC_PATH)
			return {}
	if not _spec.has(character):
		push_error("AnimLoader: no sprite spec for '%s'" % character)
		return {}
	return _spec[character]


static func frames(character: String) -> SpriteFrames:
	if _cache.has(character):
		return _cache[character]
	var s := spec(character)
	if s.is_empty():
		return SpriteFrames.new()
	var frame_w := int(s["frame_size"][0])
	var frame_h := int(s["frame_size"][1])
	var result := SpriteFrames.new()
	result.remove_animation("default")
	for anim_name: String in s["animations"]:
		var anim: Dictionary = s["animations"][anim_name]
		var strip: Texture2D = load("%s/%s/%s" % [SPRITES_DIR, character, anim["file"]])
		result.add_animation(anim_name)
		result.set_animation_speed(anim_name, float(anim["fps"]))
		result.set_animation_loop(anim_name, bool(anim["loop"]))
		for i in int(anim["frames"]):
			var frame := AtlasTexture.new()
			frame.atlas = strip
			frame.region = Rect2(i * frame_w, 0, frame_w, frame_h)
			result.add_frame(anim_name, frame)
	_cache[character] = result
	return result


## Offset for a centred AnimatedSprite2D so the strip's origin point (feet or
## centre) lands on the node's position.
static func offset(character: String) -> Vector2:
	var s := spec(character)
	if s.is_empty():
		return Vector2.ZERO
	return Vector2(
		float(s["frame_size"][0]) / 2.0 - float(s["origin"][0]),
		float(s["frame_size"][1]) / 2.0 - float(s["origin"][1]))


## Applies frames and offset to a sprite in one go.
static func setup(sprite: AnimatedSprite2D, character: String) -> void:
	sprite.sprite_frames = frames(character)
	sprite.offset = offset(character)
	sprite.centered = true
