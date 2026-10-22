class_name Quests
extends RefCounted

## The promises the hero makes on the way down: the side quests, read off
## the story flags. A quest starts when its `start` flag is set, is done
## when its `done` flag is, and its steps are the flags set along the way,
## so the log in the pause menu needs nothing but Story.flags. The flags
## are the ones the dialogue entries set (see dialogue/world_1.json).

const LIST: Array[Dictionary] = [
	{
		"id": "scarf",
		"title": "The red scarf",
		"start": "quest:scarf",
		"done": "quest:scarf:done",
		"steps":
		[
			{"text": "Find the lad in the red scarf, down in Violence", "flag": "item:scarf"},
			{"text": "Bring the scarf back to Corporal Dace", "flag": "quest:scarf:done"},
		],
	},
	{
		"id": "drawings",
		"title": "Hanne's drawings",
		"start": "quest:drawings",
		"done": "quest:drawings:done",
		"steps":
		[
			{"text": "Tell Hanne about the names in the shaft", "flag": "drawing:shaft"},
			{"text": "Tell Hanne about the wall that says DON'T FOLLOW", "flag": "drawing:wall"},
			{"text": "Tell Hanne about the pipes", "flag": "drawing:pipes"},
			{"text": "Tell Hanne what Tobiah looks like", "flag": "quest:drawings:done"},
		],
	},
	{
		"id": "wife",
		"title": "Orla Pell",
		"start": "quest:wife",
		"done": "quest:wife:done",
		"steps":
		[
			{"text": "Ask the gathered in the orchard about Orla", "flag": "wife_word"},
			{"text": "Tell Old Pell what she said", "flag": "quest:wife:done"},
		],
	},
	{
		"id": "song",
		"title": "Ithuriel's song",
		"start": "quest:song",
		"done": "quest:song:done",
		"steps":
		[
			{"text": "Ask below where the song comes from", "flag": "song_word"},
			{"text": "Tell Ithuriel what he is humming", "flag": "quest:song:done"},
		],
	},
]


## The quest a flag starts, or an empty dictionary.
static func started_by(flag: String) -> Dictionary:
	for quest in LIST:
		if quest["start"] == flag:
			return quest
	return {}


## The quest a flag finishes, or an empty dictionary.
static func finished_by(flag: String) -> Dictionary:
	for quest in LIST:
		if quest["done"] == flag:
			return quest
	return {}


## The quests the hero has taken on, open ones first.
static func active() -> Array[Dictionary]:
	var open: Array[Dictionary] = []
	var done: Array[Dictionary] = []
	for quest in LIST:
		if not Story.has_flag(quest["start"]):
			continue
		if Story.has_flag(quest["done"]):
			done.append(quest)
		else:
			open.append(quest)
	return open + done


static func is_done(quest: Dictionary) -> bool:
	return Story.has_flag(quest["done"])


## The steps of a quest with whether each is done.
static func steps(quest: Dictionary) -> Array[Dictionary]:
	var result: Array[Dictionary] = []
	for step in quest["steps"]:
		result.append({"text": step["text"], "done": Story.has_flag(step["flag"])})
	return result
