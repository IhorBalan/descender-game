using UnityEngine;

namespace Descender.World.Paradise
{
    /// <summary>
    /// A labelled spot in a greybox: where a story beat, NPC, story item, secret or
    /// encounter space will go before the real thing exists (docs/design/paradise.md).
    /// Draws a gizmo in the editor and, in a development build or the editor, a small
    /// coloured flag with its label in the game view, so a playtest shows the plan.
    /// Replaced by the real object in the art and story passes.
    /// </summary>
    public sealed class DesignMarker : MonoBehaviour
    {
        public enum Kind
        {
            Story,
            Npc,
            Item,
            Secret,
            Encounter,
            Landmark,
        }

        [SerializeField]
        private Kind kind = Kind.Story;

        [SerializeField]
        private string label = "";

        [TextArea]
        [SerializeField]
        private string note = "";

        public Kind MarkerKind => kind;
        public string Label => label;
        public string Note => note;

        public void Configure(Kind markerKind, string markerLabel, string markerNote)
        {
            kind = markerKind;
            label = markerLabel;
            note = markerNote;
        }

        public static Color ColorOf(Kind k)
        {
            switch (k)
            {
                case Kind.Npc:
                    return new Color(1f, 0.35f, 0.85f);
                case Kind.Item:
                    return new Color(1f, 0.65f, 0.1f);
                case Kind.Secret:
                    return new Color(0.3f, 1f, 0.9f);
                case Kind.Encounter:
                    return new Color(1f, 0.25f, 0.25f);
                case Kind.Landmark:
                    return new Color(0.6f, 0.6f, 1f);
                default:
                    return Color.white;
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = ColorOf(kind);
            Gizmos.DrawWireSphere(transform.position, 0.6f);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 2f);
        }
    }
}
