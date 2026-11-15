using System;
using System.Collections.Generic;
using Descender.CameraRig;
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
    /// slab a Smite breaks, a wall that is not one (the sword breaks it, and a room is
    /// behind it), a platform that gives way or comes and goes, a wind, a relic to find,
    /// and the gate at the world's end. They are made of the game's parts (the LightBarrier
    /// layer, a <see cref="Breakable"/>, a <see cref="Crumble"/>, a <see cref="Blinker"/>, a
    /// <see cref="ForceZone"/>), so the hero meets them as he does in the game.
    /// </summary>
    public sealed class TilesV2Things : MonoBehaviour
    {
        public const string Checkpoint = "checkpoint";
        public const string AbilityKind = "ability";
        public const string Barrier = "barrier";
        public const string Seal = "seal";
        public const string Relic = "relic";
        public const string End = "end";
        public const string Secret = "secret";
        public const string Platform = "platform";
        public const string Wind = "wind";
        public const string Upgrade = "upgrade";
        public const string Sign = "sign";

        /// <summary>A light of Paradise: `@mark beacon column row name`. The hero lights it by walking up to it.</summary>
        public const string Beacon = "beacon";

        /// <summary>The door the lights open: `@mark sanctum column row height lights`.</summary>
        public const string Sanctum = "sanctum";

        /// <summary>
        /// One of the tile set's pictures, its foot in a cell, moved from the middle of that
        /// foot by so many pixels right and up: `@mark picture column row key [right up]`.
        /// </summary>
        public const string Stood = "picture";

        /// <summary>What a picture's companions in the tile set are named after it: its windows' light; its further frames.</summary>
        public const string GlowSuffix = "_glow";
        public const string FrameSuffix = "_f";

        /// <summary>
        /// A part of a level with a name of its own, said when the hero comes into it:
        /// `@mark zone column row width height number name ...`.
        /// </summary>
        public const string Zone = "zone";

        /// <summary>
        /// A stretch where the view draws back (the game's camera zone): `@mark vista column
        /// row width height zoom holdX holdY lookRight lookUp`, the zoom in hundredths, the
        /// looks in tiles.
        /// </summary>
        public const string Vista = "vista";

        // Seconds a view takes to draw back in a vista, and to come in again on leaving it.
        private const float VistaBlend = 1.2f;

        /// <summary>A pennant flying to the right of a cell: `@mark flag column row`.</summary>
        public const string Flag = "flag";

        /// <summary>The word after a checkpoint's cell that says it stands in an antechamber of the Pit.</summary>
        public const string PitWord = "pit";
        public const int GateOrder = TilesV2World.TerrainOrder + 3;

        private readonly List<Thing> all = new List<Thing>();

        /// <summary>Everything made, in the map's order.</summary>
        public IReadOnlyList<Thing> All => all;

        /// <summary>How many relics the map holds, and how many the hero has found.</summary>
        public int Relics { get; private set; }
        public int RelicsFound { get; private set; }
        public int Checkpoints { get; private set; }
        public int Abilities { get; private set; }

        /// <summary>The gates on the road: veils of light, seals and slabs.</summary>
        public int Gates { get; private set; }
        public int Secrets { get; private set; }
        public int SecretsFound { get; private set; }
        public int Platforms { get; private set; }
        public int Winds { get; private set; }

        /// <summary>The game's upgrades lying in the world: more hearts, more draughts.</summary>
        public int Upgrades { get; private set; }
        public int Signs { get; private set; }

        /// <summary>The lights of Paradise, and how many of them burn.</summary>
        public int Beacons { get; private set; }
        public int BeaconsLit { get; private set; }

        /// <summary>The pictures stood in the world (a rainbow).</summary>
        public int Pictures { get; private set; }

        /// <summary>The pennants flying in it.</summary>
        public int Flags { get; private set; }

        /// <summary>The pictures that are several shown in turn, the ones whose windows burn by night, and the vistas.</summary>
        public int Moving { get; private set; }
        public int Glowing { get; private set; }
        public int Vistas { get; private set; }

        /// <summary>The doors the lights open, and how many of them are open.</summary>
        public int Sanctums { get; private set; }
        public int SanctumsOpen { get; private set; }

        /// <summary>The hero lit one of the lights.</summary>
        public event Action<Thing> BeaconLit;

        /// <summary>Enough lights burn for a door: it opened.</summary>
        public event Action<Thing> SanctumOpened;

        /// <summary>The hero found a relic.</summary>
        public event Action<Thing> RelicFound;

        /// <summary>The hero broke a wall that was not one.</summary>
        public event Action<Thing> SecretFound;

        /// <summary>The hero walked into the gate at the world's end.</summary>
        public event Action Ended;

        /// <summary>The named parts of the level: where each is, in the world's units, and its name.</summary>
        public IReadOnlyList<(Rect area, string name)> Zones => zones;

        /// <summary>
        /// What the end of the journey is called and what is said under it, when the map says
        /// (`@mark end column row title ... | words ...`); empty when it does not.
        /// </summary>
        public string EndTitle { get; private set; } = "";
        public string EndWords { get; private set; } = "";

        private readonly List<(Rect area, string name)> zones = new List<(Rect area, string name)>();

        /// <summary>How many relics a region of the map holds, and how many of them are found.</summary>
        public void RelicsIn(int region, out int found, out int held)
        {
            found = 0;
            held = 0;
            foreach (var thing in all)
            {
                if (thing.Kind == Relic && thing.Region == region)
                {
                    held++;
                    found += thing.IsGone ? 1 : 0;
                }
            }
        }

        /// <summary>A picture and its further frames in the set (`key_f1` ...), in order; itself alone when it has none.</summary>
        public static Sprite[] FramesOf(Dictionary<string, TilesV2Set.Backdrop> pictures, TilesV2Set.Backdrop first)
        {
            var frames = new List<Sprite> { first.sprite };
            while (pictures.TryGetValue(first.key + FrameSuffix + frames.Count, out var next) && next.sprite != null)
            {
                frames.Add(next.sprite);
            }
            return frames.ToArray();
        }

        /// <summary>How many frames a second a moving picture shows: a pennant flies, a tree only stirs.</summary>
        public static float FramesASecond(string key)
        {
            if (key.Contains("pennant") || key.Contains("glint"))
            {
                return 9f;
            }
            if (key.Contains("fountain"))
            {
                return 7f;
            }
            return key.Contains("banner") ? 4f : 3f;
        }

        /// <summary>How long a moving picture is gone between two showings: a glint comes and goes, the rest never rest.</summary>
        public static float RestOf(string key) => key.Contains("glint") ? 3.5f : 0f;

        /// <summary>
        /// Makes everything the world's map marks, under a new object in the world. The
        /// prefabs are the game's checkpoint and ability pickup; a kind whose prefab is
        /// missing is left out.
        /// </summary>
        public static TilesV2Things Build(
            TilesV2World world,
            GameObject checkpointPrefab,
            GameObject abilityPrefab,
            GameObject upgradePrefab = null
        )
        {
            var things = new GameObject("Things").AddComponent<TilesV2Things>();
            things.transform.SetParent(world.transform, false);
            var map = world.Map;
            var set = world.Set;
            Vector2 origin = world.transform.position;
            var pictures = new Dictionary<string, TilesV2Set.Backdrop>();
            foreach (var backdrop in set.backdrops)
            {
                pictures[backdrop.key] = backdrop;
            }
            foreach (var mark in map.Marks)
            {
                // The middle of the foot of the mark's cell, and the top left corner of it.
                var cell = map.CellOf(mark.Column, mark.Row);
                var foot = origin + new Vector2(cell.x + 0.5f, cell.y);
                var corner = origin + new Vector2(cell.x, cell.y + 1f);
                var thing = new Thing
                {
                    Kind = mark.Kind,
                    Cell = new Vector2Int(cell.x, cell.y),
                    Region = map.Regions.Count > 0 ? map.RegionAt(mark.Row) : -1,
                    Word = mark.Word(0),
                };
                switch (mark.Kind)
                {
                    case Checkpoint when checkpointPrefab != null:
                    {
                        thing.Made = Instantiate(checkpointPrefab, foot, Quaternion.identity, things.transform);
                        thing.Number = things.Checkpoints;
                        thing.InPit = mark.Word(0) == PitWord;
                        if (thing.Made.TryGetComponent<Checkpoint>(out var checkpoint))
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
                            continue;
                        }
                        thing.Made = Instantiate(abilityPrefab, foot, Quaternion.identity, things.transform);
                        thing.Made.GetComponent<AbilityPickup>().Ability = ability;
                        things.Abilities++;
                        break;
                    }
                    case Barrier:
                        thing.Word = "";
                        thing.Made = things.MakeBarrier(set, corner, Mathf.Max(mark.Number(0, 1), 1));
                        things.Gates++;
                        break;
                    case Seal:
                        thing.Made = things.MakeSeal(
                            set,
                            corner,
                            mark.Word(0) == "slam",
                            Mathf.Max(mark.Number(1, 1), 1),
                            Mathf.Max(mark.Number(2, 1), 1)
                        );
                        things.Gates++;
                        break;
                    case Secret:
                    {
                        var wide = Mathf.Max(mark.Number(0, 1), 1);
                        var tall = Mathf.Max(mark.Number(1, 1), 1);
                        // The room behind it, as the map has it (rows from the top), in cells.
                        var x0 = Mathf.Min(mark.Number(2, mark.Column), mark.Column);
                        var x1 = Mathf.Max(mark.Number(4, mark.Column), mark.Column + wide - 1);
                        var top = Mathf.Min(mark.Number(3, mark.Row), mark.Row);
                        var low = Mathf.Max(mark.Number(5, mark.Row), mark.Row + tall - 1);
                        thing.Hidden = new RectInt(x0, map.Height - 1 - low, x1 - x0 + 1, low - top + 1);
                        var wall = things.MakeSecret(set, corner, wide, tall, GroundSprite(set, mark.Word(6)));
                        thing.Made = wall.gameObject;
                        var found = thing;
                        wall.Broke += _ =>
                        {
                            things.SecretsFound++;
                            things.SecretFound?.Invoke(found);
                        };
                        things.Secrets++;
                        break;
                    }
                    case Platform:
                        thing.Word = mark.Word(1);
                        thing.Made = things.MakePlatform(
                            set,
                            origin + new Vector2(cell.x, cell.y),
                            Mathf.Max(mark.Number(0, 1), 1),
                            mark.Word(1) == "blink",
                            Fraction(mark.Word(2))
                        );
                        things.Platforms++;
                        break;
                    case Wind:
                        thing.Made = things.MakeWind(
                            set,
                            corner,
                            new Vector2(Mathf.Max(mark.Number(0, 1), 1), Mathf.Max(mark.Number(1, 1), 1)),
                            new Vector2(mark.Number(2, 0), mark.Number(3, 0)),
                            Fraction(mark.Word(4)),
                            Fraction(mark.Word(5))
                        );
                        things.Winds++;
                        break;
                    case Upgrade when upgradePrefab != null:
                    {
                        if (!Enum.TryParse<Player.UpgradeKind>(mark.Word(0), out var kind))
                        {
                            Debug.LogWarning($"Tiles V2: the map marks an upgrade '{mark.Word(0)}' the game has not.");
                            continue;
                        }
                        thing.Made = Instantiate(upgradePrefab, foot, Quaternion.identity, things.transform);
                        thing
                            .Made.GetComponent<UpgradePickup>()
                            .Set(kind, Player.Upgrades.FlagFor(kind, "tilesv2", $"{mark.Column}_{mark.Row}"));
                        things.Upgrades++;
                        break;
                    }
                    case Sign:
                    {
                        var holder = new GameObject("Sign " + mark.Word(0));
                        holder.transform.SetParent(things.transform, false);
                        holder.transform.position = foot;
                        var words = mark.Rest.Length > 1 ? string.Join(" ", mark.Rest, 1, mark.Rest.Length - 1) : "";
                        var shade = mark.Word(0) == TilesV2Sign.Shade;
                        holder
                            .AddComponent<TilesV2Sign>()
                            .Configure(
                                mark.Word(0),
                                words,
                                set.Thing(mark.Word(0)),
                                shade ? set.unlitMaterial : set.litMaterial
                            );
                        thing.Made = holder;
                        things.Signs++;
                        break;
                    }
                    case Beacon:
                    {
                        var holder = new GameObject("Beacon " + mark.Word(0));
                        holder.transform.SetParent(things.transform, false);
                        holder.transform.position = foot;
                        var beacon = holder.AddComponent<TilesV2Beacon>();
                        beacon.Configure(
                            set.Thing("beacon_0"),
                            set.Thing("beacon_1"),
                            set.Thing("beacon_2"),
                            set.unlitMaterial
                        );
                        thing.Made = holder;
                        things.Beacons++;
                        var lit = thing;
                        beacon.Lit += () =>
                        {
                            lit.Taken = true;
                            things.BeaconsLit++;
                            things.BeaconLit?.Invoke(lit);
                            things.OpenSanctums();
                        };
                        break;
                    }
                    case Stood:
                    {
                        if (!pictures.TryGetValue(mark.Word(0), out var entry) || entry.sprite == null)
                        {
                            Debug.LogWarning(
                                $"Tiles V2: the map stands a picture '{mark.Word(0)}' the tile set has not."
                            );
                            continue;
                        }
                        // The sprite's pivot is its bottom middle: it stands on the foot of the cell.
                        var holder = new GameObject("Picture " + entry.key);
                        holder.transform.SetParent(things.transform, false);
                        holder.transform.position =
                            foot + new Vector2(mark.Number(1, 0), mark.Number(2, 0)) * TilesV2Particles.Pixel;
                        var shown = holder.AddComponent<SpriteRenderer>();
                        shown.sprite = entry.sprite;
                        shown.sortingOrder = entry.order;
                        shown.sharedMaterial = entry.lit ? set.litMaterial : set.unlitMaterial;
                        // A picture of light is not always the same: a rainbow and the light in a hall's air are
                        // the sun's and gone by night, and they and a glory breathe a little.
                        var sun = entry.key.Contains("rainbow") || entry.key.Contains("_rays");
                        if (sun || entry.key.Contains("glory"))
                        {
                            holder.AddComponent<TilesV2Shimmer>().Configure(world, shown, sun, 0.25f);
                        }
                        // A picture with further frames in the set is those shown in turn.
                        var frames = FramesOf(pictures, entry);
                        if (frames.Length > 1)
                        {
                            holder
                                .AddComponent<TilesV2Frames>()
                                .Configure(shown, frames, FramesASecond(entry.key), RestOf(entry.key));
                            things.Moving++;
                        }
                        // One whose windows have a light of their own in the set shows it as the day goes.
                        if (pictures.TryGetValue(entry.key + GlowSuffix, out var glow) && glow.sprite != null)
                        {
                            var lamp = new GameObject("Glow");
                            lamp.transform.SetParent(holder.transform, false);
                            var light = lamp.AddComponent<SpriteRenderer>();
                            light.sprite = glow.sprite;
                            light.sortingOrder = glow.order;
                            light.sharedMaterial = set.unlitMaterial;
                            lamp.AddComponent<TilesV2Shimmer>().Configure(world, light, false, 0.12f, true);
                            things.Glowing++;
                        }
                        thing.Made = holder;
                        things.Pictures++;
                        break;
                    }
                    case Flag:
                    {
                        // A rod in the middle of a cell has its pennant moved to it: so many pixels.
                        var holder = new GameObject("Flag");
                        holder.transform.SetParent(things.transform, false);
                        holder.transform.position = foot + new Vector2(mark.Number(0, 0) * TilesV2Particles.Pixel, 0f);
                        var atRod = new Sprite[4];
                        var tail = new Sprite[4];
                        for (var i = 0; i < 4; i++)
                        {
                            atRod[i] = set.Thing("flag_a_" + i);
                            tail[i] = set.Thing("flag_b_" + i);
                        }
                        holder.AddComponent<TilesV2Flag>().Configure(atRod, tail, set.litMaterial);
                        thing.Made = holder;
                        things.Flags++;
                        break;
                    }
                    case Zone:
                    {
                        var size = new Vector2(Mathf.Max(mark.Number(0, 1), 1), Mathf.Max(mark.Number(1, 1), 1));
                        var name = string.Join(" ", mark.Rest, 3, Mathf.Max(mark.Rest.Length - 3, 0));
                        things.zones.Add((new Rect(corner.x, corner.y - size.y, size.x, size.y), name));
                        continue;
                    }
                    case Vista:
                    {
                        // The game's own camera zone, over the stretch the mark names.
                        var size = new Vector2(Mathf.Max(mark.Number(0, 1), 1), Mathf.Max(mark.Number(1, 1), 1));
                        var holder = new GameObject("Vista");
                        holder.transform.SetParent(things.transform, false);
                        holder.transform.position = corner + new Vector2(size.x / 2f, -size.y / 2f);
                        holder
                            .AddComponent<CameraZone>()
                            .Configure(
                                size,
                                new Vector2(mark.Number(5, 0), mark.Number(6, 0)),
                                Mathf.Max(mark.Number(2, 100), 25) / 100f,
                                mark.Number(3, 0) != 0,
                                mark.Number(4, 0) != 0,
                                -1f,
                                VistaBlend
                            );
                        thing.Made = holder;
                        things.Vistas++;
                        break;
                    }
                    case Sanctum:
                    {
                        var tall = Mathf.Max(mark.Number(0, 1), 1);
                        var holder = new GameObject("Sanctum") { layer = Layers.World };
                        holder.transform.SetParent(things.transform, false);
                        holder.transform.position = corner;
                        holder.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
                        var box = holder.AddComponent<BoxCollider2D>();
                        box.size = new Vector2(1f, tall);
                        box.offset = new Vector2(0.5f, -tall / 2f);
                        Picture(holder, set.Thing("sanctum"), set.unlitMaterial, new Vector2(1f, tall));
                        thing.Made = holder;
                        thing.Number = Mathf.Max(mark.Number(1, 1), 1);
                        thing.Word = "";
                        things.Sanctums++;
                        break;
                    }
                    case Relic:
                    case End:
                    {
                        var holder = new GameObject(mark.Kind);
                        holder.transform.SetParent(things.transform, false);
                        holder.transform.position = foot;
                        var relic = holder.AddComponent<TilesV2Relic>();
                        var last = mark.Kind == End;
                        if (last && mark.Rest.Length > 0)
                        {
                            var said = string.Join(" ", mark.Rest).Split('|');
                            things.EndTitle = said[0].Trim();
                            things.EndWords = said.Length > 1 ? said[1].Trim() : "";
                        }
                        relic.Configure(set.Thing("relic_0"), set.Thing("relic_1"), set.unlitMaterial, last);
                        thing.Made = holder;
                        if (last)
                        {
                            relic.Found += () => things.Ended?.Invoke();
                        }
                        else
                        {
                            var found = thing;
                            things.Relics++;
                            relic.Found += () =>
                            {
                                found.Taken = true;
                                things.RelicsFound++;
                                things.RelicFound?.Invoke(found);
                            };
                        }
                        break;
                    }
                    default:
                        continue;
                }
                things.all.Add(thing);
            }
            return things;
        }

        // Opens every door that enough lights burn for (a door's Number is how many it asks).
        private void OpenSanctums()
        {
            foreach (var thing in all)
            {
                if (thing.Kind != Sanctum || thing.Taken || BeaconsLit < thing.Number)
                {
                    continue;
                }
                thing.Taken = true;
                thing.Made.SetActive(false);
                SanctumsOpen++;
                SanctumOpened?.Invoke(thing);
            }
        }

        // A veil of light a tile wide and `tall` tiles high, down from a corner: solid to all but a Light Dash.
        private GameObject MakeBarrier(TilesV2Set set, Vector2 corner, int tall)
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
            return holder;
        }

        // A block `wide` by `tall` tiles down from a corner that one strike of the right kind breaks: a
        // bolt of Holy Light, or (a slab in a floor) a Smite. Solid on the World layer; a child on the
        // Enemy layer, a little larger, is what the hero's strikes find.
        private GameObject MakeSeal(TilesV2Set set, Vector2 corner, bool slab, int wide, int tall)
        {
            var holder = Block(slab ? "Slab" : "Seal", corner, wide, tall);
            Picture(holder, set.Thing(slab ? "seal_slam" : "seal_bolt"), set.litMaterial, new Vector2(wide, tall));
            holder.AddComponent<Breakable>().Configure(1, slab ? StruckBy.Slam : StruckBy.Bolt, null);
            return holder;
        }

        // A wall that is not one: it is drawn as the rock round it (with a few cracks to tell it
        // by), and two cuts of the sword break it.
        private Breakable MakeSecret(TilesV2Set set, Vector2 corner, int wide, int tall, Sprite rock)
        {
            var holder = Block("Secret Wall", corner, wide, tall);
            for (var y = 0; y < tall; y++)
            {
                for (var x = 0; x < wide; x++)
                {
                    var at = new Vector3(x + 0.5f, -y - 0.5f, 0f);
                    Cell(
                        holder,
                        rock != null ? rock : set.Thing("seal_slam"),
                        set.litMaterial,
                        at,
                        TilesV2World.TerrainOrder
                    );
                    Cell(holder, set.Thing("crack"), set.litMaterial, at, TilesV2World.TerrainOrder + 1);
                }
            }
            var wall = holder.AddComponent<Breakable>();
            wall.Configure(2, StruckBy.Any, null);
            return wall;
        }

        // A platform `wide` tiles long with its foot at a corner: it gives way under the hero and
        // comes back (the game's Crumble), or comes and goes by the clock (its Blinker).
        private GameObject MakePlatform(TilesV2Set set, Vector2 foot, int wide, bool blinks, float ahead)
        {
            var holder = new GameObject(blinks ? "Blinker" : "Crumble") { layer = Layers.World };
            holder.transform.SetParent(transform, false);
            holder.transform.position = foot;
            var box = holder.AddComponent<BoxCollider2D>();
            box.size = new Vector2(wide, 12f / 16f);
            box.offset = new Vector2(wide / 2f, 1f - 6f / 16f);
            for (var x = 0; x < wide; x++)
            {
                Cell(
                    holder,
                    set.Thing(blinks ? "blink" : "crumble"),
                    set.litMaterial,
                    new Vector3(x + 0.5f, 0.5f, 0f),
                    GateOrder
                );
            }
            if (blinks)
            {
                holder.AddComponent<Blinker>().Configure(1.7f, 1.3f, 0.4f, ahead);
            }
            else
            {
                holder.AddComponent<Crumble>().Configure(0.6f, 2.5f);
            }
            return holder;
        }

        // A wind over `size` tiles down and right from a corner, in level pixels a second; gusting
        // when `blows` is over zero. Streaks drift in it so it is seen.
        private GameObject MakeWind(
            TilesV2Set set,
            Vector2 corner,
            Vector2 size,
            Vector2 wind,
            float blows,
            float rests
        )
        {
            var holder = new GameObject("Wind");
            holder.transform.SetParent(transform, false);
            holder.transform.position = corner + new Vector2(size.x / 2f, -size.y / 2f);
            var zone = holder.AddComponent<ForceZone>();
            zone.Configure(size, wind, 600f);
            if (blows > 0f)
            {
                zone.Gust(blows, rests);
            }
            var most = Mathf.Clamp(Mathf.RoundToInt(size.x * size.y / 6f), 6, 60);
            var streaks = TilesV2Particles.Make(holder.transform, "Streaks", Vector3.zero, set.unlitMaterial, most);
            var main = streaks.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.8f);
            main.startSpeed = 0f;
            main.startColor = new Color(0.92f, 0.95f, 1f, 0.45f);
            main.prewarm = true;
            var emission = streaks.emission;
            emission.rateOverTime = most / 1.4f;
            var shape = streaks.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(size.x, size.y, 0.1f);
            var drift = streaks.velocityOverLifetime;
            drift.enabled = true;
            drift.space = ParticleSystemSimulationSpace.World;
            drift.x = wind.x / 16f;
            drift.y = wind.y / 16f;
            drift.z = 0f;
            TilesV2Particles.Fade(streaks, Color.white, Color.white, 0.3f);
            streaks.Play();
            return holder;
        }

        // The sprite a ground's tile shows in the middle of rock: its rule with all eight neighbours.
        private static Sprite GroundSprite(TilesV2Set set, string character)
        {
            if (string.IsNullOrEmpty(character) || !set.TryGlyph(character[0], out var glyph))
            {
                return null;
            }
            if (!(glyph.tile is RuleTile ground))
            {
                return null;
            }
            foreach (var rule in ground.m_TilingRules)
            {
                if (
                    rule.m_Neighbors.Count == 8
                    && rule.m_Neighbors.TrueForAll(n => n == RuleTile.TilingRuleOutput.Neighbor.This)
                )
                {
                    return rule.m_Sprites.Length > 0 ? rule.m_Sprites[0] : null;
                }
            }
            return ground.m_DefaultSprite;
        }

        // A solid block `wide` by `tall` tiles down from a corner, with the larger box on the Enemy
        // layer that the hero's strikes find.
        private GameObject Block(string name, Vector2 corner, int wide, int tall)
        {
            var holder = new GameObject(name) { layer = Layers.World };
            holder.transform.SetParent(transform, false);
            holder.transform.position = corner;
            holder.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
            var size = new Vector2(wide, tall);
            var offset = new Vector2(wide / 2f, -tall / 2f);
            var box = holder.AddComponent<BoxCollider2D>();
            box.size = size;
            box.offset = offset;
            var struck = new GameObject("Struck") { layer = Layers.Enemy };
            struck.transform.SetParent(holder.transform, false);
            var reach = struck.AddComponent<BoxCollider2D>();
            reach.isTrigger = true;
            reach.size = size + new Vector2(0.5f, 0.75f);
            reach.offset = offset + new Vector2(0f, 0.125f);
            return holder;
        }

        // One tile-sized sprite in a holder.
        private static void Cell(GameObject holder, Sprite sprite, Material material, Vector3 at, int order)
        {
            var child = new GameObject("Cell");
            child.transform.SetParent(holder.transform, false);
            child.transform.localPosition = at;
            var shown = child.AddComponent<SpriteRenderer>();
            shown.sprite = sprite;
            shown.sortingOrder = order;
            if (material != null)
            {
                shown.sharedMaterial = material;
            }
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

        private static float Fraction(string word) =>
            float.TryParse(
                word,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var value
            )
                ? value
                : 0f;

        /// <summary>One thing a map marks, as it stands in the world.</summary>
        public sealed class Thing
        {
            public string Kind;

            /// <summary>The cell the map marks (y up, as the world's grid has it).</summary>
            public Vector2Int Cell;

            /// <summary>The number of its region among the map's; -1 on a map with none.</summary>
            public int Region;

            /// <summary>What the kind says first after its cell: an ability's name, what breaks a seal.</summary>
            public string Word;
            public GameObject Made;

            /// <summary>A checkpoint's number among the checkpoints.</summary>
            public int Number;

            /// <summary>A checkpoint in an antechamber of the Pit.</summary>
            public bool InPit;

            /// <summary>The cells a secret wall hides until it is broken.</summary>
            public RectInt Hidden;

            /// <summary>A relic the hero has taken.</summary>
            public bool Taken;

            /// <summary>Taken, broken or picked up: no longer there.</summary>
            public bool IsGone => Taken || Made == null || !Made.activeSelf;
        }
    }
}
