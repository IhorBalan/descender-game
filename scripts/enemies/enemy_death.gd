class_name EnemyDeath
extends RefCounted

## How an enemy's body goes: it plays the sprite's death strip (or, for a
## sprite without one, topples over), lies there as a corpse for a moment,
## then fades out. `done` is called at the end so the enemy can free itself.
## The caller has already stopped the enemy and turned its collision off.

const TOPPLE_TIME := 0.5
const FADE_TIME := 0.4


static func play(
	sprite: AnimatedSprite2D, fall_dir: int, corpse_time: float, done: Callable
) -> void:
	var tween := sprite.create_tween()
	if sprite.sprite_frames != null and sprite.sprite_frames.has_animation("death"):
		sprite.play("death")
		tween.tween_interval(strip_length(sprite, "death") + corpse_time)
	else:
		sprite.animation = "hurt"
		sprite.pause()
		sprite.frame = 0
		tween.tween_property(sprite, "rotation", fall_dir * PI / 2.0, TOPPLE_TIME).set_ease(
			Tween.EASE_OUT
		)
		tween.parallel().tween_property(sprite, "position:x", fall_dir * 6.0, TOPPLE_TIME).set_ease(
			Tween.EASE_OUT
		)
		tween.tween_interval(corpse_time)
	tween.tween_property(sprite, "modulate:a", 0.0, FADE_TIME)
	tween.tween_callback(done)


## How long one run of an animation takes, in seconds.
static func strip_length(sprite: AnimatedSprite2D, anim: String) -> float:
	var frames := sprite.sprite_frames
	var fps := frames.get_animation_speed(anim) * absf(sprite.speed_scale)
	return frames.get_frame_count(anim) / maxf(fps, 1.0)
