extends Node

## Unit and data tests: small checks that need no world, run headless by
## CI next to the smoke test (see .github/workflows/tests.yml):
##
##   godot --headless --path . res://tests/unit_test.tscn
##
## The data tests read the files the game reads (the placements, the
## dialogue, the sprite spec, the region art) and check that every name
## in one resolves in another: a placement's character has sprites, a
## dialogue id exists, a flag something requires is set somewhere, a boss
## has the strips its moves use. A failing check names the entry, so a
## broken reference is found before the smoke test walks past it. The
## unit tests cover the pure pieces: the story flags, the dialogue data,
## the quests, the levels registry, the upgrade flags, the map, the
## save slots, the button prompts and the developer console.
##
## Every method named test_* runs once, in order; `_check` counts.

const PLACEMENTS_PATH := "res://levels/world/placements.json"
const DIALOGUE_PATH := "res://dialogue/world_1.json"
const WORLD_PATH := "res://levels/world/world.json"
const SPRITES_PATH := "res://assets/sprites/sprites.json"
const REGIONS_DIR := "res://assets/regions"
const SFX_DIR := "res://assets/audio/sfx"
## A scratch dialogue file for the DialogueData tests.
const SCRATCH_DIALOGUE := "user://unit_test_dialogue.json"
const UPGRADE_KINDS: Array[String] = ["heart", "vial", "iron"]
const POWER_KINDS: Array[String] = ["halo", "blade", "zeal"]
const PLACEMENT_TYPES: Array[String] = [
	"npc", "item", "trigger", "enemy", "upgrade", "power", "barrier"
]
## The flags the code sets without a dialogue entry: their prefixes.
const CODE_FLAG_PREFIXES: Array[String] = [
	"boss:", "secret:", "upgrade:", "sword:", "seen:", "room:"
]

var _checks := 0
var _failures := 0
var _section := ""


func _ready() -> void:
	for method in get_method_list():
		var name := str(method["name"])
		if name.begins_with("test_"):
			_section = name.trim_prefix("test_")
			Story.reset()
			call(name)
	Story.reset()
	print("%d checks, %d failed" % [_checks, _failures])
	get_tree().quit(1 if _failures > 0 else 0)


# Data


func test_placements() -> void:
	var placements := _json(PLACEMENTS_PATH)
	var world := _json(WORLD_PATH)
	var dialogue := DialogueData.new(str(placements.get("dialogue", "")))
	var sprites := _json(SPRITES_PATH)
	_check(not dialogue.entries.is_empty(), "the placements name a dialogue file that reads")
	var objects: Array = placements.get("objects", [])
	_check(objects.size() > 200, "the world has its objects (%d)" % objects.size())
	var width := int(world.get("width", 0))
	var height := int(world.get("height", 0))
	var bad := 0
	for i in objects.size():
		var spec: Dictionary = objects[i]
		var type := str(spec.get("type", ""))
		var where := "object %d (%s at %d, %d)" % [i, type, spec.get("x", -1), spec.get("y", -1)]
		if type not in PLACEMENT_TYPES:
			bad += _fail("%s: unknown type" % where)
			continue
		var x := int(spec.get("x", -1))
		var y := int(spec.get("y", -1))
		if x < 0 or y < 0 or x >= width or y >= height:
			bad += _fail("%s: outside the world" % where)
		match type:
			"npc", "item", "trigger":
				bad += _check_story_object(spec, where, dialogue, sprites)
			"enemy":
				bad += _check_enemy(spec, where, sprites)
			"upgrade":
				if str(spec.get("upgrade", "")) not in UPGRADE_KINDS:
					bad += _fail("%s: unknown upgrade '%s'" % [where, spec.get("upgrade", "")])
			"power":
				if str(spec.get("power", "")) not in POWER_KINDS:
					bad += _fail("%s: unknown power '%s'" % [where, spec.get("power", "")])
			"barrier":
				if int(spec.get("height", 0)) <= 0:
					bad += _fail("%s: a barrier needs a height" % where)
	_check(bad == 0, "every placement resolves (%d bad)" % bad)


func _check_story_object(
	spec: Dictionary, where: String, dialogue: DialogueData, sprites: Dictionary
) -> int:
	var bad := 0
	for key in ["dialogue", "rotate"]:
		for id in spec.get(key, []):
			if dialogue.entry(str(id)).is_empty():
				bad += _fail("%s: no dialogue entry '%s'" % [where, id])
	if str(spec.get("type", "")) == "npc":
		var character := str(spec.get("character", ""))
		if not sprites.has(character):
			bad += _fail("%s: no sprites for '%s'" % [where, character])
		elif not sprites[character]["animations"].has("idle"):
			bad += _fail("%s: '%s' has no idle" % [where, character])
	if str(spec.get("type", "")) == "item" and str(spec.get("item", "")).is_empty():
		bad += _fail("%s: an item needs an id" % where)
	return bad


func _check_enemy(spec: Dictionary, where: String, sprites: Dictionary) -> int:
	var bad := 0
	var character := str(spec.get("character", "imp"))
	var behaviour := str(spec.get("behaviour", ""))
	if character in Walker.CHARACTERS:
		if not behaviour.is_empty() and behaviour not in Walker.BEHAVIOURS:
			bad += _fail("%s: no walker habit '%s'" % [where, behaviour])
		var barks := str(spec.get("barks", ""))
		if not barks.is_empty() and not Walker.BARKS.has(barks):
			bad += _fail("%s: no bark set '%s'" % [where, barks])
	elif character in Flyer.CHARACTERS:
		if behaviour != "" and behaviour != "dive":
			bad += _fail("%s: a flyer only dives ('%s')" % [where, behaviour])
	elif character == "hanged":
		character = Hanger.CHARACTER
	elif not Boss.has_profile(character):
		return _fail("%s: no enemy or boss called '%s'" % [where, character])
	else:
		character = Boss.profile_for(character).character
	if not sprites.has(character):
		bad += _fail("%s: no sprites for '%s'" % [where, character])
	return bad


func test_dialogue() -> void:
	var dialogue := DialogueData.new(DIALOGUE_PATH)
	var placements := _json(PLACEMENTS_PATH)
	_check(
		dialogue.entries.size() > 100, "the dialogue has its entries (%d)" % dialogue.entries.size()
	)
	# What sets each flag: an entry's `sets`, an entry read to the end, a
	# relic picked up, a boss killed, an upgrade taken, or the code.
	var set_by: Dictionary = {}
	for id: String in dialogue.entries:
		set_by[DialogueData.seen_flag(id)] = id
		for flag in DialogueData.sets(dialogue.entries[id]):
			set_by[flag] = id
	for spec: Dictionary in placements.get("objects", []):
		if str(spec.get("type", "")) == "item":
			set_by[LoreItem.flag_for(str(spec.get("item", "")))] = "a relic"
	for id: String in Boss.IDS:
		set_by[Boss.flag_for(id)] = "the boss"
	var bad := 0
	var choices := 0
	for id: String in dialogue.entries:
		var raw: Dictionary = dialogue.entries[id]
		var lines: Array = raw.get("lines", [])
		if lines.is_empty():
			bad += _fail("%s: no lines" % id)
		var next := DialogueData.next_id(raw)
		if not next.is_empty() and not dialogue.entries.has(next):
			bad += _fail("%s: then '%s' does not exist" % [id, next])
		var gift := str(raw.get("gives", ""))
		if not gift.is_empty() and gift not in UPGRADE_KINDS:
			bad += _fail("%s: gives an unknown upgrade '%s'" % [id, gift])
		var warp := str(raw.get("warp", ""))
		if not warp.is_empty() and warp != "start":
			bad += _fail("%s: warps to an unknown place '%s'" % [id, warp])
		for key in ["requires", "unless"]:
			for flag in _as_list(raw.get(key, [])):
				if not _flag_known(str(flag), set_by):
					bad += _fail("%s: %s '%s', which nothing sets" % [id, key, flag])
		for i in lines.size():
			if not lines[i] is Dictionary:
				continue
			var line: Dictionary = lines[i]
			if str(line.get("text", "")).is_empty():
				bad += _fail("%s: line %d has no text" % [id, i])
			for key in ["requires", "unless"]:
				for flag in _as_list(line.get(key, [])):
					if not _flag_known(str(flag), set_by):
						bad += _fail("%s: line %d %s '%s', which nothing sets" % [id, i, key, flag])
			for choice in DialogueData.choices(raw, i):
				choices += 1
				var then := str(choice.get("then", ""))
				if str(choice.get("text", "")).is_empty():
					bad += _fail("%s: line %d has a choice without text" % [id, i])
				if then.is_empty() or not dialogue.entries.has(then):
					bad += _fail("%s: line %d has a choice leading to '%s'" % [id, i, then])
	_check(choices >= 3, "the script has its choices (%d)" % choices)
	_check(bad == 0, "every dialogue entry resolves (%d bad)" % bad)
	# The quests' flags come from the dialogue too.
	bad = 0
	for quest: Dictionary in Quests.LIST:
		for flag in [quest["start"], quest["done"]]:
			if not _flag_known(str(flag), set_by):
				bad += _fail("quest %s: '%s' is set by nothing" % [quest["id"], flag])
		for step: Dictionary in quest["steps"]:
			if not _flag_known(str(step["flag"]), set_by):
				bad += _fail("quest %s: step '%s' is set by nothing" % [quest["id"], step["flag"]])
	_check(bad == 0, "every quest flag is set somewhere (%d bad)" % bad)
	# Every boss speaks before and after its fight.
	bad = 0
	for id: String in Boss.IDS:
		for part in ["intro", "defeat"]:
			if dialogue.entry("%s_%s" % [id, part]).is_empty():
				bad += _fail("%s has no %s lines" % [id, part])
	_check(bad == 0, "every boss has its intro and its last lines (%d bad)" % bad)


func _flag_known(flag: String, set_by: Dictionary) -> bool:
	if set_by.has(flag):
		return true
	for prefix in CODE_FLAG_PREFIXES:
		if flag.begins_with(prefix):
			return true
	return false


func test_sprites() -> void:
	var sprites := _json(SPRITES_PATH)
	var bad := 0
	for character: String in sprites:
		var spec: Dictionary = sprites[character]
		var frame_w := int(spec["frame_size"][0])
		var frame_h := int(spec["frame_size"][1])
		for anim_name: String in spec["animations"]:
			var anim: Dictionary = spec["animations"][anim_name]
			var path := "%s/%s/%s" % [AnimLoader.SPRITES_DIR, character, anim["file"]]
			var texture := load(path) as Texture2D
			if texture == null:
				bad += _fail("%s.%s: no strip at %s" % [character, anim_name, path])
				continue
			var frames := int(anim["frames"])
			if texture.get_width() != frame_w * frames or texture.get_height() != frame_h:
				bad += _fail(
					(
						"%s.%s: the strip is %dx%d, not %d frames of %dx%d"
						% [
							character,
							anim_name,
							texture.get_width(),
							texture.get_height(),
							frames,
							frame_w,
							frame_h
						]
					)
				)
	_check(bad == 0, "every strip in the sprite spec exists and is cut right (%d bad)" % bad)
	# Every character's SpriteFrames resource matches the spec it was cut
	# from, so a strip changed without running tools/sprite_frames.py is
	# caught here.
	bad = 0
	for character: String in sprites:
		bad += _check_frames(character, sprites[character])
	_check(bad == 0, "every character's SpriteFrames matches the sprite spec (%d bad)" % bad)


func _check_frames(character: String, spec: Dictionary) -> int:
	var bad := 0
	var path := AnimLoader.frames_path(character)
	var frames := load(path) as SpriteFrames
	if frames == null:
		return _fail("%s: no SpriteFrames at %s (run tools/sprite_frames.py)" % [character, path])
	var origin := Vector2(float(spec["origin"][0]), float(spec["origin"][1]))
	if AnimLoader.origin(frames) != origin:
		bad += _fail(
			"%s: the frames' origin is %s, not %s" % [character, AnimLoader.origin(frames), origin]
		)
	var size := Vector2(float(spec["frame_size"][0]), float(spec["frame_size"][1]))
	if AnimLoader.frame_size(frames) != size:
		bad += _fail(
			"%s: the frames are %s, not %s" % [character, AnimLoader.frame_size(frames), size]
		)
	var anims: Dictionary = spec["animations"]
	if frames.get_animation_names().size() != anims.size():
		bad += _fail(
			(
				"%s: %d animations in the frames, %d in the spec"
				% [character, frames.get_animation_names().size(), anims.size()]
			)
		)
	for anim_name: String in anims:
		var anim: Dictionary = anims[anim_name]
		if not frames.has_animation(anim_name):
			bad += _fail("%s.%s: not in the frames" % [character, anim_name])
		elif (
			frames.get_frame_count(anim_name) != int(anim["frames"])
			or frames.get_animation_speed(anim_name) != float(anim["fps"])
			or frames.get_animation_loop(anim_name) != bool(anim["loop"])
		):
			bad += _fail("%s.%s: the frames differ from the spec" % [character, anim_name])
	return bad


## Every enemy body has its profile, its scene and the strips its script
## plays, and nothing in the resource and scene folders is unlisted.
func test_enemies() -> void:
	var bad := 0
	for character: String in Walker.CHARACTERS:
		bad += _check_enemy_scene(character, Walker, WalkerProfile)
	for character: String in Flyer.CHARACTERS:
		bad += _check_enemy_scene(character, Flyer, FlyerProfile)
	for anim_name in ["idle", "attack", "hurt", "death"]:
		if not AnimLoader.frames(Hanger.CHARACTER).has_animation(anim_name):
			bad += _fail("the hanged thing has no %s strip" % anim_name)
	for id: String in Boss.IDS:
		var profile := Boss.profile_for(id)
		if profile == null:
			bad += _fail("boss %s has no profile in %s" % [id, Boss.PROFILES_DIR])
			continue
		var frames := AnimLoader.frames(profile.character)
		for anim_name in ["idle", profile.walk_anim, "attack", "hurt"]:
			if not frames.has_animation(anim_name):
				bad += _fail("boss %s (%s) has no %s strip" % [id, profile.character, anim_name])
	_check(bad == 0, "every enemy has its profile, scene and strips (%d bad)" % bad)
	bad = 0
	bad += _check_listed(Walker.PROFILES_DIR, Walker.CHARACTERS + Flyer.CHARACTERS)
	bad += _check_listed(Boss.PROFILES_DIR, Boss.IDS)
	var scenes: Array[String] = ["walker", "flyer", "hanger", "boss"]
	scenes.append_array(Walker.CHARACTERS)
	scenes.append_array(Flyer.CHARACTERS)
	bad += _check_listed(LevelBuilder.ENEMY_SCENES_DIR, scenes)
	_check(bad == 0, "every enemy resource and scene is listed in its script (%d bad)" % bad)


## A body's scene is an inherited scene of the right kind that wears the
## body's profile, with the frames its script plays set on the sprite and
## placed on its origin.
func _check_enemy_scene(character: String, kind: Variant, profile_kind: Variant) -> int:
	var bad := 0
	var profile: Resource = load("%s/%s.tres" % [Walker.PROFILES_DIR, character])
	if not is_instance_of(profile, profile_kind):
		return _fail(
			"%s: no %s in %s" % [character, profile_kind.get_global_name(), Walker.PROFILES_DIR]
		)
	var scene := LevelBuilder.enemy_scene(character)
	if scene == null:
		return _fail("%s: no scene in %s" % [character, LevelBuilder.ENEMY_SCENES_DIR])
	var enemy: Node = scene.instantiate()
	if not is_instance_of(enemy, kind):
		bad += _fail("%s: the scene is not a %s" % [character, kind.get_global_name()])
	elif enemy.profile != profile:
		bad += _fail("%s: the scene wears the wrong profile" % character)
	var sprite := enemy.get_node_or_null("Sprite") as AnimatedSprite2D
	var frames := AnimLoader.frames(character)
	if sprite == null or sprite.sprite_frames != frames:
		bad += _fail(
			(
				"%s: the scene's sprite does not use %s"
				% [character, AnimLoader.frames_path(character)]
			)
		)
	elif sprite.offset != AnimLoader.offset(frames):
		bad += _fail(
			(
				"%s: the sprite's offset is %s, not %s"
				% [character, sprite.offset, AnimLoader.offset(frames)]
			)
		)
	var walk: String = profile.walk_anim if profile is WalkerProfile else "idle"
	for anim_name in ["idle", walk, "attack", "hurt", "death"]:
		if not frames.has_animation(anim_name):
			bad += _fail("%s has no %s strip" % [character, anim_name])
	enemy.free()
	return bad


## Every .tres or .tscn in the folder is one of the names, and each name
## is in the folder.
func _check_listed(dir_path: String, names: Array[String]) -> int:
	var bad := 0
	var dir := DirAccess.open(dir_path)
	if dir == null:
		return _fail("%s does not open" % dir_path)
	var found: Array[String] = []
	for file_name in dir.get_files():
		if file_name.get_extension() in ["tres", "tscn"]:
			found.append(file_name.get_basename())
	for name in names:
		if name not in found:
			bad += _fail("%s is not in %s" % [name, dir_path])
	for name in found:
		if name not in names:
			bad += _fail("%s/%s is listed in no script" % [dir_path, name])
	return bad


func test_region_art() -> void:
	var dir := DirAccess.open(REGIONS_DIR)
	_check(dir != null, "the region art folder opens")
	if dir == null:
		return
	var regions := 0
	var bad := 0
	for region in dir.get_directories():
		regions += 1
		for file_name in ["edges.json", "parallax.json", "fx.json"]:
			var path := "%s/%s/%s" % [REGIONS_DIR, region, file_name]
			if not FileAccess.file_exists(path):
				continue
			var data := _json(path)
			if data.is_empty():
				bad += _fail("%s/%s does not read" % [region, file_name])
				continue
			for png in _pngs_named(data):
				if not FileAccess.file_exists("%s/%s/%s" % [REGIONS_DIR, region, png]):
					bad += _fail("%s/%s names %s, which is missing" % [region, file_name, png])
	_check(regions == 12, "every region has an art folder (%d)" % regions)
	_check(bad == 0, "every PNG the region JSONs name exists (%d bad)" % bad)


## Every string ending in .png anywhere in a JSON value.
func _pngs_named(value: Variant) -> Array[String]:
	var found: Array[String] = []
	if value is String and str(value).ends_with(".png"):
		found.append(str(value))
	elif value is Array:
		for item in value:
			found.append_array(_pngs_named(item))
	elif value is Dictionary:
		for key in value:
			found.append_array(_pngs_named(value[key]))
	return found


func test_sounds() -> void:
	# Every sound the scripts play by name has a file.
	var names: Dictionary = {}
	var pattern := RegEx.new()
	pattern.compile('Audio\\.play\\(\\s*"([a-z_]+)"')
	for path in _scripts_under("res://scripts"):
		for found in pattern.search_all(FileAccess.get_file_as_string(path)):
			names[found.get_string(1)] = path
	_check(names.size() >= 15, "the scripts play their sounds by name (%d)" % names.size())
	var bad := 0
	for sound_name: String in names:
		if not FileAccess.file_exists("%s/%s.wav" % [SFX_DIR, sound_name]):
			bad += _fail("%s plays '%s', which has no file" % [names[sound_name], sound_name])
	_check(bad == 0, "every sound played has a file (%d bad)" % bad)


func _scripts_under(dir_path: String) -> Array[String]:
	var found: Array[String] = []
	var dir := DirAccess.open(dir_path)
	if dir == null:
		return found
	for sub in dir.get_directories():
		found.append_array(_scripts_under("%s/%s" % [dir_path, sub]))
	for file_name in dir.get_files():
		if file_name.ends_with(".gd"):
			found.append("%s/%s" % [dir_path, file_name])
	return found


# Units


func test_story() -> void:
	_check(not Story.has_flag("a"), "a fresh story has no flags")
	Story.set_flag("a")
	Story.set_flag("")
	_check(
		Story.has_flag("a") and Story.flags.size() == 1,
		"a flag is set once, and never an empty one"
	)
	_check(Story.has_all(["a"]) and not Story.has_all(["a", "b"]), "has_all wants every flag")
	_check(Story.has_any(["b", "a"]) and not Story.has_any(["b"]), "has_any wants one")
	_check(Story.has_all([]) and not Story.has_any([]), "no flags: all hold, none do")
	Story.set_flag("c")
	Story.set_flag("b")
	_check(Story.to_list() == ["a", "b", "c"], "the list for the save is sorted")
	Story.from_list(["z", "", 7])
	_check(Story.to_list() == ["7", "z"], "a save's list replaces the flags, dropping empties")
	Story.reset()
	_check(Story.flags.is_empty(), "reset clears them")


func test_dialogue_data() -> void:
	var entries := {
		"plain": {"lines": [{"who": "A", "text": "one"}, "a bare line"]},
		"gated": {"requires": ["key"], "unless": "lock", "lines": [{"who": "", "text": "x"}]},
		"single": {"once": true, "sets": ["s1", "s2"], "then": "plain", "lines": [{"text": "y"}]},
		"picky":
		{
			"lines":
			[
				{"who": "A", "text": "always"},
				{"who": "A", "text": "with key", "requires": "key"},
				{"who": "A", "text": "without lock", "unless": ["lock"]},
			]
		},
		"choice":
		{
			"lines":
			[
				{
					"who": "A",
					"text": "pick",
					"choices":
					[{"text": "left", "then": "plain"}, {"text": "right", "then": "gated"}]
				}
			]
		},
		"empty": {"lines": []},
		"broken": {"lines": "not a list"},
	}
	var file := FileAccess.open(SCRATCH_DIALOGUE, FileAccess.WRITE)
	file.store_string(JSON.stringify(entries))
	file.close()
	var dialogue := DialogueData.new(SCRATCH_DIALOGUE)
	_check(dialogue.entries.size() == 7, "the file reads")
	var plain := dialogue.entry("plain")
	_check(plain.get("id", "") == "plain", "an entry carries its id")
	_check(DialogueData.line(plain, 0) == {"who": "A", "text": "one"}, "a line has who and text")
	_check(
		DialogueData.line(plain, 1) == {"who": "", "text": "a bare line"}, "a bare string is a line"
	)
	_check(DialogueData.line(plain, 9) == {"who": "", "text": ""}, "past the end is empty")
	_check(
		dialogue.entry("missing").is_empty() and dialogue.entry("broken").is_empty(),
		"missing or broken is empty"
	)
	_check(
		not dialogue.available(dialogue.entry("empty")), "an entry with no lines is not available"
	)
	_check(not dialogue.available(dialogue.entry("gated")), "gated: not without the key")
	Story.set_flag("key")
	_check(dialogue.available(dialogue.entry("gated")), "gated: with the key")
	Story.set_flag("lock")
	_check(not dialogue.available(dialogue.entry("gated")), "gated: not once locked")
	_check(
		dialogue.pick(["gated", "missing", "plain"]).get("id", "") == "plain",
		"pick takes the first available"
	)
	_check(dialogue.pick(["gated", "missing"]).is_empty(), "or nothing")
	_check(DialogueData.line_count(dialogue.entry("picky")) == 2, "a line's own flags leave it out")
	Story.reset()
	_check(DialogueData.line_count(dialogue.entry("picky")) == 2, "either way round")
	Story.set_flag("key")
	_check(DialogueData.line_count(dialogue.entry("picky")) == 3, "and in when they hold")
	var single := dialogue.entry("single")
	_check(dialogue.available(single), "a once entry plays the first time")
	_check(DialogueData.sets(single) == ["s1", "s2"], "sets may be a list")
	_check(DialogueData.sets(plain).is_empty(), "or nothing")
	_check(DialogueData.sets({"sets": "one"}) == ["one"], "or one flag")
	_check(
		DialogueData.sets({"sets": ["", "a", null]}) == ["a"], "empty and null flags are dropped"
	)
	_check(
		DialogueData.flags(null).is_empty() and DialogueData.flags("").is_empty(),
		"as is a null or empty field"
	)
	_check(DialogueData.next_id(single) == "plain", "then names the next entry")
	Story.set_flag(DialogueData.seen_flag("single"))
	_check(not dialogue.available(single), "a once entry seen is over")
	_check(DialogueData.choices(dialogue.entry("choice"), 0).size() == 2, "a line's choices")
	_check(DialogueData.choices(dialogue.entry("choice"), 1).is_empty(), "none past the end")
	_check(DialogueData.choices(plain, 0).is_empty(), "none on a plain line")
	DirAccess.remove_absolute(SCRATCH_DIALOGUE)
	var none := DialogueData.new("user://no_such_dialogue.json")
	_check(
		none.entries.is_empty() and none.pick(["plain"]).is_empty(),
		"a missing file is empty, not a crash"
	)


func test_quests() -> void:
	_check(Quests.active().is_empty(), "no promises to start with")
	_check(
		Quests.started_by("quest:scarf").get("id", "") == "scarf", "a start flag names its quest"
	)
	_check(Quests.finished_by("quest:scarf:done").get("id", "") == "scarf", "so does a done flag")
	_check(Quests.started_by("reveal").is_empty(), "and another flag names none")
	Story.set_flag("quest:scarf")
	Story.set_flag("quest:wife")
	Story.set_flag("quest:wife:done")
	var active := Quests.active()
	_check(active.size() == 2, "two promises made")
	_check(
		active[0]["id"] == "scarf" and active[1]["id"] == "wife",
		"the open one first, the kept one last"
	)
	_check(
		not Quests.is_done(active[0]) and Quests.is_done(active[1]), "and each knows which it is"
	)
	Story.set_flag("item:scarf")
	var steps := Quests.steps(active[0])
	_check(
		steps.size() == 2 and steps[0]["done"] and not steps[1]["done"],
		"the steps tick as their flags are set"
	)
	for quest: Dictionary in Quests.LIST:
		_check(
			quest["steps"][-1]["flag"] == quest["done"],
			"%s: the last step is the promise kept" % quest["id"]
		)


func test_levels() -> void:
	_check(Levels.has("world_1") and not Levels.has("heaven_1"), "the world is the level")
	_check(Levels.first_id == "world_1", "a new game starts in it")
	_check(Levels.next_id("world_1").is_empty(), "nothing follows it")
	_check(Levels.next_id("nope").is_empty(), "nor an unknown level")
	_check(Levels.resolve("heaven_1") == "world_1", "an old save's level falls back to the world")
	_check(Levels.resolve("res://levels/world/world.json") == "world_1", "a path resolves too")
	_check(Levels.display_name("world_1").begins_with("World 1"), "it has a name")
	_check(Levels.display_name("nope") == "nope", "an unknown id is its own name")
	_check(Levels.path("world_1") == WORLD_PATH, "and the export's path")
	_check(Levels.sky("nope") == Levels.WORLD_SKY, "an unknown level gets the world's sky")


func test_upgrades() -> void:
	_check(
		Upgrade.flag_for(Player.UpgradeKind.HEART, "world_1", 2) == "upgrade:heart:world_1:2",
		"a pickup's flag"
	)
	_check(
		(
			Upgrade.flag_for(Player.UpgradeKind.VIAL, "world_1", "gift:dace_scarf")
			== "upgrade:vial:world_1:gift:dace_scarf"
		),
		"a gift's flag"
	)
	_check(
		Upgrade.sword_bonus() == 0 and Upgrade.iron_carried() == 0,
		"nothing forged, nothing carried"
	)
	Story.set_flag("upgrade:iron:world_1:0")
	_check(Upgrade.iron_carried() == 1, "a bar picked up is carried")
	Story.set_flag("sword:1")
	_check(
		Upgrade.sword_bonus() == 1 and Upgrade.iron_carried() == 1,
		"Pell's first forging is his own stock"
	)
	Story.set_flag("sword:2")
	_check(Upgrade.sword_bonus() == 2 and Upgrade.iron_carried() == 0, "the second spends the bar")
	for kind: Player.UpgradeKind in Upgrade.DESCRIPTIONS:
		_check(
			Upgrade.DESCRIPTIONS[kind].size() >= 2,
			"%s reads like a relic" % Upgrade.FLAG_NAMES[kind]
		)


func test_skills() -> void:
	Skills.reset()
	_check(Skills.level() == 1 and Skills.points_left() == 0, "a new hero is level 1, no points")
	_check(Skills.xp_for_level(2) == Skills.XP_BASE, "the first level takes the base")
	var rising := true
	for level in range(1, Skills.MAX_LEVEL - 1):
		rising = rising and Skills.xp_to_next(level + 1) > Skills.xp_to_next(level)
	_check(rising, "every level takes more than the one before")
	_check(Skills.xp_to_next(Skills.MAX_LEVEL) == 0, "the cap takes nothing more")
	_check(Skills.add_xp(Skills.XP_BASE) == 1, "enough experience makes a level")
	_check(Skills.level() == 2 and Skills.points_left() == 1, "and a point")
	_check(Skills.level_progress() == [0, Skills.xp_to_next(2)], "the bar starts again")
	_check(not Skills.raise("mender").is_empty(), "a skill above the level is closed")
	_check(Skills.raise("vigour").is_empty(), "an open skill takes the point")
	_check(Skills.bonus("hearts") == 1.0, "and gives its heart")
	_check(Skills.raise("edge").begins_with("No skill point"), "no point, no rank")
	Skills.add_xp(Skills.xp_for_level(Skills.MAX_LEVEL) * 2)
	_check(Skills.level() == Skills.MAX_LEVEL, "experience stops at the cap")
	Skills.raise("swift_feet")
	Skills.raise("swift_feet")
	_check(is_equal_approx(Skills.factor("speed"), 1.08 * 1.08), "a factor multiplies per rank")
	Skills.raise("vigour")
	Skills.raise("vigour")
	_check(Skills.raise("vigour").contains("to the end"), "a skill stops at its ranks")
	var bad := 0
	for id in Skills.ids():
		for stat in Skills.get_skill(id).get("stats", {}):
			if stat not in Items.ADDITIVE_STATS and stat not in Items.FACTOR_STATS:
				bad += 1
		if Skills.description(id).is_empty() or Skills.max_rank(id) < 1:
			bad += 1
	_check(bad == 0, "every skill has known stats, a line and ranks (%d wrong)" % bad)
	var saved := Skills.to_dict()
	Skills.reset()
	Skills.from_dict(JSON.parse_string(JSON.stringify(saved)))
	_check(Skills.xp == saved["xp"] and Skills.rank("vigour") == 3, "the skills go through a save")
	Skills.from_dict({"xp": 0, "ranks": {"vigour": 3, "nope": 1}})
	_check(
		Skills.points_left() == 0 and not Skills.ranks.has("nope"),
		"a save spending more than it earned is given back"
	)
	Skills.reset()


func test_map() -> void:
	var world := WorldMap.read(WORLD_PATH)
	_check(world != null and world.rooms.size() > 100, "the world has its rooms")
	_check(MapScreen.room_flag(7) == "room:7", "a found room is a flag")
	_check(MapScreen.rooms_found(world) == 0, "a new game has found nothing")
	var first: Dictionary = world.rooms[0]
	var second: Dictionary = world.rooms[1]
	Story.set_flag(MapScreen.room_flag(int(first["id"])))
	Story.set_flag(MapScreen.room_flag(int(second["id"])))
	Story.set_flag(MapScreen.room_flag(999999))
	_check(
		(
			MapScreen.rooms_found(world) == 2
			and MapScreen.is_found(first)
			and MapScreen.is_found(second)
		),
		"found rooms are counted, and an unknown id is not"
	)
	var third: Dictionary = world.rooms[2]
	_check(not MapScreen.is_found(third), "the rest are not")


func test_save_slots() -> void:
	var slot := SaveManager.SLOT_COUNT - 1
	# Whatever is in the last slot on this machine is put back afterwards.
	var kept := SaveManager.read(slot)
	var had := SaveManager.has_save(slot)
	var data := {
		"version": SaveManager.VERSION,
		"level": "world_1",
		"checkpoint": 3,
		"abilities": [0, 1],
		"flags": ["a", "b"],
		"play_time": 61.0,
		"saved_at": 1.0,
		"difficulty": SaveManager.Difficulty.HARD,
	}
	_check(SaveManager.write(slot, data), "a slot writes")
	_check(SaveManager.has_save(slot), "and is there")
	var back := SaveManager.read(slot)
	_check(
		back.get("checkpoint", -1) == 3 and back.get("flags", []) == ["a", "b"], "and reads back"
	)
	_check(SaveManager.difficulty_of(back) == SaveManager.Difficulty.HARD, "with its difficulty")
	_check(SaveManager.difficulty_of({}) == SaveManager.Difficulty.NORMAL, "an old save is Normal")
	_check(
		SaveManager.difficulty_of({"difficulty": 9}) == SaveManager.Difficulty.HARD,
		"a bad value is clamped"
	)
	var summary := SaveManager.summary(slot)
	_check(
		(
			"checkpoint 4" in summary
			and "1:01" in summary
			and "Hard" in summary
			and "Wings" in summary
		),
		"the summary names the place, the time, the difficulty and the abilities (%s)" % summary
	)
	_check(
		SaveManager.load_slot(slot) and SaveManager.difficulty == SaveManager.Difficulty.HARD,
		"loading a slot sets the difficulty"
	)
	_check(
		SaveManager.scaled_damage(1) == 2 and SaveManager.bonus_hearts() == 0, "Hard doubles a hit"
	)
	SaveManager.start_new_game(slot, SaveManager.Difficulty.EASY)
	_check(
		not SaveManager.has_save(slot) and SaveManager.pending.is_empty(),
		"a new game clears the slot"
	)
	_check(
		SaveManager.bonus_hearts() == 1 and SaveManager.bonus_charges() == 1,
		"Easy gives a heart and a charge"
	)
	SaveManager.write(slot, {"version": 0})
	_check(SaveManager.read(slot).is_empty(), "a save of another version reads as empty")
	_check(SaveManager.summary(slot) == "Empty", "and is summarised as such")
	SaveManager.delete(slot)
	if had:
		SaveManager.write(slot, kept)
	SaveManager.current_slot = -1
	SaveManager.pending = {}
	SaveManager.difficulty = SaveManager.Difficulty.NORMAL


# Helpers


func test_prompts() -> void:
	var keyboard := Prompts.Device.KEYBOARD
	var xbox := Prompts.Device.XBOX
	var ps := Prompts.Device.PLAYSTATION
	_check("Space" in Prompts.render("{ui_accept}", keyboard), "the keyboard says Space to choose")
	_check("Esc" in Prompts.render("{ui_cancel}", keyboard), "and Esc to go back")
	_check("xbox_a.png" in Prompts.render("{jump}", xbox), "jump is A on an Xbox pad")
	_check("ps_cross.png" in Prompts.render("{jump}", ps), "and Cross on a PlayStation pad")
	_check("xbox_rt.png" in Prompts.render("{cast}", xbox), "cast shows the right trigger")
	_check("ps_r2.png" in Prompts.render("{cast}", ps), "as R2 on a PlayStation pad")
	_check("ps_l1.png" in Prompts.render("{page_prev}", ps), "the page buttons are the shoulders")
	_check(
		Prompts.render("{no_such_action} [b]", keyboard) == "{no_such_action} [lb]b]",
		"an unknown token and a bracket are left as written"
	)
	var a := InputEventJoypadButton.new()
	a.button_index = JOY_BUTTON_A
	var b := InputEventJoypadButton.new()
	b.button_index = JOY_BUTTON_B
	_check(InputMap.action_has_event(&"ui_accept", a), "A chooses in the menus")
	_check(InputMap.action_has_event(&"ui_cancel", b), "and B goes back")
	_check("xbox_a.png" in Prompts.render("{ui_accept}", xbox), "the hints say so")
	_check(Prompts.button_name(JOY_BUTTON_Y, ps) == "Triangle", "Y is named Triangle")
	_check(Prompts.button_name(JOY_BUTTON_Y, xbox) == "Y", "and Y on Xbox")
	# Every icon the layouts can name exists.
	var bad := 0
	for table in [
		Prompts.XBOX_BUTTONS,
		Prompts.PLAYSTATION_BUTTONS,
		Prompts.XBOX_AXES,
		Prompts.PLAYSTATION_AXES,
		Prompts.STICK_AXES,
	]:
		for key in table:
			var icon_name: String = table[key][0]
			if load("%s/%s.png" % [Prompts.ICON_DIR, icon_name]) == null:
				bad += _fail("no prompt icon %s" % icon_name)
	_check(bad == 0, "every prompt icon exists (%d missing)" % bad)
	# The device follows the last input, and a pinned layout wins.
	var changes: Array[int] = []
	var on_change := func(which: int) -> void: changes.append(which)
	Prompts.device_changed.connect(on_change)
	var press := InputEventJoypadButton.new()
	press.button_index = JOY_BUTTON_A
	press.pressed = true
	Prompts.set_layout(Prompts.Layout.PLAYSTATION)
	Prompts._input(press)
	_check(Prompts.device == ps, "a pad press with PlayStation pinned shows PlayStation")
	var drift := InputEventJoypadMotion.new()
	drift.axis = JOY_AXIS_LEFT_X
	drift.axis_value = 0.2
	var key := InputEventKey.new()
	key.physical_keycode = KEY_A
	key.pressed = true
	Prompts._input(key)
	_check(Prompts.device == keyboard, "a key press goes back to the keyboard")
	Prompts._input(drift)
	_check(Prompts.device == keyboard, "a drifting stick does not take the prompts")
	Prompts.set_layout(Prompts.Layout.XBOX)
	Prompts._input(press)
	_check(Prompts.device == xbox, "with Xbox pinned the pad shows Xbox")
	Prompts.set_layout(Prompts.Layout.AUTO)
	Prompts._input(key)
	_check(changes.size() >= 3, "each switch is announced (%d)" % changes.size())
	Prompts.device_changed.disconnect(on_change)


func test_dev_console() -> void:
	# The registry: the built-ins, a command of our own, help and errors.
	_check(DevConsole.has_command("show_boxes"), "show_boxes is registered")
	_check(
		DevConsole.has_command("help") and DevConsole.has_command("clear"), "so are help and clear"
	)
	var seen: Array = []
	DevConsole.register(
		"unit_echo",
		func(args: PackedStringArray) -> String:
			seen.append(args)
			return " ".join(args),
		"unit_echo <words>",
		"Repeats its arguments."
	)
	_check(DevConsole.run("unit_echo  one   two ") == "one two", "a command gets its arguments")
	_check(seen == [PackedStringArray(["one", "two"])], "split on spaces, blanks dropped")
	_check(DevConsole.run("") == "", "a blank line prints nothing")
	_check(
		DevConsole.run("no_such_thing 1").begins_with("Unknown command 'no_such_thing'"),
		"an unknown command says so"
	)
	_check("unit_echo <words>" in DevConsole.run("help"), "help lists a registered command")
	_check(
		DevConsole.run("help unit_echo").ends_with("Repeats its arguments."), "help explains one"
	)
	DevConsole.register("unit_quiet", func(_args: PackedStringArray) -> void: pass)
	_check(
		DevConsole.run("unit_quiet") == DevConsole.DONE_TEXT,
		"a command that returns nothing is acknowledged"
	)
	DevConsole.unregister("unit_echo")
	DevConsole.unregister("unit_quiet")
	_check(not DevConsole.has_command("unit_echo"), "a command can be removed")
	# Opening pauses the tree and closing puts the pause back as it was.
	DevConsole.open()
	_check(DevConsole.is_open() and get_tree().paused, "opening the console pauses the game")
	DevConsole.close()
	_check(not DevConsole.is_open() and not get_tree().paused, "closing it resumes")
	get_tree().paused = true
	DevConsole.open()
	DevConsole.close()
	_check(get_tree().paused, "a game paused before stays paused after")
	get_tree().paused = false
	# show_boxes: the overlay comes and goes with the command.
	_check(DevConsole.run("show_boxes") == "show_boxes is off", "show_boxes starts off")
	_check(DevConsole.run("show_boxes sideways").begins_with("Usage:"), "and only takes on or off")
	_check(DevConsole.run("show_boxes on") == "Collision boxes on", "show_boxes on answers")
	_check(DevConsole.boxes_shown(), "and the overlay is on its way under the root")
	_check(DevConsole.run("show_boxes").begins_with("show_boxes is on"), "show_boxes reports on")
	_check(DevConsole.run("show_boxes off") == "Collision boxes off", "show_boxes off answers")
	_check(not DevConsole.boxes_shown(), "and the overlay is gone")
	# The overlay itself: shapes added while it is on are picked up and
	# coloured by their owner, and dropped when they go.
	var boxes := CollisionBoxes.new()
	add_child(boxes)
	var before: int = boxes.shape_count()
	var world := StaticBody2D.new()
	world.collision_layer = 1
	world.add_child(_box_shape())
	add_child(world)
	var enemy := CharacterBody2D.new()
	enemy.add_to_group("enemy")
	enemy.collision_layer = 4
	var enemy_hit := Area2D.new()
	enemy_hit.collision_mask = 2
	enemy_hit.add_child(_box_shape())
	enemy.add_child(enemy_hit)
	add_child(enemy)
	var hero := CharacterBody2D.new()
	hero.add_to_group("player")
	hero.collision_layer = 2
	hero.add_child(_box_shape())
	add_child(hero)
	var item := Area2D.new()
	item.collision_mask = 2
	item.add_child(_box_shape())
	add_child(item)
	var hazard := Hazard.new()
	hazard.collision_mask = 2
	hazard.add_child(_box_shape())
	add_child(hazard)
	var door := DoorLock.new()
	door.add_child(_box_shape())
	add_child(door)
	_check(boxes.shape_count() == before + 6, "shapes added later are tracked")
	_check(
		CollisionBoxes.color_for(world) == CollisionBoxes.WORLD_COLOR,
		"a static body is world: black"
	)
	_check(
		CollisionBoxes.color_for(enemy_hit) == CollisionBoxes.ENEMY_COLOR,
		"an enemy's hitbox is red"
	)
	_check(CollisionBoxes.color_for(hero) == CollisionBoxes.PLAYER_COLOR, "the hero is green")
	_check(CollisionBoxes.color_for(item) == CollisionBoxes.ITEM_COLOR, "an item area is blue")
	_check(CollisionBoxes.color_for(hazard) == CollisionBoxes.HAZARD_COLOR, "a hazard is orange")
	_check(CollisionBoxes.color_for(door) == CollisionBoxes.WORLD_COLOR, "a sealed door is world")
	_check(
		CollisionBoxes.is_live(world.get_child(0), world),
		"a shape counts while its owner has a layer"
	)
	door.collision_layer = 0
	_check(
		not CollisionBoxes.is_live(door.get_child(0), door),
		"an opened door, off every layer, does not"
	)
	var hidden: CollisionShape2D = hero.get_child(0)
	hidden.disabled = true
	_check(not CollisionBoxes.is_live(hidden, hero), "a disabled shape is skipped")
	var rect := CollisionBoxes.local_rect(world.get_child(0))
	_check(rect == Rect2(-8.0, -8.0, 16.0, 16.0), "a box shape's bounds are its rect")
	for node in [world, enemy, hero, item, hazard, door]:
		remove_child(node)
		node.free()
	_check(boxes.shape_count() == before, "removed shapes are dropped")
	remove_child(boxes)
	boxes.free()


func _box_shape() -> CollisionShape2D:
	var shape := CollisionShape2D.new()
	var rect := RectangleShape2D.new()
	rect.size = Vector2(16.0, 16.0)
	shape.shape = rect
	return shape


func _json(path: String) -> Dictionary:
	var parsed: Variant = JSON.parse_string(FileAccess.get_file_as_string(path))
	return parsed if parsed is Dictionary else {}


func _as_list(value: Variant) -> Array:
	return value if value is Array else [value]


func _check(ok: bool, label: String) -> void:
	_checks += 1
	if ok:
		print("  ok   %s: %s" % [_section, label])
	else:
		_failures += 1
		printerr("  FAIL %s: %s" % [_section, label])


## A failed data check with its own line; returns 1 so callers can count.
func _fail(label: String) -> int:
	printerr("       %s" % label)
	return 1
