class_name StatusBar
extends Control

## Draws hearts (health), bars (healing charges), the unlocked angel
## abilities, the dash cooldown and the running power-ups with plain shapes.
## When the HUD shows a health bar component instead of the hearts,
## `health_bar_rect` is where it sits: the hearts are left out, the charges
## go under it and the icons to its right.

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
const HALO := Color(1.0, 0.85, 0.4)
const BLADE := Color(0.95, 0.97, 1.0)
const BLADE_FIRE := Color(1.0, 0.65, 0.25)
const ZEAL := Color(0.55, 0.85, 1.0)
const GRIP := Color(0.85, 0.75, 0.6)
const GLIDE := Color(0.8, 0.9, 1.0)
const HOLY := Color(1.0, 0.97, 0.75)
const SMITE := Color(1.0, 0.75, 0.55)
const HEAD_WHITE := Color(1.0, 1.0, 1.0)
const POWER_BAR := Color(1.0, 1.0, 1.0, 0.8)
## Where the ability icons start at most five hearts; more hearts push them
## right.
const ABILITY_X := 58.0
const HEART_STEP := 9.0

var health := 5
var max_health := 5
var charges := 3
var max_charges := 3
var abilities: Array[Player.Ability] = []
## 0 to 1; see Player.dash_readiness.
var dash_readiness := 1.0
## Player.Power -> seconds left (INF for the Halo).
var powers: Dictionary = {}
## Where a health bar component is drawn over this control (see Hud); empty
## when the hearts show health.
var health_bar_rect := Rect2()


func _draw() -> void:
	var bar := health_bar_rect.has_area()
	if not bar:
		for i in max_health:
			var r := Rect2(4 + i * HEART_STEP, 4, 7, 7)
			draw_rect(Rect2(r.position + Vector2.ONE, r.size), SHADOW)
			draw_rect(r, HEART_ON if i < health else HEART_OFF)
	var charges_y := health_bar_rect.end.y + 1.0 if bar else 14.0
	for i in max_charges:
		var r := Rect2(4 + i * 7, charges_y, 5, 8)
		draw_rect(Rect2(r.position + Vector2.ONE, r.size), SHADOW)
		draw_rect(r, CHARGE_ON if i < charges else CHARGE_OFF)
	var x := ability_x()
	for ability in abilities:
		_draw_ability(ability, Vector2(x, 4.0))
		x += 12.0
	_draw_dash(Vector2(ability_x(), 15.0))
	x = ability_x() + 26.0
	for power: Player.Power in Player.Power.values():
		if powers.has(power):
			_draw_power(power, Vector2(x, 15.0))
			x += 12.0


## The hearts take the space before the ability icons.
func ability_x() -> float:
	if health_bar_rect.has_area():
		return health_bar_rect.end.x + 4.0
	return maxf(ABILITY_X, 4.0 + max_health * HEART_STEP + 4.0)


## A 9x7 icon per ability: the wings are two feathers, the light dash a
## streak with a spark at its head, the wall grip a hand on a wall edge,
## the glide a wide open wing.
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
		Player.Ability.WALL_GRIP:
			draw_rect(Rect2(at + Vector2(1, 1), Vector2(2, 7)), SHADOW)
			draw_rect(Rect2(at + Vector2(0, 0), Vector2(2, 7)), DASH_CHARGING)
			for i in 3:
				draw_rect(Rect2(at + Vector2(3 + i * 2, 2 + i), Vector2(2, 2)), SHADOW)
				draw_rect(Rect2(at + Vector2(2 + i * 2, 1 + i), Vector2(2, 2)), GRIP)
		Player.Ability.GLIDE:
			var wing := PackedVector2Array(
				[at + Vector2(0, 5), at + Vector2(4, 1), at + Vector2(9, 5), at + Vector2(4, 3)]
			)
			draw_colored_polygon(_shift(wing), SHADOW)
			draw_colored_polygon(wing, GLIDE)
		Player.Ability.HOLY_LIGHT:
			draw_rect(Rect2(at + Vector2(4, 1), Vector2(3, 7)), SHADOW)
			draw_rect(Rect2(at + Vector2(1, 4), Vector2(7, 3)), SHADOW)
			draw_rect(Rect2(at + Vector2(3, 0), Vector2(3, 7)), HOLY)
			draw_rect(Rect2(at + Vector2(0, 3), Vector2(7, 3)), HOLY)
			draw_rect(Rect2(at + Vector2(4, 3), Vector2(1, 1)), HEAD_WHITE)
		Player.Ability.SMITE:
			draw_rect(Rect2(at + Vector2(4, 1), Vector2(3, 5)), SHADOW)
			draw_rect(Rect2(at + Vector2(3, 0), Vector2(3, 5)), SMITE)
			var head := PackedVector2Array(
				[at + Vector2(1, 4), at + Vector2(8, 4), at + Vector2(4.5, 7)]
			)
			draw_colored_polygon(_shift(head), SHADOW)
			draw_colored_polygon(head, SMITE)
			draw_rect(Rect2(at + Vector2(0, 7), Vector2(9, 1)), DASH_BACK)


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


## A 9x7 icon per power-up with a bar under it that drains as its time runs
## out: the Halo is a ring, the Lit Blade a short sword, Zeal two chevrons.
func _draw_power(power: Player.Power, at: Vector2) -> void:
	match power:
		Player.Power.HALO:
			draw_rect(Rect2(at + Vector2(2, 2), Vector2(7, 4)), SHADOW)
			draw_rect(Rect2(at + Vector2(1, 1), Vector2(7, 4)), HALO)
			draw_rect(Rect2(at + Vector2(3, 2), Vector2(3, 2)), DASH_BACK)
		Player.Power.BLADE:
			for i in 5:
				draw_rect(Rect2(at + Vector2(3 + i, 6 - i), Vector2(1, 1)), SHADOW)
				draw_rect(Rect2(at + Vector2(2 + i, 5 - i), Vector2(1, 1)), BLADE)
			draw_rect(Rect2(at + Vector2(1, 5), Vector2(3, 1)), BLADE_FIRE)
			draw_rect(Rect2(at + Vector2(6, 0), Vector2(1, 1)), BLADE_FIRE)
		Player.Power.ZEAL:
			for i in 3:
				for ox: float in [1.0, 5.0]:
					draw_rect(Rect2(at + Vector2(ox + i + 1, i + 1), Vector2(1, 1)), SHADOW)
					draw_rect(Rect2(at + Vector2(ox + i + 1, 5 - i + 1), Vector2(1, 1)), SHADOW)
					draw_rect(Rect2(at + Vector2(ox + i, i), Vector2(1, 1)), ZEAL)
					draw_rect(Rect2(at + Vector2(ox + i, 5 - i), Vector2(1, 1)), ZEAL)
	var left: float = powers[power]
	if left == INF:
		return
	var fill := roundf(9.0 * clampf(left / Player.POWER_TIME, 0.0, 1.0))
	draw_rect(Rect2(at + Vector2(1, 9), Vector2(9, 1)), DASH_BACK)
	if fill > 0.0:
		draw_rect(Rect2(at + Vector2(0, 8), Vector2(fill, 1)), POWER_BAR)


func _shift(points: PackedVector2Array) -> PackedVector2Array:
	var out := PackedVector2Array()
	for p in points:
		out.append(p + Vector2.ONE)
	return out
