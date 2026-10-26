@tool
class_name WorldLight
extends PointLight2D

## A point light of the world, placed in a region's scene. The game builds
## its own PointLight2D from it (see LevelBuilder._build_lights), with the
## radius the texture scale gives: the disc texture is TEXTURE_SIZE pixels
## across, so a scale of 1 lights TEXTURE_SIZE / 2 level pixels around.

const TEXTURE_SIZE := 128

## Whether the light wavers (data for now).
@export var flicker := false


## The light's reach in level pixels.
func radius() -> float:
	return texture_scale * TEXTURE_SIZE / 2.0
