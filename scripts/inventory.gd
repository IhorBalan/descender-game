class_name Inventory
extends RefCounted

## What the hero carries: his purse (light, the common currency every
## enemy leaks, and tally bones, the rare one the imps count on), the
## items in his pack by id and count, and what he wears in each slot. Held
## in static variables like Story, so the pickups, the traders and the
## pause menu reach one state; main.gd writes it into the save under
## "inventory" and reads it back. See docs/design/economy.md.

## The currencies: light is what a killed thing leaks, tally bones what the
## imps keep count on.
const CURRENCIES := {
	"light": {"name": "light", "color": Color(1.0, 0.95, 0.7)},
	"bones": {"name": "tally bones", "color": Color(0.95, 0.92, 0.85)},
}

static var purse: Dictionary = {"light": 0, "bones": 0}
## Item id -> how many the pack holds.
static var items: Dictionary = {}
## Items.Slot (as int) -> the id of the piece worn there.
static var equipped: Dictionary = {}


## A new game starts with an empty pack.
static func reset() -> void:
	purse = {"light": 0, "bones": 0}
	items = {}
	equipped = {}


# --- Purse ------------------------------------------------------------------


static func amount(currency: String) -> int:
	return int(purse.get(currency, 0))


static func add_currency(currency: String, count: int) -> void:
	if not CURRENCIES.has(currency) or count <= 0:
		return
	purse[currency] = amount(currency) + count


## Whether the purse covers a price ({currency: count}).
static func can_afford(price: Dictionary) -> bool:
	for currency in price:
		if amount(str(currency)) < int(price[currency]):
			return false
	return true


## Takes a price out of the purse. Returns false, and takes nothing, when
## it cannot be paid.
static func spend(price: Dictionary) -> bool:
	if not can_afford(price):
		return false
	for currency in price:
		purse[str(currency)] = amount(str(currency)) - int(price[currency])
	return true


## A price as a line: "60 light", "3 tally bones".
static func price_text(price: Dictionary) -> String:
	var parts: PackedStringArray = []
	for currency in price:
		var count := int(price[currency])
		var name := str(CURRENCIES.get(str(currency), {}).get("name", currency))
		if count == 1 and name.ends_with("s"):
			name = name.trim_suffix("s")
		parts.append("%d %s" % [count, name])
	return ", ".join(parts) if not parts.is_empty() else "free"


# --- Items ------------------------------------------------------------------


static func count(id: String) -> int:
	return int(items.get(id, 0))


static func has(id: String) -> bool:
	return count(id) > 0


static func add(id: String, quantity := 1) -> void:
	if not Items.has(id) or quantity <= 0:
		return
	items[id] = count(id) + quantity


## Takes items out of the pack (unequipping the piece if the last one is
## worn). Returns false, and takes nothing, when there are not enough.
static func remove(id: String, quantity := 1) -> bool:
	if count(id) < quantity:
		return false
	var left := count(id) - quantity
	if left > 0:
		items[id] = left
	else:
		items.erase(id)
		var slot := Items.slot_of(id)
		if equipped.get(slot, "") == id:
			equipped.erase(slot)
	return true


## The ids in the pack, equipment first, then consumables, each in table
## order.
static func sorted_ids() -> Array[String]:
	var result: Array[String] = []
	for kind: int in [Items.Kind.EQUIPMENT, Items.Kind.CONSUMABLE]:
		for id: String in Items.TABLE:
			if has(id) and Items.get_item(id).get("kind", -1) == kind:
				result.append(id)
	return result


# --- Equipment --------------------------------------------------------------


## The id worn in a slot, or "".
static func equipped_id(slot: int) -> String:
	return str(equipped.get(slot, ""))


static func is_equipped(id: String) -> bool:
	return equipped_id(Items.slot_of(id)) == id


## Wears a piece from the pack in its slot, taking off whatever was there.
## Returns false for anything that is not equipment in the pack.
static func equip(id: String) -> bool:
	if not has(id) or not Items.is_equipment(id):
		return false
	equipped[Items.slot_of(id)] = id
	return true


static func unequip(slot: int) -> void:
	equipped.erase(slot)


## The worn pieces' additive stat, summed (0 when nothing worn gives it).
static func bonus(stat: String) -> float:
	var total := 0.0
	for slot in equipped:
		total += float(Items.stats(str(equipped[slot])).get(stat, 0.0))
	return total


## The worn pieces' multiplying stat, multiplied (1 when nothing changes it).
static func factor(stat: String) -> float:
	var total := 1.0
	for slot in equipped:
		total *= float(Items.stats(str(equipped[slot])).get(stat, 1.0))
	return total


# --- Save -------------------------------------------------------------------


static func to_dict() -> Dictionary:
	var worn: Dictionary = {}
	for slot in equipped:
		worn[str(int(slot))] = str(equipped[slot])
	return {"purse": purse.duplicate(), "items": items.duplicate(), "equipped": worn}


## Replaces the pack with the one from a save; anything the table no longer
## knows is dropped, and a worn piece that is not in the pack is taken off.
static func from_dict(data: Dictionary) -> void:
	reset()
	var saved_purse: Variant = data.get("purse", {})
	if saved_purse is Dictionary:
		for currency in CURRENCIES:
			purse[currency] = maxi(int(saved_purse.get(currency, 0)), 0)
	var saved_items: Variant = data.get("items", {})
	if saved_items is Dictionary:
		for id in saved_items:
			add(str(id), int(saved_items[id]))
	var worn: Variant = data.get("equipped", {})
	if worn is Dictionary:
		for slot in worn:
			var id := str(worn[slot])
			if str(slot).is_valid_int() and Items.slot_of(id) == int(str(slot)):
				equip(id)
