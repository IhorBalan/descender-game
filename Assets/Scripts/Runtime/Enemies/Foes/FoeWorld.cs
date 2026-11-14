using System.Collections.Generic;
using Descender.World;
using UnityEngine;

namespace Descender.Enemies
{
    /// <summary>What a placement of one character is made from; null leaves that placement to the catalog.</summary>
    public delegate FoeRecipe FoeKind(WorldPlacement placement);

    /// <summary>
    /// Foes in the regions. A kind registered here by its character id stands in for
    /// the catalog wherever a <see cref="WorldPlacement"/> names that character: the
    /// placement makes a <see cref="Foe"/> from the kind's recipe, feet at the placement
    /// (a flyer's middle), facing as it says. The foe carries the placement's id, so its
    /// death is remembered in <see cref="Kills"/>, and its object goes when it dies, so
    /// the placement makes another when the hero respawns. A character that is not
    /// registered is made from the profile the placement's catalog gives for it (a
    /// <see cref="WalkerProfile"/> and the placement's habit, a <see cref="FlyerProfile"/>,
    /// a <see cref="HangerProfile"/> or a <see cref="BossProfile"/>; see
    /// <see cref="FoeKinds"/>); anything else is left to the catalog's prefab.
    /// </summary>
    public static class FoeWorld
    {
        /// <summary>The character an imp king calls.</summary>
        public const string ImpKind = "imp";

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

        /// <summary>
        /// The recipe for a placement from the profile its catalog gives for its character;
        /// null when there is none. `centred` is true for one whose origin is its middle (a
        /// flyer).
        /// </summary>
        private static FoeRecipe FromCatalog(WorldPlacement placement, out bool centred)
        {
            centred = false;
            var name = placement.Character;
            switch (placement.Foe)
            {
                case WalkerProfile walker:
                    return FoeKinds.FromWalker(name, walker, placement.Behaviour, placement.Passive);
                case FlyerProfile flyer:
                    centred = true;
                    return FoeKinds.FromFlyer(name, flyer, flyer.size, placement.Behaviour == "dive");
                case HangerProfile hanger:
                    return FoeKinds.FromHanger(name, hanger.health);
                case BossProfile boss:
                {
                    var imp = placement.Catalog.FoeFor(ImpKind) as WalkerProfile;
                    return FoeKinds.FromBoss(name, boss, imp != null ? Imp(imp) : null);
                }
                default:
                    return null;
            }
        }

        /// <summary>What an imp king calls: an imp that comes for the hero and leaves nothing.</summary>
        private static FoeRecipe Imp(WalkerProfile profile)
        {
            var imp = FoeKinds.FromWalker(ImpKind, profile, "chase", false);
            imp.drops = null;
            return imp;
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
            else
            {
                recipe = FromCatalog(placement, out centred);
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
