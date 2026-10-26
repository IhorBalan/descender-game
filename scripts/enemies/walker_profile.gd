class_name WalkerProfile
extends Resource

## The numbers of one ground enemy body (see Walker): its size, pace, health,
## swing and drops. One of these lives in resources/enemies/<name>.tres and
## is set on the enemy's scene in scenes/enemies/<name>.tscn, so a body is
## tuned in the Inspector and a new one is a new resource and a new
## inherited scene.

## Collision size, in pixels, with the origin at the feet.
@export var body := Vector2(18, 24)
## Patrol speed.
@export var speed := 32.0
## Hits to kill.
@export var health := 3
## The looping animation it walks with ("run" or "walk").
@export var walk_anim := "run"
## Whether the strips face left.
@export var faces_left := false
## Hits never cancel its attack or push it back while it attacks (and push
## it half as far otherwise).
@export var armour := false

@export_group("Swing")
## How close (horizontally) the hero must be to trigger a swing.
@export var attack_range := 30.0
## The strike hitbox size, placed in front of the body.
@export var reach := Vector2(22, 20)
## The three phases of a swing, in seconds.
@export var windup := 0.35
@export var strike := 0.15
@export var recover := 0.45
## Pause before the next swing can start.
@export var cooldown := 0.8
## Hearts a strike takes.
@export var attack_damage := 1

@export_group("Drops")
## How often a kill leaves a heal mote behind.
@export_range(0.0, 1.0) var drop_chance := 0.35
## The power-up a kill may leave instead of a mote, and how often.
@export var power: Player.Power = Player.Power.BLADE
@export_range(0.0, 1.0) var power_chance := 0.1
## The light it leaks and what else it carried (see Loot): `light` is a
## [min, max] range, `bone_chance` and `flask_chance` are odds, `xp` is
## experience.
@export var loot: Dictionary = {}
## The colour of the dust (or light) it dies in.
@export var puff := Color(0.55, 0.15, 0.15)
