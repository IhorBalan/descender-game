class_name AnimLoader
extends RefCounted

## The characters' animations: one SpriteFrames resource per character at
## godot_assets/sprites/<name>/<name>.tres, cut from the strips in that folder by
## tools/sprite_frames.py from godot_assets/sprites/sprites.json (frame size,
## fps, loop, origin). The resource's `origin` metadata is the strip's
## origin point (the feet, or the centre) in frame pixels, so a centred
## AnimatedSprite2D gets an offset that puts it on the node's position.
##
## An enemy's scene sets its sprite's frames and offset itself (see
## scenes/enemies/); the hero, the NPCs and the bosses, whose sprites are
## picked in code, go through `setup`.

const SPRITES_DIR := "res://godot_assets/sprites"


## The resource's path for a character.
static func frames_path(character: String) -> String:
	return "%s/%s/%s.tres" % [SPRITES_DIR, character, character]


## A character's frames. A missing resource is reported and yields empty
## frames, so the game keeps running with an invisible sprite instead of
## crashing on a lookup.
static func frames(character: String) -> SpriteFrames:
	var path := frames_path(character)
	if not ResourceLoader.exists(path):
		push_error("AnimLoader: no sprite frames for '%s' (%s)" % [character, path])
		return SpriteFrames.new()
	return load(path) as SpriteFrames


## The strip's origin point in frame pixels (the feet or the centre),
## from the resource's metadata; the frame's bottom centre when unset.
static func origin(sprite_frames: SpriteFrames) -> Vector2:
	if sprite_frames.has_meta("origin"):
		return sprite_frames.get_meta("origin")
	var size := frame_size(sprite_frames)
	return Vector2(size.x / 2.0, size.y)


## The size of one frame: that of the first frame of the first animation,
## or zero for empty frames.
static func frame_size(sprite_frames: SpriteFrames) -> Vector2:
	for anim_name in sprite_frames.get_animation_names():
		if sprite_frames.get_frame_count(anim_name) > 0:
			return sprite_frames.get_frame_texture(anim_name, 0).get_size()
	return Vector2.ZERO


## Offset for a centred AnimatedSprite2D so the strip's origin point lands
## on the node's position.
static func offset(sprite_frames: SpriteFrames) -> Vector2:
	var size := frame_size(sprite_frames)
	return size / 2.0 - origin(sprite_frames)


## Applies a character's frames and offset to a sprite in one go.
static func setup(sprite: AnimatedSprite2D, character: String) -> void:
	sprite.sprite_frames = frames(character)
	sprite.offset = offset(sprite.sprite_frames)
	sprite.centered = true
