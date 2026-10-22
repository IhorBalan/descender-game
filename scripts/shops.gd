class_name Shops
extends RefCounted

## The traders and what they sell. An NPC placement names one with `shop`
## (see LevelBuilder); once its unique lines are spent, talking to it plays
## a repeat line and then opens the wares (see ShopMenu). Each ware names
## an item and its price ({currency: count}); equipment is sold once per
## save (flag `bought:<shop>:<item>`), consumables as often as the purse
## allows. The Seal of the Vault scales every price. See
## docs/design/economy.md.

const TABLE := {
	"pell":
	{
		"name": "Old Pell's forge",
		"greeting":
		(
			"Church iron, lad, what there is of it. Light pays: it's what they want up there,"
			+ " so it's what I want down here."
		),
		"stock":
		[
			{"item": "iron_sword", "price": {"light": 120}},
			{"item": "hinge_blade", "price": {"light": 90}},
			{"item": "quilted_coat", "price": {"light": 100}},
			{"item": "scrap_mail", "price": {"light": 220}},
			{"item": "flask", "price": {"light": 25}},
		],
	},
	"vael":
	{
		"name": "Vael the Bargainer",
		"greeting":
		"Light, bones, memories. I take all three. Buyers don't give names; neither do I.",
		"stock":
		[
			{"item": "halo_shard", "price": {"bones": 1}},
			{"item": "blade_oil", "price": {"bones": 1}},
			{"item": "zeal_draught", "price": {"bones": 1}},
			{"item": "flask", "price": {"light": 30}},
			{"item": "cold_coin", "price": {"bones": 3}},
			{"item": "imp_tooth", "price": {"bones": 4}},
			{"item": "basin_shard", "price": {"bones": 5}},
		],
	},
}


static func has(id: String) -> bool:
	return TABLE.has(id)


static func get_shop(id: String) -> Dictionary:
	return TABLE.get(id, {})


## The story flag set once a trader has sold a piece of equipment.
static func bought_flag(shop_id: String, item_id: String) -> String:
	return "bought:%s:%s" % [shop_id, item_id]


## Whether a ware is gone from the stall: equipment the hero already bought.
static func sold_out(shop_id: String, item_id: String) -> bool:
	return Items.is_equipment(item_id) and Story.has_flag(bought_flag(shop_id, item_id))


## What a ware costs right now, with the hero's charms counted in (never
## below one of each currency).
static func price_of(ware: Dictionary) -> Dictionary:
	var result: Dictionary = {}
	var scale := Inventory.factor("prices")
	for currency in ware.get("price", {}):
		result[currency] = maxi(int(round(int(ware["price"][currency]) * scale)), 1)
	return result


## Sells a ware to the hero: spends the price, puts the item in the pack and
## marks equipment sold. Returns "" on success, else the reason.
static func buy(shop_id: String, ware: Dictionary) -> String:
	var item_id := str(ware.get("item", ""))
	if not Items.has(item_id):
		return "Nothing there"
	if sold_out(shop_id, item_id):
		return "Sold"
	var price := price_of(ware)
	if not Inventory.spend(price):
		return "Not enough: it costs %s" % Inventory.price_text(price)
	Inventory.add(item_id)
	if Items.is_equipment(item_id):
		Story.set_flag(bought_flag(shop_id, item_id))
	return ""
