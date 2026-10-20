class_name StatusBar
extends Control

## Draws hearts (health), bars (healing charges), the unlocked angel
## abilities and the dash cooldown with plain shapes.

const HEART_ON := Color(0.96, 0.22, 0.32)
const HEART_OFF := Color(0.32, 0.1, 0.16)
const CHARGE_ON := Color(1.0, 0.86, 0.32)
const CHARGE_OFF := Color(0.36, 0.3, 0.16)
const SHADOW := Color(0.0, 0.0, 0.0, 0.5)
const WINGS := Color(0.95, 0.97, 1.0)
const LIGHT := Color(1.0, 0.9, 0.5)
const DASH_READY := Color(0.6, 0.9, 1.0)
const DASH_CHARGING := Color(0.3, 0.42, 0.5)
const DASH_BACK := Color(0.0, 0.0, 0.0, 0.5)
## Where the ability icons start; the hearts take the space before it.
const ABILITY_X := 58.0

var health := 5
var max_health := 5
var charges := 3
var max_charges := 3
var abilities: Array[Player.Ability] = []
## 0 to 1; see Player.dash_readiness.
var dash_readiness := 1.0


func _draw() -> void:
	for i in max_health:
		var r := Rect2(4 + i * 9, 4, 7, 7)
		draw_rect(Rect2(r.position + Vector2.ONE, r.size), SHADOW)
		draw_rect(r, HEART_ON if i < health else HEART_OFF)
	for i in max_charges:
		var r := Rect2(4 + i * 7, 14, 5, 8)
		draw_rect(Rect2(r.position + Vector2.ONE, r.size), SHADOW)
		draw_rect(r, CHARGE_ON if i < charges else CHARGE_OFF)
	var x := ABILITY_X
	for ability in abilities:
		_draw_ability(ability, Vector2(x, 4.0))
		x += 12.0
	_draw_dash(Vector2(ABILITY_X, 15.0))


## A 9x7 icon per ability: the wings are two feathers, the light dash a
## streak with a spark at its head.
func _draw_ability(ability: Player.Ability, at: Vector2) -> void:
	match ability:
		Player.Ability.WINGS:
			var left := PackedVector2Array(
				[at + Vector2(0, 7), at + Vector2(0, 1), at + Vector2(4, 4)]
			)
			var right := PackedVector2Array(
				[at + Vector2(9, 7), at + Vector2(9, 1), at + Vector2(5, 4)]
			)
			draw_colored_polygon(_shift(left), SHADOW)
			draw_colored_polygon(_shift(right), SHADOW)
			draw_colored_polygon(left, WINGS)
			draw_colored_polygon(right, WINGS)
		Player.Ability.LIGHT_DASH:
			draw_rect(Rect2(at + Vector2(1, 4), Vector2(6, 2)), SHADOW)
			draw_rect(Rect2(at + Vector2(0, 3), Vector2(6, 2)), LIGHT)
			draw_rect(Rect2(at + Vector2(7, 2), Vector2(2, 4)), SHADOW)
			draw_rect(Rect2(at + Vector2(6, 1), Vector2(2, 4)), CHARGE_ON)


## The dash cooldown: a short bar that fills as the dash comes back and
## brightens when it is ready.
func _draw_dash(at: Vector2) -> void:
	var width := 20.0
	draw_rect(Rect2(at + Vector2.ONE, Vector2(width, 3)), DASH_BACK)
	var fill := roundf(width * clampf(dash_readiness, 0.0, 1.0))
	if fill > 0.0:
		draw_rect(
			Rect2(at, Vector2(fill, 3)), DASH_READY if dash_readiness >= 1.0 else DASH_CHARGING
		)


func _shift(points: PackedVector2Array) -> PackedVector2Array:
	var out := PackedVector2Array()
	for p in points:
		out.append(p + Vector2.ONE)
	return out
