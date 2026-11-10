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
        public static readonly Color Mark = new Color(0.6f, 0.65f, 0.75f, 0.6f);
        public static readonly Color Barrier = new Color(0.5f, 0.95f, 1f, 0.55f);
        public static readonly Color Pod = new Color(0.45f, 0.85f, 0.4f);
        public static readonly Color Hazard = new Color(0.9f, 0.15f, 0.2f);
        public static readonly Color Pad = new Color(0.95f, 0.8f, 0.25f);
        public static readonly Color DoorColor = new Color(0.5f, 0.32f, 0.2f);

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
                    zone.Configure(piece.on, piece.once, piece.hide, piece.needs, actions);
                }
            }
            for (var i = 0; i < built.Count; i++)
            {
                if (station.pieces[i].hidden)
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
                case PieceKind.Lift:
                    go.layer = Layers.Ledge;
                    go.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
                    var oneWay = go.AddComponent<PlatformEffector2D>();
                    oneWay.useOneWay = true;
                    oneWay.surfaceArc = 170f;
                    oneWay.useSideFriction = false;
                    var deck = go.AddComponent<BoxCollider2D>();
                    deck.usedByEffector = true;
                    deck.size = rect.size;
                    Draw(go, ShapeKind.Square, rect.size, Ledge, piece.label, 1);
                    go.AddComponent<Mover>().Configure(piece.travel, LiftSpeed);
                    break;
            }
            return go;
        }

        private static void Draw(GameObject go, ShapeKind kind, Vector2 size, Color color, string label, int order)
        {
            Shape.Make(go.transform, "Shape", kind, Vector2.zero, size, color, label, order);
        }
    }
}
