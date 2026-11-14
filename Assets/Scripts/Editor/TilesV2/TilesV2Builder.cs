using System.Collections.Generic;
using Descender.World.TilesV2;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Descender.EditorTools.TilesV2
{
    /// <summary>
    /// Descender > Tiles V2 > Build tiles: makes the Tiles V2 Rule Tiles from the
    /// generator's manifest and sheets (docs/art/tiles-v2.md). Each material gets one
    /// <see cref="TerrainTile"/> with a rule per autotile frame (its variants as the rule's
    /// random sprites), each slope its own tile, and each blend one overlay tile per
    /// side mask, then refills the Tile Palette (<see cref="TilesV2Palette"/>). Idempotent: an existing asset is loaded and its rules replaced, so its
    /// GUID and every reference to it stay.
    /// </summary>
    public static class TilesV2Builder
    {
        [MenuItem("Descender/Tiles V2/Build tiles")]
        public static void BuildMenu()
        {
            var count = Build();
            Debug.Log($"Tiles V2: built {count} tiles under {TilesV2Manifest.TileFolder}.");
        }

        /// <summary>Builds every tile the manifest names; returns how many.</summary>
        public static int Build()
        {
            var manifest = TilesV2Manifest.Load();
            EnsureFolder(TilesV2Manifest.TileFolder);
            EnsureFolder(TilesV2Manifest.TileFolder + "/Blends");
            var count = 0;
            foreach (var material in manifest.materials)
            {
                var sprites = SpritesOf(TilesV2Manifest.Folder + "/" + material.sheet);
                var collider = material.IsWall ? Tile.ColliderType.None : Tile.ColliderType.Grid;
                var tile = LoadOrCreate(TilesV2Manifest.TilePath(material.key));
                tile.Configure(material.key, material.group);
                tile.m_DefaultSprite = Find(sprites, TilesV2Manifest.FrameSprite(material.key, 0, 0));
                tile.m_DefaultColliderType = collider;
                var rules = new List<RuleTile.TilingRule>();
                foreach (var frame in manifest.frames)
                {
                    var variants = new Sprite[manifest.variants];
                    for (var v = 0; v < variants.Length; v++)
                    {
                        variants[v] = Find(sprites, TilesV2Manifest.FrameSprite(material.key, frame, v));
                    }
                    rules.Add(FrameRule(rules.Count, frame, variants, collider));
                }
                Finish(tile, rules);
                count++;
                if (!material.slopes)
                {
                    continue;
                }
                foreach (var slope in manifest.slopes)
                {
                    var slopeTile = LoadOrCreate(TilesV2Manifest.SlopePath(material.key, slope));
                    slopeTile.Configure(material.key + "_" + slope, material.group);
                    var variants = new Sprite[manifest.variants];
                    for (var v = 0; v < variants.Length; v++)
                    {
                        variants[v] = Find(sprites, TilesV2Manifest.SlopeSprite(material.key, slope, v));
                    }
                    // The sprite's outline is its collider: a slope the hero can walk.
                    slopeTile.m_DefaultSprite = variants[0];
                    slopeTile.m_DefaultColliderType = Tile.ColliderType.Sprite;
                    Finish(slopeTile, new List<RuleTile.TilingRule> { AnyRule(variants, Tile.ColliderType.Sprite) });
                    count++;
                }
            }
            foreach (var blend in manifest.blends)
            {
                var sprites = SpritesOf(TilesV2Manifest.Folder + "/" + blend.sheet);
                foreach (var sides in blend.masks)
                {
                    var tile = LoadOrCreate(TilesV2Manifest.BlendPath(blend.key, sides));
                    tile.Configure(blend.key + "_s" + sides, "");
                    var variants = new Sprite[manifest.variants];
                    for (var v = 0; v < variants.Length; v++)
                    {
                        variants[v] = Find(sprites, TilesV2Manifest.BlendSprite(blend.key, sides, v));
                    }
                    tile.m_DefaultSprite = variants[0];
                    tile.m_DefaultColliderType = Tile.ColliderType.None;
                    Finish(tile, new List<RuleTile.TilingRule> { AnyRule(variants, Tile.ColliderType.None) });
                    count++;
                }
            }
            AssetDatabase.SaveAssets();
            TilesV2Palette.Build(manifest);
            return count;
        }

        /// <summary>
        /// The rule for one frame: every edge says solid (this) or air (not this), and a
        /// corner between two solid edges says which it is; other corners do not matter.
        /// </summary>
        public static RuleTile.TilingRule FrameRule(int id, int frame, Sprite[] variants, Tile.ColliderType collider)
        {
            var rule = AnyRule(variants, collider);
            rule.m_Id = id;
            foreach (var (bit, offset) in TilesV2Masks.Neighbours)
            {
                if (!TilesV2Masks.Matters(frame, bit))
                {
                    continue;
                }
                rule.m_NeighborPositions.Add(offset);
                rule.m_Neighbors.Add(
                    (frame & bit) != 0
                        ? RuleTile.TilingRuleOutput.Neighbor.This
                        : RuleTile.TilingRuleOutput.Neighbor.NotThis
                );
            }
            return rule;
        }

        private static RuleTile.TilingRule AnyRule(Sprite[] variants, Tile.ColliderType collider)
        {
            return new RuleTile.TilingRule
            {
                m_Sprites = variants,
                m_Output = RuleTile.TilingRuleOutput.OutputSprite.Random,
                m_RandomTransform = RuleTile.TilingRuleOutput.Transform.Fixed,
                m_RuleTransform = RuleTile.TilingRuleOutput.Transform.Fixed,
                m_ColliderType = collider,
                m_Neighbors = new List<int>(),
                m_NeighborPositions = new List<Vector3Int>(),
            };
        }

        private static void Finish(TerrainTile tile, List<RuleTile.TilingRule> rules)
        {
            tile.m_TilingRules = rules;
            tile.UpdateNeighborPositions();
            EditorUtility.SetDirty(tile);
        }

        private static TerrainTile LoadOrCreate(string path)
        {
            var tile = AssetDatabase.LoadAssetAtPath<TerrainTile>(path);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<TerrainTile>();
                AssetDatabase.CreateAsset(tile, path);
            }
            return tile;
        }

        private static Dictionary<string, Sprite> SpritesOf(string sheetPath)
        {
            var sprites = new Dictionary<string, Sprite>();
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(sheetPath))
            {
                if (asset is Sprite sprite)
                {
                    sprites[sprite.name] = sprite;
                }
            }
            if (sprites.Count == 0)
            {
                throw new System.InvalidOperationException(
                    $"Tiles V2: no sprites in {sheetPath}; run the generator first."
                );
            }
            return sprites;
        }

        private static Sprite Find(Dictionary<string, Sprite> sprites, string name)
        {
            if (!sprites.TryGetValue(name, out var sprite))
            {
                throw new KeyNotFoundException($"Tiles V2: the sheet has no sprite {name}.");
            }
            return sprite;
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }
            var slash = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
