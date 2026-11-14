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
    /// registered is left to the catalog.
    /// </summary>
    public static class FoeWorld
    {
        private static readonly Dictionary<string, FoeKind> Kinds = new Dictionary<string, FoeKind>();

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
            Hook();
        }

        /// <summary>Tells the placements to ask here first. Runs once at load.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Hook()
        {
            WorldPlacement.Remake = Stand;
        }

        /// <summary>The foe that stands at a placement, or null when its character is not one of these.</summary>
        public static GameObject Stand(WorldPlacement placement)
        {
            if (placement == null || !Kinds.TryGetValue(placement.Character ?? "", out var kind))
            {
                return null;
            }
            var recipe = kind(placement);
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
