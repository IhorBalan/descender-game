class_name StatusBar
extends Control

## Draws hearts (health) and bars (healing charges) with plain rectangles.

const HEART_ON := Color(0.96, 0.22, 0.32)
const HEART_OFF := Color(0.32, 0.1, 0.16)
const CHARGE_ON := Color(1.0, 0.86, 0.32)
const CHARGE_OFF := Color(0.36, 0.3, 0.16)
const SHADOW := Color(0.0, 0.0, 0.0, 0.5)

var health := 5
var max_health := 5
var charges := 3
var max_charges := 3


func _draw() -> void:
	for i in max_health:
		var r := Rect2(4 + i * 9, 4, 7, 7)
		draw_rect(Rect2(r.position + Vector2.ONE, r.size), SHADOW)
		draw_rect(r, HEART_ON if i < health else HEART_OFF)
	for i in max_charges:
		var r := Rect2(4 + i * 7, 14, 5, 8)
		draw_rect(Rect2(r.position + Vector2.ONE, r.size), SHADOW)
		draw_rect(r, CHARGE_ON if i < charges else CHARGE_OFF)
