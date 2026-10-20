class_name Upgrade
extends Area2D

## A permanent upgrade lying in the world: Kept Light (one more heart) or a
## Vial of the Basin (one more healing charge). Touching it raises the
## hero's maximum for the rest of the save; the pickup is a story flag
## (`upgrade:<kind>:<level>:<n>`), so the level builder leaves it out once
## it is taken. The node's origin is at the base, like the hero's, so a
## level cell places it on the floor. See docs/design/upgrades.md.

signal collected(kind: Player.UpgradeKind)

const TEXTURES := {
	Player.UpgradeKind.HEART: preload("res://assets/sprites/upgrade_heart.png"),
	Player.UpgradeKind.VIAL: preload("res://assets/sprites/upgrade_vial.png"),
}
## What the dialogue box shows when the upgrade is taken: the relic-style
## description, then what changed. The second line is filled in with the
## new maximum.
const DESCRIPTIONS := {
	Player.UpgradeKind.HEART:
	[
		(
			"A little of someone's light, kept back from the Basin before it could be pressed."
			+ " It is warm, and it does not ask whose it was."
		),
		"Hearts: %d."
	],
	Player.UpgradeKind.VIAL:
	[
		(
			"Basin water in Warden's glass. The gathered drink it on the way in so they do not"
			+ " struggle. In him it only closes wounds."
		),
		"Healing charges: %d."
	],
}
const FLAG_NAMES := {
	Player.UpgradeKind.HEART: "heart",
	Player.UpgradeKind.VIAL: "vial",
}
const BOB_HEIGHT := 2.0
const BOB_SPEED := 2.5
const GLOW := Color(1.0, 0.9, 0.6)

var kind: Player.UpgradeKind = Player.UpgradeKind.HEART
## The story flag this pickup sets (see LevelBuilder).
var flag := ""

var _time := randf() * TAU
var _sprite: Sprite2D


## Every pickup of a kind sets a flag starting with this, whatever level it
## is in, so the hero's maximums can be counted back from the save.
static func flag_prefix(p_kind: Player.UpgradeKind) -> String:
	return "upgrade:%s:" % FLAG_NAMES[p_kind]


static func flag_for(p_kind: Player.UpgradeKind, level_id: String, index: int) -> String:
	return "%s%s:%d" % [flag_prefix(p_kind), level_id, index]


## The dialogue entry for the pickup, with the hero's new maximum filled in.
static func description(p_kind: Player.UpgradeKind, player: Player) -> Dictionary:
	var maximum := (
		player.max_health if p_kind == Player.UpgradeKind.HEART else player.max_heal_charges
	)
	var lines: Array = []
	for text: String in DESCRIPTIONS[p_kind]:
		lines.append({"who": "", "text": text % maximum if "%d" in text else text})
	return {"id": "upgrade_%s" % FLAG_NAMES[p_kind], "lines": lines}


func _ready() -> void:
	collision_layer = 0
	collision_mask = 2
	var shape := CollisionShape2D.new()
	var rect := RectangleShape2D.new()
	rect.size = Vector2(20, 28)
	shape.shape = rect
	shape.position = Vector2(0, -14)
	add_child(shape)
	_sprite = Sprite2D.new()
	_sprite.texture = TEXTURES[kind]
	_sprite.scale = Vector2(2, 2)
	_sprite.position = Vector2(0, -16)
	add_child(_sprite)
	body_entered.connect(_on_body_entered)


func _process(delta: float) -> void:
	_time += delta
	_sprite.position.y = -16.0 + sin(_time * BOB_SPEED) * BOB_HEIGHT


func _on_body_entered(body: Node2D) -> void:
	if not body is Player or body.state == Player.State.DEAD:
		return
	# Stop listening right away so a second overlap in the same step cannot
	# collect it twice before queue_free runs.
	set_deferred("monitoring", false)
	body.apply_upgrade(kind, flag)
	Audio.play("heal")
	Feedback.spark(global_position + Vector2(0.0, -16.0), GLOW)
	collected.emit(kind)
	queue_free()
