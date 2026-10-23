class_name PostFx
extends CanvasLayer

## The post stack: the region's colour grade and a soft vignette drawn
## over the level and under the HUD (assets/shaders/post.gdshader). It
## sits on the HUD's canvas layer, placed before the HUD in the tree so
## it is drawn first, and reads the screen behind it. Main eases the grade
## between regions (see RegionLook).

const SHADER := preload("res://assets/shaders/post.gdshader")

var _rect := ColorRect.new()
var _material := ShaderMaterial.new()


func _ready() -> void:
	layer = 1
	_material.shader = SHADER
	_rect.name = "Grade"
	_rect.material = _material
	_rect.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_rect.set_anchors_preset(Control.PRESET_FULL_RECT)
	add_child(_rect)


## The grade of the region the view is in.
func set_grade(tint: Color, contrast: float, saturation: float) -> void:
	_material.set_shader_parameter("tint", tint)
	_material.set_shader_parameter("contrast", contrast)
	_material.set_shader_parameter("saturation", saturation)


func grade() -> Dictionary:
	return {
		"tint": _material.get_shader_parameter("tint"),
		"contrast": _material.get_shader_parameter("contrast"),
		"saturation": _material.get_shader_parameter("saturation"),
	}
