using System.Collections.Generic;
using UnityEngine;

namespace Descender.World.TilesV2
{
    /// <summary>
    /// Petals and leaves of gold falling from the crowns of a Tiles V2 world's trees in
    /// blossom: one system of single pixels, fed from whichever crowns are near the view,
    /// so a world of trees costs what a screen of them does.
    /// </summary>
    public sealed class TilesV2Petals : MonoBehaviour
    {
        /// <summary>The crown materials that shed, and what they shed.</summary>
        public const string Blossom = "blossom_wall";
        public const string GoldLeaf = "gold_leaf_wall";

        /// <summary>The trees stood as pictures that shed: what their keys begin with.</summary>
        public const string BlossomTree = "heaven_blossom";
        public const string GoldTree = "heaven_gold";

        // Of a tree's picture, the part that is crown: from this much of its height up, and this much of its width.
        private const float CrownFrom = 0.45f;
        private const float CrownWide = 0.8f;

        // Petals a second from the crowns in view, and how far from the view's middle a crown still sheds.
        private const float Rate = 9f;
        private static readonly Vector2 Near = new Vector2(24f, 15f);
        private static readonly Color Pink = new Color(1f, 0.78f, 0.88f, 1f);
        private static readonly Color Gold = new Color(1f, 0.88f, 0.45f, 1f);

        private readonly List<Vector3> crowns = new List<Vector3>();
        private readonly List<bool> golden = new List<bool>();
        private ParticleSystem system;
        private float owed;

        /// <summary>How many cells of crown shed.</summary>
        public int Crowns => crowns.Count;

        /// <summary>The system the petals are.</summary>
        public ParticleSystem Falling => system;

        /// <summary>Finds the world's crowns and makes the system; null for a world with none.</summary>
        public static TilesV2Petals Build(TilesV2World world, Transform parent)
        {
            var found = new List<Vector3>();
            var gold = new List<bool>();
            if (world.Foliage != null)
            {
                world.Foliage.CompressBounds();
                foreach (var cell in world.Foliage.cellBounds.allPositionsWithin)
                {
                    var tile = world.Foliage.GetTile<TerrainTile>(cell);
                    if (tile == null || (tile.MaterialKey != Blossom && tile.MaterialKey != GoldLeaf))
                    {
                        continue;
                    }
                    found.Add(world.Foliage.GetCellCenterWorld(cell));
                    gold.Add(tile.MaterialKey == GoldLeaf);
                }
            }
            TreesOf(world, found, gold);
            if (found.Count == 0)
            {
                return null;
            }
            // By column, so the ones near a view are a run of the list.
            var order = new int[found.Count];
            for (var i = 0; i < order.Length; i++)
            {
                order[i] = i;
            }
            System.Array.Sort(order, (a, b) => found[a].x.CompareTo(found[b].x));
            var holder = new GameObject("Petals");
            holder.transform.SetParent(parent, false);
            var petals = holder.AddComponent<TilesV2Petals>();
            foreach (var i in order)
            {
                petals.crowns.Add(found[i]);
                petals.golden.Add(gold[i]);
            }
            petals.system = TilesV2Particles.Make(
                holder.transform,
                "Falling",
                Vector3.zero,
                world.Set.litMaterial,
                160
            );
            var main = petals.system.main;
            main.startLifetime = 6f;
            main.startSpeed = 0f;
            var emission = petals.system.emission;
            emission.enabled = false;
            TilesV2Particles.Fade(petals.system, Color.white, Color.white, 0.12f);
            petals.system.Play();
            return petals;
        }

        // The crowns of the trees a map stands as pictures: a place for every cell of each one's crown.
        private static void TreesOf(TilesV2World world, List<Vector3> found, List<bool> gold)
        {
            if (world.Map == null || world.Set == null)
            {
                return;
            }
            Vector2 origin = world.transform.position;
            foreach (var mark in world.Map.Marks)
            {
                var key = mark.Word(0);
                var golden = key.StartsWith(GoldTree);
                if (mark.Kind != TilesV2Things.Stood || (!golden && !key.StartsWith(BlossomTree)))
                {
                    continue;
                }
                var entry = System.Array.Find(world.Set.backdrops, backdrop => backdrop.key == key);
                if (entry == null || entry.sprite == null)
                {
                    continue;
                }
                // The picture stands on the foot of its cell, its floor line at its pivot.
                var cell = world.Map.CellOf(mark.Column, mark.Row);
                var foot = origin + new Vector2(cell.x + 0.5f, cell.y);
                var bounds = entry.sprite.bounds;
                var low = bounds.min.y + bounds.size.y * CrownFrom;
                var half = bounds.size.x * CrownWide / 2f;
                for (var y = low + 0.5f; y < bounds.max.y - 0.5f; y += 1f)
                {
                    for (var x = bounds.center.x - half + 0.5f; x < bounds.center.x + half; x += 1f)
                    {
                        found.Add(new Vector3(foot.x + x, foot.y + y, 0f));
                        gold.Add(golden);
                    }
                }
            }
        }

        /// <summary>Sheds from the crowns near a view for a time; returns how many petals it let go.</summary>
        public int Shed(Vector2 view, float seconds)
        {
            if (system == null || crowns.Count == 0)
            {
                return 0;
            }
            var first = FirstAt(view.x - Near.x);
            var end = FirstAt(view.x + Near.x);
            if (end <= first)
            {
                return 0;
            }
            owed += Rate * seconds;
            var let = 0;
            while (owed >= 1f)
            {
                owed -= 1f;
                var i = Random.Range(first, end);
                if (Mathf.Abs(crowns[i].y - view.y) > Near.y)
                {
                    continue;
                }
                var petal = new ParticleSystem.EmitParams
                {
                    position = crowns[i] + new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(-0.5f, 0.5f), 0f),
                    velocity = new Vector3(Random.Range(-0.9f, -0.2f), Random.Range(-1.1f, -0.5f), 0f),
                    startLifetime = Random.Range(4f, 7f),
                    startColor = golden[i] ? Gold : Pink,
                    startSize = TilesV2Particles.Pixel * (Random.value < 0.3f ? 2f : 1f),
                };
                system.Emit(petal, 1);
                let++;
            }
            return let;
        }

        // The first crown at or past a column.
        private int FirstAt(float x)
        {
            var low = 0;
            var high = crowns.Count;
            while (low < high)
            {
                var middle = (low + high) / 2;
                if (crowns[middle].x < x)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle;
                }
            }
            return low;
        }

        private void LateUpdate()
        {
            var view = Camera.main;
            if (view != null)
            {
                Shed(view.transform.position, Time.deltaTime);
            }
        }
    }
}
