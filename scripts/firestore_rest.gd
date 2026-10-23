class_name FirestoreRest
extends HTTPRequest

## Reads the map editor's Firestore project over the REST API, without
## signing in: the `published` collection is public (see LiveMap and
## MapStore). One request at a time; add it to the tree before use.

## Seconds one request may take before it is given up on.
const REQUEST_TIMEOUT := 60.0


func _init() -> void:
	timeout = REQUEST_TIMEOUT
	use_threads = true


## The documents URL of a project's default database. The
## DESCENDER_FIRESTORE_HOST environment variable (an origin such as
## http://127.0.0.1:8080) points it at an emulator or a stand-in server.
static func base_url(project: String) -> String:
	var origin := OS.get_environment("DESCENDER_FIRESTORE_HOST")
	if origin.is_empty():
		origin = "https://firestore.googleapis.com"
	return "%s/v1/projects/%s/databases/(default)/documents/" % [origin.rstrip("/"), project]


## Fetches a document. Returns {"fields": {...}} with the values unwrapped,
## or {"error": message, "code": http status or 0}.
func get_document(url: String) -> Dictionary:
	var err := request(url)
	if err != OK:
		return {"error": "request failed (%d)" % err, "code": 0}
	var reply: Array = await request_completed
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
	return {"fields": unwrap({"mapValue": {"fields": parsed["fields"]}})}


## Downloads the files a published head lists into `dir` (subfolders made as
## needed): a Storage object (the head's `mediaBase` + the file's `storage`
## path) streams straight to disk in one request; otherwise the file's
## Firestore pieces are read and joined. Returns an empty string, or what
## went wrong.
func download_files(
	base: String,
	head_id: String,
	files: Array,
	dir: String,
	progress: Callable = Callable(),
	media_base := ""
) -> String:
	for index in files.size():
		var file: Dictionary = files[index]
		var file_name := str(file.get("name", ""))
		if file_name.is_empty() or file_name.begins_with("/") or ".." in file_name:
			continue
		if progress.is_valid():
			progress.call(file_name, index, files.size())
		var target := dir.path_join(file_name)
		var object_path := str(file.get("storage", ""))
		if not object_path.is_empty() and not media_base.is_empty():
			var problem := await download_object(
				media_base + object_path.uri_encode() + "?alt=media", target
			)
			if not problem.is_empty():
				return "%s: %s" % [file_name, problem]
			continue
		var text := ""
		for part in int(file.get("parts", 0)):
			var piece := await get_document(base + "%s_f%d_%d" % [head_id, index, part])
			if piece.has("error"):
				return str(piece["error"])
			text += str(piece["fields"].get("text", ""))
		var bytes := (
			Marshalls.base64_to_raw(text)
			if str(file.get("encoding", "")) == "base64"
			else text.to_utf8_buffer()
		)
		if not write_file(target, bytes):
			return "could not write %s" % file_name
	return ""


## Streams a URL to `path` (written next to it and renamed when complete).
## Returns an empty string, or what went wrong.
func download_object(url: String, path: String) -> String:
	var target := ProjectSettings.globalize_path(path)
	DirAccess.make_dir_recursive_absolute(target.get_base_dir())
	var temp := target + ".tmp"
	download_file = temp
	var err := request(url)
	if err != OK:
		download_file = ""
		return "request failed (%d)" % err
	var reply: Array = await request_completed
	download_file = ""
	var result: int = reply[0]
	var code: int = reply[1]
	if result != HTTPRequest.RESULT_SUCCESS:
		DirAccess.remove_absolute(temp)
		return "no connection (%d)" % result
	if code != 200:
		DirAccess.remove_absolute(temp)
		return "HTTP %d" % code
	return "" if DirAccess.rename_absolute(temp, target) == OK else "could not write the file"


## Turns a Firestore typed value ({"stringValue": "x"} and the like) into a
## plain Variant.
static func unwrap(value: Variant) -> Variant:
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
	if value.has("nullValue"):
		return null
	if value.has("mapValue"):
		var out := {}
		var fields: Dictionary = value["mapValue"].get("fields", {})
		for key in fields:
			out[key] = unwrap(fields[key])
		return out
	if value.has("arrayValue"):
		var items: Array = value["arrayValue"].get("values", [])
		return items.map(unwrap)
	return null


## Writes next to the target and renames, so a half-written file is never
## read as the world. Makes the folders on the way.
static func write_file(path: String, bytes: PackedByteArray) -> bool:
	var target := ProjectSettings.globalize_path(path)
	DirAccess.make_dir_recursive_absolute(target.get_base_dir())
	var temp := target + ".tmp"
	var file := FileAccess.open(temp, FileAccess.WRITE)
	if file == null:
		return false
	file.store_buffer(bytes)
	file.close()
	return DirAccess.rename_absolute(temp, target) == OK
