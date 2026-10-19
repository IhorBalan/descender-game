class_name LightBarrier
extends StaticBody2D

## A wall of angel light. Solid for everyone, except the hero while he is
## light dashing: the barrier sits alone on physics layer 4 (LAYER_BIT) and
## the hero drops that bit from his collision mask for the dash. Tiles are
## added by the level builder; this script only makes them shimmer.

const LAYER_BIT := 8
const SHIMMER_SPEED := 4.0
const SHIMMER_MIN := 0.7

var _time := randf() * TAU


func _process(delta: float) -> void:
	_time += delta
	var glow := lerpf(SHIMMER_MIN, 1.0, (sin(_time * SHIMMER_SPEED) + 1.0) / 2.0)
	modulate = Color(1.0, 1.0, 1.0, glow)
