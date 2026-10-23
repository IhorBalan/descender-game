# Descender: experience, levels, skills and the camp

Written 2026-10-23 against `main` after PR #93. It adds a fourth layer next
to the abilities, the upgrades and the equipment of
[`upgrades.md`](upgrades.md) and [`economy.md`](economy.md): numbers the
player picks, earned by fighting.

## Experience and levels

Every kill gives **experience**, the `xp` in the enemy's `loot` table
(`scripts/loot.gd` pays it to the hero, next to the light). It is kept
through death, restart and load, like light.

| Enemy | Experience |
| --- | --- |
| Imp | 3 |
| Watcher, shade | 4 |
| Angel guard, hound | 5 |
| Slug, siren | 6 |
| Burned | 7 |
| Hanged thing | 8 |
| Hoarder | 9 |
| Brute | 10 |
| Bosses | 40 (the Seraph Warden) rising to 100 (the Lowest) and 120 (the Gardener) |

A level takes `25 + 10 x (level - 1)` experience, so the early levels come
in a handful of fights and the deep circles still pay out. The curve stops
at level 20. Total experience to reach a level:

| Level | Experience |
| --- | --- |
| 2 | 25 |
| 5 | 160 |
| 10 | 585 |
| 15 | 1260 |
| 20 | 2185 |

## Skill points and skills

Every level after the first is one **skill point**, spent at a checkpoint
camp. A skill is a few ranks of one number, in the item vocabulary
(`Items.ADDITIVE_STATS` and `Items.FACTOR_STATS`), so the hero sums the
skills next to the equipment (`Player._stat_bonus` and `_stat_factor`).
Some skills open only at a level.

| Skill | Ranks | Opens at | Per rank |
| --- | --- | --- | --- |
| Vigour | 3 | 1 | +1 heart |
| Edge | 3 | 1 | +1 damage |
| Swift Feet | 3 | 1 | run x1.08 |
| Deep Draught | 2 | 3 | +1 healing charge |
| Long Reach | 2 | 3 | reach x1.15 |
| Quick Dash | 2 | 4 | dash cooldown x0.8 |
| Lightkeeper | 2 | 5 | light x1.25 |
| Iron Skin | 2 | 6 | +1 guard |
| Quick Hand | 2 | 8 | swing time x0.88 |
| Mender | 1 | 10 | +1 heart per heal |

That is 22 ranks for 19 points, so a hero at the cap still chooses what
to leave out. Points are not refunded; a save that spent more than it
earned (an edited file, a changed table) is given the extra back.

## The camp

Touching a checkpoint still rests and saves, as before. Standing at it and
pressing the talk key **makes camp**, a menu over the paused game:

- **The Pack**: the same page as the pause menu's, to wear and use what
  the hero carries.
- **Skills**: the level, the experience towards the next, the points left
  and every skill with its ranks. Space puts a point in.
- **Fast Travel**: a slot for the fast travel between checkpoints. The
  button is grey until it is wired; `CheckpointMenu.set_fast_travel` and
  the `fast_travel_requested` signal are the hooks.
- **Save Game** and **Leave**.

Making camp rests the hero again, so a heart learned is a full heart.
The HUD shows the level and an experience bar under the purse, and says
when a point is waiting.

## In the save

`skills`: `{"xp": <int>, "ranks": {<skill id>: <rank>}}`. Older saves
without it start at level 1.
