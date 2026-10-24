extends Node

## Downloads the live map from the map editor's Firebase project when the
## game starts.
##
## Registered as the LiveMap autoload. The editor publishes the Godot export
## of its live map (world.json and the three tile sheets) into Firestore's
## `published` collection, which anyone may read over the REST API, so the
## game needs no sign-in: only the project id, from the `live_map/project_id`
## project setting. It acts while the game runs from the project (never in
## an exported build) and `live_map/enabled` is on; a developer can skip it
## for one run with the `--no-live-map` user argument or by setting the
## DESCENDER_LIVE_MAP environment variable to 0.
##
## The head document carries a revision of the export and lists its files,
## each published in pieces (world.json as text, the sheets as base64). When
## the revision matches the one recorded in levels/world/live_map.json and
## the files are in place, the start costs that one small read; otherwise
## the pieces are downloaded, joined and written over levels/world (region
## backdrops under levels/world/regions/ among them). placements.json is
## never touched. The REST reading lives in FirestoreRest, shared with
## MapStore, which fetches the other published maps. The title screen shows
## the outcome and a game started while the download is still running waits
## for it (see wait()).

## Emitted once the sync has ended, well or badly.
signal finished(ok: bool, message: String)

## Loaded by path, so a fresh checkout works before the class cache knows it.
const FirestoreRest := preload("res://scripts/firestore_rest.gd")
const SETTING_ENABLED := "live_map/enabled"
const SETTING_PROJECT := "live_map/project_id"
const WORLD_DIR := "res://levels/world"
const STAMP_PATH := WORLD_DIR + "/live_map.json"
const PLACEMENTS_FILE := "placements.json"
const WORLD_FILE := "world.json"
const SHEETS: Array[String] = ["world_tiles.png", "world_tiles_n.png", "world_tiles_e.png"]
const HEAD_DOCUMENT := "live"

## Whether the sync is on for this run.
var enabled := false
## Whether the sync is still going.
var running := false
## What the title screen shows: progress, then the outcome.
var status := ""
## Whether the last sync ended well.
var ok := false

var _http: FirestoreRest


func _ready() -> void:
	if not OS.has_feature("editor") or not ProjectSettings.get_setting(SETTING_ENABLED, false):
		return
	var skipped := OS.get_environment("DESCENDER_LIVE_MAP") == "0"
	if "--no-live-map" in OS.get_cmdline_user_args() or skipped:
		return
	var project := str(ProjectSettings.get_setting(SETTING_PROJECT, ""))
	if project.is_empty():
		push_warning("LiveMap: %s is not set" % SETTING_PROJECT)
		return
	enabled = true
	_http = FirestoreRest.new()
	add_child(_http)
	_sync(project)


## Waits for a running sync; returns at once when none is running.
func wait() -> void:
	if running:
		await finished


func _sync(project: String) -> void:
	running = true
	status = "Checking the live map…"
	var base := FirestoreRest.base_url(project) + "published/"
	var head := await _http.get_document(base + HEAD_DOCUMENT)
	if head.has("error"):
		if head.get("code", 0) == 404:
			_finish(true, "Live map: none published yet")
		else:
			_finish(false, "Live map: %s" % head["error"])
		return
	var fields: Dictionary = head["fields"]
	var revision := str(fields.get("revision", ""))
	var map_name := str(fields.get("name", "?"))
	var files: Array = fields.get("files", [])
	if revision.is_empty() or files.is_empty():
		_finish(false, "Live map: the published map is incomplete")
		return
	if revision == _stamped_revision() and _files_present():
		_finish(true, "Live map: %s" % map_name)
		return

	status = "Downloading the live map…"
	var wanted := [WORLD_FILE] + SHEETS
	for file in files:
		if file is Dictionary:
			wanted.erase(str(file.get("name", "")))
	if not wanted.is_empty():
		_finish(false, "Live map: the published map has no %s" % ", ".join(wanted))
		return
	var problem := await _http.download_files(
		base,
		str(fields.get("prefix", HEAD_DOCUMENT)),
		files,
		WORLD_DIR,
		Callable(),
		str(fields.get("mediaBase", ""))
	)
	if not problem.is_empty():
		_finish(false, "Live map: %s" % problem)
		return
	# A region backdrop the map no longer has must not linger under regions/.
	FirestoreRest.prune_files(WORLD_DIR, files, [STAMP_PATH.get_file(), PLACEMENTS_FILE])
	(
		FirestoreRest
		. write_file(
			STAMP_PATH,
			(
				JSON
				. stringify(
					{
						"revision": revision,
						"name": map_name,
						"mapId": str(fields.get("mapId", "")),
						"publishedAt": int(fields.get("publishedAt", 0)),
						"syncedAt": int(Time.get_unix_time_from_system() * 1000.0),
					},
					"  "
				)
				. to_utf8_buffer()
			)
		)
	)
	_finish(true, "Live map: %s, updated" % map_name)


func _stamped_revision() -> String:
	var text := FileAccess.get_file_as_string(STAMP_PATH)
	var stamp: Variant = JSON.parse_string(text) if not text.is_empty() else null
	return str(stamp.get("revision", "")) if stamp is Dictionary else ""


func _files_present() -> bool:
	if not FileAccess.file_exists(WORLD_DIR + "/" + WORLD_FILE):
		return false
	for sheet in SHEETS:
		if not FileAccess.file_exists(WORLD_DIR + "/" + sheet):
			return false
	return true


func _finish(well: bool, message: String) -> void:
	running = false
	ok = well
	status = message
	if not well:
		push_warning(message)
	finished.emit(well, message)
