using System.Collections.Generic;
using UnityEngine;

namespace Descender.World.TilesV2
{
    /// <summary>
    /// Makes a Tiles V2 world's backdrop from the tile set's layers (docs/art/tiles-v2.md),
    /// back to front: the sky with its stars, sun and moon, pinned to the view; the
    /// clouds, drifting along the top of the view; the ridges, standing on the map's
    /// surface line and scrolling sideways; and under the surface line the rock behind the
    /// caves, each region's own, which takes the tile light, scrolls both ways and is cut
    /// to its region's rows by a sprite mask, with a ragged strip along its top.
    /// </summary>
    public static class TilesV2Backdrops
    {
        // How far past the map's sides and foot the cave rock is shown.
        private const float Margin = 24f;

        // The ridges' feet stand this far over the surface line; the terrain hides them.
        private const float RidgeFoot = 2f;

        /// <summary>The region a map that names none is all made of.</summary>
        public const string EarthKey = "earth";

        /// <summary>Builds the backdrop under `parent`; returns the sky.</summary>
        public static Sky Build(TilesV2World world, Transform parent)
        {
            var set = world.Set;
            var map = world.Map;
            var view = set.View;
            var root = new GameObject("Backdrop").transform;
            root.SetParent(parent, false);
            Vector2 origin = world.transform.position;
            var surface = origin.y + map.Height - map.Surface;
            var viewAnchor = new Vector2(origin.x + map.Width / 2f, surface);

            var skyHolder = new GameObject("Sky");
            skyHolder.transform.SetParent(root, false);
            skyHolder.transform.position = viewAnchor;
            var gradient = MakePicture(skyHolder.transform, "Gradient", null, set.skyOrder, set.unlitMaterial);
            SpriteRenderer stars = null;
            SpriteRenderer sun = null;
            SpriteRenderer moon = null;
            var tinted = new List<SpriteRenderer>();
            foreach (var entry in set.backdrops)
            {
                if (entry.sprite == null)
                {
                    continue;
                }
                var material = entry.lit ? set.litMaterial : set.unlitMaterial;
                var size = (Vector2)entry.sprite.bounds.size;
                switch (entry.kind)
                {
                    case TilesV2Set.BackdropKind.Stars:
                        stars = MakePicture(skyHolder.transform, entry.key, entry.sprite, entry.order, material);
                        // The sprite's pivot is its bottom middle.
                        stars.transform.localPosition = new Vector3(0f, -size.y / 2f, 0f);
                        break;
                    case TilesV2Set.BackdropKind.Sun:
                        sun = MakePicture(skyHolder.transform, entry.key, entry.sprite, entry.order, material);
                        break;
                    case TilesV2Set.BackdropKind.Moon:
                        moon = MakePicture(skyHolder.transform, entry.key, entry.sprite, entry.order, material);
                        break;
                    case TilesV2Set.BackdropKind.Clouds:
                    {
                        // Along the top of the view, wherever the view is.
                        var layer = MakeLayer(root, entry, material, new Vector2(size.x * 3f, size.y));
                        var foot = viewAnchor.y + view.y / 2f - size.y - 0.5f;
                        layer
                            .GetComponent<ParallaxLayer>()
                            .Configure(
                                new Vector2(entry.scroll, 0f),
                                new Vector2(viewAnchor.x, foot),
                                viewAnchor,
                                new Vector2(size.x, 0f),
                                set.cloudDrift
                            );
                        tinted.Add(layer);
                        break;
                    }
                    case TilesV2Set.BackdropKind.Ridge:
                    {
                        // Standing on the world's horizon: it scrolls sideways only.
                        var layer = MakeLayer(root, entry, material, new Vector2(size.x * 3f, size.y));
                        Place(
                            layer,
                            new Vector2(entry.scroll, 1f),
                            new Vector2(viewAnchor.x, surface + RidgeFoot),
                            viewAnchor,
                            size.x,
                            0f
                        );
                        tinted.Add(layer);
                        break;
                    }
                }
            }
            Caves(world, root, viewAnchor);
            var sky = skyHolder.AddComponent<Sky>();
            sky.Configure(gradient, stars, sun, moon, tinted.ToArray(), set.skyBands, set.ViewPixels);
            return sky;
        }

        private static SpriteRenderer MakePicture(
            Transform parent,
            string name,
            Sprite sprite,
            int order,
            Material material
        )
        {
            var holder = new GameObject(name);
            holder.transform.SetParent(parent, false);
            var picture = holder.AddComponent<SpriteRenderer>();
            picture.sprite = sprite;
            picture.sortingOrder = order;
            if (material != null)
            {
                picture.sharedMaterial = material;
            }
            return picture;
        }

        // The rock behind the caves, a region at a time: each region's own picture, cut to the
        // region's rows by a mask that works on that picture alone, with a ragged strip along
        // its top, so one region's rock runs into the next one's. A map that names no regions
        // is all the Earth's.
        private static void Caves(TilesV2World world, Transform root, Vector2 viewAnchor)
        {
            var set = world.Set;
            var map = world.Map;
            var view = set.View;
            Vector2 origin = world.transform.position;
            var count = Mathf.Max(map.Regions.Count, 1);
            for (var i = 0; i < count; i++)
            {
                var region = set.RegionOf(map.Regions.Count > 0 ? map.Regions[i].Key : EarthKey);
                if (region == null || region.cave == null)
                {
                    continue;
                }
                var first = Mathf.Max(map.Regions.Count > 0 ? map.Regions[i].Top : 0, map.Surface);
                var end = map.Regions.Count > 0 ? map.RegionEnd(i) : map.Height;
                if (first >= end)
                {
                    continue;
                }
                var order = set.caveOrder + 3 * i;
                var top = origin.y + map.Height - first;
                var foot = origin.y + map.Height - end - (i == count - 1 ? Margin : 0f);
                var size = (Vector2)region.cave.bounds.size;
                var cover = view + size * 3f;
                var material = set.litMaterial;
                var rock = MakeLayer(root, "cave " + region.key, region.cave, order, material, cover);
                rock.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                Place(
                    rock,
                    new Vector2(set.caveScroll, set.caveScroll),
                    new Vector2(viewAnchor.x, viewAnchor.y - cover.y),
                    viewAnchor,
                    size.x,
                    size.y
                );
                var holder = new GameObject("Cave Mask " + region.key);
                holder.transform.SetParent(root, false);
                var mask = holder.AddComponent<SpriteMask>();
                mask.sprite = region.cave;
                mask.alphaCutoff = 0.1f;
                mask.isCustomRangeActive = true;
                mask.backSortingOrder = order - 1;
                mask.frontSortingOrder = order;
                // The sprite's pivot is its bottom middle.
                holder.transform.position = new Vector3(origin.x + map.Width / 2f, foot, 0f);
                holder.transform.localScale = new Vector3(
                    (map.Width + 2f * Margin) / size.x,
                    (top - foot) / size.y,
                    1f
                );
                if (region.caveTop == null)
                {
                    continue;
                }
                var strip = (Vector2)region.caveTop.bounds.size;
                var ragged = MakeLayer(
                    root,
                    "cave top " + region.key,
                    region.caveTop,
                    order + 1,
                    material,
                    new Vector2(view.x + strip.x * 3f, strip.y)
                );
                Place(ragged, new Vector2(set.caveScroll, 1f), new Vector2(viewAnchor.x, top), viewAnchor, strip.x, 0f);
            }
        }

        // A layer drawn tiled over `size` units, with the ParallaxLayer that moves it.
        private static SpriteRenderer MakeLayer(
            Transform parent,
            TilesV2Set.Backdrop entry,
            Material material,
            Vector2 size
        ) => MakeLayer(parent, entry.key, entry.sprite, entry.order, material, size);

        private static SpriteRenderer MakeLayer(
            Transform parent,
            string name,
            Sprite sprite,
            int order,
            Material material,
            Vector2 size
        )
        {
            var picture = MakePicture(parent, name, sprite, order, material);
            picture.drawMode = SpriteDrawMode.Tiled;
            picture.tileMode = SpriteTileMode.Continuous;
            picture.size = size;
            picture.gameObject.AddComponent<ParallaxLayer>();
            return picture;
        }

        private static void Place(
            SpriteRenderer layer,
            Vector2 scroll,
            Vector2 anchor,
            Vector2 viewAnchor,
            float repeatAcross,
            float repeatUp
        )
        {
            layer
                .GetComponent<ParallaxLayer>()
                .Configure(scroll, anchor, viewAnchor, new Vector2(repeatAcross, repeatUp), 0f);
        }
    }
}
