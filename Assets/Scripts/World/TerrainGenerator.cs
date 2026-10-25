using UnityEngine;

namespace SnowBoarder
{
    /// <summary>
    /// Builds the slope from a list of surface points:
    ///  - a filled mesh for the snow body (vertex-colour gradient),
    ///  - a textured strip along the surface using the lab's snow tile,
    ///  - an EdgeCollider2D with a low friction material so the boarder slides.
    /// Runs in edit mode too so levels are visible in the Scene view.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(EdgeCollider2D))]
    public class TerrainGenerator : MonoBehaviour
    {
        [SerializeField] Vector2[] points = new Vector2[0];
        [SerializeField] Material fillMaterial;
        [SerializeField] Material edgeMaterial;
        [SerializeField] float depth = 40f;
        [SerializeField] float gradientDepth = 9f;
        [SerializeField] float edgeAbove = 0.35f;
        [SerializeField] float edgeBelow = 0.75f;
        [SerializeField] float edgeTileLength = 4.5f;
        // A zero-thickness edge has no interior: a body that ends up with the line inside it can
        // be resolved out of either side, and coming out of the bottom means falling through the
        // world. A radius gives the surface real volume ("a larger collider with rounded convex
        // corners", per Unity's Edge Collider 2D reference) so contacts get pushed back up. 0.12
        // is well under the boarder's 0.3 collider thickness and invisible at this scale.
        [SerializeField] float edgeThickness = 0.12f;
        [SerializeField] Color topColor = new Color(0.85f, 0.97f, 1f);
        [SerializeField] Color bottomColor = new Color(0.25f, 0.45f, 0.75f);
        [SerializeField] int sortingOrder = 0;

        public Vector2[] Points => points;

        GameObject fillObj, edgeObj;

        public void SetPoints(Vector2[] newPoints)
        {
            points = newPoints;
            Rebuild();
        }

        void OnEnable() => Rebuild();

        void OnDisable()
        {
            DestroySafe(fillObj);
            DestroySafe(edgeObj);
        }

        void OnValidate()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += () => { if (this) Rebuild(); };
#endif
        }

        public void Rebuild()
        {
            if (points == null || points.Length < 2) return;

            var col = GetComponent<EdgeCollider2D>();
            col.edgeRadius = Mathf.Max(0f, edgeThickness);
            col.points = points;
            if (!GetComponent<Ground>()) gameObject.AddComponent<Ground>();

            fillObj = GetOrCreateChild("Fill (generated)", fillObj);
            edgeObj = GetOrCreateChild("SnowEdge (generated)", edgeObj);
            BuildFill(fillObj);
            BuildEdge(edgeObj);
        }

        GameObject GetOrCreateChild(string name, GameObject existing)
        {
            if (existing) return existing;
            var t = transform.Find(name);
            if (t) return t.gameObject;
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave | HideFlags.NotEditable };
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>();
            return go;
        }

        void BuildFill(GameObject go)
        {
            float minY = float.MaxValue;
            foreach (var p in points) minY = Mathf.Min(minY, p.y);
            float bottom = minY - depth;

            // three rows: surface -> a few metres down (depth gradient) -> bottom
            int n = points.Length;
            var verts = new Vector3[n * 3];
            var colors = new Color[n * 3];
            var uvs = new Vector2[n * 3];
            var tris = new int[(n - 1) * 12];
            Color midColor = Color.Lerp(topColor, bottomColor, 0.75f);
            for (int i = 0; i < n; i++)
            {
                Vector2 p = points[i];
                float y0 = p.y - edgeBelow * 0.5f;
                float y1 = Mathf.Max(bottom, y0 - gradientDepth);
                verts[i * 3] = new Vector3(p.x, y0, 0);
                verts[i * 3 + 1] = new Vector3(p.x, y1, 0);
                verts[i * 3 + 2] = new Vector3(p.x, bottom, 0);
                colors[i * 3] = topColor;
                colors[i * 3 + 1] = midColor;
                colors[i * 3 + 2] = bottomColor;
                uvs[i * 3] = new Vector2(p.x / 8f, y0 / 8f);
                uvs[i * 3 + 1] = new Vector2(p.x / 8f, y1 / 8f);
                uvs[i * 3 + 2] = new Vector2(p.x / 8f, bottom / 8f);
            }
            for (int i = 0; i < n - 1; i++)
            {
                int t = i * 12;
                for (int row = 0; row < 2; row++)
                {
                    int a = i * 3 + row, b = a + 3, o = t + row * 6;
                    tris[o] = a; tris[o + 1] = b; tris[o + 2] = a + 1;
                    tris[o + 3] = a + 1; tris[o + 4] = b; tris[o + 5] = b + 1;
                }
            }
            ApplyMesh(go, verts, uvs, colors, tris, fillMaterial, sortingOrder);
        }

        void BuildEdge(GameObject go)
        {
            int n = points.Length;
            var verts = new Vector3[n * 2];
            var uvs = new Vector2[n * 2];
            var colors = new Color[n * 2];
            var tris = new int[(n - 1) * 6];
            float dist = 0f;
            for (int i = 0; i < n; i++)
            {
                Vector2 prev = points[Mathf.Max(0, i - 1)];
                Vector2 next = points[Mathf.Min(n - 1, i + 1)];
                Vector2 dir = (next - prev).normalized;
                Vector2 normal = new Vector2(-dir.y, dir.x);
                if (i > 0) dist += Vector2.Distance(points[i - 1], points[i]);

                verts[i * 2] = points[i] + normal * edgeAbove;
                verts[i * 2 + 1] = points[i] - normal * edgeBelow;
                float u = dist / edgeTileLength;
                uvs[i * 2] = new Vector2(u, 1f);
                uvs[i * 2 + 1] = new Vector2(u, 0f);
                colors[i * 2] = colors[i * 2 + 1] = Color.white;
            }
            for (int i = 0; i < n - 1; i++)
            {
                int a = i * 2, t = i * 6;
                tris[t] = a; tris[t + 1] = a + 2; tris[t + 2] = a + 1;
                tris[t + 3] = a + 1; tris[t + 4] = a + 2; tris[t + 5] = a + 3;
            }
            ApplyMesh(go, verts, uvs, colors, tris, edgeMaterial, sortingOrder + 1);
        }

        static void ApplyMesh(GameObject go, Vector3[] v, Vector2[] uv, Color[] c, int[] t, Material mat, int order)
        {
            var mf = go.GetComponent<MeshFilter>();
            var mesh = mf.sharedMesh;
            if (!mesh)
            {
                mesh = new Mesh { name = go.name, hideFlags = HideFlags.DontSave };
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mf.sharedMesh = mesh;
            }
            mesh.Clear();
            mesh.vertices = v;
            mesh.uv = uv;
            mesh.colors = c;
            mesh.triangles = t;
            mesh.RecalculateBounds();
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.sortingOrder = order;
        }

        static void DestroySafe(Object o)
        {
            if (!o) return;
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }

        /// <summary>Surface height at world x (linear interpolation).</summary>
        public float HeightAt(float x)
        {
            if (points.Length == 0) return 0f;
            x -= transform.position.x;
            if (x <= points[0].x) return points[0].y + transform.position.y;
            for (int i = 1; i < points.Length; i++)
            {
                if (x <= points[i].x)
                {
                    float k = Mathf.InverseLerp(points[i - 1].x, points[i].x, x);
                    return Mathf.Lerp(points[i - 1].y, points[i].y, k) + transform.position.y;
                }
            }
            return points[points.Length - 1].y + transform.position.y;
        }

        /// <summary>Surface angle (degrees) at world x.</summary>
        public float AngleAt(float x)
        {
            float h0 = HeightAt(x - 0.5f), h1 = HeightAt(x + 0.5f);
            return Mathf.Atan2(h1 - h0, 1f) * Mathf.Rad2Deg;
        }
    }
}
