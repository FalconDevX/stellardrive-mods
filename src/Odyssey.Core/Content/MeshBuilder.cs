using System.Collections.Generic;
using UnityEngine;

namespace Odyssey.Content
{
    internal sealed class MeshBuilder
    {
        private static readonly string[] Cells =
        {
            "yellow", "yellow_dark", "dark", "grey", "light", "orange", "steel", "screen", "black", "rubber", "olive", "white", "cyan", "blue", "green", "red",
        };

        private const int Grid = 4;
        private const float Margin = 26f / 256f;
        private const float Inset = 1.5f / 256f;
        private const float Border = 0.03f;

        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<Vector3> _normals = new List<Vector3>();
        private readonly List<Vector2> _uvs = new List<Vector2>();
        private readonly List<int> _triangles = new List<int>();
        private bool _atlas;

        private static float Slice(float t, float length)
        {
            if (length < Border * 2.5f) return Margin + (1f - 2f * Margin) * Mathf.Clamp01(t / length);
            if (t <= Border) return Margin * t / Border;
            if (t >= length - Border) return 1f - Margin * (length - t) / Border;
            return Margin + (1f - 2f * Margin) * (t - Border) / (length - 2f * Border);
        }

        private static float[] Cuts(float length) =>
            length < Border * 2.5f ? new[] { 0f, length } : new[] { 0f, Border, length - Border, length };

        private static Vector2 CellUv(string color, float fu, float fv)
        {
            int cell = Mathf.Max(0, System.Array.IndexOf(Cells, color));
            float cx = cell % Grid, cy = cell / Grid;
            fu = Inset + fu * (1f - 2f * Inset);
            fv = Inset + fv * (1f - 2f * Inset);
            return new Vector2((cx + fu) / Grid, 1f - (cy + 1f - fv) / Grid);
        }

        private void Add(Vector3 position, Vector3 normal, Vector2 uv)
        {
            _vertices.Add(position);
            _normals.Add(normal);
            _uvs.Add(uv);
        }

        internal MeshBuilder Box(Vector3 center, Vector3 size, Quaternion rotation = default) => Box(center, size, null, rotation);

        internal MeshBuilder Box(Vector3 center, Vector3 size, string color, Quaternion rotation = default)
        {
            if (rotation == default) rotation = Quaternion.identity;
            if (color != null) _atlas = true;
            Vector3 h = size * 0.5f;
            Vector3[] axes = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (Vector3 n in axes)
            {
                Vector3 u = Mathf.Abs(n.y) > 0.5f ? Vector3.right : Vector3.up;
                Vector3 v = Vector3.Cross(n, u);
                Vector3 faceCenter = Vector3.Scale(n, h);
                Vector3 du = Vector3.Scale(u, h), dv = Vector3.Scale(v, h);
                if (color == null)
                {
                    int start = _vertices.Count;
                    Vector3[] corners = { faceCenter - du - dv, faceCenter + du - dv, faceCenter + du + dv, faceCenter - du + dv };
                    for (int i = 0; i < 4; i++)
                    {
                        Add(center + rotation * corners[i], rotation * n, new Vector2(Vector3.Dot(corners[i], u) + 0.5f, Vector3.Dot(corners[i], v) + 0.5f));
                    }
                    _triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
                    continue;
                }
                float lu = du.magnitude * 2f, lv = dv.magnitude * 2f;
                Vector3 origin = faceCenter - du - dv;
                float[] cu = Cuts(lu), cv = Cuts(lv);
                for (int i = 0; i + 1 < cu.Length; i++)
                {
                    for (int j = 0; j + 1 < cv.Length; j++)
                    {
                        int start = _vertices.Count;
                        foreach (var (a, b) in new[] { (cu[i], cv[j]), (cu[i + 1], cv[j]), (cu[i + 1], cv[j + 1]), (cu[i], cv[j + 1]) })
                        {
                            Add(center + rotation * (origin + u * a + v * b), rotation * n, CellUv(color, Slice(a, lu), Slice(b, lv)));
                        }
                        _triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
                    }
                }
            }
            return this;
        }

        internal MeshBuilder Prism(IList<Vector2> outline, float x0, float x1, string color)
        {
            _atlas = true;
            float area = 0f;
            for (int i = 0; i < outline.Count; i++)
            {
                Vector2 p = outline[i], q = outline[(i + 1) % outline.Count];
                area += p.x * q.y - q.x * p.y;
            }
            var poly = new List<Vector2>(outline);
            if (area < 0f) poly.Reverse();
            float zMin = float.MaxValue, zMax = float.MinValue, yMin = float.MaxValue, yMax = float.MinValue;
            foreach (Vector2 p in poly)
            {
                zMin = Mathf.Min(zMin, p.x);
                zMax = Mathf.Max(zMax, p.x);
                yMin = Mathf.Min(yMin, p.y);
                yMax = Mathf.Max(yMax, p.y);
            }
            float Fu(float z) => Margin + (1f - 2f * Margin) * (z - zMin) / Mathf.Max(1e-4f, zMax - zMin);
            float Fv(float y) => Margin + (1f - 2f * Margin) * (y - yMin) / Mathf.Max(1e-4f, yMax - yMin);
            foreach (float x in new[] { x0, x1 })
            {
                var n = new Vector3(x == x0 ? -1f : 1f, 0f, 0f);
                int start = _vertices.Count;
                foreach (Vector2 p in poly) Add(new Vector3(x, p.y, p.x), n, CellUv(color, Fu(p.x), Fv(p.y)));
                for (int k = 1; k + 1 < poly.Count; k++) Triangle(start, start + k, start + k + 1, n);
            }
            float thickness = Mathf.Abs(x1 - x0);
            for (int i = 0; i < poly.Count; i++)
            {
                Vector2 a = poly[i], b = poly[(i + 1) % poly.Count], d = b - a;
                if (d.sqrMagnitude < 1e-10f) continue;
                var n = new Vector3(0f, -d.x, d.y).normalized;
                float length = d.magnitude;
                float[] cu = Cuts(length), cv = Cuts(thickness);
                for (int u = 0; u + 1 < cu.Length; u++)
                {
                    for (int v = 0; v + 1 < cv.Length; v++)
                    {
                        int start = _vertices.Count;
                        foreach (var (s, t) in new[] { (cu[u], cv[v]), (cu[u + 1], cv[v]), (cu[u + 1], cv[v + 1]), (cu[u], cv[v + 1]) })
                        {
                            Vector2 p = a + d * (s / length);
                            Add(new Vector3(Mathf.Min(x0, x1) + t, p.y, p.x), n, CellUv(color, Slice(s, length), Slice(t, thickness)));
                        }
                        Triangle(start, start + 1, start + 2, n);
                        Triangle(start, start + 2, start + 3, n);
                    }
                }
            }
            return this;
        }

        private void Triangle(int a, int b, int c, Vector3 n)
        {
            bool flip = Vector3.Dot(Vector3.Cross(_vertices[b] - _vertices[a], _vertices[c] - _vertices[a]), n) < 0f;
            _triangles.Add(a);
            _triangles.Add(flip ? c : b);
            _triangles.Add(flip ? b : c);
        }

        internal MeshBuilder Cylinder(Vector3 from, Vector3 to, float radius, int segments = 16) => Cylinder(from, to, radius, null, segments);

        internal MeshBuilder Cylinder(Vector3 from, Vector3 to, float radius, string color, int segments = 16)
        {
            if (color != null) _atlas = true;
            Vector3 axis = (to - from).normalized;
            Vector3 side = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 side2 = Vector3.Cross(axis, side);
            float length = (to - from).magnitude;
            float[] cuts = color == null ? new[] { 0f, length } : Cuts(length);
            for (int c = 0; c + 1 < cuts.Length; c++)
            {
                int start = _vertices.Count;
                for (int i = 0; i <= segments; i++)
                {
                    float a = i * Mathf.PI * 2f / segments;
                    Vector3 n = side * Mathf.Cos(a) + side2 * Mathf.Sin(a);
                    float around = (float)i / segments;
                    float fu = Margin + (1f - 2f * Margin) * Mathf.Abs(around * 2f - 1f);
                    Add(from + axis * cuts[c] + n * radius, n, color == null ? new Vector2(around, cuts[c]) : CellUv(color, fu, Slice(cuts[c], length)));
                    Add(from + axis * cuts[c + 1] + n * radius, n, color == null ? new Vector2(around, cuts[c + 1]) : CellUv(color, fu, Slice(cuts[c + 1], length)));
                }
                for (int i = 0; i < segments; i++)
                {
                    int a = start + i * 2;
                    _triangles.AddRange(new[] { a, a + 3, a + 1, a, a + 2, a + 3 });
                }
            }
            Cap(from, -axis, side, side2, radius, segments, color);
            Cap(to, axis, side, side2, radius, segments, color);
            return this;
        }

        private void Cap(Vector3 center, Vector3 normal, Vector3 side, Vector3 side2, float radius, int segments, string color)
        {
            bool flip = Vector3.Dot(Vector3.Cross(side, side2), normal) > 0f;
            var rings = new List<(float radius, float uv)>();
            if (color != null && radius > Border * 1.5f) rings.Add((radius - Border, 0.5f - Margin));
            rings.Add((radius, 0.5f));
            int c = _vertices.Count;
            Add(center, normal, color == null ? new Vector2(0.5f, 0.5f) : CellUv(color, 0.5f, 0.5f));
            int previous = -1;
            foreach (var (r, extent) in rings)
            {
                int ring = _vertices.Count;
                for (int i = 0; i <= segments; i++)
                {
                    float a = i * Mathf.PI * 2f / segments;
                    float cos = Mathf.Cos(a), sin = Mathf.Sin(a);
                    float square = color == null ? 1f : 1f / Mathf.Max(Mathf.Abs(cos), Mathf.Abs(sin));
                    float fu = 0.5f + extent * square * cos, fv = 0.5f + extent * square * sin;
                    Add(center + (side * cos + side2 * sin) * r, normal, color == null ? new Vector2(fu, fv) : CellUv(color, fu, fv));
                }
                for (int i = 0; i < segments; i++)
                {
                    int a1 = ring + i, b1 = ring + i + 1;
                    if (previous < 0)
                    {
                        if (flip) _triangles.AddRange(new[] { c, a1, b1 });
                        else _triangles.AddRange(new[] { c, b1, a1 });
                    }
                    else
                    {
                        int a0 = previous + i, b0 = previous + i + 1;
                        if (flip) _triangles.AddRange(new[] { a0, a1, b1, a0, b1, b0 });
                        else _triangles.AddRange(new[] { a0, b1, a1, a0, b0, b1 });
                    }
                }
                previous = ring;
            }
        }

        internal MeshBuilder Lathe(Vector3 origin, Vector3 axis, IList<Vector2> profile, string color, int segments = 24, bool inward = false)
        {
            _atlas = true;
            axis.Normalize();
            Vector3 side = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 side2 = Vector3.Cross(axis, side);
            int count = profile.Count;
            var normals = new Vector2[count];
            for (int k = 0; k < count; k++)
            {
                Vector2 prev = profile[Mathf.Max(0, k - 1)], next = profile[Mathf.Min(count - 1, k + 1)];
                Vector2 t = next - prev;
                Vector2 n = new Vector2(t.y, -t.x);
                normals[k] = (n.sqrMagnitude > 1e-8f ? n.normalized : Vector2.right) * (inward ? -1f : 1f);
            }
            float total = 0f;
            var along = new float[count];
            for (int k = 1; k < count; k++)
            {
                total += (profile[k] - profile[k - 1]).magnitude;
                along[k] = total;
            }
            int start = _vertices.Count;
            for (int k = 0; k < count; k++)
            {
                float fv = Margin + (1f - 2f * Margin) * (total > 0f ? along[k] / total : 0f);
                for (int i = 0; i <= segments; i++)
                {
                    float a = i * Mathf.PI * 2f / segments;
                    Vector3 radial = side * Mathf.Cos(a) + side2 * Mathf.Sin(a);
                    float fu = Margin + (1f - 2f * Margin) * Mathf.Abs((float)i / segments * 2f - 1f);
                    Vector3 normal = (radial * normals[k].x + axis * normals[k].y).normalized;
                    Add(origin + radial * profile[k].x + axis * profile[k].y, normal, CellUv(color, fu, fv));
                }
            }
            for (int k = 0; k + 1 < count; k++)
            {
                for (int i = 0; i < segments; i++)
                {
                    int a0 = start + k * (segments + 1) + i, b0 = a0 + 1;
                    int a1 = a0 + segments + 1, b1 = a1 + 1;
                    if (inward) _triangles.AddRange(new[] { a0, a1, b1, a0, b1, b0 });
                    else _triangles.AddRange(new[] { a0, b1, a1, a0, b0, b1 });
                }
            }
            return this;
        }

        internal MeshBuilder Sphere(Vector3 center, float radius, string color, int segments = 20)
        {
            int rings = Mathf.Max(6, segments / 2);
            var profile = new List<Vector2>();
            for (int k = 0; k <= rings; k++)
            {
                float a = -Mathf.PI * 0.5f + Mathf.PI * k / rings;
                profile.Add(new Vector2(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius));
            }
            return Lathe(center, Vector3.up, profile, color, segments);
        }

        internal MeshBuilder Pipe(IList<Vector3> points, float radius, string color, int segments = 10)
        {
            for (int k = 0; k + 1 < points.Count; k++)
            {
                Cylinder(points[k], points[k + 1], radius, color, segments);
                if (k > 0)
                {
                    Sphere(points[k], radius, color, segments);
                }
            }
            return this;
        }

        internal Mesh Build(string name)
        {
            ZFight.Separate(_vertices, _normals, _uvs, _triangles);
            var mesh = new Mesh { name = name, indexFormat = _vertices.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            mesh.SetVertices(_vertices);
            mesh.SetNormals(_normals);
            mesh.SetUVs(0, _uvs);
            if (_atlas)
            {
                var palette = new Vector2[_vertices.Count];
                for (int i = 0; i < palette.Length; i++) palette[i] = ModAssets.PaletteUv;
                mesh.uv2 = palette;
            }
            mesh.SetTriangles(_triangles, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

        internal static Mesh Letter(char letter)
        {
            const float stroke = 0.022f, height = 0.13f, width = 0.085f, depth = 0.02f;
            var b = new MeshBuilder();
            float left = -width * 0.5f, bottom = -height * 0.5f;
            b.Box(new Vector3(left + stroke * 0.5f, 0f, 0f), new Vector3(stroke, height, depth));
            if (letter == 'L')
            {
                b.Box(new Vector3(left + width * 0.5f, bottom + stroke * 0.5f, 0f), new Vector3(width, stroke, depth));
            }
            else
            {
                float bowl = height * 0.55f;
                float top = height * 0.5f;
                b.Box(new Vector3(left + width * 0.5f, top - stroke * 0.5f, 0f), new Vector3(width, stroke, depth));
                b.Box(new Vector3(left + width * 0.5f, top - bowl + stroke * 0.5f, 0f), new Vector3(width, stroke, depth));
                b.Box(new Vector3(left + width - stroke * 0.5f, top - bowl * 0.5f, 0f), new Vector3(stroke, bowl, depth));
            }
            return b.Build("Letter" + letter);
        }
    }
}
