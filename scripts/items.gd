class_name Items
extends RefCounted

## Every item the hero can carry, by id: the equipment (a weapon, an armour
## and a charm may be worn at once), the consumables (used from the pack)
## and the unique loot the bosses leave. Each has a name, a line of
## description in the voice of the relics, a colour for its pickup, and
## its `stats`: what it does to the hero's numbers while it is worn (see
## Inventory.bonus and Inventory.factor). See docs/design/economy.md.
##
## Additive stats (summed over the worn pieces): damage (sword damage),
## guard (hearts taken off every hit, never below one), hearts, charges
## (healing charges), heal (hearts per healing charge), mote_chance (how
## much more often a kill leaves a heal mote), halo_at_rest (a Halo at
## every checkpoint rest), heal_on_kill (hearts back per kill).
## Multiplying stats (multiplied over the worn pieces): reach (the swing's
## width), attack_time (how long the swing takes), speed (running),
## dash_cooldown, light (light dropped by enemies), prices (what traders ask).

enum Slot { WEAPON, ARMOUR, CHARM }
enum Kind { EQUIPMENT, CONSUMABLE }

const SLOT_NAMES := {Slot.WEAPON: "Sword", Slot.ARMOUR: "Armour", Slot.CHARM: "Charm"}
const ADDITIVE_STATS: Array[String] = [
	"damage", "guard", "hearts", "charges", "heal", "mote_chance", "halo_at_rest", "heal_on_kill"
]
const FACTOR_STATS: Array[String] = [
	"reach", "attack_time", "speed", "dash_cooldown", "light", "prices"
]

const TABLE := {
	# --- Old Pell's forge --------------------------------------------------
	"iron_sword":
	{
		"name": "Church Iron Sword",
		"kind": Kind.EQUIPMENT,
		"slot": Slot.WEAPON,
		"text":
		(
			"A blade of hinge iron off a church door, hammered "
			+ "flat. Heavier than his, and it goes in further."
		),
		"stats": {"damage": 1},
		"color": Color(0.75, 0.78, 0.85),
	},
	"hinge_blade":
	{
		"name": "Hinge Blade",
		"kind": Kind.EQUIPMENT,
		"slot": Slot.WEAPON,
		"text":
		"Long and thin, a strap hinge ground to an edge. It reaches further than a sword should.",
		"stats": {"reach": 1.35},
		"color": Color(0.7, 0.72, 0.8),
	},
	"quilted_coat":
	{
		"name": "Quilted Coat",
		"kind": Kind.EQUIPMENT,
		"slot": Slot.ARMOUR,
		"text":
		"Pell's coat, the layers sewn with church iron wire. A blow lands a little softer through it.",
		"stats": {"guard": 1, "speed": 0.95},
		"color": Color(0.6, 0.45, 0.3),
	},
	"scrap_mail":
	{
		"name": "Scrap Mail",
		"kind": Kind.EQUIPMENT,
		"slot": Slot.ARMOUR,
		"text":
		"Hinge plates riveted to a coat. It holds a little more of him in and slows his feet.",
		"stats": {"guard": 1, "hearts": 1, "speed": 0.85},
		"color": Color(0.55, 0.55, 0.62),
	},
	# --- Vael the Bargainer --------------------------------------------------
	"imp_tooth":
	{
		"name": "Imp's Tooth",
		"kind": Kind.EQUIPMENT,
		"slot": Slot.CHARM,
		"text":
		"A tooth on a cord. The light of a killed thing leaks towards it, as if it still wanted to bite.",
		"stats": {"light": 1.5},
		"color": Color(0.9, 0.85, 0.75),
	},
	"cold_coin":
	{
		"name": "Cold Coin",
		"kind": Kind.EQUIPMENT,
		"slot": Slot.CHARM,
		"text":
		"A coin from a dead man's mouth, Vael says. The dying give up more of their light around it.",
		"stats": {"mote_chance": 0.3},
		"color": Color(0.6, 0.75, 0.85),
	},
	"basin_shard":
	{
		"name": "Basin Shard",
		"kind": Kind.EQUIPMENT,
		"slot": Slot.CHARM,
		"text": "Warden's glass from the Basin's rim. Its water closes more of a wound through it.",
		"stats": {"heal": 1},
		"color": Color(0.8, 0.9, 1.0),
	},
	# --- Consumables -----------------------------------------------------
	"flask":
	{
		"name": "Basin Flask",
		"kind": Kind.CONSUMABLE,
		"text": "Basin water in a soldier's flask. Drinking it puts a healing charge back.",
		"stats": {},
		"color": Color(1.0, 0.85, 0.45),
	},
	"halo_shard":
	{
		"name": "Halo Shard",
		"kind": Kind.CONSUMABLE,
		"text": "A piece of a cracked halo. Held up, it turns the next hit aside.",
		"stats": {},
		"color": Color(1.0, 0.85, 0.4),
	},
	"blade_oil":
	{
		"name": "Choir Oil",
		"kind": Kind.CONSUMABLE,
		"text": "Oil that burns with the Choir's fire. The sword hits harder for a while.",
		"stats": {},
		"color": Color(1.0, 0.7, 0.3),
	},
	"zeal_draught":
	{
		"name": "Draught of Zeal",
		"kind": Kind.CONSUMABLE,
		"text": "The hurry the Making put in him, bottled. Faster dashes and feet for a while.",
		"stats": {},
		"color": Color(0.6, 0.9, 1.0),
	},
	# --- Unique loot, one per boss ------------------------------------------
	"wardens_halo":
	{
		"name": "Warden's Halo",
		"kind": Kind.EQUIPMENT,
		"slot": Slot.CHARM,
		"unique": true,
		"text":
		"The Warden's halo, cracked through. It still remembers how to protect: a Halo at every rest.",
		"stats": {"halo_at_rest": 1},
		"color": Color(1.0, 0.9, 0.5),
	},
	"penitents_shroud":
	{
		"name": "Penitent's Shroud",
		"kind": Kind.EQUIPMENT,
		"slot": Slot.ARMOUR,
		"unique": true,
		"text":
		"The cloth an angel fell in. It weighs nothing, and a blow through it lands softer.",
		"stats": {"guard": 1},
		"color": Color(0.8, 0.8, 0.9),
	},
	"lords_tally":
	{
		"name": "Lord's Tally",
		"kind": Kind.EQUIPMENT,
		"slot": Slot.CHARM,
		"unique": true,
		"text":
		"The Limbo Lord's tally bone, notched to the end. The light of a kill comes to it twice over.",
		"stats": {"light": 2.0},
		"color": Color(0.95, 0.9, 0.8),
	},
	"velvet_gloves":
	{
		"name": "Velvet Gloves",
		"kind": Kind.EQUIPMENT,
		"slot": Slot.CHARM,
		"unique": true,
		"text":
		"The Keeper's gloves. Whatever they touch lets go sooner: the dash comes back in half the time.",
		"stats": {"dash_cooldown": 0.5},
		"color": Color(0.85, 0.4, 0.7),
	},
	"gluttons_maw":
	{
		"name": "Glutton's Maw",
		"kind": Kind.EQUIPMENT,
		"slot": Slot.WEAPON,
		"unique": true,
		"text":
		(
			"A jaw of the Bloated One, the teeth still in it. Slow "
			+ "to swing, and nothing it bites stays whole."
		),
		"stats": {"damage": 2, "attack_time": 1.35},
		"color": Color(0.55, 0.75, 0.35),
	},
	"vault_seal":
	{
		"name": "Seal of the Vault",
		"kind": Kind.EQUIPMENT,
		"slot": Slot.CHARM,
		"unique": true,
		"text":
		(
			"The Warden of the Vault's seal. Every trader below "
			+ "knows it, and asks less of whoever carries it."
		),
		"stats": {"prices": 0.6},
		"color": Color(1.0, 0.8, 0.3),
	},
	"furys_edge":
	{
		"name": "Fury's Edge",
		"kind": Kind.EQUIPMENT,
		"slot": Slot.WEAPON,
		"unique": true,
		"text":
		"Gorrow's blade, too long for a man. It reaches further and bites deeper than it should.",
		"stats": {"damage": 1, "reach": 1.3},
		"color": Color(1.0, 0.45, 0.3),
	},
	"bishops_mitre":
	{
		"name": "Bishop's Mitre",
		"kind": Kind.EQUIPMENT,
		"slot": Slot.ARMOUR,
		"unique": true,
		"text":
		(
			"The Cursed Bishop's hat, scorched. A blow lands softer "
			+ "under it and the Basin's water goes further."
		),
		"stats": {"guard": 1, "heal": 1},
		"color": Color(0.9, 0.6, 0.3),
	},
	"champions_plate":
	{
		"name": "Champion's Plate",
		"kind": Kind.EQUIPMENT,
		"slot": Slot.ARMOUR,
		"unique": true,
		"text":
		(
			"The Champion's breastplate, dented by the arena. Two "
			+ "hearts of every blow stop at it; so do his feet."
		),
		"stats": {"guard": 2, "speed": 0.8},
		"color": Color(0.85, 0.3, 0.3),
	},
	"false_crown":
	{
		"name": "False Crown",
		"kind": Kind.EQUIPMENT,
		"slot": Slot.CHARM,
		"unique": true,
		"text":
		"The False King's crown, which fooled an army. It holds two more hearts in whoever wears it.",
		"stats": {"hearts": 2},
		"color": Color(0.7, 0.5, 0.9),
	},
	"brothers_knife":
	{
		"name": "Brother's Knife",
		"kind": Kind.EQUIPMENT,
		"slot": Slot.WEAPON,
		"unique": true,
		"text":
		"A kitchen knife from Earth, the handle worn by a hand like his. Quick, and it goes deep.",
		"stats": {"damage": 2, "attack_time": 0.75},
		"color": Color(0.6, 0.8, 1.0),
	},
	"gardeners_seed":
	{
		"name": "Gardener's Seed",
		"kind": Kind.EQUIPMENT,
		"slot": Slot.CHARM,
		"unique": true,
		"text":
		"The first light, kept as a seed. Every life the sword takes gives a little of it back.",
		"stats": {"heal_on_kill": 1},
		"color": Color(1.0, 1.0, 0.85),
	},
}

## Story flags that are things the hero carries, shown in the pack under
## "kept things" (the dialogue sets them; see dialogue/world_1.json).
const KEPT_FLAGS := {
	"item:letter": "Dace's letter",
	"item:warrant": "The warrant",
	"item:hook": "The hook",
	"item:token": "The token",
	"item:scarf": "Thorne's red scarf",
	"item:wheel_tablet": "The Choir's tablet",
	"clock_hand": "The clock's hand",
	"item:clock_weight": "The clock weight",
	"item:clock_chain": "The clock chain",
	"item:clock_key": "The clock key",
}


static func has(id: String) -> bool:
	return TABLE.has(id)


## The item's table entry, or an empty dictionary for an unknown id.
static func get_item(id: String) -> Dictionary:
	return TABLE.get(id, {})


static func display_name(id: String) -> String:
	return str(get_item(id).get("name", id))


static func description(id: String) -> String:
	return str(get_item(id).get("text", ""))


static func color(id: String) -> Color:
	return get_item(id).get("color", Color.WHITE)


static func is_equipment(id: String) -> bool:
	return get_item(id).get("kind", -1) == Kind.EQUIPMENT


static func is_consumable(id: String) -> bool:
	return get_item(id).get("kind", -1) == Kind.CONSUMABLE


static func is_unique(id: String) -> bool:
	return bool(get_item(id).get("unique", false))


## The slot a piece of equipment goes in, or -1 for anything else.
static func slot_of(id: String) -> int:
	return int(get_item(id).get("slot", -1))


static func stats(id: String) -> Dictionary:
	return get_item(id).get("stats", {})


## The story flag set when a boss's unique loot has been picked up.
static func loot_flag(id: String) -> String:
	return "loot:%s" % id


## One short line of what a piece does, for the pack and the traders:
## "+1 damage, reach x1.3".
static func summary(id: String) -> String:
	var parts: PackedStringArray = []
	var values := stats(id)
	for stat in values:
		var value: float = values[stat]
		match stat:
			"damage":
				parts.append("%+d damage" % int(value))
			"guard":
				parts.append("%+d guard" % int(value))
			"hearts":
				parts.append("%+d hearts" % int(value))
			"charges":
				parts.append("%+d healing charges" % int(value))
			"heal":
				parts.append("%+d heart per heal" % int(value))
			"mote_chance":
				parts.append("more heal motes")
			"halo_at_rest":
				parts.append("a Halo at every rest")
			"heal_on_kill":
				parts.append("%+d heart per kill" % int(value))
			"reach":
				parts.append("reach x%.2g" % value)
			"attack_time":
				parts.append("swing %s" % ("slower" if value > 1.0 else "faster"))
			"speed":
				parts.append("%s feet" % ("slower" if value < 1.0 else "faster"))
			"dash_cooldown":
				parts.append("dash back x%.2g sooner" % (1.0 / value))
			"light":
				parts.append("light x%.2g" % value)
			"prices":
				parts.append("prices x%.2g" % value)
	return ", ".join(parts)
