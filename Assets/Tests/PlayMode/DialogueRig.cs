using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Descender.Player;
using Descender.UI;
using Descender.World;
using UnityEngine;

namespace Descender.Tests.PlayMode
{
    /// <summary>
    /// What the dialogue tests stand on, all made in code: a floor, the hero, a
    /// DialogueBox and a DialogueRunner bound to it with the real world_1 dialogue
    /// loaded, and placements that spawn from a catalog of templates.
    /// </summary>
    internal sealed class DialogueRig
    {
        public const string DialoguePath = "Assets/Data/dialogue/world_1.json";

        /// <summary>Where templates stand, far from the hero, since an instantiated copy must come from an active object.</summary>
        private static readonly Vector3 Backstage = new Vector3(-500f, 0f, 0f);

        private readonly List<Object> made = new List<Object>();
        private bool parkedBefore;

        public DialogueRunner Runner { get; private set; }
        public DialogueBox Box { get; private set; }
        public PlayerController Hero { get; private set; }
        public PlacementCatalog Catalog { get; private set; }

        public static DialogueRig Make()
        {
            Story.StoryFlags.Reset();
            Economy.Inventory.Reset();
            Economy.Skills.Reset();
            Enemies.Kills.Reset();
            Pickups.Checkpoint.Reset();
            Physics2D.gravity = Vector2.zero;
            var rig = new DialogueRig();
            // The story is parked in the game; these tests keep the dialogue working for its return.
            rig.parkedBefore = Story.StoryMode.Parked;
            Story.StoryMode.Parked = false;

            var floor = rig.Thing("Floor");
            floor.layer = LayerMask.NameToLayer("World");
            floor.AddComponent<BoxCollider2D>().size = new Vector2(40f, 1f);
            floor.transform.position = new Vector3(0f, -0.5f, 0f);

            var hero = rig.Thing("Hero");
            hero.layer = LayerMask.NameToLayer("Player");
            hero.transform.position = new Vector3(0f, 0.05f, 0f);
            var body = hero.AddComponent<BoxCollider2D>();
            body.size = new Vector2(0.75f, 1.5f);
            body.offset = new Vector2(0f, 0.75f);
            rig.Hero = hero.AddComponent<PlayerController>();

            rig.Box = rig.Thing("Dialogue Box").AddComponent<DialogueBox>();
            rig.Runner = rig.Thing("Dialogue Runner").AddComponent<DialogueRunner>();
            rig.Runner.Bind(rig.Box, null, null);
            rig.Runner.Load(System.IO.File.ReadAllText(DialoguePath), DialoguePath);

            rig.Catalog = ScriptableObject.CreateInstance<PlacementCatalog>();
            rig.made.Add(rig.Catalog);
            return rig;
        }

        /// <summary>A catalog entry: an object carrying T that placements of `key` copy.</summary>
        public T Template<T>(string key)
            where T : Component
        {
            var template = Thing(key + " template");
            template.transform.position = Backstage;
            var component = template.AddComponent<T>();
            Catalog.Set(key, template);
            return component;
        }

        /// <summary>A catalog entry with no behaviour, to tell apart from the others.</summary>
        public GameObject Template(string key)
        {
            var template = Thing(key + " template");
            template.transform.position = Backstage;
            Catalog.Set(key, template);
            return template;
        }

        /// <summary>A placement from its fields as JSON (the serialized names); it spawns on its Start, a frame later.</summary>
        public WorldPlacement Place(Vector2 at, string json)
        {
            var thing = Thing("Placement");
            thing.SetActive(false);
            thing.transform.position = at;
            var placement = thing.AddComponent<WorldPlacement>();
            JsonUtility.FromJsonOverwrite(json, placement);
            typeof(WorldPlacement)
                .GetField("catalog", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(placement, Catalog);
            thing.SetActive(true);
            return placement;
        }

        /// <summary>Stands the hero somewhere at once; the physics step after it tells the triggers.</summary>
        public void MoveHero(float x)
        {
            var at = new Vector2(x, 0.05f);
            Hero.GetComponent<Rigidbody2D>().position = at;
            Hero.transform.position = at;
        }

        /// <summary>Reads the open box to its end: each press finishes the typing or turns the page.</summary>
        public void ReadToTheEnd()
        {
            for (var i = 0; i < 200 && Box.IsOpen; i++)
            {
                Box.Advance();
            }
        }

        public static IEnumerator Steps(int count)
        {
            for (var i = 0; i < count; i++)
            {
                yield return new WaitForFixedUpdate();
            }
        }

        public void Dispose()
        {
            if (Box != null && Box.IsOpen)
            {
                Box.Close();
            }
            foreach (var thing in made)
            {
                if (thing != null)
                {
                    Object.Destroy(thing);
                }
            }
            made.Clear();
            Story.StoryFlags.Reset();
            Story.StoryMode.Parked = parkedBefore;
        }

        private GameObject Thing(string name)
        {
            var thing = new GameObject(name);
            made.Add(thing);
            return thing;
        }
    }
}
