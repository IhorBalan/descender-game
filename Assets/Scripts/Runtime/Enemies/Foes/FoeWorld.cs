using System.Collections.Generic;
using Descender.World;
using UnityEngine;

namespace Descender.Enemies
{
    /// <summary>What a placement of one character is made from; null leaves that placement to the catalog.</summary>
    public delegate FoeRecipe FoeKind(WorldPlacement placement);

    /// <summary>
    /// Foes in the regions. A kind registered here by its character id stands in for
    /// the catalog's prefab wherever a <see cref="WorldPlacement"/> names that
    /// character: the placement makes a <see cref="Foe"/> from the kind's recipe, feet
    /// at the placement, facing as it says. The foe carries the placement's id, so its
    /// death is remembered in <see cref="Kills"/>, and its object goes when it dies, so
    /// the placement makes another when the hero respawns. A character that is not
    /// registered but whose prefab is a <see cref="Walker"/> is made from that walker's
    /// profile and the placement's habit (<see cref="FoeKinds.FromWalker"/>), unless
    /// <see cref="RemakesWalkers"/> is off; anything else is left to the catalog.
    /// </summary>
    public static class FoeWorld
    {
        private static readonly Dictionary<string, FoeKind> Kinds = new Dictionary<string, FoeKind>();

        /// <summary>On, a placement whose prefab is a walker makes a foe from its profile; off, the old walker stands.</summary>
        public static bool RemakesWalkers { get; set; } = true;

        /// <summary>
        /// From now on a placement of this character makes what `recipe` gives for it
        /// (the body's size is the foe's size); a null recipe leaves that placement to the
        /// catalog.
        /// </summary>
        public static void Register(string character, FoeKind recipe)
        {
            if (!string.IsNullOrEmpty(character) && recipe != null)
            {
                Kinds[character] = recipe;
            }
        }

        public static bool Has(string character) => !string.IsNullOrEmpty(character) && Kinds.ContainsKey(character);

        /// <summary>Forgets every kind; for the tests.</summary>
        public static void Reset()
        {
            Kinds.Clear();
            RemakesWalkers = true;
            Hook();
        }

        /// <summary>Tells the placements to ask here first. Runs once at load.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Hook()
        {
            WorldPlacement.Remake = Stand;
        }

        /// <summary>The recipe for a placement whose prefab is a walker, from its profile; null for any other.</summary>
        private static FoeRecipe WalkerAt(WorldPlacement placement)
        {
            var prefab = placement.Prefab;
            if (prefab == null || !prefab.TryGetComponent<Walker>(out var walker) || walker.Profile == null)
            {
                return null;
            }
            var name = string.IsNullOrEmpty(placement.Character) ? prefab.name : placement.Character;
            return FoeKinds.FromWalker(name, walker.Profile, placement.Behaviour, placement.Passive);
        }

        /// <summary>The foe that stands at a placement, or null when its character is not one of these.</summary>
        public static GameObject Stand(WorldPlacement placement)
        {
            if (placement == null)
            {
                return null;
            }
            FoeRecipe recipe = null;
            if (Kinds.TryGetValue(placement.Character ?? "", out var kind))
            {
                recipe = kind(placement);
            }
            else if (RemakesWalkers)
            {
                recipe = WalkerAt(placement);
            }
            if (recipe == null)
            {
                return null;
            }
            var size = recipe.body.size / EnemyPhysics.Ppu;
            var centre = (Vector2)placement.transform.position + new Vector2(0f, size.y / 2f);
            var foe = Foe.Make(recipe, placement.transform, centre, size);
            foe.PlacementId = placement.Id;
            foe.GoesWhenDead = true;
            foe.Face(placement.FacesLeft ? -1 : 1);
            return foe.gameObject;
        }
    }
}
