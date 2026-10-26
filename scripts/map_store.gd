extends Node

## The maps the editor published for the game's Maps menu.
##
## Registered as the MapStore autoload. The editor can publish any map under
## its own head in Firestore's public `published` collection and list it in
## `published/index`. This store lists them for the title screen, downloads
## a chosen one into user://maps/<map id>/ (world.json, the sheets and any
## region backdrops, with a map.json stamp of the published revision) and
## registers it with Levels under the id `map_<map id>`, so saves made on it
## reopen it. Maps already downloaded are registered at start and listed
## without a connection; the live map (see LiveMap) lands under
## user://maps/live/ the same way and is one of them.
##
## `--map=<map id or name>` on the command line downloads (if needed) and
## makes it what New Game starts: `godot --path . -- --map="World 3"`; add
## `--play` to skip the menu and play it at once, in no save slot. It only
## needs the `live_map/project_id` project setting, not the live map sync,
## and works in exported builds too since it writes under user://.

## Emitted when a download or the --map start-up ends, well or badly.
signal finished(ok: bool, message: String)

## Loaded by path, so a fresh checkout works before the class cache knows it.
const FirestoreRest := preload("res://scripts/firestore_rest.gd")
const MAPS_DIR := "user://maps"
const STAMP_FILE := "map.json"
const WORLD_FILE := "world.json"
const HEAD_PREFIX := "map_"
const INDEX_DOCUMENT := "index"
const LEVEL_PREFIX := "map_"
const CHOICE_PATH := "user://map_choice.cfg"

## Whether the project is configured, so the online list can be read.
var enabled := false
## Whether a download (or the --map start-up) is still going.
var running := false
## What the title screen shows: progress, then the outcome.
var status := ""
## The level id picked in the Maps menu or by --map; Main starts a new game
## on it when no save says otherwise. Empty: the descent. The pick from the
## Maps menu is kept in CHOICE_PATH across runs, as long as the level is
## still there.
var chosen_level := ""

var _http: FirestoreRest
var _project := ""
## Set by --map: the next fetched map becomes the chosen level before
## `finished` fires, so Main (waiting on it) starts that map.
var _choose_fetched := false


func _ready() -> void:
	_project = str(ProjectSettings.get_setting(LiveMap.SETTING_PROJECT, ""))
	enabled = not _project.is_empty()
	_http = FirestoreRest.new()
	add_child(_http)
	for entry in cached():
		_register(entry)
	_load_choice()
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--map="):
			_start_from_arg(arg.trim_prefix("--map=").strip_edges())
			break


## Makes `level` (a level id, or empty for the descent) what New Game starts
## on, now and in later runs.
func choose(level: String) -> void:
	chosen_level = level
	var config := ConfigFile.new()
	config.set_value("map", "level", level)
	config.save(CHOICE_PATH)


## The name of what New Game starts on.
func chosen_name() -> String:
	return Levels.display_name(chosen_level) if not chosen_level.is_empty() else ""


func _load_choice() -> void:
	var config := ConfigFile.new()
	if config.load(CHOICE_PATH) != OK:
		return
	var level := str(config.get_value("map", "level", ""))
	# A map that is gone from the cache falls back to the descent quietly.
	if Levels.has(level):
		chosen_level = level


## Waits for a running download; returns at once when none is running.
func wait() -> void:
	if running:
		await finished


## The level id a published map runs under.
static func level_id(map_id: String) -> String:
	return LEVEL_PREFIX + map_id


## Whether a map id from the index can name a folder: letters, digits,
## dashes and underscores only.
static func is_safe_id(map_id: String) -> bool:
	if map_id.is_empty():
		return false
	for c in map_id:
		if not (c.is_valid_ascii_identifier() or c.is_valid_int() or c == "-"):
			return false
	return true


## The maps already downloaded: their stamps (mapId, name, revision,
## publishedAt, artTile) plus `path` to their world.json.
func cached() -> Array[Dictionary]:
	var out: Array[Dictionary] = []
	var dir := DirAccess.open(MAPS_DIR)
	if dir == null:
		return out
	for folder in dir.get_directories():
		var base := MAPS_DIR.path_join(folder)
		var text := FileAccess.get_file_as_string(base.path_join(STAMP_FILE))
		var stamp: Variant = JSON.parse_string(text) if not text.is_empty() else null
		if not stamp is Dictionary or not FileAccess.file_exists(base.path_join(WORLD_FILE)):
			continue
		stamp["path"] = base.path_join(WORLD_FILE)
		stamp["cached"] = true
		out.append(stamp)
	out.sort_custom(
		func(a, b): return str(a.get("name", "")).naturalnocasecmp_to(str(b.get("name", ""))) < 0
	)
	return out


## The published maps, from the editor's index: mapId, name, revision,
## publishedAt, publishedBy, artTile, and `cached` when the downloaded copy
## is at that revision. Without a connection the downloaded ones are listed
## and `status` says why.
func list() -> Array[Dictionary]:
	var local := cached()
	if not enabled:
		status = "No map editor project is set"
		return local
	var reply := await _http.get_document(
		FirestoreRest.base_url(_project) + "published/" + INDEX_DOCUMENT
	)
	if reply.has("error"):
		status = (
			"Published maps: none yet"
			if reply.get("code", 0) == 404
			else "Published maps: %s" % reply["error"]
		)
		return local
	var out: Array[Dictionary] = []
	var seen := {}
	for entry in reply["fields"].get("maps", []):
		if not entry is Dictionary or str(entry.get("mapId", "")).is_empty():
			continue
		var map_id := str(entry["mapId"])
		seen[map_id] = true
		var copy: Dictionary = entry.duplicate()
		for stamp in local:
			if str(stamp.get("mapId", "")) == map_id:
				copy["cached"] = str(stamp.get("revision", "")) == str(entry.get("revision", ""))
				copy["path"] = stamp["path"]
		out.append(copy)
	# Downloaded maps the editor has since unpublished stay playable.
	for stamp in local:
		if not seen.has(str(stamp.get("mapId", ""))):
			var copy: Dictionary = stamp.duplicate()
			copy["unpublished"] = true
			out.append(copy)
	status = ""
	return out


## Downloads a published map unless the copy under user://maps is at the
## published revision, registers it with Levels and returns its level id,
## or an empty string when it could not be had (status says why).
func fetch(map_id: String) -> String:
	if running:
		await finished
	running = true
	# The id names a folder under user://maps and comes from the index.
	if not is_safe_id(map_id):
		_finish(false, "Map: '%s' is not a map id" % map_id)
		return ""
	status = "Checking the map…"
	var head := await _http.get_document(
		FirestoreRest.base_url(_project) + "published/" + HEAD_PREFIX + map_id
	)
	if head.has("error"):
		var local := _stamp(map_id)
		if not local.is_empty():
			# Offline, or unpublished since: the downloaded copy will do.
			_fetched(map_id, "Playing the downloaded copy of %s" % str(local.get("name", map_id)))
			return level_id(map_id)
		_finish(
			false,
			"Map: %s" % ("not published" if head.get("code", 0) == 404 else str(head["error"]))
		)
		return ""
	var problem := await _download(map_id, head["fields"])
	if not problem.is_empty():
		_finish(false, "Map: %s" % problem)
		return ""
	return level_id(map_id)


## Brings the map a head describes into user://maps (skipping the download
## when the copy there is at the head's revision), registers it and reports
## through _fetched. Returns an empty string, or what went wrong.
func _download(map_id: String, fields: Dictionary) -> String:
	var revision := str(fields.get("revision", ""))
	var map_name := str(fields.get("name", map_id))
	var files: Array = fields.get("files", [])
	if revision.is_empty() or files.is_empty():
		return "the published export is incomplete"
	var dir := MAPS_DIR.path_join(map_id)
	var stamp := _stamp(map_id)
	if (
		str(stamp.get("revision", "")) == revision
		and FileAccess.file_exists(dir.path_join(WORLD_FILE))
	):
		_register(_entry(fields, dir))
		_fetched(map_id, "Map: %s" % map_name)
		return ""
	status = "Downloading %s…" % map_name
	var base := FirestoreRest.base_url(_project) + "published/"
	var problem := await _http.download_files(
		base,
		str(fields.get("prefix", HEAD_PREFIX + map_id)),
		files,
		dir,
		_on_progress.bind(map_name),
		str(fields.get("mediaBase", ""))
	)
	if not problem.is_empty():
		return problem
	if not FileAccess.file_exists(dir.path_join(WORLD_FILE)):
		return "the published export has no %s" % WORLD_FILE
	# Files the earlier revision had and this one does not (a removed region
	# backdrop) would otherwise stay and be used.
	FirestoreRest.prune_files(dir, files, [STAMP_FILE])
	var entry := _entry(fields, dir)
	entry["syncedAt"] = int(Time.get_unix_time_from_system() * 1000.0)
	FirestoreRest.write_file(
		dir.path_join(STAMP_FILE), JSON.stringify(entry, "  ").to_utf8_buffer()
	)
	_register(entry)
	_fetched(map_id, "Map: %s, downloaded" % map_name)
	return ""


## Registers the map downloaded into user://maps/<map id>/ with Levels, from
## its stamp. For a folder written after start (the live map sync).
func register_cached(map_id: String) -> void:
	var stamp := _stamp(map_id)
	if stamp.is_empty():
		return
	stamp["path"] = MAPS_DIR.path_join(map_id).path_join(WORLD_FILE)
	_register(stamp)


## Forgets a downloaded map (its files and its level).
func remove(map_id: String) -> void:
	var dir := ProjectSettings.globalize_path(MAPS_DIR.path_join(map_id))
	if DirAccess.dir_exists_absolute(dir):
		OS.move_to_trash(dir)
	Levels.unregister(level_id(map_id))


## A map is in place: chosen when --map asked for it, then `finished`.
func _fetched(map_id: String, message: String) -> void:
	if _choose_fetched:
		chosen_level = level_id(map_id)
		_choose_fetched = false
	_finish(true, message)


func _on_progress(file_name: String, index: int, count: int, map_name: String) -> void:
	status = "Downloading %s… %d/%d %s" % [map_name, index + 1, count, file_name.get_file()]


## The stamp of a downloaded map, or an empty dictionary.
func _stamp(map_id: String) -> Dictionary:
	var text := FileAccess.get_file_as_string(MAPS_DIR.path_join(map_id).path_join(STAMP_FILE))
	var stamp: Variant = JSON.parse_string(text) if not text.is_empty() else null
	return stamp if stamp is Dictionary else {}


static func _entry(fields: Dictionary, dir: String) -> Dictionary:
	return {
		"mapId": str(fields.get("mapId", "")),
		"name": str(fields.get("name", "")),
		"revision": str(fields.get("revision", "")),
		"publishedAt": int(fields.get("publishedAt", 0)),
		"publishedBy": str(fields.get("publishedBy", "")),
		"artTile": int(fields.get("artTile", 16)),
		"path": dir.path_join(WORLD_FILE),
	}


func _register(entry: Dictionary) -> void:
	var map_id := str(entry.get("mapId", ""))
	if map_id.is_empty():
		return
	(
		Levels
		. register(
			{
				"id": level_id(map_id),
				"name": str(entry.get("name", map_id)),
				"path": str(entry.get("path", MAPS_DIR.path_join(map_id).path_join(WORLD_FILE))),
				"ambient": "heaven",
				"sky": Levels.WORLD_SKY,
			}
		)
	)


## --map=<id or name>: finds the map in the index (or among the downloaded
## ones), downloads it and makes it the chosen level.
func _start_from_arg(wanted: String) -> void:
	if wanted.is_empty():
		return
	running = true
	var found := ""
	for entry in await list():
		var map_id := str(entry.get("mapId", ""))
		if map_id == wanted or str(entry.get("name", "")).nocasecmp_to(wanted) == 0:
			found = map_id
			break
	if found.is_empty():
		_finish(false, "Map: no published map '%s'" % wanted)
		return
	_choose_fetched = true
	running = false
	await fetch(found)
	_choose_fetched = false


func _finish(well: bool, message: String) -> void:
	running = false
	status = message
	if not well:
		push_warning(message)
	finished.emit(well, message)
