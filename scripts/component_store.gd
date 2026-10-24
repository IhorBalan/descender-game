extends Node

## The UI components made in the map editor's Components page.
##
## Registered as the ComponentStore autoload. The editor publishes every
## component it saves to Firestore's public `published` collection (a head
## per component, `component_<id>`, listed in `published/components`, see
## the editor's src/component-export.ts). This store downloads the ones
## that changed into user://components/<id>/ (component.json, textures/ and
## a stamp of the revision) when the game starts, and falls back to the
## copies shipped in res://assets/ui/components/<id>/ (written by the
## editor's `npm run publish-components -- --out ...`) when it cannot reach
## Firebase. Components are found by name, ignoring case and spaces.
##
## The sync only needs the `live_map/project_id` project setting. It is
## skipped with the `--no-component-sync` user argument or the
## DESCENDER_COMPONENTS environment variable set to 0. `updated` fires for
## each component that arrived newer than what was on disk, so whatever
## shows it can reload.

signal updated(component_name: String)
## Emitted once the sync has ended, well or badly.
signal finished(ok: bool, message: String)

const FirestoreRest := preload("res://scripts/firestore_rest.gd")
const DOWNLOAD_DIR := "user://components"
const BUNDLED_DIR := "res://assets/ui/components"
const INDEX_DOCUMENT := "components"
const HEAD_PREFIX := "component_"
const STAMP_FILE := "stamp.json"
const COMPONENT_FILE := "component.json"

## Whether a sync is still going.
var running := false
## What the last sync did, for the title screen and logs.
var status := ""

var _http: FirestoreRest


func _ready() -> void:
	var project := str(ProjectSettings.get_setting("live_map/project_id", ""))
	if project.is_empty():
		return
	if (
		"--no-component-sync" in OS.get_cmdline_user_args()
		or OS.get_environment("DESCENDER_COMPONENTS") == "0"
	):
		return
	_http = FirestoreRest.new()
	add_child(_http)
	_sync(project)


## Waits for a running sync; returns at once when none is running.
func wait() -> void:
	if running:
		await finished


## A normalised name for matching: lowercase, without spaces, dashes or
## underscores, so "Health Bar" finds "HealthBar".
static func key(component_name: String) -> String:
	return component_name.to_lower().replace(" ", "").replace("_", "").replace("-", "")


## The folder of the component called `component_name`: the downloaded copy
## when there is one, else the shipped one; empty when there is neither.
func dir_for(component_name: String) -> String:
	var wanted := key(component_name)
	for base in [DOWNLOAD_DIR, BUNDLED_DIR]:
		for dir in _component_dirs(base):
			if key(str(_read_json(dir.path_join(COMPONENT_FILE)).get("name", ""))) == wanted:
				return dir
	return ""


## A UiComponent showing `component_name` at `width` level pixels wide, or
## null when no such component is available.
func create(component_name: String, width := 0.0) -> UiComponent:
	var dir := dir_for(component_name)
	if dir.is_empty():
		return null
	var node := UiComponent.new()
	node.name = component_name.replace(" ", "")
	node.display_width = width
	if not node.load_from(dir):
		node.free()
		return null
	return node


## The names of every component on disk, downloaded or shipped.
func names() -> PackedStringArray:
	var out := PackedStringArray()
	var seen := {}
	for base in [DOWNLOAD_DIR, BUNDLED_DIR]:
		for dir in _component_dirs(base):
			var name := str(_read_json(dir.path_join(COMPONENT_FILE)).get("name", ""))
			if not name.is_empty() and not seen.has(key(name)):
				seen[key(name)] = true
				out.append(name)
	return out


func _component_dirs(base: String) -> PackedStringArray:
	var out := PackedStringArray()
	var dir := DirAccess.open(base)
	if dir == null:
		return out
	for folder in dir.get_directories():
		if FileAccess.file_exists(base.path_join(folder).path_join(COMPONENT_FILE)):
			out.append(base.path_join(folder))
	return out


func _sync(project: String) -> void:
	running = true
	var base := FirestoreRest.base_url(project) + "published/"
	var index := await _http.get_document(base + INDEX_DOCUMENT)
	if index.has("error"):
		var nothing: bool = index.get("code", 0) == 404
		_finish(
			nothing, "Components: none published" if nothing else "Components: %s" % index["error"]
		)
		return
	var fetched := 0
	for entry: Variant in index["fields"].get("components", []):
		if not entry is Dictionary:
			continue
		var id := str(entry.get("mapId", ""))
		if not MapStore.is_safe_id(id):
			continue
		var dir := DOWNLOAD_DIR.path_join(id)
		var revision := str(entry.get("revision", ""))
		if str(_read_json(dir.path_join(STAMP_FILE)).get("revision", "")) == revision:
			continue
		var problem := await _download(base, id, dir)
		if not problem.is_empty():
			push_warning("Components: %s: %s" % [entry.get("name", id), problem])
			continue
		fetched += 1
		updated.emit(str(entry.get("name", "")))
	_finish(true, "Components: %d updated" % fetched)


## Brings one component's files into `dir`; returns what went wrong, or an
## empty string.
func _download(base: String, id: String, dir: String) -> String:
	var head := await _http.get_document(base + HEAD_PREFIX + id)
	if head.has("error"):
		return str(head["error"])
	var fields: Dictionary = head["fields"]
	var files: Array = fields.get("files", [])
	# A fresh folder, so textures a component no longer uses do not linger.
	_clear(dir)
	var problem := await _http.download_files(
		base,
		str(fields.get("prefix", HEAD_PREFIX + id)),
		files,
		dir,
		Callable(),
		str(fields.get("mediaBase", ""))
	)
	if not problem.is_empty():
		return problem
	if not FileAccess.file_exists(dir.path_join(COMPONENT_FILE)):
		return "the publication has no %s" % COMPONENT_FILE
	var stamp := {
		"id": id,
		"name": str(fields.get("name", "")),
		"revision": str(fields.get("revision", "")),
		"publishedAt": int(fields.get("publishedAt", 0)),
	}
	FirestoreRest.write_file(
		dir.path_join(STAMP_FILE), JSON.stringify(stamp, "  ").to_utf8_buffer()
	)
	return ""


static func _clear(dir: String) -> void:
	var path := ProjectSettings.globalize_path(dir)
	var access := DirAccess.open(path)
	if access == null:
		return
	for sub in access.get_directories():
		_clear(dir.path_join(sub))
		DirAccess.remove_absolute(path.path_join(sub))
	for file in access.get_files():
		DirAccess.remove_absolute(path.path_join(file))


static func _read_json(path: String) -> Dictionary:
	var text := FileAccess.get_file_as_string(path)
	var parsed: Variant = JSON.parse_string(text) if not text.is_empty() else null
	return parsed if parsed is Dictionary else {}


func _finish(well: bool, message: String) -> void:
	running = false
	status = message
	if not well:
		push_warning(message)
	finished.emit(well, message)
