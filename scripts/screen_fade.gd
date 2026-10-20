class_name ScreenFade
extends CanvasLayer

## A black cover over the whole screen for fading levels in and out.
## Sits above the HUD and below the pause menu, and never takes input.

const FADE_TIME := 0.5

var _tween: Tween

@onready var cover: ColorRect = $Cover


func _ready() -> void:
	cover.mouse_filter = Control.MOUSE_FILTER_IGNORE


## Fades to black. Await the returned signal to know when the screen is
## fully covered.
func fade_out(duration: float = FADE_TIME) -> Signal:
	return _fade_to(1.0, duration)


## Fades back in from black.
func fade_in(duration: float = FADE_TIME) -> Signal:
	return _fade_to(0.0, duration)


## Covers the screen at once, for a level that then fades in.
func cover_now() -> void:
	if _tween:
		_tween.kill()
	cover.modulate.a = 1.0


func is_covered() -> bool:
	return is_equal_approx(cover.modulate.a, 1.0)


func _fade_to(alpha: float, duration: float) -> Signal:
	if _tween:
		_tween.kill()
	_tween = create_tween()
	_tween.tween_property(cover, "modulate:a", alpha, duration)
	return _tween.finished
