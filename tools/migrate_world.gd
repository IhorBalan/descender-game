extends SceneTree

## Turns the map editor's export into the world's scenes (see WorldMigration):
##
##   godot --headless --path . --script tools/migrate_world.gd -- \
##       <world.json of an export> [res://scenes/world] [res://levels/world/world_tiles.tres]
##
## then `godot --headless --path . --import` once, so the editor picks up the
## new resources. Run from an import that has been done, so the sheets are
## referenced, not embedded.


func _initialize() -> void:
	var args := OS.get_cmdline_user_args()
	if args.is_empty():
		printerr("usage: -- <world.json of an export> [scenes dir] [tileset path]")
		quit(1)
		return
	var json_path: String = args[0]
	var scenes_dir := args[1] if args.size() > 1 else "res://scenes/world"
	var tile_set_path := args[2] if args.size() > 2 else "res://levels/world/world_tiles.tres"
	var out := WorldMigration.migrate(json_path, scenes_dir, tile_set_path)
	if out.is_empty():
		print("migration failed")
		quit(1)
	else:
		print("wrote ", out)
		quit()
