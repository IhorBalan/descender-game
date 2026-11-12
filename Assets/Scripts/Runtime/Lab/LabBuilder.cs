using System.Collections.Generic;
using Descender.Combat;
using Descender.Fx;
using Descender.Player;
using Descender.Triggers;
using Descender.World;
using Descender.World.Paradise;
using UnityEngine;

namespace Descender.Lab
{
    /// <summary>
    /// Builds the lab from <see cref="LabLayout"/>: every piece is a collider on the
    /// right layer and a <see cref="Shape"/> to see it by. Nothing is an asset and
    /// nothing is placed by hand, so the lab is the same in the game, in the editor and
    /// in a test.
    /// </summary>
    public static class LabBuilder
    {
        public static readonly Color Solid = new Color(0.22f, 0.24f, 0.3f);
        public static readonly Color Ledge = new Color(0.55f, 0.5f, 0.35f);
        public static readonly Color Unsteady = new Color(0.75f, 0.55f, 0.3f);
        public static readonly Color ClimbColor = new Color(0.45f, 0.6f, 0.35f, 0.7f);
        public static readonly Color WaterColor = new Color(0.25f, 0.5f, 0.9f, 0.3f);
        public static readonly Color WindArea = new Color(0.6f, 0.85f, 1f, 0.1f);
        public static readonly Color WindMark = new Color(0.8f, 0.95f, 1f, 0.5f);
        public static readonly Color Mark = new Color(0.6f, 0.65f, 0.75f, 0.6f);
        public static readonly Color Barrier = new Color(0.5f, 0.95f, 1f, 0.55f);
        public static readonly Color Pod = new Color(0.45f, 0.85f, 0.4f);
        public static readonly Color Hazard = new Color(0.9f, 0.15f, 0.2f);
        public static readonly Color Pad = new Color(0.95f, 0.8f, 0.25f);
        public static readonly Color DoorColor = new Color(0.5f, 0.32f, 0.2f);
        public static readonly Color DummyColor = new Color(0.85f, 0.6f, 0.75f);
        public static readonly Color BreakColor = new Color(0.7f, 0.62f, 0.5f);

        /// <summary>Units a second.</summary>
        public const float LiftSpeed = 2f;

        /// <summary>The lab's stations from the hero's tuning (the defaults when null), as objects under one root.</summary>
        public static LabRoot Build(PlayerTuning tuning = null)
        {
            var made = tuning == null;
            if (made)
            {
                tuning = ScriptableObject.CreateInstance<PlayerTuning>();
            }
            var stations = LabLayout.Make(tuning);
            if (made && Application.isPlaying)
            {
                Object.Destroy(tuning);
            }
            else if (made)
            {
                Object.DestroyImmediate(tuning);
            }
            return Build(stations);
        }

        public static LabRoot Build(List<Station> stations)
        {
            var root = new GameObject("Lab").AddComponent<LabRoot>();
            root.Init(stations);
            foreach (var station in stations)
            {
                root.Hold(station, BuildStation(root, station));
            }
            return root;
        }

        /// <summary>One station's objects under a new parent, as the layout has them: nothing taken or opened.</summary>
        public static Transform BuildStation(LabRoot root, Station station)
        {
            var parent = new GameObject("Station " + station.id).transform;
            parent.SetParent(root.transform, false);
            var byId = new Dictionary<string, GameObject>();
            var built = new List<GameObject>();
            foreach (var piece in station.pieces)
            {
                var go = Make(root, parent, piece);
                built.Add(go);
                if (!string.IsNullOrEmpty(piece.id))
                {
                    byId[piece.id] = go;
                }
            }
            // The zones' actions name other pieces, so they are made once every piece stands.
            System.Func<string, GameObject> find = id => byId.TryGetValue(id, out var found) ? found : null;
            for (var i = 0; i < built.Count; i++)
            {
                var piece = station.pieces[i];
                if (piece.kind == PieceKind.Zone)
                {
                    var actions = piece.actions != null ? piece.actions(find) : new TriggerAction[0];
                    var zone = built[i].AddComponent<TriggerZone>();
                    zone.Configure(piece.on, piece.once, piece.hide, piece.needs, actions, piece.by);
                }
                if (piece.kind == PieceKind.Breakable)
                {
                    var actions = piece.actions != null ? piece.actions(find) : new TriggerAction[0];
                    built[i].GetComponent<Breakable>().Configure(Mathf.RoundToInt(piece.value), piece.by, actions);
                }
            }
            for (var i = 0; i < built.Count; i++)
            {
                // A hidden platform that does not stay, or a hidden wind, is there but off: a trigger starts it.
                var kind = station.pieces[i].kind;
                var switched = kind == PieceKind.Crumble || kind == PieceKind.Blinker || kind == PieceKind.Wind;
                if (station.pieces[i].hidden && !switched)
                {
                    built[i].SetActive(false);
                }
            }
            return parent;
        }

        private static GameObject Make(LabRoot root, Transform parent, Piece piece)
        {
            var rect = piece.rect;
            var go = new GameObject(string.IsNullOrEmpty(piece.label) ? piece.kind.ToString() : piece.label);
            go.transform.SetParent(parent, false);
            go.transform.position = rect.center;
            switch (piece.kind)
            {
                case PieceKind.Solid:
                    go.layer = Layers.World;
                    go.AddComponent<BoxCollider2D>().size = rect.size;
                    Draw(go, ShapeKind.Square, rect.size, Solid, piece.label, 0);
                    break;
                case PieceKind.Ledge:
                    go.layer = Layers.Ledge;
                    go.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
                    var effector = go.AddComponent<PlatformEffector2D>();
                    // The hero drops through by turning the layer pair off,
                    // which an effector's own mask would override.
                    effector.useColliderMask = false;
                    effector.useOneWay = true;
                    effector.surfaceArc = 170f;
                    effector.useSideFriction = false;
                    var strip = go.AddComponent<BoxCollider2D>();
                    strip.usedByEffector = true;
                    strip.size = rect.size;
                    Draw(go, ShapeKind.Square, rect.size, Ledge, piece.label, 1);
                    break;
                case PieceKind.Mark:
                    Draw(go, ShapeKind.Square, rect.size, Mark, piece.label, -5);
                    break;
                case PieceKind.Barrier:
                    go.layer = Layers.LightBarrier;
                    go.AddComponent<BoxCollider2D>().size = rect.size;
                    Draw(go, ShapeKind.Square, rect.size, Barrier, piece.label, 2);
                    break;
                case PieceKind.Pod:
                    // The pod's trigger is its top; BouncePod squashes the child named "Visual".
                    Shape.Make(go.transform, "Visual", ShapeKind.Circle, Vector2.zero, rect.size, Pod, piece.label, 2);
                    var top = go.AddComponent<BoxCollider2D>();
                    top.size = new Vector2(rect.width, 0.6f);
                    top.offset = new Vector2(0f, rect.height / 2f);
                    go.AddComponent<BouncePod>().Configure(piece.value);
                    break;
                case PieceKind.Hazard:
                    go.layer = Layers.Hazard;
                    var spikes = go.AddComponent<BoxCollider2D>();
                    spikes.isTrigger = true;
                    spikes.size = rect.size;
                    go.AddComponent<HazardArea>();
                    Draw(go, ShapeKind.Square, rect.size, Hazard, piece.label, 2);
                    break;
                case PieceKind.Pad:
                    go.layer = Layers.Trigger;
                    var area = go.AddComponent<BoxCollider2D>();
                    area.isTrigger = true;
                    area.size = rect.size;
                    go.AddComponent<LabPad>().Init(root, Mathf.RoundToInt(piece.value));
                    Draw(go, ShapeKind.Diamond, rect.size, Pad, piece.label, 2);
                    break;
                case PieceKind.Zone:
                    // The TriggerZone itself is added by BuildStation, once its actions can be made.
                    go.AddComponent<BoxCollider2D>().size = rect.size;
                    if (piece.color.a > 0f)
                    {
                        Draw(go, piece.shape, rect.size, piece.color, piece.label, 3);
                    }
                    break;
                case PieceKind.Door:
                    go.layer = Layers.World;
                    go.AddComponent<BoxCollider2D>().size = rect.size;
                    Draw(go, ShapeKind.Square, rect.size, DoorColor, piece.label, 1);
                    go.AddComponent<Door>();
                    break;
                case PieceKind.Camera:
                    var view = go.AddComponent<CameraRig.CameraZone>();
                    view.Configure(rect.size, piece.framing, piece.value, piece.lockX, piece.lockY);
                    // Only a line along its top, so the zone does not hide the room.
                    var line = new Vector2(rect.width, 0.1f);
                    var above = new Vector2(0f, rect.height / 2f);
                    Shape.Make(go.transform, "Shape", ShapeKind.Square, above, line, Mark, piece.label, -5);
                    break;
                case PieceKind.Lift:
                    go.layer = Layers.Ledge;
                    go.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
                    var oneWay = go.AddComponent<PlatformEffector2D>();
                    oneWay.useColliderMask = false;
                    oneWay.useOneWay = true;
                    oneWay.surfaceArc = 170f;
                    oneWay.useSideFriction = false;
                    var deck = go.AddComponent<BoxCollider2D>();
                    deck.usedByEffector = true;
                    deck.size = rect.size;
                    Draw(go, ShapeKind.Square, rect.size, Ledge, piece.label, 1);
                    go.AddComponent<Mover>().Configure(piece.travel, SpeedOf(piece), piece.running);
                    break;
                case PieceKind.Platform:
                    go.layer = Layers.World;
                    go.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
                    go.AddComponent<BoxCollider2D>().size = rect.size;
                    Draw(go, ShapeKind.Square, rect.size, Solid, piece.label, 1);
                    go.AddComponent<Mover>().Configure(piece.travel, SpeedOf(piece), piece.running);
                    break;
                case PieceKind.Wind:
                    var blow = go.AddComponent<ForceZone>();
                    blow.Configure(rect.size, piece.wind, piece.gain, piece.hidden);
                    blow.Gust(piece.gustOn, piece.gustOff, piece.ahead);
                    Draw(go, ShapeKind.Square, rect.size, WindArea, piece.label, -4);
                    blow.Drift(WindMarks(go.transform, rect.size, piece.wind));
                    break;
                case PieceKind.Climb:
                    go.AddComponent<Climbable>().Configure(rect.size);
                    Draw(go, ShapeKind.Square, rect.size, ClimbColor, piece.label, -2);
                    break;
                case PieceKind.Water:
                    go.AddComponent<Water>().Configure(rect.size);
                    Draw(go, ShapeKind.Square, rect.size, WaterColor, piece.label, 4);
                    break;
                case PieceKind.Dummy:
                    // On the enemies' layer, which the hero's attacks look on and his body passes.
                    go.layer = Layers.Enemy;
                    go.AddComponent<BoxCollider2D>().size = rect.size;
                    Draw(go, ShapeKind.Circle, rect.size, DummyColor, "", 2);
                    go.AddComponent<Dummy>().Show(go.GetComponentInChildren<Shape>(), piece.label, 2);
                    break;
                case PieceKind.Breakable:
                    // Solid to him on the World layer; his attacks find the child on the Enemy layer.
                    go.layer = Layers.World;
                    go.AddComponent<BoxCollider2D>().size = rect.size;
                    Draw(go, ShapeKind.Square, rect.size, BreakColor, piece.label, 1);
                    var struck = new GameObject("Strike") { layer = Layers.Enemy };
                    struck.transform.SetParent(go.transform, false);
                    struck.AddComponent<BoxCollider2D>().size = rect.size;
                    go.AddComponent<Breakable>();
                    break;
                case PieceKind.Striker:
                    go.layer = Layers.Enemy;
                    go.AddComponent<BoxCollider2D>().size = rect.size;
                    Draw(go, ShapeKind.Triangle, rect.size, Hazard, "", 2);
                    go.AddComponent<Dummy>().Show(go.GetComponentInChildren<Shape>(), piece.label, 2);
                    // Its reach, drawn faintly behind it.
                    var faint = new Color(Hazard.r, Hazard.g, Hazard.b, 0.12f);
                    Shape.Make(go.transform, "Reach", ShapeKind.Square, Vector2.zero, piece.reach, faint, "", -4);
                    go.AddComponent<Striker>().Configure(piece.reach, piece.value, 1);
                    break;
                case PieceKind.Crumble:
                    go.layer = Layers.World;
                    go.AddComponent<BoxCollider2D>().size = rect.size;
                    Draw(go, ShapeKind.Square, rect.size, Unsteady, piece.label, 1);
                    go.AddComponent<Crumble>().Configure(piece.solidFor, piece.goneFor, piece.hidden);
                    break;
                case PieceKind.Blinker:
                    go.layer = Layers.World;
                    go.AddComponent<BoxCollider2D>().size = rect.size;
                    Draw(go, ShapeKind.Square, rect.size, Unsteady, piece.label, 1);
                    var blinker = go.AddComponent<Blinker>();
                    blinker.Configure(piece.solidFor, piece.goneFor, piece.warnFor, piece.ahead, piece.hidden);
                    break;
            }
            return go;
        }

        /// <summary>Small triangles over a wind's area, pointing where it blows; the zone drifts them along.</summary>
        private static Transform[] WindMarks(Transform parent, Vector2 area, Vector2 wind)
        {
            var marks = new List<Transform>();
            var turn = Quaternion.FromToRotation(Vector3.up, wind == Vector2.zero ? Vector2.up : wind.normalized);
            var size = new Vector2(0.3f, 0.45f);
            var columns = Mathf.Max(Mathf.RoundToInt(area.x / 2.5f), 1);
            var rows = Mathf.Max(Mathf.RoundToInt(area.y / 2.5f), 1);
            for (var row = 0; row < rows; row++)
            {
                for (var column = 0; column < columns; column++)
                {
                    // Every other row half a step along, so they do not stand in lines.
                    var along = (column + (row % 2 == 0 ? 0.25f : 0.75f)) / columns;
                    var at = new Vector2((along - 0.5f) * area.x, ((row + 0.5f) / rows - 0.5f) * area.y);
                    var mark = Shape.Make(parent, "Drift", ShapeKind.Triangle, at, size, WindMark, "", -3);
                    mark.transform.localRotation = turn;
                    marks.Add(mark.transform);
                }
            }
            return marks.ToArray();
        }

        /// <summary>A moving piece's speed: its own, or the lab's usual one.</summary>
        private static float SpeedOf(Piece piece) => piece.speed > 0f ? piece.speed : LiftSpeed;

        private static void Draw(GameObject go, ShapeKind kind, Vector2 size, Color color, string label, int order)
        {
            Shape.Make(go.transform, "Shape", kind, Vector2.zero, size, color, label, order);
        }
    }
}
