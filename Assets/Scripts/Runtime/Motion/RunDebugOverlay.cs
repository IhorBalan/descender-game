using System.Collections.Generic;
using UnityEngine;

namespace Descender.Motion
{
    /// <summary>
    /// Draws over a <see cref="RunSkeleton"/> what is hard to see in the shapes: the bones
    /// as lines, every joint as a dot, a ring under each foot that is on the ground, a mark
    /// left where each foot came down (if a foot slid, its ring would leave its mark), and
    /// in the corner the phase, the key pose it is nearest, which feet bear and the run's
    /// numbers. Shown or hidden with <see cref="Visible"/> (Tab in the lab).
    /// </summary>
    public sealed class RunDebugOverlay : MonoBehaviour
    {
        public const int Footprints = 16;
        private const int Order = 40;

        // The joints marked: hips, chest, neck, head; hip, knee, ankle, toes of each leg; elbow and wrist of each arm.
        private const int JointCount = 16;
        private const float Line = 0.025f;
        private const float Dot = 0.065f;

        private static readonly Color Bone = new Color(1f, 1f, 1f, 0.85f);
        private static readonly Color Joint = new Color(1f, 0.85f, 0.2f);
        private static readonly Color Ground = new Color(0.25f, 1f, 0.45f);
        private static readonly Color Print = new Color(0.25f, 1f, 0.45f, 0.45f);

        [SerializeField]
        private RunSkeleton skeleton;

        private readonly List<SpriteRenderer> joints = new List<SpriteRenderer>(24);
        private readonly SpriteRenderer[] rings = new SpriteRenderer[2];
        private readonly SpriteRenderer[] prints = new SpriteRenderer[Footprints];
        private readonly bool[] wasDown = new bool[2];
        private readonly bool[] placed = new bool[Footprints];
        private LineRenderer spine;
        private LineRenderer leftLeg;
        private LineRenderer rightLeg;
        private LineRenderer leftArm;
        private LineRenderer rightArm;
        private GUIStyle style;
        private GUIStyle hint;
        private int nextPrint;
        private bool visible = true;

        /// <summary>Whether it is drawn.</summary>
        public bool Visible
        {
            get => visible;
            set
            {
                visible = value;
                if (spine != null)
                {
                    SetShown(value);
                }
            }
        }

        /// <summary>Where the last foot came down, in the world (for tests).</summary>
        public Vector3 LastFootprint { get; private set; }

        /// <summary>How many times a foot has come down.</summary>
        public int Contacts { get; private set; }

        public void Bind(RunSkeleton target) => skeleton = target;

        private void Start()
        {
            spine = Chain("Spine", 4);
            leftLeg = Chain("Leg.L", 4);
            rightLeg = Chain("Leg.R", 4);
            leftArm = Chain("Arm.L", 5);
            rightArm = Chain("Arm.R", 5);
            for (var i = 0; i < JointCount; i++)
            {
                joints.Add(Disc("Joint", Dot, Joint, Order + 2));
            }
            for (var i = 0; i < 2; i++)
            {
                rings[i] = Disc("Contact", 0.2f, Ground, Order + 1);
            }
            for (var i = 0; i < Footprints; i++)
            {
                prints[i] = Disc("Footprint", 0.12f, Print, Order);
                prints[i].transform.localScale = new Vector3(0.22f, 0.06f, 1f);
                prints[i].enabled = false;
            }
            if (skeleton != null)
            {
                skeleton.Recentred += Shift;
            }
            SetShown(visible);
        }

        private void LateUpdate()
        {
            if (skeleton == null || spine == null)
            {
                return;
            }
            var pose = skeleton.Pose;
            var j = 0;
            Draw(spine, ref j, pose.hips, pose.chest, pose.neck, pose.head);
            Draw(leftLeg, ref j, pose.leftLeg.root, pose.leftLeg.middle, pose.leftLeg.end, pose.leftLeg.tip);
            Draw(rightLeg, ref j, pose.rightLeg.root, pose.rightLeg.middle, pose.rightLeg.end, pose.rightLeg.tip);
            DrawArm(leftArm, ref j, pose.chest, pose.leftArm);
            DrawArm(rightArm, ref j, pose.chest, pose.rightArm);
            Foot(0, pose.leftLeg);
            Foot(1, pose.rightLeg);
        }

        private void OnDestroy()
        {
            if (skeleton != null)
            {
                skeleton.Recentred -= Shift;
            }
        }

        private void OnGUI()
        {
            if (skeleton == null)
            {
                return;
            }
            if (!visible)
            {
                hint ??= new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    normal = { textColor = new Color(1f, 1f, 1f, 0.6f) },
                };
                GUI.Label(
                    new Rect(12, 8, 640, 24),
                    "Tab: bones and numbers   K: painted / parts / bones   Space: pause   1 / 2 / 3: speed",
                    hint
                );
                return;
            }
            style ??= new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 15,
                richText = true,
            };
            var pose = skeleton.Pose;
            var run = skeleton.Settings;
            var text =
                $"<b>Phase</b> {pose.phase:0.000}   <b>{Describe(pose.key)}</b>{(pose.InFlight ? "   (in the air)" : "")}\n"
                + $"Left foot: {(pose.leftLeg.contact ? "<color=#5f5>ground</color>" : "air")}    Right foot: {(pose.rightLeg.contact ? "<color=#5f5>ground</color>" : "air")}\n"
                + $"Speed {run.runningSpeed:0.00} u/s   Stride {run.strideLength:0.00} u   Cycle {run.cycleDuration:0.000} s   Lean {pose.lean:0.0}°\n"
                + $"Time x{skeleton.TimeScale:0.##}{(skeleton.Paused ? "   PAUSED" : "")}\n"
                + "Tab overlay   K knight   Space pause   → step (paused)   1 / 2 / 3: speed x1 / x0.25 / x0.1";
            GUI.Box(new Rect(10, 10, 640, 104), text, style);
        }

        /// <summary>The words for a key pose.</summary>
        public static string Describe(RunKey key)
        {
            switch (key)
            {
                case RunKey.ContactRight:
                    return "Contact (right)";
                case RunKey.DownRight:
                    return "Down (right)";
                case RunKey.PassingRight:
                    return "Passing (right)";
                case RunKey.FlightRight:
                    return "Flight (after right)";
                case RunKey.ContactLeft:
                    return "Contact (left)";
                case RunKey.DownLeft:
                    return "Down (left)";
                case RunKey.PassingLeft:
                    return "Passing (left)";
                default:
                    return "Flight (after left)";
            }
        }

        private void Foot(int side, RunLimb leg)
        {
            var ring = rings[side];
            ring.enabled = visible && leg.contact;
            var at = skeleton.World(leg.bearing);
            ring.transform.position = at;
            if (leg.contact && !wasDown[side])
            {
                Contacts++;
                LastFootprint = at;
                var print = prints[nextPrint];
                placed[nextPrint] = true;
                nextPrint = (nextPrint + 1) % Footprints;
                print.transform.position = at;
                print.enabled = visible;
            }
            wasDown[side] = leg.contact;
        }

        private void Draw(LineRenderer line, ref int j, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            line.SetPosition(0, skeleton.World(a));
            line.SetPosition(1, skeleton.World(b));
            line.SetPosition(2, skeleton.World(c));
            line.SetPosition(3, skeleton.World(d));
            Mark(ref j, a);
            Mark(ref j, b);
            Mark(ref j, c);
            Mark(ref j, d);
        }

        private void DrawArm(LineRenderer line, ref int j, Vector2 chest, RunLimb arm)
        {
            line.SetPosition(0, skeleton.World(chest));
            line.SetPosition(1, skeleton.World(arm.root));
            line.SetPosition(2, skeleton.World(arm.middle));
            line.SetPosition(3, skeleton.World(arm.end));
            line.SetPosition(4, skeleton.World(arm.tip));
            Mark(ref j, arm.middle);
            Mark(ref j, arm.end);
        }

        private void Mark(ref int j, Vector2 at)
        {
            if (j < joints.Count)
            {
                joints[j].transform.position = skeleton.World(at);
            }
            j++;
        }

        private void Shift(float distance)
        {
            var back = new Vector3(distance, 0f, 0f);
            foreach (var print in prints)
            {
                print.transform.position -= back;
            }
            LastFootprint -= back;
        }

        private void SetShown(bool shown)
        {
            foreach (var line in new[] { spine, leftLeg, rightLeg, leftArm, rightArm })
            {
                line.enabled = shown;
            }
            foreach (var joint in joints)
            {
                joint.enabled = shown;
            }
            for (var i = 0; i < Footprints; i++)
            {
                prints[i].enabled = shown && placed[i];
            }
            foreach (var ring in rings)
            {
                ring.enabled = false;
            }
        }

        private LineRenderer Chain(string name, int points)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = Fx.Shapes.Unlit;
            line.positionCount = points;
            line.useWorldSpace = true;
            line.widthMultiplier = Line;
            line.startColor = Bone;
            line.endColor = Bone;
            line.numCapVertices = 2;
            line.numCornerVertices = 2;
            line.sortingOrder = Order;
            return line;
        }

        private SpriteRenderer Disc(string name, float size, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localScale = new Vector3(size, size, 1f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = Fx.Shapes.Disc;
            renderer.sharedMaterial = Fx.Shapes.Unlit;
            renderer.color = color;
            renderer.sortingOrder = order;
            return renderer;
        }
    }
}
