class_name Loot
extends RefCounted

## What a dead enemy leaves on the ground, from its profile's `loot` table:
##
##   light        [min, max] light, scaled by the hero's charms
##   bone_chance  how often a kill leaves a tally bone
##   bones        tally bones left every time (bosses)
##   flask_chance how often a kill leaves a Basin Flask
##   unique       the item id a boss leaves, once per save
##   xp           the experience the kill gives the hero (see Skills)
##
## A charm that heals on a kill is paid here too, and so is the
## experience, since the enemies do not know the hero. See
## docs/design/economy.md and docs/design/progression.md.

const LIGHT_DROP_SCRIPT := preload("res://scripts/light_drop.gd")
const LOOT_DROP_SCRIPT := preload("res://scripts/loot_drop.gd")
## Drops are spread this far apart so a boss's pile reads as a pile.
const SPREAD := 12.0


## Spawns the drops of a `loot` table at `at` (the dying body's centre)
## under `parent`, deferred, since a death may land inside a physics
## callback.
static func drop(parent: Node, at: Vector2, table: Dictionary) -> void:
	if parent == null or table.is_empty():
		return
	var drops: Array[Node2D] = []
	var light_range: Variant = table.get("light", [])
	if light_range is Array and light_range.size() == 2:
		var light := randi_range(int(light_range[0]), int(light_range[1]))
		light = int(round(light * Inventory.factor("light") * Skills.factor("light")))
		if light > 0:
			drops.append(_currency("light", light))
	var bones := int(table.get("bones", 0))
	if randf() < float(table.get("bone_chance", 0.0)):
		bones += 1
	for i in bones:
		drops.append(_currency("bones", 1))
	if randf() < float(table.get("flask_chance", 0.0)):
		drops.append(_item("flask"))
	var unique := str(table.get("unique", ""))
	if Items.is_unique(unique) and not Story.has_flag(Items.loot_flag(unique)):
		drops.append(_item(unique))
	for i in drops.size():
		var drop := drops[i]
		drop.position = at + Vector2((i - (drops.size() - 1) / 2.0) * SPREAD, 0.0)
		parent.add_child.call_deferred(drop)
	var player := parent.get_tree().get_first_node_in_group("player") as Player
	if player == null:
		return
	var heal := int(Inventory.bonus("heal_on_kill"))
	if heal > 0 and player.restore_health(heal):
		Feedback.spark(player.global_position + Vector2(0.0, -20.0), HealMote.COLOR)
	player.gain_xp(int(table.get("xp", 0)))


static func _currency(currency: String, amount: int) -> LightDrop:
	var drop: LightDrop = LIGHT_DROP_SCRIPT.new()
	drop.currency = currency
	drop.amount = amount
	return drop


static func _item(item_id: String) -> LootDrop:
	var drop: LootDrop = LOOT_DROP_SCRIPT.new()
	drop.item_id = item_id
	return drop
