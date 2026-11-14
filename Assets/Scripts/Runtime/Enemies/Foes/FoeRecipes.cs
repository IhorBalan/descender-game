using Descender.Fx;
using UnityEngine;

namespace Descender.Enemies
{
    /// <summary>
    /// The kinds of foe the lab and the encounters use, each a new recipe when asked: a
    /// name, a body and its behaviours in the order they are asked.
    /// </summary>
    public static class FoeRecipes
    {
        /// <summary>Stands and takes hits.</summary>
        public static FoeRecipe Still()
        {
            var body = new FoeBody { color = new Color(0.8f, 0.45f, 0.4f) };
            return new FoeRecipe("stands", body);
        }

        /// <summary>Walks its platform; only its touch hurts.</summary>
        public static FoeRecipe Walker()
        {
            return new FoeRecipe("walker", new FoeBody { health = 2 }, new Patrol());
        }

        /// <summary>Patrols, comes for the hero when it sees him and swings when it reaches him.</summary>
        public static FoeRecipe Chaser()
        {
            var body = new FoeBody { color = new Color(0.9f, 0.4f, 0.2f) };
            return new FoeRecipe("chaser", body, new Swing(), new Chase(), new Patrol());
        }

        /// <summary>Still until he is near, then a warning and a fast rush.</summary>
        public static FoeRecipe Ambusher()
        {
            var body = new FoeBody
            {
                health = 2,
                shape = ShapeKind.Triangle,
                color = new Color(0.55f, 0.35f, 0.6f),
            };
            var rush = new Chase { pace = 2.6f, sight = 200f };
            return new FoeRecipe("ambusher", body, new Ambush(), new Swing { windup = 0.25f }, rush);
        }

        /// <summary>Walks its platform and throws a bolt at him when he is in range.</summary>
        public static FoeRecipe Shooter()
        {
            var body = new FoeBody
            {
                health = 2,
                speed = 20f,
                shape = ShapeKind.Circle,
                color = new Color(0.9f, 0.55f, 0.2f),
            };
            return new FoeRecipe("shooter", body, new Shoot(), new Patrol());
        }

        /// <summary>Stands and throws flat bolts the way it faces: to be jumped.</summary>
        public static FoeRecipe Turret()
        {
            var body = new FoeBody { health = 4, color = new Color(0.7f, 0.5f, 0.3f) };
            var flat = new Shoot
            {
                level = true,
                range = 220f,
                reach = 260f,
            };
            return new FoeRecipe("turret", body, flat);
        }

        /// <summary>Hovers, and dives at him when he passes below.</summary>
        public static FoeRecipe Diver()
        {
            var body = new FoeBody
            {
                health = 2,
                flies = true,
                shape = ShapeKind.Triangle,
                color = new Color(0.4f, 0.6f, 0.85f),
            };
            return new FoeRecipe("diver", body, new Dive(), new Hover());
        }

        /// <summary>Patrols behind a shield and swings: to be hit from behind, from above or with the slam.</summary>
        public static FoeRecipe Guard()
        {
            var body = new FoeBody
            {
                health = 4,
                speed = 24f,
                color = new Color(0.5f, 0.55f, 0.7f),
            };
            return new FoeRecipe("guard", body, new Shield(), new Swing(), new Patrol());
        }

        /// <summary>Stands and makes walkers, two at a time at most.</summary>
        public static FoeRecipe Summoner()
        {
            var body = new FoeBody
            {
                health = 4,
                speed = 0f,
                shape = ShapeKind.Diamond,
                color = new Color(0.45f, 0.7f, 0.5f),
            };
            return new FoeRecipe("summoner", body, new Summon { summons = Walker() });
        }

        /// <summary>
        /// The boss template: armoured, in three phases at two thirds and one third of its
        /// health. It swings and charges; then shoots, charges and calls walkers; then does
        /// all three faster.
        /// </summary>
        public static FoeRecipe Boss()
        {
            var body = new FoeBody
            {
                health = 12,
                speed = 28f,
                armour = true,
                pushShare = 0f,
                color = new Color(0.7f, 0.2f, 0.25f),
            };
            var swing = new Swing
            {
                range = 34f,
                reach = new Vector2(30f, 30f),
                windup = 0.5f,
                cooldown = 0f,
            };
            var charge = new Charge { cooldown = 0f };
            var first = new BossPhase(0.66f, 0.8f, swing, charge);

            var shot = new Shoot
            {
                range = 300f,
                reach = 320f,
                cooldown = 0f,
            };
            var call = new Summon
            {
                summons = Walker(),
                range = 400f,
                windup = 0.7f,
                cooldown = 0f,
            };
            var second = new BossPhase(0.33f, 0.6f, shot, new Charge { cooldown = 0f }, call);

            var quickSwing = new Swing
            {
                range = 34f,
                reach = new Vector2(30f, 30f),
                windup = 0.35f,
                recover = 0.3f,
                cooldown = 0f,
            };
            var quickShot = new Shoot
            {
                range = 300f,
                reach = 320f,
                windup = 0.35f,
                speed = 150f,
                cooldown = 0f,
            };
            var quickCharge = new Charge
            {
                windup = 0.4f,
                speed = 240f,
                recover = 0.4f,
                cooldown = 0f,
            };
            var third = new BossPhase(0f, 0.35f, quickSwing, quickShot, quickCharge);

            var pattern = new Pattern();
            pattern.phases = new[] { first, second, third };
            return new FoeRecipe("boss", body, pattern);
        }

        /// <summary>Comes for him and, from a few steps off, leaps: long and high.</summary>
        public static FoeRecipe Pouncer()
        {
            var body = new FoeBody
            {
                health = 2,
                shape = ShapeKind.Triangle,
                color = new Color(0.85f, 0.5f, 0.3f),
            };
            return new FoeRecipe("pouncer", body, new Swing(), new Pounce(), new Chase(), new Patrol());
        }

        /// <summary>Sees him from far, runs at him and lunges: a short, low, fast leap.</summary>
        public static FoeRecipe Lunger()
        {
            var body = new FoeBody
            {
                health = 2,
                shape = ShapeKind.Triangle,
                color = new Color(0.75f, 0.3f, 0.45f),
            };
            var lunge = new Pounce
            {
                nearest = 28f,
                farthest = 72f,
                above = 24f,
                windup = 0.25f,
                spring = new Vector2(210f, 90f),
                cooldown = 1.2f,
            };
            var run = new Chase { sight = 200f, pace = 2.4f };
            return new FoeRecipe("lunger", body, new Swing(), lunge, run, new Patrol());
        }

        /// <summary>Throws bolts from a distance and backs away when he comes close.</summary>
        public static FoeRecipe Hurler()
        {
            var body = new FoeBody
            {
                health = 2,
                speed = 28f,
                shape = ShapeKind.Circle,
                color = new Color(0.9f, 0.7f, 0.25f),
            };
            var hurl = new Shoot
            {
                nearest = 48f,
                range = 150f,
                cooldown = 1.8f,
            };
            return new FoeRecipe("hurler", body, hurl, new Retreat(), new Patrol());
        }

        /// <summary>Slow and armoured: swings at a hero beside it and, at one further off, sends a wave along the floor.</summary>
        public static FoeRecipe Slammer()
        {
            var body = new FoeBody
            {
                health = 6,
                speed = 20f,
                armour = true,
                shape = ShapeKind.Diamond,
                color = new Color(0.55f, 0.4f, 0.3f),
            };
            return new FoeRecipe("slammer", body, new Swing(), new Quake(), new Patrol());
        }

        /// <summary>Slow and armoured: a hit does not stop its swing, which is long to come and hurts twice as much.</summary>
        public static FoeRecipe Brute()
        {
            var body = new FoeBody
            {
                health = 6,
                speed = 20f,
                armour = true,
                shape = ShapeKind.Diamond,
                color = new Color(0.6f, 0.3f, 0.5f),
            };
            var swing = new Swing
            {
                windup = 0.6f,
                recover = 0.7f,
                reach = new Vector2(30f, 28f),
                damage = 2,
            };
            return new FoeRecipe("brute", body, swing, new Chase { pace = 1.2f }, new Patrol());
        }
    }
}
