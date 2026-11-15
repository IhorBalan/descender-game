using System.Collections.Generic;
using Descender.World.TilesV2;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Descender.EditorTools.TilesV2
{
    /// <summary>
    /// Descender > Tiles V2 > Build tiles: makes the Tiles V2 tiles and the tile set from
    /// the generator's manifest and sheets (docs/art/tiles-v2.md). Each material gets one
    /// <see cref="TerrainTile"/> with a rule per autotile frame (its variants as the rule's
    /// random sprites), each slope its own tile, each blend one overlay tile per side mask;
    /// then the decor and shade tiles, the liquids, the glint, and
    /// <see cref="TilesV2Set"/>, the one asset a world is built from at run time; last, the
    /// Tile Palette (<see cref="TilesV2Palette"/>). Idempotent: an existing asset is loaded
    /// and filled again, so its GUID and every reference to it stay.
    /// </summary>
    public static class TilesV2Builder
    {
        private const string LitMaterialPath =
            "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat";
        private const string UnlitMaterialPath =
            "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";

        [MenuItem("Descender/Tiles V2/Build tiles")]
        public static void BuildMenu()
        {
            var count = Build();
            Debug.Log(
                $"Tiles V2: built {count} tiles under {TilesV2Manifest.TileFolder} and {TilesV2Manifest.SetPath}."
            );
        }

        /// <summary>Builds every tile the manifest names and the tile set; returns how many tiles.</summary>
        public static int Build()
        {
            var manifest = TilesV2Manifest.Load();
            EnsureFolder(TilesV2Manifest.TileFolder);
            EnsureFolder(TilesV2Manifest.TileFolder + "/Blends");
            EnsureFolder(TilesV2Manifest.TileFolder + "/Decor");
            EnsureFolder(TilesV2Manifest.TileFolder + "/Shade");
            EnsureFolder(TilesV2Manifest.TileFolder + "/Ledges");
            var count = BuildTerrain(manifest) + BuildBlends(manifest);
            count += BuildDecor(manifest) + BuildShade(manifest) + BuildLiquids(manifest) + BuildGlint(manifest);
            count += BuildLedges(manifest);
            BuildSet(manifest);
            AssetDatabase.SaveAssets();
            TilesV2Palette.Build(manifest);
            return count;
        }

        /// <summary>The tile set asset, as last built; null before the first build.</summary>
        public static TilesV2Set LoadSet() => AssetDatabase.LoadAssetAtPath<TilesV2Set>(TilesV2Manifest.SetPath);

        private static int BuildTerrain(TilesV2Manifest manifest)
        {
            var count = 0;
            foreach (var material in manifest.materials)
            {
                var sprites = SpritesOf(TilesV2Manifest.SheetPath(material.sheet));
                var collider = material.IsWall ? Tile.ColliderType.None : Tile.ColliderType.Grid;
                var tile = LoadOrCreate<TerrainTile>(TilesV2Manifest.TilePath(material.key));
                tile.Configure(
                    material.key,
                    material.group,
                    material.key,
                    false,
                    TilesV2Manifest.ColourOf(material.emits)
                );
                tile.Open = material.open;
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
                foreach (var slope in manifest.SlopesOf(material))
                {
                    var slopeTile = LoadOrCreate<TerrainTile>(TilesV2Manifest.SlopePath(material.key, slope));
                    slopeTile.Configure(material.key + "_" + slope, material.group, material.key, true, Color.black);
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
            return count;
        }

        private static int BuildBlends(TilesV2Manifest manifest)
        {
            var count = 0;
            foreach (var blend in manifest.blends)
            {
                var sprites = SpritesOf(TilesV2Manifest.SheetPath(blend.sheet));
                foreach (var sides in blend.masks)
                {
                    var tile = LoadOrCreate<TerrainTile>(TilesV2Manifest.BlendPath(blend.key, sides));
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
            return count;
        }

        private static int BuildDecor(TilesV2Manifest manifest)
        {
            var sprites = SpritesOf(TilesV2Manifest.SheetPath(manifest.decor.sheet));
            var made = new HashSet<string>();
            foreach (var rule in manifest.decor.rules)
            {
                foreach (var part in new[] { rule.first, rule.middle, rule.last })
                {
                    for (var v = 0; v < rule.variants; v++)
                    {
                        var path = TilesV2Manifest.DecorPath(part, v);
                        if (made.Add(path))
                        {
                            Plain(path, Find(sprites, TilesV2Manifest.DecorSprite(part, v)));
                        }
                    }
                }
            }
            return made.Count;
        }

        private static int BuildShade(TilesV2Manifest manifest)
        {
            var sprites = SpritesOf(TilesV2Manifest.SheetPath(manifest.shade.sheet));
            Plain(TilesV2Manifest.ShadeFullPath, Find(sprites, TilesV2Manifest.ShadeFullSprite));
            foreach (var mask in manifest.shade.masks)
            {
                Plain(TilesV2Manifest.ShadePath(mask), Find(sprites, TilesV2Manifest.ShadeSprite(mask)));
            }
            return manifest.shade.masks.Length + 1;
        }

        private static int BuildLiquids(TilesV2Manifest manifest)
        {
            foreach (var liquid in manifest.liquids)
            {
                var sprites = SpritesOf(TilesV2Manifest.SheetPath(liquid.sheet));
                var surface = new Sprite[liquid.frames];
                var body = new Sprite[liquid.frames];
                for (var f = 0; f < liquid.frames; f++)
                {
                    surface[f] = Find(sprites, TilesV2Manifest.LiquidSprite(liquid.key, "surface", f));
                    body[f] = Find(sprites, TilesV2Manifest.LiquidSprite(liquid.key, "body", f));
                }
                var tile = LoadOrCreate<LiquidTile>(TilesV2Manifest.TilePath(liquid.key));
                tile.Configure(
                    liquid.key,
                    surface,
                    body,
                    liquid.fps,
                    TilesV2Manifest.ColourOf(liquid.keeps),
                    TilesV2Manifest.ColourOf(liquid.emits)
                );
                EditorUtility.SetDirty(tile);
            }
            return manifest.liquids.Length;
        }

        private static int BuildGlint(TilesV2Manifest manifest)
        {
            var sprites = SpritesOf(TilesV2Manifest.SheetPath(manifest.glint.sheet));
            var frames = new Sprite[manifest.variants * GlintTile.FramesPerVariant];
            for (var v = 0; v < manifest.variants; v++)
            {
                for (var f = 0; f < GlintTile.FramesPerVariant; f++)
                {
                    frames[v * GlintTile.FramesPerVariant + f] = Find(sprites, TilesV2Manifest.GlintSprite(v, f));
                }
            }
            var tile = LoadOrCreate<GlintTile>(TilesV2Manifest.GlintPath);
            tile.Configure(
                Find(sprites, TilesV2Manifest.GlintNoneSprite),
                frames,
                (int[])manifest.glint.loop.Clone(),
                manifest.glint.fps
            );
            EditorUtility.SetDirty(tile);
            return 1;
        }

        // A ledge piece is one sprite whose collider is the sprite's own shape: the strip at its top.
        private static int BuildLedges(TilesV2Manifest manifest)
        {
            var sprites = SpritesOf(TilesV2Manifest.SheetPath(manifest.ledges.sheet));
            var count = 0;
            foreach (var key in manifest.ledges.keys)
            {
                foreach (var piece in manifest.ledges.pieces)
                {
                    var tile = LoadOrCreate<Tile>(TilesV2Manifest.LedgePath(key, piece));
                    tile.sprite = Find(sprites, TilesV2Manifest.LedgeSprite(key, piece));
                    tile.color = Color.white;
                    tile.colliderType = Tile.ColliderType.Sprite;
                    EditorUtility.SetDirty(tile);
                    count++;
                }
            }
            return count;
        }

        // Fills the tile set from the manifest and the tiles just built.
        private static void BuildSet(TilesV2Manifest manifest)
        {
            var set = LoadOrCreate<TilesV2Set>(TilesV2Manifest.SetPath);
            set.litMaterial = AssetDatabase.LoadAssetAtPath<Material>(LitMaterialPath);
            set.unlitMaterial = AssetDatabase.LoadAssetAtPath<Material>(UnlitMaterialPath);

            var legend = new List<TilesV2Set.Glyph>();
            foreach (var entry in manifest.legend)
            {
                var layer = Parse<TilesV2Set.Layer>(entry.layer);
                var tiled =
                    layer == TilesV2Set.Layer.Terrain
                    || layer == TilesV2Set.Layer.Wall
                    || layer == TilesV2Set.Layer.Liquid;
                var path = string.IsNullOrEmpty(entry.slope)
                    ? TilesV2Manifest.TilePath(entry.key)
                    : TilesV2Manifest.SlopePath(entry.key, entry.slope);
                legend.Add(
                    new TilesV2Set.Glyph
                    {
                        character = entry.character,
                        layer = layer,
                        key = entry.key,
                        tile = tiled ? Load<TileBase>(path) : null,
                    }
                );
            }
            set.legend = legend.ToArray();

            var blends = new List<TilesV2Set.Blend>();
            foreach (var entry in manifest.blends)
            {
                var blend = new TilesV2Set.Blend
                {
                    key = entry.key,
                    onto = entry.onto,
                    from = (string[])entry.from.Clone(),
                };
                foreach (var sides in entry.masks)
                {
                    blend.overlays[sides] = Load<TileBase>(TilesV2Manifest.BlendPath(entry.key, sides));
                }
                blends.Add(blend);
            }
            set.blends = blends.ToArray();

            set.shadeFull = Load<TileBase>(TilesV2Manifest.ShadeFullPath);
            var shades = new List<TilesV2Set.Shade>();
            foreach (var mask in manifest.shade.masks)
            {
                shades.Add(
                    new TilesV2Set.Shade { mask = mask, tile = Load<TileBase>(TilesV2Manifest.ShadePath(mask)) }
                );
            }
            set.shades = shades.ToArray();

            var decor = new List<TilesV2Set.DecorRule>();
            foreach (var rule in manifest.decor.rules)
            {
                decor.Add(
                    new TilesV2Set.DecorRule
                    {
                        key = rule.key,
                        anchor = Parse<TilesV2Set.Anchor>(rule.anchor),
                        on = (string[])rule.on.Clone(),
                        where = Parse<TilesV2Set.Where>(rule.where),
                        chance = rule.chance,
                        min = rule.min,
                        max = rule.max,
                        first = DecorTiles(rule.first, rule.variants),
                        middle = DecorTiles(rule.middle, rule.variants),
                        last = DecorTiles(rule.last, rule.variants),
                    }
                );
            }
            set.decor = decor.ToArray();
            set.floorSalt = manifest.decor.floorSalt;
            set.ceilingSalt = manifest.decor.ceilingSalt;
            set.glint = Load<GlintTile>(TilesV2Manifest.GlintPath);
            set.glintOn = (string[])manifest.glint.on.Clone();

            set.torchFrames = new Sprite[0];
            var props = new List<TilesV2Set.Prop>();
            foreach (var prop in manifest.props)
            {
                var sprites = SpritesOf(TilesV2Manifest.SheetPath(prop.sheet));
                var frames = new Sprite[prop.frames];
                for (var f = 0; f < prop.frames; f++)
                {
                    frames[f] = Find(sprites, TilesV2Manifest.PropSprite(prop.key, f));
                }
                props.Add(
                    new TilesV2Set.Prop
                    {
                        key = prop.key,
                        frames = frames,
                        colour = TilesV2Manifest.ColourOf(prop.colour),
                        intensity = prop.intensity,
                        embers = prop.embers,
                    }
                );
                if (prop.key == TilesV2World.TorchKey)
                {
                    set.torchFrames = frames;
                }
            }
            set.props = props.ToArray();
            var ledges = new List<TilesV2Set.Ledge>();
            foreach (var key in manifest.ledges.keys)
            {
                ledges.Add(
                    new TilesV2Set.Ledge
                    {
                        key = key,
                        single = Load<TileBase>(TilesV2Manifest.LedgePath(key, "single")),
                        left = Load<TileBase>(TilesV2Manifest.LedgePath(key, "left")),
                        middle = Load<TileBase>(TilesV2Manifest.LedgePath(key, "middle")),
                        right = Load<TileBase>(TilesV2Manifest.LedgePath(key, "right")),
                    }
                );
            }
            set.ledges = ledges.ToArray();
            var thingSprites = SpritesOf(TilesV2Manifest.SheetPath(manifest.things.sheet));
            var things = new List<Sprite>();
            foreach (var name in manifest.things.names)
            {
                things.Add(Find(thingSprites, TilesV2Manifest.ThingSprite(name)));
            }
            set.things = things.ToArray();
            var regions = new List<TilesV2Set.Region>();
            foreach (var entry in manifest.regions)
            {
                var air = entry.air;
                regions.Add(
                    new TilesV2Set.Region
                    {
                        key = entry.key,
                        name = entry.name,
                        subtitle = entry.subtitle,
                        ambient = TilesV2Manifest.ColourOf(entry.ambient),
                        cave = string.IsNullOrEmpty(entry.cave)
                            ? null
                            : Load<Sprite>(TilesV2Manifest.SheetPath(entry.cave + ".png")),
                        caveTop = string.IsNullOrEmpty(entry.caveTop)
                            ? null
                            : Load<Sprite>(TilesV2Manifest.SheetPath(entry.caveTop + ".png")),
                        far = string.IsNullOrEmpty(entry.far)
                            ? null
                            : Load<Sprite>(TilesV2Manifest.SheetPath(entry.far + ".png")),
                        airRate = air.rate,
                        airColour =
                            air.colour.Length >= 4
                                ? new Color(air.colour[0], air.colour[1], air.colour[2], air.colour[3])
                                : Color.white,
                        airAcross = air.across.Length >= 2 ? new Vector2(air.across[0], air.across[1]) : Vector2.zero,
                        airUp = air.up.Length >= 2 ? new Vector2(air.up[0], air.up[1]) : Vector2.zero,
                        airLife = air.life,
                        airLit = air.lit,
                    }
                );
            }
            set.regions = regions.ToArray();
            set.caveScroll = manifest.sky.caveScroll;
            set.caveOrder = manifest.sky.caveOrder;
            set.farScroll = manifest.sky.farScroll;

            var liquids = new List<TilesV2Set.Liquid>();
            foreach (var entry in manifest.liquids)
            {
                liquids.Add(
                    new TilesV2Set.Liquid
                    {
                        key = entry.key,
                        tile = Load<LiquidTile>(TilesV2Manifest.TilePath(entry.key)),
                        lit = entry.lit,
                        swim = entry.swim,
                        hazard = entry.hazard,
                        order = entry.order,
                    }
                );
            }
            set.liquids = liquids.ToArray();

            var light = manifest.light;
            set.ambient = light.ambient;
            set.ambientColour = TilesV2Manifest.ColourOf(light.ambientColour);
            set.airKeeps = light.airKeeps;
            set.solidKeeps = light.solidKeeps;
            set.torchColour = TilesV2Manifest.ColourOf(light.torchColour);
            set.torchIntensity = light.torchIntensity;
            set.torchGlow = light.torchGlow;
            set.torchGlowRadius = light.torchGlowRadius;
            set.wallTint = TilesV2Manifest.ColourOf(light.wallTint);

            set.dayLength = manifest.day.length;
            set.dayStart = manifest.day.start;
            set.sunrise = manifest.day.sunrise;
            set.sunset = manifest.day.sunset;
            var keys = new List<TilesV2Set.DayKey>();
            foreach (var key in manifest.day.keys)
            {
                keys.Add(
                    new TilesV2Set.DayKey
                    {
                        hour = key.hour,
                        light = TilesV2Manifest.ColourOf(key.light),
                        top = TilesV2Manifest.ColourOf(key.top),
                        low = TilesV2Manifest.ColourOf(key.low),
                        tint = TilesV2Manifest.ColourOf(key.tint),
                        stars = key.stars,
                    }
                );
            }
            set.day = keys.ToArray();

            set.viewWidth = manifest.view.width;
            set.viewHeight = manifest.view.height;
            set.skyBands = manifest.sky.bands;
            set.skyOrder = manifest.sky.order;
            set.cloudDrift = manifest.sky.cloudDrift;
            var backdrops = new List<TilesV2Set.Backdrop>();
            foreach (var entry in manifest.backdrops)
            {
                backdrops.Add(
                    new TilesV2Set.Backdrop
                    {
                        key = entry.key,
                        kind = Parse<TilesV2Set.BackdropKind>(entry.kind),
                        sprite = Load<Sprite>(TilesV2Manifest.SheetPath(entry.key + ".png")),
                        scroll = entry.scroll,
                        order = entry.order,
                        lit = entry.lit,
                        scrollUp = entry.scrollUp,
                        rise = entry.rise,
                        drift = entry.drift,
                        tinted = entry.tinted,
                    }
                );
            }
            set.backdrops = backdrops.ToArray();
            EditorUtility.SetDirty(set);
        }

        private static TileBase[] DecorTiles(string part, int variants)
        {
            var tiles = new TileBase[variants];
            for (var v = 0; v < variants; v++)
            {
                tiles[v] = Load<TileBase>(TilesV2Manifest.DecorPath(part, v));
            }
            return tiles;
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

        // A tile that is one sprite and nothing else: decor, shade.
        private static void Plain(string path, Sprite sprite)
        {
            var tile = LoadOrCreate<Tile>(path);
            tile.sprite = sprite;
            tile.color = Color.white;
            tile.colliderType = Tile.ColliderType.None;
            EditorUtility.SetDirty(tile);
        }

        private static T LoadOrCreate<T>(string path)
            where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }
            return asset;
        }

        private static T Load<T>(string path)
            where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                throw new System.IO.FileNotFoundException("Tiles V2: run the generator, then build the tiles.", path);
            }
            return asset;
        }

        // A manifest word as an enum member: `cave_top` is CaveTop.
        private static T Parse<T>(string word)
            where T : struct
        {
            if (System.Enum.TryParse<T>((word ?? "").Replace("_", ""), true, out var value))
            {
                return value;
            }
            throw new System.FormatException($"Tiles V2: the manifest says '{word}', which is no {typeof(T).Name}.");
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
