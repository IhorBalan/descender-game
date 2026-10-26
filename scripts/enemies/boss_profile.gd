class_name BossProfile
extends Resource

## The numbers of one boss (see Boss). One of these lives in
## resources/bosses/<id>.tres; the id is the file name, which the map's
## boss markers and the placements name (see Boss.id_for).

## Shown on the health bar.
@export var display_name := ""
## The character whose strips it wears (a folder of assets/sprites), and
## the name of their walking loop. The move it has on top comes from the
## character too (see Boss.MOVES).
@export var character := "angel_guard"
@export var walk_anim := "walk"
## How much bigger than the strips, and the sprite's colour.
@export var scale := 1.5
@export var tint := Color.WHITE
## Whether the strips face left.
@export var faces_left := false
## Collision size, in pixels, at the scale, with the origin at the feet.
@export var body := Vector2(32, 80)
## Hits to kill.
@export var health := 24
## Walking speed.
@export var speed := 34.0
## Hearts for touching it.
@export var contact_damage := 1
## The third-phase move of the last bosses ("rampage", "court", "mirror"),
## or "" for a boss with two phases.
@export var third := ""

@export_group("Swing")
## The swing, as a Walker's: how close the hero must be, the hitbox in
## front, the three phases in seconds, the pause before the next, the
## hearts it takes.
@export var attack_range := 60.0
@export var reach := Vector2(52, 60)
@export var windup := 0.55
@export var strike := 0.2
@export var recover := 0.6
@export var cooldown := 1.0
@export var attack_damage := 2

@export_group("Charge")
## The rush across the room: its speed, wind-up and the pause between.
@export var charge_speed := 260.0
@export var charge_windup := 0.45
@export var charge_cooldown := 2.0

@export_group("Drops")
## Heal motes dropped on death.
@export var motes := 2
## The light, tally bones and unique item it leaves (see Loot).
@export var loot: Dictionary = {}
