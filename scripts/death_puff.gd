class_name DeathPuff
extends CPUParticles2D

## A slow puff of dust or light where an enemy died. Frees itself when the
## last mote has faded.


func _ready() -> void:
	add_to_group("death_puff")
	emitting = true
	get_tree().create_timer(lifetime + 0.05).timeout.connect(queue_free)
