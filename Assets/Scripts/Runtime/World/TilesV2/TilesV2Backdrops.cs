using System.Collections.Generic;
using UnityEngine;

namespace Descender.World.TilesV2
{
    /// <summary>
    /// Makes a Tiles V2 world's backdrop from the tile set's layers (docs/art/tiles-v2.md),
    /// back to front: the sky with its stars, sun and moon, pinned to the view; the
    /// clouds, drifting along the top of the view; the ridges, standing on the map's
    /// surface line and scrolling sideways; and under the surface line the cave rock, which
    /// takes the tile light, scrolls both ways and is cut to the ground by a sprite mask,
    /// with a ragged strip along its top.
    /// </summary>
    public static class TilesV2Backdrops
    {
        // How far past the map's sides and foot the cave rock is shown.
        private const float Margin = 24f;

        // The ridges' feet stand this far over the surface line; the terrain hides them.
        private const float RidgeFoot = 2f;

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
                    case TilesV2Set.BackdropKind.CaveTop:
                    {
                        var layer = MakeLayer(root, entry, material, new Vector2(view.x + size.x * 3f, size.y));
                        Place(
                            layer,
                            new Vector2(entry.scroll, 1f),
                            new Vector2(viewAnchor.x, surface),
                            viewAnchor,
                            size.x,
                            0f
                        );
                        break;
                    }
                    case TilesV2Set.BackdropKind.Cave:
                    {
                        var cover = view + size * 3f;
                        var layer = MakeLayer(root, entry, material, cover);
                        layer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
                        Place(
                            layer,
                            new Vector2(entry.scroll, entry.scroll),
                            new Vector2(viewAnchor.x, surface - cover.y),
                            viewAnchor,
                            size.x,
                            size.y
                        );
                        MakeMask(root, entry.sprite, origin, map, surface);
                        break;
                    }
                }
            }
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

        // A layer drawn tiled over `size` units, with the ParallaxLayer that moves it.
        private static SpriteRenderer MakeLayer(
            Transform parent,
            TilesV2Set.Backdrop entry,
            Material material,
            Vector2 size
        )
        {
            var picture = MakePicture(parent, entry.key, entry.sprite, entry.order, material);
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

        // The cave rock shows only under the surface line: a mask over the ground, a little past the map.
        private static void MakeMask(Transform parent, Sprite sprite, Vector2 origin, TilesV2Map map, float surface)
        {
            var holder = new GameObject("Cave Mask");
            holder.transform.SetParent(parent, false);
            var mask = holder.AddComponent<SpriteMask>();
            mask.sprite = sprite;
            mask.alphaCutoff = 0.1f;
            var width = map.Width + 2f * Margin;
            var height = surface - origin.y + Margin;
            var size = (Vector2)sprite.bounds.size;
            // The sprite's pivot is its bottom middle.
            holder.transform.position = new Vector3(origin.x + map.Width / 2f, origin.y - Margin, 0f);
            holder.transform.localScale = new Vector3(width / size.x, height / size.y, 1f);
        }
    }
}
