# Descender: the enemies, by region

Who stands where, and why. The placements are in
the `Placements` of the region scenes under `scenes/world/` (`WorldPlacement` nodes); the bodies are the walker profiles
(ground) and flyer profiles (air) in `resources/enemies/`, each with a
scene in `scenes/enemies/`, and the `Hanger`; the strips are drawn by
`tools/enemy_sprites/export.py` and cut into each body's `SpriteFrames` by
`tools/sprite_frames.py`.

## The rule

Heaven is kept by angels and nothing else. Purgatory is the border, where
the angels sort the dead and the first demons climb up to test the gate.
Everything below the hub is Hell, and Hell is demons: the story's
sent-down, worn to teeth by the fall. Each circle has a body of its own
that is found nowhere else, on top of the demons found everywhere.

Three bodies are **generalists** and may stand in any circle:

| Body | What it is | Where |
| --- | --- | --- |
| **Imp** | The common demon: small, quick, three hits. Barks when fresh fallen. | Every circle; one on Earth, nine in Purgatory |
| **Brute** | The heavy: armoured, eight hits, a two-heart swing. | Gluttony and below; slams from Wrath |
| **Shade** | A forgotten soul, drifting: the demons' flyer. | Limbo and below; dives from Heresy |

Everything else belongs to one region.

## Region by region

| Region | Unique body | What it does | Also there |
| --- | --- | --- | --- |
| **Paradise** | **Angel guard**, **watcher** | The Choir's soldier (spear, five hits) and its eye (hovers, chases). | Nothing: angels only. |
| **Ruined Earth** | | The hub. | One imp that climbed out of the churchyard. |
| **Purgatory** | *(the border)* | Guards and watchers do the sorting. | Imps testing the gate. |
| **1. Limbo** | **Shade** (first seen) | The Forgotten: shades drift here before anywhere else. | Fresh-fallen imps that still talk. |
| **2. Lust** | **Siren** | The winged want: a velvet-skinned flyer, keener and quicker than a shade, three hits. | Imps that chase. |
| **3. Gluttony** | **Slug** | The devourer: a bloated maw on a crawling gut. Armoured, six hits, slow; lies like a heap until the hero is close, then bites. Leaves a Halo now and then. | Imps that ambush, the first brutes. |
| **4. Greed** | **Hoarder** | The miser: a hunched demon under a sack of what it took. Keeps its distance and throws from the sack; leaks more light than anything its size and carries a tally bone one time in five. | Imps, brutes, shades. |
| **5. Wrath** | **Brute** (its home) | The brutes slam here: every one of them, in the Red Arena's circle. | Imps that pounce, shades. |
| **6. Heresy** | **Burned** | A sent-down angel: the guard's bones, charred, the halo an ember. Chases, hurls, ambushes. Tobiah's messengers are the same kind. | Imps that hurl, brutes, the first diving shades. |
| **7. Violence** | **Hound** | The lean dog of the Wounded, hunting in packs, that runs the hero down and springs. | Imps that pounce, brutes that slam, shades that dive. |
| **8. Fraud** | *(the last camp)* | The army rests: a passive one of everything. | Imps, brutes, a hound, a hoarder, none of them swinging. |
| **9. Treachery** | **Hanged** | The frozen traitor on a rope that lets go when the hero walks beneath. | Imps, brutes that slam, shades that dive. |

The bosses are unchanged: each room's profile is still built on the guard,
imp, brute or hero strips.

## What was wrong before

Imps stood in the first room of Heaven, angel guards patrolled every deep
circle down to Treachery, and the angel watchers were the only flyer in
all of Hell. Nothing below Gluttony was anything but the four bodies the
prototype started with, except the hound and the hanged thing.

## Numbers

| Body | Health | Speed | Damage | Habit | Count |
| --- | --- | --- | --- | --- | --- |
| Angel guard | 5 | 18 | 2 | | 11 |
| Watcher | 2 | hover 25, chase 55 | 1 (contact) | dive | 7 |
| Imp | 3 | 32 | 1 | chase, ambush, pounce, hurl | 86 |
| Brute | 8 (armour) | 14 | 2 | slam | 53 |
| Shade | 2 | hover 18, chase 48 | 1 (contact) | dive | 40 |
| Siren | 3 | hover 30, chase 72 | 1 (contact) | | 6 |
| Slug | 6 (armour) | 9 | 1 | ambush | 5 |
| Hoarder | 4 | 22 | 1 | hurl | 12 |
| Burned | 5 | 20 | 2 | chase, hurl, ambush | 9 |
| Hound | 3 | 40 | 1 | lunge | 18 |
| Hanged | 3 | | 2 (the drop) | | 7 |

255 enemies in all, the same as before: every change is a body swapped
in place, so the density of each circle is what the level design chose.
