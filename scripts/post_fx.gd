class_name PostFx
extends CanvasLayer

## The post stack: the region's colour grade and a soft vignette drawn
## over the level and under the HUD (assets/shaders/post.gdshader). It
## sits on the HUD's canvas layer, placed before the HUD in the tree so
## it is drawn first, and reads the screen behind it. Main eases the grade
## between regions (see RegionLook).
##
## Reading the screen copies the whole frame first, which at a large
## window's full resolution costs more than a 120 Hz frame has. Only
## contrast and saturation need it, so a grade that leaves both at 1 draws
## the tint and vignette with post_tint.gdshader instead, which multiplies
## through the blend and looks the same.

const SHADER := preload("res://assets/shaders/post.gdshader")
const TINT_SHADER := preload("res://assets/shaders/post_tint.gdshader")

var _rect := ColorRect.new()
var _material := ShaderMaterial.new()
var _tint_material := ShaderMaterial.new()


func _ready() -> void:
	layer = 1
	_material.shader = SHADER
	_tint_material.shader = TINT_SHADER
	_rect.name = "Grade"
	_rect.material = _tint_material
	_rect.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_rect.set_anchors_preset(Control.PRESET_FULL_RECT)
	add_child(_rect)


## The grade of the region the view is in.
func set_grade(tint: Color, contrast: float, saturation: float) -> void:
	_material.set_shader_parameter("tint", tint)
	_material.set_shader_parameter("contrast", contrast)
	_material.set_shader_parameter("saturation", saturation)
	_tint_material.set_shader_parameter("tint", tint)
	var plain := is_equal_approx(contrast, 1.0) and is_equal_approx(saturation, 1.0)
	_rect.material = _tint_material if plain else _material


## Whether the grade needs the screen read back (contrast or saturation).
func reads_screen() -> bool:
	return _rect.material == _material


func grade() -> Dictionary:
	return {
		"tint": _material.get_shader_parameter("tint"),
		"contrast": _material.get_shader_parameter("contrast"),
		"saturation": _material.get_shader_parameter("saturation"),
	}
