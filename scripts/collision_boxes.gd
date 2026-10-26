class_name CollisionBoxes
extends Node2D

## Draws every collision shape in the tree over the game, coloured by what
## owns it: the hero green, enemies red, items and triggers blue, and the
## walkable world (floors, platforms, walls, ledges, light barriers, the
## sealed doors and secret walls) black. Hazards, which are level geometry
## but not walkable, get their own orange so spikes read apart from floors.
##
## The console's `show_boxes on` adds one of these under the root (see
## DevConsole); `show_boxes off` frees it. It sits on the world canvas, so
## the camera carries it, and draws above everything. Shapes are gathered
## once and kept up to date from the tree's node_added and node_removed
## signals; only the ones in view are drawn each frame. A shape that is
## disabled, or whose owner has left both its layer and mask empty (an
## opened door, a broken secret wall), is skipped: the physics engine
## ignores it too.

const PLAYER_COLOR := Color(0.2, 1.0, 0.3)
const ENEMY_COLOR := Color(1.0, 0.2, 0.2)
const ITEM_COLOR := Color(0.3, 0.55, 1.0)
const WORLD_COLOR := Color.BLACK
const HAZARD_COLOR := Color(1.0, 0.6, 0.1)
## Shapes owned by nothing the categories know (a stray Area2D).
const OTHER_COLOR := Color(0.8, 0.8, 0.8)
## How see-through the fill inside an outline is.
const FILL_ALPHA := 0.16
## How many segments draw a circle or a capsule's ends.
const ARC_SEGMENTS := 20
## Shapes this far outside the view are still drawn, so a shape whose
## bounds are a rough guess (a rotated polygon) never pops at the edge.
const VIEW_MARGIN := 16.0

## Every CollisionShape2D and CollisionPolygon2D in the tree, keyed by
## instance id, each to its cached colour (null until first drawn) and,
## for a shape under a StaticBody2D, its global bounds, which never move.
var _entries: Dictionary = {}


func _ready() -> void:
	name = "CollisionBoxes"
	z_index = RenderingServer.CANVAS_ITEM_Z_MAX
	z_as_relative = false
	process_mode = Node.PROCESS_MODE_ALWAYS
	var tree := get_tree()
	tree.node_added.connect(_on_node_added)
	tree.node_removed.connect(_on_node_removed)
	_gather(tree.root)


func _exit_tree() -> void:
	var tree := get_tree()
	if tree.node_added.is_connected(_on_node_added):
		tree.node_added.disconnect(_on_node_added)
	if tree.node_removed.is_connected(_on_node_removed):
		tree.node_removed.disconnect(_on_node_removed)


func _process(_delta: float) -> void:
	queue_redraw()


## How many shapes are being tracked, for the console and the tests.
func shape_count() -> int:
	return _entries.size()


func _gather(node: Node) -> void:
	if _is_shape(node):
		_entries[node.get_instance_id()] = {"node": node, "color": null, "rect": null}
	for child in node.get_children():
		_gather(child)


func _on_node_added(node: Node) -> void:
	if _is_shape(node):
		_entries[node.get_instance_id()] = {"node": node, "color": null, "rect": null}


func _on_node_removed(node: Node) -> void:
	_entries.erase(node.get_instance_id())


static func _is_shape(node: Node) -> bool:
	return node is CollisionShape2D or node is CollisionPolygon2D


## The colour for a shape by what owns it: the first ancestor that is the
## hero, his bolts, an enemy or its shot decides; otherwise a static body
## is world, a hazard is a hazard and any other area is an item or trigger.
static func color_for(owner: Node) -> Color:
	var node := owner
	while node != null:
		if node is Player or node.is_in_group("player") or node.is_in_group("light_bolt"):
			return PLAYER_COLOR
		if node.is_in_group("enemy") or node.is_in_group("enemy_shot"):
			return ENEMY_COLOR
		node = node.get_parent()
	node = owner
	while node != null:
		if node is StaticBody2D:
			return WORLD_COLOR
		if node is Hazard:
			return HAZARD_COLOR
		node = node.get_parent()
	if owner is Area2D:
		return ITEM_COLOR
	return OTHER_COLOR


## The owner of a shape: the collision object it sits in, when it does.
static func owner_of(shape: Node2D) -> CollisionObject2D:
	return shape.get_parent() as CollisionObject2D


## Whether the physics engine pays the shape any attention.
static func is_live(shape: Node2D, owner: CollisionObject2D) -> bool:
	if owner == null:
		return false
	if shape.get("disabled"):
		return false
	return owner.collision_layer != 0 or owner.collision_mask != 0


## The shape's bounds in its own space, or an empty rect when it has none.
static func local_rect(shape: Node2D) -> Rect2:
	if shape is CollisionPolygon2D:
		var polygon: PackedVector2Array = shape.polygon
		if polygon.is_empty():
			return Rect2()
		var rect := Rect2(polygon[0], Vector2.ZERO)
		for point in polygon:
			rect = rect.expand(point)
		return rect
	var resource: Shape2D = shape.shape
	if resource == null:
		return Rect2()
	return resource.get_rect()


func _draw() -> void:
	var view := get_canvas_transform().affine_inverse() * get_viewport_rect()
	view = view.grow(VIEW_MARGIN)
	var stale: Array = []
	for id in _entries:
		var entry: Dictionary = _entries[id]
		var shape: Node2D = entry["node"]
		if not is_instance_valid(shape) or not shape.is_inside_tree():
			stale.append(id)
			continue
		var owner := owner_of(shape)
		if not is_live(shape, owner):
			continue
		var rect: Variant = entry["rect"]
		if rect == null:
			rect = shape.global_transform * local_rect(shape)
			if owner is StaticBody2D:
				entry["rect"] = rect
		if not view.intersects(rect):
			continue
		if entry["color"] == null:
			entry["color"] = color_for(owner)
		_draw_shape(shape, entry["color"])
	for id in stale:
		_entries.erase(id)
	draw_set_transform_matrix(Transform2D.IDENTITY)


func _draw_shape(shape: Node2D, color: Color) -> void:
	draw_set_transform_matrix(shape.global_transform)
	var fill := Color(color, FILL_ALPHA)
	if shape is CollisionPolygon2D:
		var polygon: PackedVector2Array = shape.polygon
		if polygon.size() < 2:
			return
		if shape.build_mode == CollisionPolygon2D.BUILD_SOLIDS and polygon.size() >= 3:
			draw_colored_polygon(polygon, fill)
		_draw_outline(polygon, color)
		return
	var resource: Shape2D = shape.shape
	if resource is RectangleShape2D:
		var rect := Rect2(-resource.size / 2.0, resource.size)
		draw_rect(rect, fill)
		draw_rect(rect, color, false, -1.0)
	elif resource is CircleShape2D:
		draw_circle(Vector2.ZERO, resource.radius, fill)
		draw_arc(Vector2.ZERO, resource.radius, 0.0, TAU, ARC_SEGMENTS, color, -1.0)
	elif resource is CapsuleShape2D:
		var points := capsule_points(resource.radius, resource.height)
		draw_colored_polygon(points, fill)
		_draw_outline(points, color)
	elif resource is ConvexPolygonShape2D:
		if resource.points.size() >= 3:
			draw_colored_polygon(resource.points, fill)
			_draw_outline(resource.points, color)
	elif resource is ConcavePolygonShape2D:
		if resource.segments.size() >= 2:
			draw_multiline(resource.segments, color, -1.0)
	elif resource is SegmentShape2D:
		draw_line(resource.a, resource.b, color, -1.0)
	elif resource is SeparationRayShape2D:
		draw_line(Vector2.ZERO, Vector2(0.0, resource.length), color, -1.0)
	elif resource is WorldBoundaryShape2D:
		var along: Vector2 = resource.normal.orthogonal() * 10000.0
		var at: Vector2 = resource.normal * resource.distance
		draw_line(at - along, at + along, color, -1.0)


func _draw_outline(points: PackedVector2Array, color: Color) -> void:
	var closed := PackedVector2Array(points)
	closed.append(points[0])
	draw_polyline(closed, color, -1.0)


## The outline of a capsule standing upright: two half circles joined by
## straight sides, as CapsuleShape2D is laid out.
static func capsule_points(radius: float, height: float) -> PackedVector2Array:
	var points := PackedVector2Array()
	var half := maxf(height / 2.0 - radius, 0.0)
	var steps := ARC_SEGMENTS / 2
	for i in range(steps + 1):
		var angle := PI + PI * i / steps
		points.append(Vector2(cos(angle), sin(angle)) * radius + Vector2(0.0, -half))
	for i in range(steps + 1):
		var angle := PI * i / steps
		points.append(Vector2(cos(angle), sin(angle)) * radius + Vector2(0.0, half))
	return points
