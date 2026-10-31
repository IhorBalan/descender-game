using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Descender.UI
{
    /// <summary>
    /// Draws every 2D collider over the game, coloured by what owns it: the hero
    /// green, enemies red, pickups and triggers blue, the walkable world (floors,
    /// ledges, light barriers) black and hazards orange, each by its layer. The
    /// console's `show_boxes on` makes one of these; `show_boxes off` destroys it.
    /// Colliders are gathered a few times a second and drawn with GL lines after the
    /// camera renders, with a faint fill inside boxes; a disabled collider is skipped,
    /// as the physics engine skips it. Port of scripts/collision_boxes.gd.
    /// </summary>
    public sealed class CollisionBoxes : MonoBehaviour
    {
        public static readonly Color PlayerColor = new Color(0.2f, 1f, 0.3f);
        public static readonly Color EnemyColor = new Color(1f, 0.2f, 0.2f);
        public static readonly Color ItemColor = new Color(0.3f, 0.55f, 1f);
        public static readonly Color WorldColor = Color.black;
        public static readonly Color HazardColor = new Color(1f, 0.6f, 0.1f);

        /// <summary>Colliders on a layer the categories do not know.</summary>
        public static readonly Color OtherColor = new Color(0.8f, 0.8f, 0.8f);
        public const float FillAlpha = 0.16f;
        public const int ArcSegments = 20;

        /// <summary>How often the colliders in the scene are gathered again, in seconds.</summary>
        public const float GatherEvery = 0.5f;

        private readonly List<Collider2D> colliders = new List<Collider2D>();
        private readonly List<Vector2> points = new List<Vector2>(64);
        private Material material;
        private float gatherLeft;
        private int playerLayer;
        private int enemyLayer;
        private int worldLayer;
        private int ledgeLayer;
        private int barrierLayer;
        private int hazardLayer;
        private int pickupLayer;
        private int triggerLayer;

        /// <summary>How many colliders are being tracked, for the console.</summary>
        public int ShapeCount => colliders.Count;

        private void Awake()
        {
            material = new Material(Shader.Find("Hidden/Internal-Colored")) { hideFlags = HideFlags.HideAndDontSave };
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_Cull", (int)CullMode.Off);
            material.SetInt("_ZWrite", 0);
            material.SetInt("_ZTest", (int)CompareFunction.Always);
            playerLayer = LayerMask.NameToLayer("Player");
            enemyLayer = LayerMask.NameToLayer("Enemy");
            worldLayer = LayerMask.NameToLayer("World");
            ledgeLayer = LayerMask.NameToLayer("Ledge");
            barrierLayer = LayerMask.NameToLayer("LightBarrier");
            hazardLayer = LayerMask.NameToLayer("Hazard");
            pickupLayer = LayerMask.NameToLayer("Pickup");
            triggerLayer = LayerMask.NameToLayer("Trigger");
            Gather();
        }

        private void OnEnable()
        {
            RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
        }

        private void OnDisable()
        {
            RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
        }

        private void OnDestroy()
        {
            if (material != null)
            {
                Destroy(material);
            }
        }

        private void Update()
        {
            // Unscaled: the console stops time while it is open, and the boxes keep following.
            gatherLeft -= Time.unscaledDeltaTime;
            if (gatherLeft <= 0f)
            {
                Gather();
            }
        }

        /// <summary>Finds every collider in the loaded scenes.</summary>
        public void Gather()
        {
            gatherLeft = GatherEvery;
            colliders.Clear();
            colliders.AddRange(FindObjectsByType<Collider2D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));
        }

        /// <summary>The colour for a collider by the layer it sits on.</summary>
        public Color ColorFor(Collider2D collider)
        {
            var layer = collider.gameObject.layer;
            if (layer == playerLayer)
            {
                return PlayerColor;
            }
            if (layer == enemyLayer)
            {
                return EnemyColor;
            }
            if (layer == hazardLayer)
            {
                return HazardColor;
            }
            if (layer == worldLayer || layer == ledgeLayer || layer == barrierLayer)
            {
                return WorldColor;
            }
            if (layer == pickupLayer || layer == triggerLayer || collider.isTrigger)
            {
                return ItemColor;
            }
            return OtherColor;
        }

        private void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (camera != Camera.main || material == null)
            {
                return;
            }
            var view = camera.orthographic
                ? ViewBounds(camera)
                : new Bounds(camera.transform.position, Vector3.one * 1000f);
            material.SetPass(0);
            GL.PushMatrix();
            GL.LoadProjectionMatrix(camera.projectionMatrix);
            GL.modelview = camera.worldToCameraMatrix;
            for (var i = colliders.Count - 1; i >= 0; i--)
            {
                var collider = colliders[i];
                if (collider == null)
                {
                    colliders.RemoveAt(i);
                    continue;
                }
                if (!collider.enabled || !collider.gameObject.activeInHierarchy || !view.Intersects(collider.bounds))
                {
                    continue;
                }
                Draw(collider, ColorFor(collider));
            }
            GL.PopMatrix();
        }

        private static Bounds ViewBounds(Camera camera)
        {
            var height = camera.orthographicSize * 2f;
            var size = new Vector3(height * camera.aspect + 2f, height + 2f, 1000f);
            return new Bounds(camera.transform.position, size);
        }

        private void Draw(Collider2D collider, Color color)
        {
            var fill = new Color(color.r, color.g, color.b, FillAlpha);
            var tr = collider.transform;
            switch (collider)
            {
                case BoxCollider2D box:
                    var half = box.size / 2f;
                    points.Clear();
                    points.Add(tr.TransformPoint(box.offset + new Vector2(-half.x, -half.y)));
                    points.Add(tr.TransformPoint(box.offset + new Vector2(half.x, -half.y)));
                    points.Add(tr.TransformPoint(box.offset + new Vector2(half.x, half.y)));
                    points.Add(tr.TransformPoint(box.offset + new Vector2(-half.x, half.y)));
                    Polygon(points, color, fill, true);
                    break;
                case CircleCollider2D circle:
                    points.Clear();
                    var scale = Mathf.Max(Mathf.Abs(tr.lossyScale.x), Mathf.Abs(tr.lossyScale.y));
                    var center = (Vector2)tr.TransformPoint(circle.offset);
                    for (var i = 0; i < ArcSegments; i++)
                    {
                        var angle = Mathf.PI * 2f * i / ArcSegments;
                        points.Add(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * circle.radius * scale);
                    }
                    Polygon(points, color, fill, true);
                    break;
                case CapsuleCollider2D capsule:
                    points.Clear();
                    foreach (var point in CapsulePoints(capsule.size, capsule.direction))
                    {
                        points.Add(tr.TransformPoint(capsule.offset + point));
                    }
                    Polygon(points, color, fill, true);
                    break;
                case PolygonCollider2D polygon:
                    for (var p = 0; p < polygon.pathCount; p++)
                    {
                        points.Clear();
                        foreach (var point in polygon.GetPath(p))
                        {
                            points.Add(tr.TransformPoint(polygon.offset + point));
                        }
                        Polygon(points, color, fill, points.Count >= 3);
                    }
                    break;
                case EdgeCollider2D edge:
                    points.Clear();
                    foreach (var point in edge.points)
                    {
                        points.Add(tr.TransformPoint(edge.offset + point));
                    }
                    Outline(points, color, false);
                    break;
                case CompositeCollider2D composite:
                    for (var p = 0; p < composite.pathCount; p++)
                    {
                        var count = composite.GetPathPointCount(p);
                        var path = new Vector2[count];
                        composite.GetPath(p, path);
                        points.Clear();
                        foreach (var point in path)
                        {
                            points.Add(tr.TransformPoint(point));
                        }
                        Outline(points, color, true);
                    }
                    break;
                default:
                    // A tilemap collider without a composite, or an unknown shape: its bounds.
                    var bounds = collider.bounds;
                    points.Clear();
                    points.Add(new Vector2(bounds.min.x, bounds.min.y));
                    points.Add(new Vector2(bounds.max.x, bounds.min.y));
                    points.Add(new Vector2(bounds.max.x, bounds.max.y));
                    points.Add(new Vector2(bounds.min.x, bounds.max.y));
                    Outline(points, color, true);
                    break;
            }
        }

        /// <summary>The outline of a capsule: two half circles joined by straight sides.</summary>
        public static List<Vector2> CapsulePoints(Vector2 size, CapsuleDirection2D direction)
        {
            var result = new List<Vector2>();
            var vertical = direction == CapsuleDirection2D.Vertical;
            var radius = (vertical ? size.x : size.y) / 2f;
            var half = Mathf.Max((vertical ? size.y : size.x) / 2f - radius, 0f);
            var steps = ArcSegments / 2;
            for (var i = 0; i <= steps; i++)
            {
                var angle = Mathf.PI + Mathf.PI * i / steps;
                var arc = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                result.Add(vertical ? new Vector2(arc.x, arc.y - half) : new Vector2(arc.y - half, arc.x));
            }
            for (var i = 0; i <= steps; i++)
            {
                var angle = Mathf.PI * i / steps;
                var arc = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                result.Add(vertical ? new Vector2(arc.x, arc.y + half) : new Vector2(arc.y + half, arc.x));
            }
            return result;
        }

        private static void Polygon(List<Vector2> shape, Color color, Color fill, bool filled)
        {
            if (shape.Count < 2)
            {
                return;
            }
            if (filled && shape.Count >= 3)
            {
                GL.Begin(GL.TRIANGLES);
                GL.Color(fill);
                for (var i = 1; i < shape.Count - 1; i++)
                {
                    GL.Vertex3(shape[0].x, shape[0].y, 0f);
                    GL.Vertex3(shape[i].x, shape[i].y, 0f);
                    GL.Vertex3(shape[i + 1].x, shape[i + 1].y, 0f);
                }
                GL.End();
            }
            Outline(shape, color, true);
        }

        private static void Outline(List<Vector2> shape, Color color, bool closed)
        {
            if (shape.Count < 2)
            {
                return;
            }
            GL.Begin(GL.LINES);
            GL.Color(color);
            var count = closed ? shape.Count : shape.Count - 1;
            for (var i = 0; i < count; i++)
            {
                var a = shape[i];
                var b = shape[(i + 1) % shape.Count];
                GL.Vertex3(a.x, a.y, 0f);
                GL.Vertex3(b.x, b.y, 0f);
            }
            GL.End();
        }
    }
}
