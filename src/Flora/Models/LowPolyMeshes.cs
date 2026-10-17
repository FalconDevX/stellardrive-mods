using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Flora.Models
{
    internal static class LowPolyMeshes
    {
        internal sealed class Builder
        {
            private readonly List<Vector3> _vertices = new List<Vector3>();
            private readonly List<Vector3> _normals = new List<Vector3>();
            private readonly List<Vector2> _uvs = new List<Vector2>();

            public static readonly Vector2 UvA = new Vector2(0.02f, 0.02f);
            public static readonly Vector2 UvB = new Vector2(0.98f, 0.02f);
            public static readonly Vector2 UvC = new Vector2(0.5f, 0.98f);
            public static readonly Vector2 UvPlain = new Vector2(0.5f, 0.33f);
            private readonly List<List<int>> _submeshes = new List<List<int>>();

            public Builder(int submeshCount)
            {
                for (int i = 0; i < submeshCount; i++)
                {
                    _submeshes.Add(new List<int>());
                }
            }

            public float ProjectScale { get; set; }

            public int Count => _vertices.Count;

            public Vector3 Position(int index) => _vertices[index];

            public void SetNormal(int index, Vector3 normal) => _normals[index] = normal;

            public void Triangle(int submesh, Vector3 a, Vector3 b, Vector3 c)
            {
                if (ProjectScale > 0f)
                {
                    Vector3 n = Vector3.Cross(b - a, c - a);
                    Vector3 abs = new Vector3(Mathf.Abs(n.x), Mathf.Abs(n.y), Mathf.Abs(n.z));
                    Vector2 Project(Vector3 p) =>
                        (abs.y >= abs.x && abs.y >= abs.z ? new Vector2(p.x, p.z) : abs.x >= abs.z ? new Vector2(p.z, p.y) : new Vector2(p.x, p.y)) * ProjectScale;
                    Triangle(submesh, a, b, c, Project(a), Project(b), Project(c));
                    return;
                }
                Triangle(submesh, a, b, c, UvPlain, UvPlain, UvPlain);
            }

            public int Vertex(Vector3 position, Vector3 normal, Vector2 uv)
            {
                _vertices.Add(position);
                _normals.Add(normal);
                _uvs.Add(uv);
                return _vertices.Count - 1;
            }

            public void OutwardIndexed(int submesh, Vector3 inside, int a, int b, int c)
            {
                Vector3 pa = _vertices[a], pb = _vertices[b], pc = _vertices[c];
                Vector3 n = Vector3.Cross(pb - pa, pc - pa);
                bool flip = Vector3.Dot(n, (pa + pb + pc) / 3f - inside) < 0f;
                _submeshes[submesh].Add(a);
                _submeshes[submesh].Add(flip ? c : b);
                _submeshes[submesh].Add(flip ? b : c);
            }

            public void OutwardTriangle(int submesh, Vector3 inside, Vector3 a, Vector3 b, Vector3 c, Vector2 uvA, Vector2 uvB, Vector2 uvC)
            {
                Vector3 n = Vector3.Cross(b - a, c - a);
                if (Vector3.Dot(n, (a + b + c) / 3f - inside) < 0f)
                {
                    Triangle(submesh, a, c, b, uvA, uvC, uvB);
                }
                else
                {
                    Triangle(submesh, a, b, c, uvA, uvB, uvC);
                }
            }

            public void Triangle(int submesh, Vector3 a, Vector3 b, Vector3 c, Vector2 uvA, Vector2 uvB, Vector2 uvC)
            {
                Vector3 n = Vector3.Cross(b - a, c - a).normalized;
                int i = _vertices.Count;
                _vertices.Add(a);
                _vertices.Add(b);
                _vertices.Add(c);
                _uvs.Add(uvA);
                _uvs.Add(uvB);
                _uvs.Add(uvC);
                _normals.Add(n);
                _normals.Add(n);
                _normals.Add(n);
                _submeshes[submesh].Add(i);
                _submeshes[submesh].Add(i + 1);
                _submeshes[submesh].Add(i + 2);
            }

            public void OutwardTriangle(int submesh, Vector3 inside, Vector3 a, Vector3 b, Vector3 c)
            {
                Vector3 n = Vector3.Cross(b - a, c - a);
                if (Vector3.Dot(n, (a + b + c) / 3f - inside) < 0f)
                {
                    Triangle(submesh, a, c, b);
                }
                else
                {
                    Triangle(submesh, a, b, c);
                }
            }

            public void DoubleTriangle(int submesh, Vector3 a, Vector3 b, Vector3 c)
            {
                Triangle(submesh, a, b, c);
                Triangle(submesh, a, c, b);
            }

            public void OutlinedTriangle(int submesh, Vector3 a, Vector3 b, Vector3 c)
            {
                Triangle(submesh, a, b, c, UvA, UvB, UvC);
                Triangle(submesh, a, c, b, UvA, UvC, UvB);
            }

            public Mesh Build(string name, bool upNormals = false, Vector3? softCenter = null, float softBlend = 1f)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(_vertices);
                List<Vector3> normals = _normals;
                if (upNormals)
                {
                    normals = Enumerable.Repeat(Vector3.up, _vertices.Count).ToList();
                }
                else if (softCenter.HasValue)
                {
                    normals = _vertices.Select((v, i) => (Vector3.Lerp(_normals[i], (v - softCenter.Value).normalized, softBlend).normalized + Vector3.up * 0.6f).normalized).ToList();
                }
                mesh.SetNormals(normals);
                mesh.SetUVs(0, _uvs);
                mesh.subMeshCount = _submeshes.Count;
                for (int i = 0; i < _submeshes.Count; i++)
                {
                    mesh.SetTriangles(_submeshes[i], i);
                }
                mesh.RecalculateBounds();
                mesh.RecalculateTangents();
                mesh.UploadMeshData(false);
                return mesh;
            }
        }

        private static readonly Vector3[] IcoVertices = CreateIcoVertices();

        private static readonly int[] IcoTriangles =
        {
            0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
            1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
            3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
            4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
        };

        private static Vector3[] CreateIcoVertices()
        {
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            var v = new[]
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            for (int i = 0; i < v.Length; i++)
            {
                v[i] = v[i].normalized;
            }
            return v;
        }

        private static readonly List<(Vector3[] vertices, int[] triangles)> SmoothLevels = new List<(Vector3[], int[])>();

        private static (Vector3[] vertices, int[] triangles) SmoothSphere(int level)
        {
            if (SmoothLevels.Count == 0)
            {
                SmoothLevels.Add((IcoVertices, IcoTriangles));
            }
            while (SmoothLevels.Count <= level)
            {
                (Vector3[] baseVerts, int[] baseTris) = SmoothLevels[SmoothLevels.Count - 1];
                var verts = new List<Vector3>(baseVerts);
                var tris = new List<int>();
                var midpoints = new Dictionary<long, int>();
                int Mid(int a, int c)
                {
                    long key = a < c ? ((long)a << 32) | (uint)c : ((long)c << 32) | (uint)a;
                    if (!midpoints.TryGetValue(key, out int index))
                    {
                        index = verts.Count;
                        verts.Add(((verts[a] + verts[c]) * 0.5f).normalized);
                        midpoints[key] = index;
                    }
                    return index;
                }
                for (int i = 0; i < baseTris.Length; i += 3)
                {
                    int a = baseTris[i], b = baseTris[i + 1], c = baseTris[i + 2];
                    int ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                    tris.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                SmoothLevels.Add((verts.ToArray(), tris.ToArray()));
            }
            return SmoothLevels[level];
        }

        internal static void Blob(Builder b, int submesh, Vector3 center, Vector3 radius, System.Random rng, float jitter, bool smooth = false, int detail = 1,
            bool roundNormals = false)
        {
            (Vector3[] source, int[] triangles) = SmoothSphere(smooth ? detail : 0);
            var verts = new Vector3[source.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                float j = 1f + ((float)rng.NextDouble() * 2f - 1f) * jitter;
                Vector3 v = source[i] * j;
                verts[i] = center + Vector3.Scale(v, radius);
            }
            var inverse = new Vector3(1f / (radius.x * radius.x), 1f / (radius.y * radius.y), 1f / (radius.z * radius.z));
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int first = b.Count;
                b.OutwardTriangle(submesh, center, verts[triangles[i]], verts[triangles[i + 1]], verts[triangles[i + 2]]);
                if (!roundNormals)
                {
                    continue;
                }
                for (int k = first; k < b.Count; k++)
                {
                    b.SetNormal(k, Vector3.Scale(b.Position(k) - center, inverse).normalized);
                }
            }
        }

        internal static Mesh FlowerClump(int seed, int flowers, float height)
        {
            var rng = new System.Random(seed);
            var b = new Builder(2);
            for (int f = 0; f < flowers; f++)
            {
                float angle = (float)(rng.NextDouble() * Mathf.PI * 2f);
                float dist = f == 0 ? 0f : 0.1f + 0.35f * (float)rng.NextDouble();
                var basePos = new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
                float h = height * (0.7f + 0.5f * (float)rng.NextDouble());
                var lean = new Vector3((float)rng.NextDouble() - 0.5f, 0f, (float)rng.NextDouble() - 0.5f) * 0.15f;
                Vector3 top = basePos + Vector3.up * h + lean;

                float w = 0.02f;
                Vector3 s0 = new Vector3(w, 0, 0), s1 = new Vector3(-w * 0.5f, 0, w * 0.87f), s2 = new Vector3(-w * 0.5f, 0, -w * 0.87f);
                Vector3[] bottom = { basePos + s0, basePos + s1, basePos + s2 };
                Vector3[] upper = { top + s0, top + s1, top + s2 };
                Vector3 axisMid = (basePos + top) * 0.5f;
                for (int i = 0; i < 3; i++)
                {
                    int n = (i + 1) % 3;
                    b.OutwardTriangle(0, axisMid, bottom[i], upper[i], upper[n]);
                    b.OutwardTriangle(0, axisMid, bottom[i], upper[n], bottom[n]);
                }

                Vector3 mid = Vector3.Lerp(basePos, top, 0.4f);
                float la = (float)(rng.NextDouble() * Mathf.PI * 2f);
                var leafDir = new Vector3(Mathf.Cos(la), 0.35f, Mathf.Sin(la));
                var leafSide = Vector3.Cross(leafDir, Vector3.up).normalized * 0.03f;
                b.OutlinedTriangle(0, mid + leafSide * 1.5f, mid - leafSide * 1.5f, mid + leafDir * 0.1f);

                float size = 0.08f + 0.04f * (float)rng.NextDouble();
                FlowerHead(b, 1, top + Vector3.up * 0.005f, (Vector3.up + lean * 3f).normalized, size, (float)(rng.NextDouble() * Mathf.PI * 2f));
            }
            return b.Build($"FloraFlowers_{seed}", upNormals: true);
        }

        internal static void FlowerHead(Builder b, int submesh, Vector3 center, Vector3 facing, float size, float spin)
        {
            Vector3 reference = Mathf.Abs(facing.y) > 0.9f ? Vector3.forward : Vector3.up;
            Vector3 u0 = Vector3.Cross(facing, reference).normalized;
            Vector3 v0 = Vector3.Cross(facing, u0);
            Vector3 u = (u0 * Mathf.Cos(spin) + v0 * Mathf.Sin(spin)) * size;
            Vector3 v = Vector3.Cross(facing, u);
            Vector3 p00 = center - u - v, p10 = center + u - v, p11 = center + u + v, p01 = center - u + v;
            b.Triangle(submesh, p00, p10, p11, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f));
            b.Triangle(submesh, p00, p11, p01, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f));
        }
    }
}
