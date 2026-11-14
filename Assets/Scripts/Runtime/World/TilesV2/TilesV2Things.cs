using System;
using System.Collections.Generic;
using Descender.Combat;
using Descender.Pickups;
using Descender.Save;
using Descender.Triggers;
using UnityEngine;

namespace Descender.World.TilesV2
{
    /// <summary>
    /// Stands the game's own things where a Tiles V2 map marks them (`@mark kind column row
    /// ...`, docs/art/tiles-v2-world.md): a checkpoint and an ability from their prefabs, a
    /// barrier of light only a Light Dash passes, a seal a bolt of Holy Light breaks, a
    /// slab a Smite breaks, a relic to find, and the gate at the world's end. The gates
    /// are made of the game's parts (the LightBarrier layer, a <see cref="Breakable"/>), so
    /// the hero's abilities work on them as they do in the game.
    /// </summary>
    public sealed class TilesV2Things : MonoBehaviour
    {
        public const string Checkpoint = "checkpoint";
        public const string AbilityKind = "ability";
        public const string Barrier = "barrier";
        public const string Seal = "seal";
        public const string Relic = "relic";
        public const string End = "end";
        public const int GateOrder = TilesV2World.TerrainOrder + 3;

        private readonly List<TilesV2Relic> relics = new List<TilesV2Relic>();

        /// <summary>How many relics the map holds, and how many the hero has found.</summary>
        public int Relics => relics.Count;
        public int RelicsFound { get; private set; }
        public int Checkpoints { get; private set; }
        public int Abilities { get; private set; }
        public int Gates { get; private set; }

        /// <summary>The hero found a relic.</summary>
        public event Action RelicFound;

        /// <summary>The hero walked into the gate at the world's end.</summary>
        public event Action Ended;

        /// <summary>
        /// Makes everything the world's map marks, under a new object in the world. The
        /// prefabs are the game's checkpoint and ability pickup; a kind whose prefab is
        /// missing is left out.
        /// </summary>
        public static TilesV2Things Build(TilesV2World world, GameObject checkpointPrefab, GameObject abilityPrefab)
        {
            var things = new GameObject("Things").AddComponent<TilesV2Things>();
            things.transform.SetParent(world.transform, false);
            var map = world.Map;
            var set = world.Set;
            Vector2 origin = world.transform.position;
            foreach (var mark in map.Marks)
            {
                // The middle of the foot of the mark's cell, and the top left corner of it.
                var cell = map.CellOf(mark.Column, mark.Row);
                var foot = origin + new Vector2(cell.x + 0.5f, cell.y);
                var corner = origin + new Vector2(cell.x, cell.y + 1f);
                switch (mark.Kind)
                {
                    case Checkpoint when checkpointPrefab != null:
                    {
                        var made = Instantiate(checkpointPrefab, foot, Quaternion.identity, things.transform);
                        if (made.TryGetComponent<Checkpoint>(out var checkpoint))
                        {
                            checkpoint.Index = things.Checkpoints;
                        }
                        things.Checkpoints++;
                        break;
                    }
                    case AbilityKind when abilityPrefab != null:
                    {
                        if (!Enum.TryParse<Ability>(mark.Word(0), out var ability))
                        {
                            Debug.LogWarning($"Tiles V2: the map marks an ability '{mark.Word(0)}' the game has not.");
                            break;
                        }
                        var made = Instantiate(abilityPrefab, foot, Quaternion.identity, things.transform);
                        made.GetComponent<AbilityPickup>().Ability = ability;
                        things.Abilities++;
                        break;
                    }
                    case Barrier:
                        things.MakeBarrier(set, corner, Mathf.Max(mark.Number(0, 1), 1));
                        break;
                    case Seal:
                        things.MakeSeal(
                            set,
                            corner,
                            mark.Word(0) == "slam",
                            Mathf.Max(mark.Number(1, 1), 1),
                            Mathf.Max(mark.Number(2, 1), 1)
                        );
                        break;
                    case Relic:
                    case End:
                    {
                        var holder = new GameObject(mark.Kind);
                        holder.transform.SetParent(things.transform, false);
                        holder.transform.position = foot;
                        var relic = holder.AddComponent<TilesV2Relic>();
                        var last = mark.Kind == End;
                        relic.Configure(set.Thing("relic_0"), set.Thing("relic_1"), set.unlitMaterial, last);
                        if (last)
                        {
                            relic.Found += () => things.Ended?.Invoke();
                        }
                        else
                        {
                            things.relics.Add(relic);
                            relic.Found += () =>
                            {
                                things.RelicsFound++;
                                things.RelicFound?.Invoke();
                            };
                        }
                        break;
                    }
                }
            }
            return things;
        }

        // A veil of light a tile wide and `tall` tiles high, down from a corner: solid to all but a Light Dash.
        private void MakeBarrier(TilesV2Set set, Vector2 corner, int tall)
        {
            var holder = new GameObject("Light Barrier") { layer = Layers.LightBarrier };
            holder.transform.SetParent(transform, false);
            holder.transform.position = corner;
            holder.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
            var box = holder.AddComponent<BoxCollider2D>();
            box.size = new Vector2(1f, tall);
            box.offset = new Vector2(0.5f, -tall / 2f);
            var shown = Picture(holder, set.Thing("barrier_0"), set.unlitMaterial, new Vector2(1f, tall));
            holder.AddComponent<TilesV2Veil>().Configure(shown, set.Thing("barrier_0"), set.Thing("barrier_1"));
            Gates++;
        }

        // A block `wide` by `tall` tiles down from a corner that one strike of the right kind breaks: a
        // bolt of Holy Light, or (a slab in a floor) a Smite. Solid on the World layer; a child on the
        // Enemy layer, a little larger, is what the hero's strikes find.
        private void MakeSeal(TilesV2Set set, Vector2 corner, bool slab, int wide, int tall)
        {
            var holder = new GameObject(slab ? "Slab" : "Seal") { layer = Layers.World };
            holder.transform.SetParent(transform, false);
            holder.transform.position = corner;
            holder.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
            var size = new Vector2(wide, tall);
            var offset = new Vector2(wide / 2f, -tall / 2f);
            var box = holder.AddComponent<BoxCollider2D>();
            box.size = size;
            box.offset = offset;
            Picture(holder, set.Thing(slab ? "seal_slam" : "seal_bolt"), set.litMaterial, size);
            var struck = new GameObject("Struck") { layer = Layers.Enemy };
            struck.transform.SetParent(holder.transform, false);
            var reach = struck.AddComponent<BoxCollider2D>();
            reach.isTrigger = true;
            reach.size = size + new Vector2(0.5f, 0.75f);
            reach.offset = offset + new Vector2(0f, 0.125f);
            holder.AddComponent<Breakable>().Configure(1, slab ? StruckBy.Slam : StruckBy.Bolt, null);
            Gates++;
        }

        // A sprite drawn tiled over `size` tiles down and right from the holder.
        private static SpriteRenderer Picture(GameObject holder, Sprite sprite, Material material, Vector2 size)
        {
            var child = new GameObject("Picture");
            child.transform.SetParent(holder.transform, false);
            child.transform.localPosition = new Vector3(size.x / 2f, -size.y / 2f, 0f);
            var shown = child.AddComponent<SpriteRenderer>();
            shown.sprite = sprite;
            shown.drawMode = SpriteDrawMode.Tiled;
            shown.tileMode = SpriteTileMode.Continuous;
            shown.size = size;
            shown.sortingOrder = GateOrder;
            if (material != null)
            {
                shown.sharedMaterial = material;
            }
            return shown;
        }
    }
}
