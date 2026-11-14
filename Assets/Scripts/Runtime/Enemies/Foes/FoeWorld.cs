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
    /// at the placement (a flyer's middle), facing as it says. The foe carries the placement's id, so its
    /// death is remembered in <see cref="Kills"/>, and its object goes when it dies, so
    /// the placement makes another when the hero respawns. A character that is not
    /// registered but whose prefab is one of the old enemies (a <see cref="Walker"/>,
    /// a <see cref="Flyer"/> or a <see cref="Hanger"/>) is made from that prefab's
    /// numbers and the placement's habit (<see cref="FoeKinds"/>), unless
    /// <see cref="Remakes"/> is off; anything else is left to the catalog.
    /// </summary>
    public static class FoeWorld
    {
        private static readonly Dictionary<string, FoeKind> Kinds = new Dictionary<string, FoeKind>();

        /// <summary>On, a placement whose prefab is an old enemy makes a foe from its numbers; off, the old enemy stands.</summary>
        public static bool Remakes { get; set; } = true;

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
            Remakes = true;
            Hook();
        }

        /// <summary>Tells the placements to ask here first. Runs once at load.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Hook()
        {
            WorldPlacement.Remake = Stand;
        }

        /// <summary>
        /// The recipe for a placement whose prefab is an old enemy, from its numbers; null
        /// for any other. `centred` is true for one whose origin is its middle (a flyer).
        /// </summary>
        private static FoeRecipe OldAt(WorldPlacement placement, out bool centred)
        {
            centred = false;
            var prefab = placement.Prefab;
            if (prefab == null)
            {
                return null;
            }
            var name = string.IsNullOrEmpty(placement.Character) ? prefab.name : placement.Character;
            if (prefab.TryGetComponent<Walker>(out var walker) && walker.Profile != null)
            {
                return FoeKinds.FromWalker(name, walker.Profile, placement.Behaviour, placement.Passive);
            }
            if (prefab.TryGetComponent<Flyer>(out var flyer) && flyer.Profile != null)
            {
                centred = true;
                return FoeKinds.FromFlyer(name, flyer.Profile, flyer.Size, placement.Behaviour == "dive");
            }
            if (prefab.TryGetComponent<Hanger>(out var hanger))
            {
                return FoeKinds.FromHanger(name, hanger.MaxHealth);
            }
            return null;
        }

        /// <summary>The foe that stands at a placement, or null when its character is not one of these.</summary>
        public static GameObject Stand(WorldPlacement placement)
        {
            if (placement == null)
            {
                return null;
            }
            FoeRecipe recipe = null;
            var centred = false;
            if (Kinds.TryGetValue(placement.Character ?? "", out var kind))
            {
                recipe = kind(placement);
            }
            else if (Remakes)
            {
                recipe = OldAt(placement, out centred);
            }
            if (recipe == null)
            {
                return null;
            }
            var size = recipe.body.size / EnemyPhysics.Ppu;
            Vector2 centre = placement.transform.position;
            if (!centred)
            {
                centre.y += size.y / 2f;
            }
            var foe = Foe.Make(recipe, placement.transform, centre, size);
            foe.PlacementId = placement.Id;
            foe.GoesWhenDead = true;
            foe.Face(placement.FacesLeft ? -1 : 1);
            return foe.gameObject;
        }
    }
}
