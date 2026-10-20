class_name StoryTrigger
extends Interactable

## An invisible area that plays a dialogue when the hero walks in. Its
## entries use `unless` with the flag they set, so each one plays once per
## save and the trigger is quiet afterwards.


func _ready() -> void:
	automatic = true
	area_size = Vector2(16, 48)
	super()
