class_name Skills
extends RefCounted

## The hero's experience, his level and the skills he has put it into.
## Every kill gives experience (`xp` in the enemy's loot table, see Loot);
## enough of it is a level, and every level after the first is a skill
## point to spend at a checkpoint camp (see CheckpointMenu and SkillMenu).
## Each skill is a few ranks of one number: a heart, a point of damage, a
## little speed. Held in static variables like Inventory, so the kills,
## the camp and the HUD reach one state; main.gd writes it into the save
## under "skills" and reads it back. See docs/design/progression.md.
##
## A skill's `stats` use the item vocabulary (see Items): additive stats
## are added once per rank, multiplying stats multiplied once per rank, so
## Skills.bonus and Skills.factor sit next to Inventory's in the hero.

## The level the experience curve stops at: the points it gives are all
## there are to spend.
const MAX_LEVEL := 20
## Experience from one level to the next: a flat part and a part that
## grows with the level, so the early levels come quickly and the deep
## circles still pay out.
const XP_BASE := 25
const XP_PER_LEVEL := 10

## Every skill by id: its name, a line in the voice of the relics, how
## many ranks it has, the level it needs before a point can go into it,
## and what one rank does.
const TABLE := {
	"vigour":
	{
		"name": "Vigour",
		"text": "More of him holds together. One more heart for every rank.",
		"ranks": 3,
		"level": 1,
		"stats": {"hearts": 1},
	},
	"edge":
	{
		"name": "Edge",
		"text": "He learns where the sword goes in. One more damage for every rank.",
		"ranks": 3,
		"level": 1,
		"stats": {"damage": 1},
	},
	"swift_feet":
	{
		"name": "Swift Feet",
		"text": "The hurry the Making put in him, kept. He runs a little faster.",
		"ranks": 3,
		"level": 1,
		"stats": {"speed": 1.08},
	},
	"deep_draught":
	{
		"name": "Deep Draught",
		"text": "He learns to make the Basin's water last. One more healing charge.",
		"ranks": 2,
		"level": 3,
		"stats": {"charges": 1},
	},
	"long_reach":
	{
		"name": "Long Reach",
		"text": "The swing starts from the shoulder. The sword reaches further.",
		"ranks": 2,
		"level": 3,
		"stats": {"reach": 1.15},
	},
	"quick_dash":
	{
		"name": "Quick Dash",
		"text": "The light in his legs gathers sooner. The dash comes back faster.",
		"ranks": 2,
		"level": 4,
		"stats": {"dash_cooldown": 0.8},
	},
	"lightkeeper":
	{
		"name": "Lightkeeper",
		"text":
		"What leaks from a killed thing finds him. The light of a kill comes to him fuller.",
		"ranks": 2,
		"level": 5,
		"stats": {"light": 1.25},
	},
	"iron_skin":
	{
		"name": "Iron Skin",
		"text": "He stops flinching before the blow lands. Every hit takes one heart less.",
		"ranks": 2,
		"level": 6,
		"stats": {"guard": 1},
	},
	"quick_hand":
	{
		"name": "Quick Hand",
		"text": "The sword comes round again before they see it. The swing is faster.",
		"ranks": 2,
		"level": 8,
		"stats": {"attack_time": 0.88},
	},
	"mender":
	{
		"name": "Mender",
		"text": "A healing charge closes more of the wound. One more heart for every heal.",
		"ranks": 1,
		"level": 10,
		"stats": {"heal": 1},
	},
}

## Experience gathered this save, never lost.
static var xp := 0
## Skill id -> the rank bought, for the skills with any.
static var ranks: Dictionary = {}


## A new game starts at the first level with nothing learned.
static func reset() -> void:
	xp = 0
	ranks = {}


# --- Experience and levels ---------------------------------------------------


## Experience needed to go from `level` to the next (0 at the cap).
static func xp_to_next(level: int) -> int:
	if level >= MAX_LEVEL:
		return 0
	return XP_BASE + XP_PER_LEVEL * (level - 1)


## Experience needed, from nothing, to reach `level`.
static func xp_for_level(level: int) -> int:
	var total := 0
	for step in range(1, mini(level, MAX_LEVEL)):
		total += xp_to_next(step)
	return total


## The level `amount` experience makes.
static func level_for(amount: int) -> int:
	var level := 1
	while level < MAX_LEVEL and amount >= xp_for_level(level + 1):
		level += 1
	return level


static func level() -> int:
	return level_for(xp)


## Experience past the start of the current level, and how much the level
## takes; [0, 0] at the cap.
static func level_progress() -> Array[int]:
	var current := level()
	if current >= MAX_LEVEL:
		return [0, 0]
	return [xp - xp_for_level(current), xp_to_next(current)]


## Adds experience. Returns how many levels it made, so the hero can say.
static func add_xp(amount: int) -> int:
	if amount <= 0:
		return 0
	var before := level()
	xp += amount
	return level() - before


# --- Skill points -----------------------------------------------------------


## Every level after the first is a point.
static func points_earned() -> int:
	return level() - 1


static func points_spent() -> int:
	var total := 0
	for id in ranks:
		total += int(ranks[id])
	return total


static func points_left() -> int:
	return points_earned() - points_spent()


# --- Skills ----------------------------------------------------------------


static func has(id: String) -> bool:
	return TABLE.has(id)


static func get_skill(id: String) -> Dictionary:
	return TABLE.get(id, {})


static func display_name(id: String) -> String:
	return str(get_skill(id).get("name", id))


static func description(id: String) -> String:
	return str(get_skill(id).get("text", ""))


static func max_rank(id: String) -> int:
	return int(get_skill(id).get("ranks", 0))


## The level a skill asks for before its first point.
static func level_needed(id: String) -> int:
	return int(get_skill(id).get("level", 1))


static func rank(id: String) -> int:
	return int(ranks.get(id, 0))


## The ids in table order, for the menu.
static func ids() -> Array[String]:
	var result: Array[String] = []
	for id: String in TABLE:
		result.append(id)
	return result


## Why a point cannot go into a skill right now, or "" when it can.
static func blocked_reason(id: String) -> String:
	if not has(id):
		return "No such skill"
	if rank(id) >= max_rank(id):
		return "%s is learned to the end" % display_name(id)
	if level() < level_needed(id):
		return "%s needs level %d" % [display_name(id), level_needed(id)]
	if points_left() <= 0:
		return "No skill point to spend: the next level gives one"
	return ""


## Spends a point on the next rank of a skill. Returns "" when it was
## spent, else why it was not.
static func raise(id: String) -> String:
	var reason := blocked_reason(id)
	if not reason.is_empty():
		return reason
	ranks[id] = rank(id) + 1
	return ""


## The skills' additive stat, summed over every rank (0 when none give it).
static func bonus(stat: String) -> float:
	var total := 0.0
	for id in ranks:
		total += float(get_skill(str(id)).get("stats", {}).get(stat, 0.0)) * rank(str(id))
	return total


## The skills' multiplying stat, multiplied once per rank (1 when none
## change it).
static func factor(stat: String) -> float:
	var total := 1.0
	for id in ranks:
		var value := float(get_skill(str(id)).get("stats", {}).get(stat, 1.0))
		total *= pow(value, rank(str(id)))
	return total


## One short line of what a rank does, in the item vocabulary: "+1 heart".
static func summary(id: String) -> String:
	var parts: PackedStringArray = []
	var values: Dictionary = get_skill(id).get("stats", {})
	for stat in values:
		var value: float = values[stat]
		match stat:
			"hearts":
				parts.append("%+d heart" % int(value))
			"charges":
				parts.append("%+d healing charge" % int(value))
			"damage":
				parts.append("%+d damage" % int(value))
			"guard":
				parts.append("%+d guard" % int(value))
			"heal":
				parts.append("%+d heart per heal" % int(value))
			"speed":
				parts.append("feet x%.2f" % value)
			"reach":
				parts.append("reach x%.2f" % value)
			"dash_cooldown":
				parts.append("dash back x%.2f sooner" % (1.0 / value))
			"attack_time":
				parts.append("swing x%.2f faster" % (1.0 / value))
			"light":
				parts.append("light x%.2f" % value)
			_:
				parts.append("%s %s" % [stat, str(value)])
	return ", ".join(parts) + " per rank"


# --- Save -------------------------------------------------------------------


static func to_dict() -> Dictionary:
	return {"xp": xp, "ranks": ranks.duplicate()}


## Replaces the progress with the one from a save; a skill the table no
## longer knows is dropped, a rank past the table's is cut to it, and
## points spent past the ones earned are given back.
static func from_dict(data: Dictionary) -> void:
	reset()
	xp = maxi(int(data.get("xp", 0)), 0)
	var saved: Variant = data.get("ranks", {})
	if saved is Dictionary:
		for id in saved:
			var value := mini(int(saved[id]), max_rank(str(id)))
			if has(str(id)) and value > 0:
				ranks[str(id)] = value
	while points_left() < 0 and not ranks.is_empty():
		var last := str(ranks.keys().back())
		ranks[last] = rank(last) - 1
		if ranks[last] <= 0:
			ranks.erase(last)
