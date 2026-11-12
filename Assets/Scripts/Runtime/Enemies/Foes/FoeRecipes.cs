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
