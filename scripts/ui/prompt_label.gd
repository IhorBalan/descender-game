class_name PromptLabel
extends RichTextLabel

## A line of hint text with button prompts in it, drawn for whichever
## device the player last used and redrawn when that changes. `template`
## is written for Prompts.render: words with `{action}` tokens, such as
## "{navigate} move   {ui_accept} choose". Used for the hints along the
## bottom of the menus, the HUD's message line and the talk prompt over
## an NPC; any other UI can drop one in the same way.

## The hint, with its tokens; see Prompts.
@export_multiline var template := "":
	set = set_template


func _ready() -> void:
	bbcode_enabled = true
	scroll_active = false
	fit_content = false
	mouse_filter = Control.MOUSE_FILTER_IGNORE
	Prompts.device_changed.connect(_on_device_changed)
	_render()


func set_template(value: String) -> void:
	template = value
	if is_inside_tree():
		_render()


func _on_device_changed(_device: int) -> void:
	_render()


func _render() -> void:
	text = Prompts.render(template)
