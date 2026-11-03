using UnityEngine;

namespace Descender.World.Paradise
{
    /// <summary>
    /// Puts the full-resolution Ansel (a HeroRig prefab) on the hero in the new Paradise and
    /// hides his pixel-art sprite. Sits on the camera of ParadiseMain.unity; the shared Player
    /// prefab and the prototype in Main.unity keep the pixel-art hero. His colliders, hitboxes
    /// and everything else stay as they are: only what is drawn changes.
    /// </summary>
    public sealed class HeroRigInstaller : MonoBehaviour
    {
        public const string RigName = "HeroRig";

        [SerializeField]
        private HeroRig rigPrefab;

        private Player.PlayerController dressed;
        private SpriteRenderer[] hidden = new SpriteRenderer[0];

        public void Configure(HeroRig prefab)
        {
            rigPrefab = prefab;
        }

        private void LateUpdate()
        {
            var hero = Player.PlayerController.Current;
            if (hero == null || rigPrefab == null)
            {
                return;
            }
            if (hero != dressed)
            {
                dressed = hero;
                var rig = hero.transform.Find(RigName);
                if (rig == null)
                {
                    var made = Instantiate(rigPrefab, hero.transform, false);
                    made.name = RigName;
                    made.transform.localPosition = Vector3.zero;
                    made.Bind(hero);
                    rig = made.transform;
                }
                // Only the pixel-art body ("Visual"): effects attached to him stay visible. Its
                // PlayerSprite re-enables the renderer every frame (the hurt blink), so it is
                // switched off here; the rig blinks on its own. Its methods stay callable.
                foreach (var driver in hero.GetComponentsInChildren<Player.PlayerSprite>(true))
                {
                    driver.enabled = false;
                }
                var visual = hero.transform.Find("Visual");
                hidden = visual != null ? visual.GetComponentsInChildren<SpriteRenderer>(true) : new SpriteRenderer[0];
            }
            // The old animator keeps setting its sprite; keep it hidden.
            foreach (var r in hidden)
            {
                if (r != null && r.enabled)
                {
                    r.enabled = false;
                }
            }
        }
    }
}
