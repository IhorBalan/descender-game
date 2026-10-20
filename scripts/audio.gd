extends Node

## Plays sound effects and the ambient loop.
##
## Registered as the Audio autoload. Effects are short WAVs under
## assets/audio/sfx, named by what they are for; `play("jump")` picks one
## of a small pool of players so overlapping sounds do not cut each other
## off. The ambient loop is one long WAV per area, looped by the player.
## Every sound is a placeholder made by tools/gen_sounds.py.

const SFX_DIR := "res://assets/audio/sfx/"
const AMBIENT_DIR := "res://assets/audio/"
const POOL_SIZE := 8
## Random pitch spread around 1.0, so repeated sounds do not sound stamped.
const PITCH_SPREAD := 0.06

## The name of the last effect played; the smoke test reads it.
var last_played := ""
var ambient_name := ""

var _sounds: Dictionary = {}
var _pool: Array[AudioStreamPlayer] = []
var _next := 0
var _ambient: AudioStreamPlayer


func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS
	for i in POOL_SIZE:
		var player := AudioStreamPlayer.new()
		add_child(player)
		_pool.append(player)
	_ambient = AudioStreamPlayer.new()
	add_child(_ambient)


## Plays an effect by name (its file name without the extension). An
## unknown name is reported once and ignored.
func play(sound_name: String, volume_db := 0.0) -> void:
	var stream := _stream(SFX_DIR + sound_name + ".wav")
	if stream == null:
		return
	var player := _pool[_next]
	_next = (_next + 1) % POOL_SIZE
	player.stream = stream
	player.volume_db = volume_db
	player.pitch_scale = 1.0 + randf_range(-PITCH_SPREAD, PITCH_SPREAD)
	player.play()
	last_played = sound_name


## Starts the area's ambient loop, or keeps it running if it is already the
## one playing. An empty name stops it.
func play_ambient(area_name: String) -> void:
	if area_name == ambient_name and _ambient.playing:
		return
	ambient_name = area_name
	if area_name.is_empty():
		_ambient.stop()
		return
	var stream := _stream(AMBIENT_DIR + "ambient_" + area_name + ".wav")
	if stream == null:
		return
	if stream is AudioStreamWAV:
		var wav := stream as AudioStreamWAV
		wav.loop_mode = AudioStreamWAV.LOOP_FORWARD
		wav.loop_begin = 0
		wav.loop_end = int(wav.get_length() * wav.mix_rate)
	_ambient.stream = stream
	_ambient.volume_db = -6.0
	_ambient.play()


func stop_ambient() -> void:
	play_ambient("")


## Gives every button under the node a move blip on focus and a confirm
## sound on press.
func wire_menu(root: Node) -> void:
	for child in root.find_children("*", "Button", true, false):
		var button := child as Button
		button.focus_entered.connect(play.bind("menu_move"))
		button.pressed.connect(play.bind("menu_confirm"))


func _stream(path: String) -> AudioStream:
	if not _sounds.has(path):
		var stream: AudioStream = null
		if ResourceLoader.exists(path):
			stream = load(path)
		else:
			push_warning("No sound at %s" % path)
		_sounds[path] = stream
	return _sounds[path]
