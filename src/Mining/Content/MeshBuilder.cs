using System.Collections.Generic;
using UnityEngine;

namespace Mining.Content
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

        internal Mesh Build(string name)
        {
            var mesh = new Mesh { name = name };
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
