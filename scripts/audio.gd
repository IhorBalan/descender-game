extends Node

## Plays sound effects, the music and the ambient loop.
##
## Registered as the Audio autoload. Effects are short WAVs under
## assets/audio/sfx, named by what they are for; `play("jump")` picks one
## of a small pool of players so overlapping sounds do not cut each other
## off. Music is one loop per region of the world under assets/audio/music
## (plus `title` and `boss`), named by the region's key; `play_music`
## crossfades from whatever is playing. The ambient loop is one long WAV
## per area, looped by the player, under the music. Every sound is made by
## tools/gen_sounds.py and tools/gen_music.py. Effects play on the SFX bus
## and the music and ambient loops on the Music bus, so the settings
## screen can set their volumes apart (see default_bus_layout.tres). While
## the tree is paused the music is ducked.

const SFX_DIR := "res://assets/audio/sfx/"
const MUSIC_DIR := "res://assets/audio/music/"
const AMBIENT_DIR := "res://assets/audio/"
const POOL_SIZE := 8
## Random pitch spread around 1.0, so repeated sounds do not sound stamped.
const PITCH_SPREAD := 0.06
const MUSIC_DB := -8.0
const AMBIENT_DB := -10.0
const SILENT_DB := -40.0
## How far the music drops while the game is paused.
const DUCK_DB := -12.0
const CROSSFADE_TIME := 1.5
const DUCK_TIME := 0.3

## The name of the last effect played; the smoke test reads it.
var last_played := ""
## Every effect played since the last idle frame, for checks on a moment
## that plays several sounds at once.
var frame_played: PackedStringArray = []
var ambient_name := ""
## The music loop playing (or fading in), by name; empty when none.
var music_name := ""

var _sounds: Dictionary = {}
var _pool: Array[AudioStreamPlayer] = []
var _next := 0
var _ambient: AudioStreamPlayer
## Two music players, so one loop can fade out under the next.
var _music: Array[AudioStreamPlayer] = []
var _music_live := 0
var _fade: Tween
var _ducked := false
var _duck: Tween


func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS
	for i in POOL_SIZE:
		var player := AudioStreamPlayer.new()
		player.bus = &"SFX"
		add_child(player)
		_pool.append(player)
	_ambient = AudioStreamPlayer.new()
	_ambient.bus = &"Music"
	add_child(_ambient)
	for i in 2:
		var player := AudioStreamPlayer.new()
		player.bus = &"Music"
		add_child(player)
		_music.append(player)


## The pause menu stops the tree; the music keeps playing, quieter.
func _process(_delta: float) -> void:
	frame_played.clear()
	var paused := get_tree().paused
	if paused != _ducked and not (_fade and _fade.is_running()):
		_ducked = paused
		if _duck:
			_duck.kill()
		_duck = create_tween()
		_duck.tween_property(_music[_music_live], "volume_db", _music_target_db(), DUCK_TIME)


## Plays an effect by name (its file name without the extension). An
## unknown name is reported once and ignored. `pitch` scales the sound
## (the dialogue blips differ by speaker); the small random spread is
## applied on top of it.
func play(sound_name: String, volume_db := 0.0, pitch := 1.0) -> void:
	var stream := _stream(SFX_DIR + sound_name + ".wav")
	if stream == null:
		return
	var player := _pool[_next]
	_next = (_next + 1) % POOL_SIZE
	player.stream = stream
	player.volume_db = volume_db
	player.pitch_scale = pitch * (1.0 + randf_range(-PITCH_SPREAD, PITCH_SPREAD))
	player.play()
	last_played = sound_name
	frame_played.append(sound_name)


## Crossfades to the named music loop, or keeps it going if it is the one
## playing. An empty name fades the music out. A name with no file stops
## the music, once a warning is given.
func play_music(track_name: String) -> void:
	if track_name == music_name:
		return
	music_name = track_name
	var stream: AudioStream = null
	if not track_name.is_empty():
		stream = _music_stream(track_name)
	var outgoing := _music[_music_live]
	_music_live = 1 - _music_live
	var incoming := _music[_music_live]
	if _fade:
		_fade.kill()
	_fade = create_tween().set_parallel(true)
	if stream == null:
		incoming.stop()
	else:
		incoming.stream = stream
		incoming.volume_db = SILENT_DB
		incoming.play()
		_fade.tween_property(incoming, "volume_db", _music_target_db(), CROSSFADE_TIME)
	if outgoing.playing:
		_fade.tween_property(outgoing, "volume_db", SILENT_DB, CROSSFADE_TIME)
		# After both fades: the old loop stops for good.
		_fade.chain().tween_callback(outgoing.stop)
	elif stream == null:
		_fade.kill()
		_fade = null


func stop_music() -> void:
	play_music("")


## Whether a music loop is sounding (or fading in).
func is_music_playing() -> bool:
	return _music[_music_live].playing


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
	_loop(stream)
	_ambient.stream = stream
	_ambient.volume_db = AMBIENT_DB
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


func _music_target_db() -> float:
	return MUSIC_DB + (DUCK_DB if _ducked else 0.0)


## The music loop by name: an Ogg file, else a WAV (what gen_music.py
## writes without ffmpeg), set to loop.
func _music_stream(track_name: String) -> AudioStream:
	var base := MUSIC_DIR + track_name
	var stream: AudioStream = null
	if ResourceLoader.exists(base + ".ogg"):
		stream = _stream(base + ".ogg")
	else:
		stream = _stream(base + ".wav")
	if stream != null:
		_loop(stream)
	return stream


func _loop(stream: AudioStream) -> void:
	if stream is AudioStreamWAV:
		var wav := stream as AudioStreamWAV
		wav.loop_mode = AudioStreamWAV.LOOP_FORWARD
		wav.loop_begin = 0
		wav.loop_end = int(wav.get_length() * wav.mix_rate)
	elif stream is AudioStreamOggVorbis:
		(stream as AudioStreamOggVorbis).loop = true


func _stream(path: String) -> AudioStream:
	if not _sounds.has(path):
		var stream: AudioStream = null
		if ResourceLoader.exists(path):
			stream = load(path)
		else:
			push_warning("No sound at %s" % path)
		_sounds[path] = stream
	return _sounds[path]
