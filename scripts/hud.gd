class_name Hud
extends CanvasLayer

## Health, healing charges, unlocked abilities, the dash cooldown and short
## messages. main.gd hands over the hero so the dash readout can follow him.

const BOSS_BAR_WIDTH := 160.0
const BOSS_BAR_BACK := Color(0.0, 0.0, 0.0, 0.6)
const BOSS_BAR_FILL := Color(1.0, 0.85, 0.45)
const BOSS_BAR_HURT := Color(0.9, 0.3, 0.3)

var player: Player

## The purse in the top right corner: light and tally bones.
var purse_label: Label
## The boss bar at the bottom of the screen, built in _ready.
var boss_bar: Control
var boss_name_label: Label
var _boss_fill: ColorRect

var _message_timer := 0.0
## Messages waiting for the current timed one to end: [text, duration].
var _queue: Array[Array] = []

@onready var status: StatusBar = $Status
@onready var message: PromptLabel = $Message


func _ready() -> void:
	boss_bar = Control.new()
	boss_bar.name = "BossBar"
	boss_bar.position = Vector2((320.0 - BOSS_BAR_WIDTH) / 2.0, 140.0)
	boss_bar.size = Vector2(BOSS_BAR_WIDTH, 16.0)
	boss_bar.mouse_filter = Control.MOUSE_FILTER_IGNORE
	boss_bar.visible = false
	add_child(boss_bar)
	boss_name_label = Label.new()
	boss_name_label.size = Vector2(BOSS_BAR_WIDTH, 10.0)
	boss_name_label.add_theme_font_size_override("font_size", 8)
	boss_name_label.add_theme_color_override("font_shadow_color", Color(0.0, 0.0, 0.0, 0.8))
	boss_name_label.add_theme_constant_override("shadow_offset_x", 1)
	boss_name_label.add_theme_constant_override("shadow_offset_y", 1)
	boss_name_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	boss_bar.add_child(boss_name_label)
	var back := ColorRect.new()
	back.position = Vector2(0.0, 11.0)
	back.size = Vector2(BOSS_BAR_WIDTH, 4.0)
	back.color = BOSS_BAR_BACK
	boss_bar.add_child(back)
	_boss_fill = ColorRect.new()
	_boss_fill.position = Vector2(1.0, 12.0)
	_boss_fill.size = Vector2(BOSS_BAR_WIDTH - 2.0, 2.0)
	_boss_fill.color = BOSS_BAR_FILL
	boss_bar.add_child(_boss_fill)
	purse_label = Label.new()
	purse_label.name = "Purse"
	purse_label.position = Vector2(196.0, 3.0)
	purse_label.size = Vector2(120.0, 10.0)
	purse_label.add_theme_font_size_override("font_size", 8)
	purse_label.add_theme_color_override("font_color", Inventory.CURRENCIES["light"]["color"])
	purse_label.add_theme_color_override("font_shadow_color", Color(0.0, 0.0, 0.0, 0.8))
	purse_label.add_theme_constant_override("shadow_offset_x", 1)
	purse_label.add_theme_constant_override("shadow_offset_y", 1)
	purse_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	add_child(purse_label)
	set_purse()


func _process(delta: float) -> void:
	if is_instance_valid(player):
		var readiness := player.dash_readiness()
		if readiness != status.dash_readiness:
			status.dash_readiness = readiness
			status.queue_redraw()
		# The power-up bars drain every frame while one is running.
		if not status.powers.is_empty():
			set_powers(player.powers)
	if _message_timer > 0.0:
		_message_timer -= delta
		if _message_timer <= 0.0:
			message.visible = false
			if not _queue.is_empty():
				var next: Array = _queue.pop_front()
				show_message(next[0], next[1])


func set_health(current: int, max_value: int) -> void:
	status.health = current
	status.max_health = max_value
	status.queue_redraw()


func set_charges(current: int, max_value: int) -> void:
	status.charges = current
	status.max_charges = max_value
	status.queue_redraw()


func set_abilities(abilities: Array[Player.Ability]) -> void:
	status.abilities = abilities.duplicate()
	status.queue_redraw()


## The hero's power-ups (Player.Power -> seconds left).
func set_powers(powers: Dictionary) -> void:
	status.powers = powers.duplicate()
	status.queue_redraw()


## Writes the purse from the pack (see Inventory).
func set_purse() -> void:
	purse_label.text = ShopMenu.purse_text()


## Shows the boss bar with the boss's name and health.
func show_boss(boss_name: String, current: int, max_value: int) -> void:
	boss_name_label.text = boss_name
	set_boss_health(current, max_value)
	boss_bar.visible = true


func set_boss_health(current: int, max_value: int) -> void:
	var share := clampf(float(current) / float(maxi(max_value, 1)), 0.0, 1.0)
	_boss_fill.size.x = roundf((BOSS_BAR_WIDTH - 2.0) * share)
	_boss_fill.color = BOSS_BAR_HURT if share <= Boss.PHASE_TWO_AT else BOSS_BAR_FILL


func hide_boss() -> void:
	boss_bar.visible = false


## Clears the message line and whatever was queued behind it.
func hide_message() -> void:
	_queue.clear()
	message.visible = false
	_message_timer = 0.0


## A negative duration keeps the message until the next one. Showing a
## message drops whatever was queued behind the previous one. The text
## can name keys with `{action}` tokens (see Prompts); the line redraws
## them for the device in the player's hands.
func show_message(text: String, duration: float = 2.5) -> void:
	_queue.clear()
	message.template = text
	message.visible = true
	_message_timer = duration if duration > 0.0 else 0.0


## Shows the message once the current timed one has run out, or at once
## when nothing timed is showing.
func queue_message(text: String, duration: float = 2.5) -> void:
	if _message_timer > 0.0:
		_queue.append([text, duration])
	else:
		show_message(text, duration)
