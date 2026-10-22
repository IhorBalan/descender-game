# Descender: economy, loot and the pack

Written 2026-10-22 against `main` after PR #85. Section 1 is the design:
what the hero picks up, what it buys and what he wears. Section 2 is what
is in the game now. Section 3 is how the pieces are built.

This changes one rule of [`upgrades.md`](upgrades.md): "nothing is bought
with a currency" is gone. The abilities, the permanent upgrades and the
power-ups stay exactly as that doc has them; this adds a layer of money,
drops and equipment next to them. Pell's forging for church iron and
Vael's trade for a memory are unchanged, and sit alongside their stalls.

---

## 1. Design

### Two currencies

| Currency | What it is | Where it comes from | What it buys |
| --- | --- | --- | --- |
| **Light** | What leaks out of a killed thing; the angels are made of it and the demons took it. Common. | Every enemy, in amounts that grow with the enemy; every boss, a hoard. | Old Pell's forge: blades and coats. Flasks anywhere. |
| **Tally bones** | The notched bones the imps keep count on. Rare. | Three from every boss; now and then from an imp, a guard, a brute or a watcher. | Vael's stall: charms and the bottled power-ups. |

Rules:

- **Light is never lost.** It is kept through death, restart and load, so
  a bad run still bought something. What is lost is light left lying on
  the floor when the hero dies: a drop goes with the heal motes when the
  enemies come back.
- **Light flies to the hero.** A drop bobs where the enemy fell and comes
  to him once he is within a few tiles, so picking it up never means
  standing in a fight.
- **Bones are the slow currency.** Three per boss, a dozen or so over a
  whole descent plus whatever the imps give up, so every charm at Vael's
  stall is a choice. Light is the fast one: a few hundred by Earth, a few
  thousand by the bottom.
- **Prices are flat numbers**, in the shop table, scaled only by one charm
  (the Seal of the Vault).

Why two: one currency makes every shop the same shop. With light for the
blacksmith and bones for the Bargainer, the common drop pays for the
common gear and the boss reward pays for the rare, and a player who
farms imps gets stronger without getting everything.

### Enemy loot

Every enemy has a `loot` table in its profile (see `Loot`):

| Enemy | Light | Tally bone | Basin Flask | Also |
| --- | --- | --- | --- | --- |
| Imp | 3 to 5 | 8% | 3% | a Lit Blade, 10% (as before) |
| Watcher | 4 to 6 | 4% | 3% | a heal mote, half the time |
| Angel guard | 6 to 9 | 4% | 4% | a heal mote always; Zeal, 10% |
| Brute | 10 to 14 | 10% | 6% | a Halo always |
| Boss | 40 to 160, rising with the depth | 3, always | | its unique piece, once |

### Unique loot

Every boss leaves one piece of equipment that only it drops, once per save
(flag `loot:<item>`). It lies where the boss fell; a death, a restart or a
load lays it back down by the boss's placement until it is taken, so it
cannot be lost. Each is the boss made into a tool:

| Boss | Piece | Slot | What it does |
| --- | --- | --- | --- |
| Seraph Warden | Warden's Halo | charm | A Halo at every checkpoint rest and respawn |
| The Penitent | Penitent's Shroud | armour | Guard 1, no weight |
| Limbo Lord | Lord's Tally | charm | Light drops doubled |
| Keeper of the Palace | Velvet Gloves | charm | The dash comes back in half the time |
| The Bloated One | Glutton's Maw | sword | +2 damage, swings slower |
| Warden of the Vault | Seal of the Vault | charm | Every price at 60% |
| Gorrow the Fury | Fury's Edge | sword | +1 damage, reach x1.3 |
| Cursed Bishop | Bishop's Mitre | armour | Guard 1, +1 heart per healing charge |
| Champion of the Red Arena | Champion's Plate | armour | Guard 2, slower feet |
| The False King | False Crown | charm | +2 hearts |
| The Lowest | Brother's Knife | sword | +2 damage, swings quicker |
| The Gardener | Gardener's Seed | charm | +1 heart on every kill |

### Equipment

Three slots: **sword**, **armour**, **charm**. One piece in each; a piece
in the pack is worn or taken off from the pack page of the pause menu.
What a piece does is a small table of stats on the item (see
`Items.TABLE`), added or multiplied over the three slots:

| Stat | Kind | Read by |
| --- | --- | --- |
| damage | add | `Player.attack_damage` |
| reach | multiply | the swing's hitbox width |
| attack_time | multiply | how long the swing takes (and its strip) |
| guard | add | hearts taken off every hit, never below one |
| hearts, charges | add | the maximums, with the upgrade flags |
| heal | add | hearts per healing charge |
| speed, dash_cooldown | multiply | `run_speed`, `dash_cooldown` |
| light | multiply | light dropped by enemies |
| mote_chance | add | how often a kill leaves a heal mote |
| halo_at_rest, heal_on_kill | add | a Halo at every rest; hearts per kill |
| prices | multiply | what traders ask |

Why these and not others: every stat is one the game already had a dial
for (the sword's damage, the hero's maximums, the drops), so a piece
changes a fight the player already understands. Jump height and dash
length stay off the list for the reason the upgrades doc gives: the level
design depends on them.

The bought pieces are plain trades: a damage for nothing (Church Iron
Sword), reach for nothing (Hinge Blade), guard for a little speed
(Quilted Coat), guard and a heart for more speed (Scrap Mail). The boss
pieces are the strong ones and the odd ones. Nothing is strictly better
than everything else: the Champion's Plate stops two hearts of a hit and
slows the hero, the Maw hits for three and swings slow, the Knife is
quick.

### Consumables

Used from the pack page. A **Basin Flask** puts one healing charge back
(the common drop, and the cheap ware at both stalls). Vael bottles the
three power-ups: a **Halo Shard**, **Choir Oil** (a Lit Blade) and a
**Draught of Zeal**, one bone each, so a boss can be prepared for.

### Traders

Two, at the shop markers the map already had:

- **Old Pell's forge** (Earth, the hub): blades and coats for light, each
  once; flasks for 25. His forging of church iron is a separate thing and
  still free.
- **Vael the Bargainer** (Greed, and again in Violence): the bottled
  power-ups and three charms (Imp's Tooth: light x1.5; Cold Coin: more
  heal motes; Basin Shard: +1 heart per healing charge) for bones; flasks
  for light. His memory trade for the clock key is unchanged.

Talking to a trader plays his lines as before. Once the unique ones are
spent, each talk plays a repeat line and then opens the stall, so the
flavour lines are still heard and the wares are two presses away. Space
buys, Esc leaves; the line under the list says what the picked ware does,
or why it could not be bought.

### The pack

The pause menu's **The Pack** page: the purse, what is worn in each slot,
one line per item (equipment marked `*` when worn, consumables counted)
and the kept things of the story (the scarf, the clock pieces, church
iron not yet forged), which are story flags rather than items. The HUD
shows the purse in the top right corner.

---

## 2. In the game now

Everything above. Twelve unique pieces (one per boss profile, the
Gardener included), four bought pieces at Pell's, three charms and three
bottled power-ups at Vael's, the flask at both. The drop tables are the
ones in section 1. The drops are drawn in code (a cluster of light, a
bone, a gem in the item's colour) rather than from sprites.

Not done, and worth a thread each: selling to a trader; a drop sound of
its own; placed tally bones in the secret rooms; the economy's numbers
played through end to end and tuned.

---

## 3. How it is built

- `scripts/items.gd` (`Items`): the item table and the stat keys.
- `scripts/inventory.gd` (`Inventory`): the purse, the items by count and
  what is worn, as static state like `Story`; `bonus(stat)` sums the worn
  pieces' additive stats and `factor(stat)` multiplies the rest.
  `to_dict` / `from_dict` go under `"inventory"` in the save.
- `scripts/shops.gd` (`Shops`): the traders and their stock; `buy` spends
  the purse, fills the pack and flags equipment sold
  (`bought:<shop>:<item>`).
- `scripts/loot.gd` (`Loot`): spawns the drops of a profile's `loot` table
  (`light` range, `bone_chance`, `bones`, `flask_chance`, `unique`) and
  pays a charm that heals on a kill. The walkers, the flyer and the
  bosses call it where they die.
- `scripts/light_drop.gd` (`LightDrop`): light or a bone on the floor,
  which flies to the hero. `scripts/loot_drop.gd` (`LootDrop`): an item on
  the floor. Both call the hero (`collect_currency`, `collect_loot`),
  who emits `purse_changed` and `loot_collected` for the HUD and the
  text box. Drops are in the `loot` group, cleared with the motes; a
  unique is in `unique_loot` and stays.
- `Player` reads the worn pieces in `attack_damage`, `attack_time`,
  `guard` (through `guarded_damage`), `heal_amount`, `run_speed`,
  `dash_cooldown` and `_set_maximums`; `toggle_equipment` and `use_item`
  are what the pack page calls. `refresh_equipment` sets the maximums
  again without filling them.
- `LevelBuilder` reads an NPC placement's `shop` and lays a dead boss's
  untaken unique back down. `main.gd` opens the `ShopMenu` after a
  trader's repeat line, plays a unique's lines through the text box, and
  saves and restores the pack.
- `scenes/shop_menu.tscn` / `scripts/ui/shop_menu.gd`: the stall.
  `scenes/inventory_menu.tscn` / `scripts/ui/inventory_menu.gd`: the pack
  page, under the pause menu.
