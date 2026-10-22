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
## the pieces are downloaded, joined and written over levels/world. placements.json
## is never touched. The title screen shows the outcome and a game started
## while the download is still running waits for it (see wait()).

## Emitted once the sync has ended, well or badly.
signal finished(ok: bool, message: String)

const SETTING_ENABLED := "live_map/enabled"
const SETTING_PROJECT := "live_map/project_id"
const WORLD_DIR := "res://levels/world"
const STAMP_PATH := WORLD_DIR + "/live_map.json"
const WORLD_FILE := "world.json"
const SHEETS: Array[String] = ["world_tiles.png", "world_tiles_n.png", "world_tiles_e.png"]
const HEAD_DOCUMENT := "live"
## Seconds one request may take before it is given up on.
const REQUEST_TIMEOUT := 60.0

## Whether the sync is on for this run.
var enabled := false
## Whether the sync is still going.
var running := false
## What the title screen shows: progress, then the outcome.
var status := ""
## Whether the last sync ended well.
var ok := false

var _http: HTTPRequest


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
	_http = HTTPRequest.new()
	_http.timeout = REQUEST_TIMEOUT
	_http.use_threads = true
	add_child(_http)
	_sync(project)


## Waits for a running sync; returns at once when none is running.
func wait() -> void:
	if running:
		await finished


func _sync(project: String) -> void:
	running = true
	status = "Checking the live map…"
	var base := (
		"https://firestore.googleapis.com/v1/projects/%s/databases/(default)/documents/published/"
		% project
	)
	var head := await _get_document(base + HEAD_DOCUMENT)
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
	for index in files.size():
		var file: Dictionary = files[index]
		var file_name := str(file.get("name", ""))
		if not file_name in wanted:
			continue
		var text := ""
		for part in int(file.get("parts", 0)):
			var piece := await _get_document(base + "%s_f%d_%d" % [HEAD_DOCUMENT, index, part])
			if piece.has("error"):
				_finish(false, "Live map: %s" % piece["error"])
				return
			text += str(piece["fields"].get("text", ""))
		var bytes := text.to_utf8_buffer()
		if str(file.get("encoding", "")) == "base64":
			bytes = Marshalls.base64_to_raw(text)
		if not _write(WORLD_DIR + "/" + file_name, bytes):
			_finish(false, "Live map: could not write %s" % file_name)
			return
		wanted.erase(file_name)
	if not wanted.is_empty():
		_finish(false, "Live map: the published map has no %s" % ", ".join(wanted))
		return
	_write(
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
	_finish(true, "Live map: %s, updated" % map_name)


## Fetches a Firestore document. Returns {"fields": {...}} with the values
## unwrapped, or {"error": message, "code": http status or 0}.
func _get_document(url: String) -> Dictionary:
	var err := _http.request(url)
	if err != OK:
		return {"error": "request failed (%d)" % err, "code": 0}
	var reply: Array = await _http.request_completed
	var result: int = reply[0]
	var code: int = reply[1]
	var body: PackedByteArray = reply[3]
	if result != HTTPRequest.RESULT_SUCCESS:
		return {"error": "no connection (%d)" % result, "code": 0}
	if code != 200:
		return {"error": "HTTP %d" % code, "code": code}
	var parsed: Variant = JSON.parse_string(body.get_string_from_utf8())
	if not parsed is Dictionary or not parsed.has("fields"):
		return {"error": "unexpected reply", "code": code}
	return {"fields": _unwrap({"mapValue": {"fields": parsed["fields"]}})}


## Turns a Firestore typed value ({"stringValue": "x"} and the like) into a
## plain Variant.
static func _unwrap(value: Variant) -> Variant:
	if not value is Dictionary:
		return null
	if value.has("stringValue"):
		return value["stringValue"]
	if value.has("integerValue"):
		return int(str(value["integerValue"]))
	if value.has("doubleValue"):
		return float(value["doubleValue"])
	if value.has("booleanValue"):
		return bool(value["booleanValue"])
	if value.has("mapValue"):
		var out := {}
		var fields: Dictionary = value["mapValue"].get("fields", {})
		for key in fields:
			out[key] = _unwrap(fields[key])
		return out
	if value.has("arrayValue"):
		var items: Array = value["arrayValue"].get("values", [])
		return items.map(_unwrap)
	return null


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


## Writes next to the target and renames, so a half-written file is never
## read as the world.
static func _write(path: String, bytes: PackedByteArray) -> bool:
	var target := ProjectSettings.globalize_path(path)
	var temp := target + ".tmp"
	var file := FileAccess.open(temp, FileAccess.WRITE)
	if file == null:
		return false
	file.store_buffer(bytes)
	file.close()
	return DirAccess.rename_absolute(temp, target) == OK


func _finish(well: bool, message: String) -> void:
	running = false
	ok = well
	status = message
	if not well:
		push_warning(message)
	finished.emit(well, message)
