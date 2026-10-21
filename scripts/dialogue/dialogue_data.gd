class_name DialogueData
extends RefCounted

## The dialogue of one level, read from a JSON file (see dialogue/world_1.json).
##
## Each entry is keyed by id and has:
##   lines     a list of {"who": speaker, "text": one box of text}
##   requires  flags that must all be set for the entry to play (optional)
##   unless    flags that must all be unset for the entry to play (optional)
##   sets      a flag, or a list of flags, set when the entry has been read to
##             the end (optional)
##   then      the id of the entry that plays right after this one (optional)
##   warp      "start": the hero goes back to the level's start when the
##             entry ends (optional; the clock ending)
##   save      true for an entry that saves the game when it ends (optional;
##             the point of no return)
##   once      true for an entry that plays a single time per save (optional);
##             every entry read to the end sets the flag `seen:<id>`, and a
##             `once` entry is skipped when its flag is set
##
## An NPC or trigger lists several ids in priority order; `pick` returns the
## first whose flags match, so lines change as the story moves on. Once the
## unique entries are used up, an NPC rotates through its repeat lines (see
## Interactable.rotation_ids).

var path := ""
var entries: Dictionary = {}


func _init(p_path: String) -> void:
	path = p_path
	if not FileAccess.file_exists(path):
		push_error("DialogueData: no dialogue file at %s" % path)
		return
	var parsed: Variant = JSON.parse_string(FileAccess.get_file_as_string(path))
	if parsed is Dictionary:
		entries = parsed
	else:
		push_error("DialogueData: could not read %s" % path)


## The entry with this id, with its id filled in, or an empty dictionary.
func entry(id: String) -> Dictionary:
	var found: Variant = entries.get(id)
	if not found is Dictionary or not found.get("lines", []) is Array:
		return {}
	var result: Dictionary = found.duplicate()
	result["id"] = id
	return result


## Whether the entry's flag conditions hold right now.
func available(data: Dictionary) -> bool:
	if data.is_empty():
		return false
	if bool(data.get("once", false)) and Story.has_flag(seen_flag(str(data.get("id", "")))):
		return false
	return Story.has_all(data.get("requires", [])) and not Story.has_any(data.get("unless", []))


## A line may also carry `choices`: a list of {text, then}. The box offers
## them once the line is typed out, and the picked one's `then` becomes
## the entry's `then` (see DialogueBox.choose).
static func choices(data: Dictionary, index: int) -> Array:
	var lines: Array = data.get("lines", [])
	if index < 0 or index >= lines.size() or not lines[index] is Dictionary:
		return []
	var raw: Variant = lines[index].get("choices", [])
	return raw if raw is Array else []


## The entry to play right after this one, or "".
static func next_id(data: Dictionary) -> String:
	return str(data.get("then", ""))


## The flags an entry sets when it has been read to the end.
static func sets(data: Dictionary) -> Array[String]:
	var result: Array[String] = []
	var value: Variant = data.get("sets", "")
	for flag in value if value is Array else [value]:
		if not str(flag).is_empty():
			result.append(str(flag))
	return result


## The flag set once an entry has been read to the end.
static func seen_flag(id: String) -> String:
	return "seen:%s" % id


## The first of the ids whose conditions hold, or an empty dictionary when
## none does (then nothing plays).
func pick(ids: Array) -> Dictionary:
	for id in ids:
		var data := entry(str(id))
		if available(data):
			return data
	return {}


## The speaker and text of one box, with missing fields filled in.
static func line(data: Dictionary, index: int) -> Dictionary:
	var lines: Array = data.get("lines", [])
	if index < 0 or index >= lines.size():
		return {"who": "", "text": ""}
	var raw: Variant = lines[index]
	if raw is Dictionary:
		return {"who": str(raw.get("who", "")), "text": str(raw.get("text", ""))}
	return {"who": "", "text": str(raw)}


static func line_count(data: Dictionary) -> int:
	return data.get("lines", []).size()
