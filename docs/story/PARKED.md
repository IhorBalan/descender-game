# The story is parked

Decided by Ihor on 2026-11-10: the game is built mechanics first (tools,
then movement, combat and enemies), and the story waits. Nothing of the
story was deleted. It is switched off in one place and this file points to
every part, so it can come back as it was.

## What "parked" means in the game

`Descender.Story.StoryMode.Parked` is `true`
(`Assets/Scripts/Runtime/Story/StoryMode.cs`). While it is, a
`WorldPlacement` that is a story element does not spawn
(`WorldPlacement.ShouldSpawn`):

- `trigger`: the invisible places where the story speaks;
- `npc` without a shop: the characters who only talk (traders stay, they
  are a mechanic);
- `item` with dialogue: the relics (an item without lines is loot and
  stays).

Everything else a placement can be (start, save, ability, enemy, upgrade,
power, loot, trader) is untouched. The flags in `StoryFlags` are not the
story: they are the game's progress flags (bosses beaten, shrines rested,
loot taken) and stay in use.

## Bringing it back

1. Set `StoryMode.Parked` to `false` (the default in `StoryMode.cs`).
2. `StoryModeTests.TheStoryIsParkedInTheGame` then fails on purpose:
   delete it, and this file with it.
3. Run the hook. `ParadiseRunTests` already checks the three main-path
   beats (`opening`, `makinghouse`, `gate`) when the story is not parked,
   and the dialogue tests below never stopped running.
4. Put the story items back on the playtest list in
   `docs/delivery/STATE.md` (they are listed at the end of this file).

## Where each part lives

| Part | Where |
| --- | --- |
| The script: the truth of the world, the cast, the beat outline, every line | `docs/story/script.md` |
| The story in Paradise: which beat is in which zone | `docs/design/paradise.md`, section 3 and the zone list in section 4 |
| The lines the game plays (entries by id, with `set`, `unless`, `once`) | `Assets/Data/dialogue/world_1.json` |
| The dialogue model, flags and quests | `Assets/Scripts/Runtime/Story/` (`DialogueData`, `Story`, `Quests`) |
| The runner, the box, the typer | `Assets/Scripts/Runtime/UI/` (`DialogueRunner`, `DialogueBox`, `DialogueTyper`) |
| What the hero talks to | `Assets/Scripts/Runtime/UI/` (`Interactable`, `StoryTrigger`, `Npc`, `Relic`) and their prefabs in the placement catalogs (`Assets/Data/Placements.asset`, `Assets/Data/Placements Paradise.asset`) |
| The beats of the rebuilt Paradise, as layout data | `Assets/Scripts/Editor/Paradise/ParadiseLayout.cs`, the `z.Place(...)` calls of type `trigger`, `npc` and `item`, each with a `z.Mark(...)` beside it |
| The same beats in the scenes | `Assets/Scenes/Paradise/Paradise_Z*.unity`, the `WorldPlacement` objects of those types (11 in all) |
| The prototype world's story | `Assets/Scenes/Region_*.unity`: 14 triggers, 15 NPCs, 22 items, some of them loot |
| The tests that keep it working | EditMode: `DialogueDataTests`, `DialogueFileTests`, `StoryTests`, `QuestsTests`, `RelicTests`, `NpcPlacementTests`, `ParadisePlacementSyncTests`, `ParadiseLayoutTests` (the beats). PlayMode: `DialogueFlowTests`, `StoryTriggerTests`, `RelicTests` (they turn the story on through `DialogueRig`) |

## The beats of the rebuilt Paradise

| Beat | Placement | Type | At (tiles) |
| --- | --- | --- | --- |
| 1. The opening | `opening` | trigger | the start, zone 1 |
| 2. Mereth | `mereth` | npc | 515, -62 (zone 2) |
| 3. The harvest token | `token` | item | 568, -76.2 (zone 2) |
| 5. The ledger page | `ledger` | item | 3093, -58.4 (zone 4) |
| 6. Ithuriel | `ithuriel` | npc | 3156, -58.05 (zone 4) |
| The tally stone | `tally_stone` | item | 4060, -18.1 (zone 5) |
| The prayer card | `prayer_card` | item | 4330, -18.9 (zone 5) |
| The Maker's glove | `glove` | item | 4480, -19.4 (zone 5) |
| 10. The Making-house | `makinghouse` | trigger | 5720, -18.2 (zone 6) |
| 7. The basin shard | `basin_shard` | item | 5920, -50.2 (zone 6) |
| 8. The Voice at the gate | `gate` | trigger | 7470, -34 (zone 7) |

Beat 4 (the feather) and the light dash are abilities, not story, and
stay in the game.

## What was waiting when the story was parked

- The playtest items of T-0002 to T-0005 that are about lines and relics
  (the opening, Mereth, the token, the ledger page, Ithuriel, the three
  zone 5 items, the Making-house and gate lines, the basin shard) were
  never played by Ihor. They come back with the story.
- The roadmap's story work: the Warden's lines and the drop through the
  gate (old M4), the Ruined Earth hub's dialogue, quests, camp and shop
  (old M6), and the October design items (Vael's memory shop, credits
  text).
- One enemy in the prototype world waits for the flag `clock_turned`, which
  only a dialogue sets; it does not appear while the story is parked.
