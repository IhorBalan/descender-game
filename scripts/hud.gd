class_name Hud
extends CanvasLayer

## Health, healing charges, unlocked abilities, the dash cooldown, the purse,
## the level with its experience bar and short messages. main.gd hands
## over the hero so the dash readout can follow him.

## The editor component shown as the hero's health, its width in level
## pixels and where it sits.
const HEALTH_BAR_COMPONENT := "HealthBar"
const HEALTH_BAR_WIDTH := 110.0
const HEALTH_BAR_AT := Vector2(2.0, 1.0)
const BOSS_BAR_WIDTH := 160.0
const BOSS_BAR_BACK := Color(0.0, 0.0, 0.0, 0.6)
const BOSS_BAR_FILL := Color(1.0, 0.85, 0.45)
const BOSS_BAR_HURT := Color(0.9, 0.3, 0.3)
const XP_BAR_WIDTH := 40.0
const XP_BAR_BACK := Color(0.0, 0.0, 0.0, 0.6)
const XP_BAR_FILL := Color(0.75, 0.85, 1.0)
const LEVEL_COLOR := Color(0.85, 0.9, 1.0)
## The level line brightens while a skill point waits to be spent.
const POINT_COLOR := Color(1.0, 0.95, 0.7)

var player: Player

## The purse in the top right corner: light and tally bones.
var purse_label: Label
## Under it, the level and a bar of the experience towards the next.
var level_label: Label
## The boss bar at the bottom of the screen, built in _ready.
var boss_bar: Control
var boss_name_label: Label
## The health bar component from the editor, when there is one (see
## _setup_health_bar); the hearts show health otherwise.
var health_bar: UiComponent = null
var _boss_fill: ColorRect
var _xp_fill: ColorRect

var _message_timer := 0.0
## Messages waiting for the current timed one to end: [text, duration].
var _queue: Array[Array] = []

@onready var status: StatusBar = $Status
@onready var message: PromptLabel = $Message


func _ready() -> void:
	_setup_health_bar()
	ComponentStore.updated.connect(_on_component_updated)
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
	level_label = Label.new()
	level_label.name = "Level"
	level_label.position = Vector2(196.0, 12.0)
	level_label.size = Vector2(120.0, 10.0)
	level_label.add_theme_font_size_override("font_size", 8)
	level_label.add_theme_color_override("font_color", LEVEL_COLOR)
	level_label.add_theme_color_override("font_shadow_color", Color(0.0, 0.0, 0.0, 0.8))
	level_label.add_theme_constant_override("shadow_offset_x", 1)
	level_label.add_theme_constant_override("shadow_offset_y", 1)
	level_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_RIGHT
	add_child(level_label)
	var xp_back := ColorRect.new()
	xp_back.name = "XpBar"
	xp_back.position = Vector2(316.0 - XP_BAR_WIDTH, 23.0)
	xp_back.size = Vector2(XP_BAR_WIDTH, 3.0)
	xp_back.color = XP_BAR_BACK
	add_child(xp_back)
	_xp_fill = ColorRect.new()
	_xp_fill.position = Vector2(1.0, 1.0)
	_xp_fill.size = Vector2(0.0, 1.0)
	_xp_fill.color = XP_BAR_FILL
	xp_back.add_child(_xp_fill)
	set_purse()
	set_progress()


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
	_show_health()


## The editor's health bar component in place of the hearts, when there is
## one (see ComponentStore), reloaded when a newer one arrives.
func _setup_health_bar() -> void:
	if health_bar:
		health_bar.queue_free()
		health_bar = null
	health_bar = ComponentStore.create(HEALTH_BAR_COMPONENT, HEALTH_BAR_WIDTH)
	if health_bar == null:
		status.health_bar_rect = Rect2()
	else:
		health_bar.position = HEALTH_BAR_AT
		add_child(health_bar)
		move_child(health_bar, status.get_index() + 1)
		status.health_bar_rect = Rect2(HEALTH_BAR_AT, health_bar.size)
		_show_health()
	status.queue_redraw()


func _on_component_updated(component_name: String) -> void:
	if ComponentStore.key(component_name) == ComponentStore.key(HEALTH_BAR_COMPONENT):
		_setup_health_bar()


## Feeds the hero's health to the bar: its health (or value) and maxHealth
## (or max) props, whichever the component has.
func _show_health() -> void:
	if health_bar == null:
		return
	for name in ["health", "value", "current"]:
		health_bar.set_prop(name, float(status.health))
	for name in ["maxHealth", "max", "max_health"]:
		health_bar.set_prop(name, float(status.max_health))


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


## Writes the level and fills the experience bar from the skills (see
## Skills). The line names a skill point waiting to be spent.
func set_progress() -> void:
	var points := Skills.points_left()
	var text := "Lv %d" % Skills.level()
	if points > 0:
		text += "   %d skill point%s" % [points, "" if points == 1 else "s"]
	level_label.text = text
	level_label.add_theme_color_override("font_color", POINT_COLOR if points > 0 else LEVEL_COLOR)
	var progress := Skills.level_progress()
	var share := 1.0
	if progress[1] > 0:
		share = clampf(float(progress[0]) / float(progress[1]), 0.0, 1.0)
	_xp_fill.size.x = roundf((XP_BAR_WIDTH - 2.0) * share)


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
