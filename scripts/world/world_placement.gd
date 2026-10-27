@tool
class_name WorldPlacement
extends Node2D

## Something that stands in the world with its feet at its position: what
## placements.json lists (see LevelBuilder, the top of the file, for every
## field). `type` says which fields matter:
##
##   start     nothing: where a new game puts the hero (the world has one)
##   save      nothing: a checkpoint
##   ability   ability, the name of the feather's ability as the world's
##             abilities list has it ("Wings (double jump)", "Halo Dash"...)
##   npc       placement_name, character, tint, faces_left, dialogue, rotate, unless, shop
##   item      item, dialogue, unless
##   trigger   dialogue, unless
##   enemy     character, barks, passive, behaviour, requires; a boss is an
##             enemy with its profile's id (Boss.IDS) in its room
##   upgrade   upgrade (heart, vial, iron); upgrades are numbered in scene
##             order, region by region, so keep that order to keep saves straight
##   power     power (halo, blade, zeal)
##   barrier   height, the cells of light below the position

const COLORS := {
	"start": Color(1.0, 1.0, 1.0, 0.9),
	"save": Color(1.0, 0.9, 0.3, 0.9),
	"ability": Color(0.9, 0.8, 1.0, 0.9),
	"npc": Color(0.5, 1.0, 0.6, 0.9),
	"item": Color(1.0, 0.85, 0.4, 0.9),
	"trigger": Color(0.7, 0.7, 1.0, 0.8),
	"enemy": Color(1.0, 0.4, 0.4, 0.9),
	"upgrade": Color(1.0, 0.6, 0.9, 0.9),
	"power": Color(0.6, 0.9, 1.0, 0.9),
	"barrier": Color(1.0, 1.0, 0.7, 0.9),
}

## One of start, save, ability, npc, item, trigger, enemy, upgrade, power, barrier.
@export var type := "enemy"
## A feather's ability, by its name in the world's abilities list.
@export var ability := ""
## An NPC's name, as the dialogue box shows it.
@export var placement_name := ""
## An NPC's or an enemy's body (see Npc, Walker.PROFILES, Flyer.PROFILES, Boss.PROFILES).
@export var character := ""
## An NPC's tint, white for none.
@export var tint := Color.WHITE
@export var faces_left := false
## The dialogue entries to try in order, and the lines to rotate through after.
@export var dialogue: PackedStringArray = []
@export var rotate: PackedStringArray = []
## A story flag that, once set, leaves the thing out.
@export var unless := ""
## A Shops id for an NPC that trades.
@export var shop := ""
## A relic's item id (see LoreItem).
@export var item := ""
## An enemy's bark set, habit, and the flag it waits for.
@export var barks := ""
@export var passive := false
@export var behaviour := ""
@export var requires := ""
@export var upgrade := ""
@export var power := ""
## A wall of light's height in cells.
@export var height := 1


func _ready() -> void:
	if WorldRegion.overlays_shown():
		queue_redraw()


func _draw() -> void:
	if not WorldRegion.overlays_shown():
		return
	var color: Color = COLORS.get(type, Color.WHITE)
	draw_rect(Rect2(Vector2(-6.0, -14.0), Vector2(12.0, 14.0)), color, false, 1.5)
	var text := type
	var detail := character
	if type == "npc":
		detail = placement_name
	elif type == "item":
		detail = item
	elif type == "ability":
		detail = ability
	elif type == "upgrade":
		detail = upgrade
	elif type == "power":
		detail = power
	if not detail.is_empty():
		text += ": " + detail
	draw_string(ThemeDB.fallback_font, Vector2(8.0, -4.0), text, 0, -1, 11, color)


## The placement as placements.json lists it, with only the fields that
## are set, at the cell `cell` (x, y) its feet are on.
func to_spec(cell: Vector2i) -> Dictionary:
	var spec := {"type": type, "x": cell.x, "y": cell.y}
	match type:
		"ability":
			spec["ability"] = ability
		"npc":
			spec["name"] = placement_name
			spec["character"] = character if not character.is_empty() else "hero"
			if tint != Color.WHITE:
				spec["tint"] = [tint.r, tint.g, tint.b]
			if faces_left:
				spec["faces_left"] = true
			if not shop.is_empty():
				spec["shop"] = shop
			_put_lines(spec)
		"item":
			spec["item"] = item
			_put_lines(spec)
		"trigger":
			_put_lines(spec)
		"enemy":
			spec["character"] = character if not character.is_empty() else "imp"
			if not barks.is_empty():
				spec["barks"] = barks
			if passive:
				spec["passive"] = true
			if not behaviour.is_empty():
				spec["behaviour"] = behaviour
			if not requires.is_empty():
				spec["requires"] = requires
		"upgrade":
			spec["upgrade"] = upgrade
		"power":
			spec["power"] = power
		"barrier":
			spec["height"] = height
	if not unless.is_empty():
		spec["unless"] = unless
	return spec


func _put_lines(spec: Dictionary) -> void:
	if dialogue.size() > 0:
		spec["dialogue"] = Array(dialogue)
	if rotate.size() > 0:
		spec["rotate"] = Array(rotate)


## Fills the fields from a placements.json entry.
func from_spec(spec: Dictionary) -> void:
	type = str(spec.get("type", "enemy"))
	ability = str(spec.get("ability", ""))
	placement_name = str(spec.get("name", ""))
	character = str(spec.get("character", ""))
	var color: Variant = spec.get("tint")
	if color is Array and color.size() >= 3:
		tint = Color(float(color[0]), float(color[1]), float(color[2]))
	faces_left = bool(spec.get("faces_left", false))
	dialogue = PackedStringArray(spec.get("dialogue", []))
	rotate = PackedStringArray(spec.get("rotate", []))
	unless = str(spec.get("unless", ""))
	shop = str(spec.get("shop", ""))
	item = str(spec.get("item", ""))
	barks = str(spec.get("barks", ""))
	passive = bool(spec.get("passive", false))
	behaviour = str(spec.get("behaviour", ""))
	requires = str(spec.get("requires", ""))
	upgrade = str(spec.get("upgrade", ""))
	power = str(spec.get("power", ""))
	height = int(spec.get("height", 1))
