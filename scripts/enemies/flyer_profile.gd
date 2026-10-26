class_name FlyerProfile
extends Resource

## The numbers of one flying enemy body (see Flyer). One of these lives in
## resources/enemies/<name>.tres and is set on the enemy's scene in
## scenes/enemies/<name>.tscn.

## Hits to kill.
@export var health := 2
## Speed about its post.
@export var hover := 25.0
## Speed after the hero.
@export var chase := 55.0
## How close the hero has to come.
@export var aggro := 110.0

@export_group("Drops")
## How often a kill leaves a heal mote behind.
@export_range(0.0, 1.0) var drop_chance := 0.5
## The light it leaks and what else it carried (see Loot).
@export var loot: Dictionary = {}
## The colour it dies in.
@export var puff := Color(1.0, 0.95, 0.8)
