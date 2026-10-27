using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Audio.Interface;
using Core.Services;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.StateTypes;
using Ships.Parts.Common;
using UnityEngine;

namespace Odyssey.Content
{
    internal static class BigDoorModel
    {
        private static readonly string[] Names =
        {
            "yellow", "yellow_dark", "dark", "grey", "light", "orange", "steel", "screen", "black", "rubber", "olive", "white", "cyan", "blue", "green", "red",
        };

        private static readonly Dictionary<string, string> HullColors = new Dictionary<string, string>
        {
            { "yellow", "light" }, { "yellow_dark", "grey" }, { "orange", "steel" }, { "dark", "grey" }, { "olive", "grey" },
        };

        private static string HullStyle(string color) => HullColors.TryGetValue(color, out string c) ? c : color;

        internal const string ModelName = "BigDoorModel";
        internal const string NarrowMarker = "Narrow";
        internal const float Frame = 0.22f;
        internal const float NarrowFrame = 0.06f, NarrowTop = 0.1f;

        internal static bool IsNarrow(Bounds b) => b.size.x < 1.5f;

        internal static float SideFrame(Bounds b) => IsNarrow(b) ? NarrowFrame : Frame;

        internal static float TopFrame(Bounds b) => IsNarrow(b) ? NarrowTop : Frame;

        internal const float PlateDepth = 0.012f;

        internal static float SocketY(Bounds b) => Mathf.Min(b.min.y + 1.2f, b.max.y - TopFrame(b) - 0.2f);

        private static void SocketPlates(MeshBuilder m, Bounds b, float z, float depth)
        {
            float frame = SideFrame(b), x = b.min.x + frame / 2, y = SocketY(b);
            foreach (float side in new[] { -1f, 1f })
            {
                m.Chamfer(new Vector3(x, y, z + side * (depth / 2 + PlateDepth / 2)), new Vector3(frame * 0.92f, 0.22f, PlateDepth), 0.03f, "yellow");
                m.Chamfer(new Vector3(x, y, z + side * (depth / 2 + PlateDepth / 2 + 0.001f)), new Vector3(frame * 0.6f, 0.15f, PlateDepth), 0.02f, "black");
            }
        }
        private const float Sill = 0.08f;
        private const float Strip = 0.25f;

        private enum Style { Slide, Jaw, Blast }

        private sealed class Box
        {
            public readonly List<Vector2> Poly;
            public readonly float Z0, Z1;
            public readonly string Color;
            public readonly Rect Bounds;

            public Box(Vector3 c, Vector3 s, string color)
                : this(Corners(c.x - s.x / 2, c.y - s.y / 2, c.x + s.x / 2, c.y + s.y / 2, 0f), c.z, s.z, color)
            {
            }

            public Box(List<Vector2> poly, float z, float depth, string color)
            {
                if (Area(poly) < 0f) poly.Reverse();
                Poly = poly;
                Z0 = z - depth / 2;
                Z1 = z + depth / 2;
                Color = color;
                Bounds = Rect.MinMaxRect(poly.Min(p => p.x), poly.Min(p => p.y), poly.Max(p => p.x), poly.Max(p => p.y));
            }

            public static Box Chamfer(Vector3 c, Vector3 s, float r, string color) =>
                new Box(Corners(c.x - s.x / 2, c.y - s.y / 2, c.x + s.x / 2, c.y + s.y / 2, Mathf.Min(r, s.x * 0.45f, s.y * 0.45f)), c.z, s.z, color);

            private static List<Vector2> Corners(float x0, float y0, float x1, float y1, float r)
            {
                if (r <= 0f) return new List<Vector2> { new Vector2(x0, y0), new Vector2(x1, y0), new Vector2(x1, y1), new Vector2(x0, y1) };
                return new List<Vector2>
                {
                    new Vector2(x0 + r, y0), new Vector2(x1 - r, y0), new Vector2(x1, y0 + r), new Vector2(x1, y1 - r),
                    new Vector2(x1 - r, y1), new Vector2(x0 + r, y1), new Vector2(x0, y1 - r), new Vector2(x0, y0 + r),
                };
            }
        }

        private static float Area(List<Vector2> poly)
        {
            float a = 0f;
            for (int i = 0; i < poly.Count; i++)
            {
                Vector2 p = poly[i], q = poly[(i + 1) % poly.Count];
                a += p.x * q.y - q.x * p.y;
            }
            return a / 2;
        }

        private static List<Vector2> Clip(List<Vector2> poly, int axis, float lo, float hi) => Half(Half(poly, axis, lo, 1f), axis, hi, -1f);

        private static List<Vector2> Half(List<Vector2> poly, int axis, float limit, float sign)
        {
            var result = new List<Vector2>();
            for (int i = 0; i < poly.Count; i++)
            {
                Vector2 a = poly[i], b = poly[(i + 1) % poly.Count];
                float da = (a[axis] - limit) * sign, db = (b[axis] - limit) * sign;
                if (da >= 0f) result.Add(a);
                if ((da >= 0f) != (db >= 0f)) result.Add(Vector2.Lerp(a, b, da / (da - db)));
            }
            return result;
        }

        internal static Transform Build(Transform parent, Bounds b, Material material, MeshRenderer look)
        {
            var model = new GameObject(ModelName).transform;
            model.SetParent(parent, false);
            Vector3 min = b.min, max = b.max;
            float w = b.size.x, h = b.size.y, z = b.center.z;
            float depth = Mathf.Clamp(b.size.z, 0.25f, 0.35f);
            var plates = new MeshBuilder();
            SocketPlates(plates, b, z, depth);
            PartFactory.CreateRenderer("SocketPlates", model, plates.ToMesh("BigDoorSocketPlates"), material, look);
            if (IsNarrow(b))
            {
                BuildNarrow(model, material, look, min, max, z, depth);
                return model;
            }
            Style style = w >= 8f ? Style.Blast : w > h && h >= 3f ? Style.Jaw : Style.Slide;

            float ix0 = min.x + Frame, ix1 = max.x - Frame, iy0 = min.y + Sill, iy1 = max.y - Frame;
            float leaf = Mathf.Min(style == Style.Blast ? 0.2f : 0.14f, depth * 0.7f);
            BuildFrame(model, material, look, min, max, z, depth, style, ix0, ix1, leaf);

            float mx = (ix0 + ix1) / 2, my = (iy0 + iy1) / 2;
            switch (style)
            {
                case Style.Slide:
                    AddLeaf(model, material, look, SlideLeaf(ix0, mx, iy0, iy1, z, leaf, mx), 'x', -1, mx - ix0, ix0, ix1, ix0, mx);
                    AddLeaf(model, material, look, SlideLeaf(mx, ix1, iy0, iy1, z, leaf, mx), 'x', 1, ix1 - mx, ix0, ix1, mx, ix1);
                    break;
                case Style.Blast:
                    AddLeaf(model, material, look, BlastLeaf(ix0, mx, iy0, iy1, z, leaf, mx), 'x', -1, mx - ix0, ix0, ix1, ix0, mx);
                    AddLeaf(model, material, look, BlastLeaf(mx, ix1, iy0, iy1, z, leaf, mx), 'x', 1, ix1 - mx, ix0, ix1, mx, ix1);
                    break;
                case Style.Jaw:
                    AddLeaf(model, material, look, JawHalf(ix0, ix1, my, iy1, z, leaf, true), 'y', 1, iy1 - my + 0.12f, iy0, iy1, my - 0.12f, iy1);
                    AddLeaf(model, material, look, JawHalf(ix0, ix1, iy0, my, z, leaf, false), 'y', -1, my - iy0 + 0.12f, iy0, iy1, iy0, my + 0.12f);
                    break;
            }
            return model;
        }

        private static void BuildFrame(Transform model, Material material, MeshRenderer look, Vector3 min, Vector3 max, float z, float depth, Style style,
            float ix0, float ix1, float leaf)
        {
            float w = max.x - min.x, h = max.y - min.y, cx = (min.x + max.x) / 2, cy = (min.y + max.y) / 2;
            float iy0 = min.y + Sill, iy1 = max.y - Frame, sock = Mathf.Min(min.y + 1.2f, max.y - Frame - 0.2f);
            var m = new MeshBuilder();
            foreach (float sx in new[] { min.x + Frame / 2, max.x - Frame / 2 })
            {
                m.Box(new Vector3(sx, cy, z), new Vector3(Frame, h, depth), "dark");
                m.Box(new Vector3(sx, cy, z), new Vector3(Frame * 0.5f, h - 0.3f, depth + 0.02f), "black");
                m.Box(new Vector3(sx, max.y - 0.18f, z), new Vector3(Frame + 0.06f, 0.36f, depth + 0.06f), "orange");
                m.Box(new Vector3(sx, max.y - 0.18f, z), new Vector3(Frame + 0.07f, 0.06f, depth + 0.07f), "black");
                m.Box(new Vector3(sx, min.y + 0.16f, z), new Vector3(Frame + 0.06f, 0.32f, depth + 0.06f), "orange");
                for (float y = min.y + 0.55f; y < max.y - 0.5f; y += 0.5f)
                {
                    m.Box(new Vector3(sx, y, z), new Vector3(Frame + 0.02f, 0.05f, depth + 0.02f), "grey");
                    if (sx < cx && Mathf.Abs(y + 0.25f - sock) < 0.15f) continue;
                    foreach (float side in new[] { -1f, 1f })
                    {
                        m.Chamfer(new Vector3(sx, y + 0.25f, z + side * (depth / 2 + 0.008f)), new Vector3(0.04f, 0.04f, 0.02f), 0.012f, "steel");
                    }
                }
            }
            m.Box(new Vector3(cx, max.y - Frame / 2, z), new Vector3(w, Frame, depth), "dark");
            m.Box(new Vector3(cx, max.y - Frame - 0.03f, z), new Vector3(w - 2 * Frame, 0.06f, depth * 0.8f), "grey");
            for (float x = min.x + 0.5f; x < max.x - 0.4f; x += 0.5f)
            {
                foreach (float side in new[] { -1f, 1f })
                {
                    m.Chamfer(new Vector3(x, max.y - Frame / 2, z + side * (depth / 2 + 0.008f)), new Vector3(0.04f, 0.04f, 0.02f), 0.012f, "steel");
                }
            }
            foreach (float lx in style == Style.Slide ? new[] { cx } : new[] { min.x + w * 0.25f, max.x - w * 0.25f })
            {
                foreach (float side in new[] { -1f, 1f })
                {
                    float lz = z + side * (depth / 2 + 0.048f);
                    m.Chamfer(new Vector3(lx, max.y - Frame / 2, lz), new Vector3(0.24f, 0.14f, 0.1f), 0.04f, "black");
                    m.Chamfer(new Vector3(lx, max.y - Frame / 2, lz + side * 0.04f), new Vector3(0.18f, 0.1f, 0.04f), 0.03f, "orange");
                    m.Chamfer(new Vector3(lx, max.y - Frame / 2, lz + side * 0.06f), new Vector3(0.1f, 0.06f, 0.02f), 0.02f, "yellow");
                }
            }
            m.Box(new Vector3(cx, min.y + Sill / 2, z), new Vector3(w - 2 * Frame, Sill, depth * 0.9f), "black");
            for (float x = ix0 + 0.25f; x < ix1 - 0.2f; x += 0.5f)
            {
                foreach (float side in new[] { -1f, 1f })
                {
                    m.Chamfer(new Vector3(x, min.y + Sill / 2, z + side * (depth * 0.45f + 0.006f)), new Vector3(0.04f, 0.04f, 0.016f), 0.012f, "steel");
                }
            }
            foreach (float side in new[] { -1f, 1f })
            {
                float pz = z + side * (depth / 2 + 0.033f);
                float px = max.x - Frame / 2, py = min.y + Mathf.Min(1.3f, h * 0.45f);
                m.Chamfer(new Vector3(px, py, pz), new Vector3(Frame * 0.85f, 0.42f, 0.07f), 0.04f, "white");
                m.Chamfer(new Vector3(px, py + 0.08f, pz + side * 0.036f), new Vector3(Frame * 0.6f, 0.16f, 0.01f), 0.02f, "dark");
                m.Chamfer(new Vector3(px - 0.04f, py - 0.08f, pz + side * 0.04f), new Vector3(0.05f, 0.05f, 0.02f), 0.015f, "orange");
                m.Chamfer(new Vector3(px + 0.04f, py - 0.08f, pz + side * 0.04f), new Vector3(0.05f, 0.05f, 0.02f), 0.015f, "yellow");
            }
            if (style != Style.Slide)
            {
                foreach (float sx in new[] { min.x + Frame + 0.08f, max.x - Frame - 0.08f })
                {
                    foreach (float side in new[] { -1f, 1f })
                    {
                        float pz = z + side * (depth / 2 + 0.048f);
                        m.Chamfer(new Vector3(sx, max.y - Frame / 2, z + side * (depth / 2 + 0.008f)), new Vector3(0.22f, Frame * 0.8f, 0.02f), 0.03f, "dark");
                        m.Chamfer(new Vector3(sx, max.y - Frame / 2 - 0.02f, pz), new Vector3(0.16f, Frame * 0.6f, 0.1f), 0.03f, "orange");
                        m.Chamfer(new Vector3(sx, max.y - Frame - 0.42f, pz), new Vector3(0.12f, 0.9f, 0.1f), 0.03f, "steel");
                        m.Chamfer(new Vector3(sx, max.y - Frame - 1.1f, pz), new Vector3(0.06f, 0.5f, 0.06f), 0.015f, "light");
                    }
                }
            }
            bool blast = style == Style.Blast;
            float gz0 = blast ? Mathf.Min(depth / 2 - 0.004f, leaf / 2 + 0.044f) : leaf / 2 + 0.014f, gz1 = leaf / 2 + (blast ? 0.064f : 0.03f);
            float gw = blast ? 0.044f : 0.013f, lip = blast ? 0.004f : 0.012f, gh = iy1 - 0.06f - iy0;
            foreach (float side in new[] { -1f, 1f })
            {
                foreach (float gx in new[] { ix0 + lip - gw / 2, ix1 - lip + gw / 2 })
                {
                    m.Box(new Vector3(gx, iy0 + gh / 2, z + side * (gz0 + gz1) / 2), new Vector3(gw, gh, gz1 - gz0), "steel");
                }
                if (style != Style.Slide) continue;
                m.Box(new Vector3(cx, iy0 + 0.015f, z + side * (leaf / 2 + 0.03f)), new Vector3(ix1 - ix0, 0.032f, 0.012f), "steel");
                float gzf = z + side * (depth / 2 + 0.009f);
                foreach (float dir in new[] { 1f, -1f })
                {
                    float gx0 = (dir > 0 ? ix0 : ix1) + dir * 0.03f;
                    m.Poly(new List<Vector2> { new Vector2(gx0, iy1 + 0.06f), new Vector2(gx0 + dir * 0.26f, iy1 + 0.06f), new Vector2(gx0, iy1 - 0.2f) }, gzf, 0.02f, "grey");
                    foreach (Vector2 p in new[] { new Vector2(0.05f, 0.03f), new Vector2(0.15f, 0.03f), new Vector2(0.04f, -0.1f) })
                    {
                        m.Chamfer(new Vector3(gx0 + dir * p.x, iy1 + p.y, gzf + side * 0.013f), new Vector3(0.03f, 0.03f, 0.008f), 0.009f, "steel");
                    }
                }
            }
            PartFactory.CreateRenderer("Frame", model, m.ToMesh("BigDoorFrame"), material, look);
        }

        private static List<Vector2> Disc(float x, float y, float r, int n)
        {
            var poly = new List<Vector2>();
            for (int i = 0; i < n; i++)
            {
                float a = (i + 0.5f) * Mathf.PI * 2f / n;
                poly.Add(new Vector2(x + Mathf.Cos(a) * r, y + Mathf.Sin(a) * r));
            }
            return poly;
        }

        private static void BuildNarrow(Transform model, Material material, MeshRenderer look, Vector3 min, Vector3 max, float z, float depth)
        {
            float w = max.x - min.x, h = max.y - min.y, cx = (min.x + max.x) / 2, cy = (min.y + max.y) / 2;
            float ix0 = min.x + NarrowFrame, ix1 = max.x - NarrowFrame, iy0 = min.y, iy1 = max.y - NarrowTop;
            var m = new MeshBuilder();
            foreach (float sx in new[] { min.x + NarrowFrame / 2, max.x - NarrowFrame / 2 })
            {
                m.Box(new Vector3(sx, cy, z), new Vector3(NarrowFrame, h, depth), "grey");
            }
            m.Box(new Vector3(cx, max.y - NarrowTop / 2, z), new Vector3(w, NarrowTop, depth), "grey");
            m.Box(new Vector3(cx, max.y - NarrowTop / 2, z), new Vector3(w - 0.3f, NarrowTop * 0.4f, depth + 0.02f), "yellow");
            float d = Mathf.Min(0.1f, depth * 0.6f), sock = Mathf.Min(min.y + 1.2f, max.y - NarrowTop - 0.2f);
            foreach (float side in new[] { -1f, 1f })
            {
                foreach (float gx in new[] { ix0 + 0.0095f, ix1 - 0.0095f })
                {
                    m.Box(new Vector3(gx, (iy0 + iy1) / 2, z + side * (d / 2 + 0.022f)), new Vector3(0.021f, iy1 - iy0, 0.02f), "steel");
                }
                m.Box(new Vector3(cx, iy1 - 0.015f, z + side * (d / 2 + 0.008f)), new Vector3(ix1 - ix0, 0.032f, 0.012f), "rubber");
                foreach (float sx in new[] { min.x + NarrowFrame / 2, max.x - NarrowFrame / 2 })
                {
                    for (float y = min.y + 0.2f; y < max.y - NarrowTop - 0.1f; y += 0.4f)
                    {
                        if (sx < cx && Mathf.Abs(y - sock) < 0.16f) continue;
                        m.Chamfer(new Vector3(sx, y, z + side * (depth / 2 + 0.003f)), new Vector3(0.024f, 0.024f, 0.008f), 0.007f, "steel");
                    }
                }
            }
            PartFactory.CreateRenderer("Frame", model, m.ToMesh("BigDoorFrame"), material, look);

            float wl = ix1 - ix0 - 0.01f, hl = iy1 - iy0, lcx = (ix0 + ix1) / 2, lcy = (iy0 + iy1) / 2;
            int slats = Mathf.Max(1, Mathf.CeilToInt(hl / Strip));
            float pitch = hl / slats;
            var leaf = new List<Box> { new Box(new Vector3(lcx, lcy, z), new Vector3(wl, hl, d * 0.6f), "dark") };
            for (int i = 0; i < slats; i++)
            {
                float sy = iy0 + pitch * (i + 0.5f);
                leaf.Add(new Box(new Vector3(lcx, sy, z), new Vector3(wl, pitch - 0.03f, d), "light"));
                foreach (float side in new[] { -1f, 1f })
                {
                    float face = z + side * d / 2;
                    leaf.Add(Box.Chamfer(new Vector3(lcx, sy, face + side * 0.004f), new Vector3(wl - 0.1f, 0.04f, 0.01f), 0.012f, "grey"));
                    foreach (float ex in new[] { lcx - wl / 2 + 0.02f, lcx + wl / 2 - 0.02f })
                    {
                        leaf.Add(new Box(new Vector3(ex, sy, face + side * 0.003f), new Vector3(0.03f, pitch - 0.06f, 0.008f), "steel"));
                    }
                }
            }
            leaf.Add(new Box(new Vector3(lcx, iy0 + 0.04f, z), new Vector3(wl, 0.08f, d + 0.016f), "dark"));
            foreach (float side in new[] { -1f, 1f })
            {
                float face = z + side * d / 2, hy = iy0 + pitch * 0.5f;
                foreach (float sx in new[] { lcx - 0.09f, lcx + 0.09f })
                {
                    leaf.Add(Box.Chamfer(new Vector3(sx, hy, face + side * 0.014f), new Vector3(0.03f, 0.03f, 0.03f), 0.008f, "steel"));
                }
                leaf.Add(Box.Chamfer(new Vector3(lcx, hy, face + side * 0.036f), new Vector3(0.24f, 0.035f, 0.018f), 0.012f, "steel"));
                if (wl > 0.7f)
                {
                    float kx = lcx + Mathf.Min(0.28f, wl / 2 - 0.12f);
                    leaf.Add(Box.Chamfer(new Vector3(kx, iy0 + 0.045f, face + side * 0.01f), new Vector3(0.07f, 0.05f, 0.006f), 0.012f, "steel"));
                    leaf.Add(Box.Chamfer(new Vector3(kx, iy0 + 0.045f, face + side * 0.014f), new Vector3(0.012f, 0.024f, 0.004f), 0.004f, "black"));
                }
            }
            leaf.Add(new Box(new Vector3(lcx, iy0 + 0.02f, z), new Vector3(wl, 0.04f, d + 0.02f), "rubber"));
            AddLeaf(model, material, look, leaf, 'y', 1, hl, iy0, iy1, iy0, iy1);
            new GameObject(NarrowMarker).transform.SetParent(model, false);
        }

        private static void Lattice(List<Box> list, float x0, float x1, float y0, float y1, float face, float side, int cols, int rows, float bar)
        {
            float cw = (x1 - x0) / cols, ch = (y1 - y0) / rows, mx = (x0 + x1) / 2, my = (y0 + y1) / 2;
            for (int j = 0; j <= rows; j++)
            {
                float yb = y0 + j * ch, lo = j == 0 ? y0 : yb - bar, hi = j == rows ? y1 : yb + bar;
                list.Add(new Box(new Vector3(mx, (lo + hi) / 2, face + side * 0.007f), new Vector3(x1 - x0, hi - lo, 0.016f), "grey"));
            }
            for (int i = 0; i <= cols; i++)
            {
                float xb = x0 + i * cw, lo = i == 0 ? x0 + 0.002f : xb - bar, hi = i == cols ? x1 - 0.002f : xb + bar;
                list.Add(new Box(new Vector3((lo + hi) / 2, my, face + side * 0.006f), new Vector3(hi - lo, y1 - y0 - 0.004f, 0.014f), "grey"));
            }
            float bx = cw / 2 - bar / 2, by = ch / 2 - bar / 2, bz = face + side * 0.018f;
            var bolt = new Vector3(0.026f, 0.026f, 0.012f);
            for (int i = 0; i < cols; i++)
            {
                for (int j = 0; j < rows; j++)
                {
                    float pcx = x0 + cw * (i + 0.5f), pcy = y0 + ch * (j + 0.5f);
                    list.Add(Box.Chamfer(new Vector3(pcx, pcy, face + side * 0.0025f), new Vector3(cw - 2 * bar - 0.004f, ch - 2 * bar - 0.004f, 0.007f), 0.03f, "light"));
                    foreach (float sx in new[] { -1f, 1f })
                    {
                        foreach (float sy in new[] { -1f, 1f })
                        {
                            list.Add(Box.Chamfer(new Vector3(pcx + sx * bx, pcy + sy * by, bz), bolt, 0.008f, "steel"));
                        }
                        if (cw > 0.45f) list.Add(Box.Chamfer(new Vector3(pcx, pcy + sx * by, bz), bolt, 0.008f, "steel"));
                        if (ch > 0.6f) list.Add(Box.Chamfer(new Vector3(pcx + sx * bx, pcy, bz), bolt, 0.008f, "steel"));
                    }
                }
            }
        }

        private static List<Box> SlideLeaf(float x0, float x1, float y0, float y1, float z, float d, float mid)
        {
            var list = new List<Box>();
            float cx = (x0 + x1) / 2, cy = (y0 + y1) / 2, wl = x1 - x0 - 0.01f, hl = y1 - y0;
            bool left = x1 <= mid + 0.001f;
            float edge = left ? x1 : x0, outer = left ? x0 : x1, inward = left ? -1f : 1f;
            float px0 = edge + inward * 0.08f, px1 = outer - inward * 0.14f;
            float lx0 = Mathf.Min(px0, px1), lx1 = Mathf.Max(px0, px1);
            float bottom = y0 + 0.32f, top = y1 - 0.18f;
            int rows = Mathf.Max(1, Mathf.RoundToInt((top - bottom) / 0.85f)), cols = Mathf.Max(1, Mathf.RoundToInt((lx1 - lx0) / 0.65f));
            list.Add(new Box(new Vector3(cx, cy, z), new Vector3(wl, hl, d), "light"));
            foreach (float side in new[] { -1f, 1f })
            {
                float face = z + side * d / 2, fz = z + side * (d / 2 + 0.005f);
                list.Add(Box.Chamfer(new Vector3(cx, y1 - 0.065f, fz), new Vector3(wl, 0.13f, 0.012f), 0.02f, "grey"));
                list.Add(new Box(new Vector3(outer - inward * 0.06f, cy, fz), new Vector3(0.12f, hl - 0.02f, 0.012f), "grey"));
                list.Add(new Box(new Vector3(edge + inward * 0.04f, cy, fz), new Vector3(0.08f, hl - 0.02f, 0.012f), "grey"));
                for (float y = y0 + 0.45f; y < y1 - 0.3f; y += 0.5f)
                {
                    list.Add(Box.Chamfer(new Vector3(outer - inward * 0.06f, y, fz + side * 0.008f), new Vector3(0.04f, 0.04f, 0.012f), 0.012f, "steel"));
                    list.Add(Box.Chamfer(new Vector3(edge + inward * 0.05f, y + 0.25f, fz + side * 0.008f), new Vector3(0.03f, 0.03f, 0.012f), 0.01f, "steel"));
                }
                Lattice(list, lx0, lx1, bottom, top, face, side, cols, rows, 0.035f);
                foreach (float rx in wl > 0.9f ? new[] { outer - inward * 0.3f, edge + inward * 0.3f } : new[] { cx })
                {
                    float ry = y1 - 0.095f;
                    list.Add(Box.Chamfer(new Vector3(rx, ry, face + side * 0.014f), new Vector3(0.18f, 0.06f, 0.008f), 0.015f, "dark"));
                    list.Add(new Box(Disc(rx, ry, 0.026f, 10), face + side * 0.023f, 0.012f, "steel"));
                    list.Add(new Box(Disc(rx, ry, 0.011f, 8), face + side * 0.031f, 0.006f, "black"));
                }
                float hx = edge + inward * 0.22f, hy = y0 + Mathf.Min(1.1f, hl * 0.4f);
                list.Add(Box.Chamfer(new Vector3(hx, hy, face + side * 0.012f), new Vector3(0.15f, 0.56f, 0.014f), 0.05f, "dark"));
                foreach (float sy in new[] { hy - 0.19f, hy + 0.19f })
                {
                    list.Add(Box.Chamfer(new Vector3(hx, sy, face + side * 0.032f), new Vector3(0.05f, 0.05f, 0.03f), 0.015f, "steel"));
                }
                list.Add(Box.Chamfer(new Vector3(hx, hy, face + side * 0.06f), new Vector3(0.05f, 0.46f, 0.03f), 0.018f, "steel"));
                for (float x = Mathf.Min(x0, x1) + 0.15f; x < Mathf.Max(x0, x1) - 0.1f; x += 0.3f)
                {
                    list.Add(Box.Chamfer(new Vector3(x, y0 + 0.12f, z + side * (d / 2 + 0.016f)), new Vector3(0.04f, 0.04f, 0.016f), 0.012f, "steel"));
                }
            }
            list.Add(Box.Chamfer(new Vector3(cx, y0 + 0.12f, z), new Vector3(wl, 0.24f, d + 0.02f), 0.03f, "orange"));
            list.Add(new Box(new Vector3(cx, y0 + 0.012f, z), new Vector3(wl, 0.024f, d + 0.024f), "rubber"));
            list.Add(new Box(new Vector3(edge + inward * 0.015f, cy, z), new Vector3(0.03f, hl, d + 0.03f), "rubber"));
            return list;
        }

        private static List<Box> BlastLeaf(float x0, float x1, float y0, float y1, float z, float d, float mid)
        {
            var list = new List<Box>();
            float cx = (x0 + x1) / 2, cy = (y0 + y1) / 2, wl = x1 - x0 - 0.01f, hl = y1 - y0;
            bool left = x1 <= mid + 0.001f;
            float edge = left ? x1 : x0, outer = left ? x0 : x1, inward = left ? -1f : 1f;
            float xa = edge + inward * 0.12f, xb = outer - inward * 0.16f;
            float py0 = y0 + 0.3f, py1 = y1 - 0.16f, pitch = wl > 6f ? 0.75f : 0.5f;
            int cols = Mathf.Max(1, Mathf.RoundToInt(Mathf.Abs(xb - xa) / 1.9f)), rows = Mathf.Max(1, Mathf.RoundToInt((py1 - py0) / 1.9f));
            float cw = Mathf.Abs(xb - xa) / cols, ch = (py1 - py0) / rows;
            var nut = new Vector3(0.05f, 0.05f, 0.012f);
            list.Add(new Box(new Vector3(cx, cy, z), new Vector3(wl, hl, d), "dark"));
            foreach (float side in new[] { -1f, 1f })
            {
                float face = z + side * d / 2;
                for (int i = 0; i < cols; i++)
                {
                    float pcx = xa + inward * cw * (i + 0.5f);
                    for (int j = 0; j < rows; j++)
                    {
                        float pcy = py0 + ch * (j + 0.5f), pw = cw - 0.1f, ph = ch - 0.1f;
                        list.Add(Box.Chamfer(new Vector3(pcx, pcy, face + side * 0.014f), new Vector3(pw, ph, 0.03f), 0.12f, "grey"));
                        int nx = Mathf.Max(2, Mathf.RoundToInt(pw / 0.9f)), ny = Mathf.Max(2, Mathf.RoundToInt(ph / 0.9f));
                        float sw = (pw - 0.16f) / nx, sh = (ph - 0.16f) / ny, sx0 = pcx - (pw - 0.16f) / 2, sy0 = pcy - (ph - 0.16f) / 2;
                        for (int a = 0; a < nx; a++)
                        {
                            for (int c = 0; c < ny; c++)
                            {
                                list.Add(Box.Chamfer(new Vector3(sx0 + sw * (a + 0.5f), sy0 + sh * (c + 0.5f), face + side * 0.032f), new Vector3(sw - 0.1f, sh - 0.1f, 0.008f), 0.05f, "light"));
                            }
                        }
                        for (int a = 1; a < nx; a++)
                        {
                            float rx = sx0 + sw * a;
                            list.Add(new Box(new Vector3(rx, pcy, face + side * 0.033f), new Vector3(0.12f, ph - 0.09f, 0.018f), "dark"));
                            for (int c = 0; c < ny; c++)
                            {
                                list.Add(new Box(new Vector3(rx, sy0 + sh * (c + 0.5f), face + side * 0.047f), nut, "steel"));
                            }
                        }
                        for (int c = 1; c < ny; c++)
                        {
                            float ry = sy0 + sh * c;
                            list.Add(new Box(new Vector3(pcx, ry, face + side * 0.036f), new Vector3(pw - 0.056f, 0.12f, 0.02f), "dark"));
                            for (int a = 0; a < nx; a++)
                            {
                                list.Add(new Box(new Vector3(sx0 + sw * (a + 0.5f), ry, face + side * 0.051f), nut, "steel"));
                            }
                            for (int a = 1; a < nx; a++)
                            {
                                list.Add(new Box(new Vector3(sx0 + sw * a, ry, face + side * 0.051f), nut, "steel"));
                            }
                        }
                    }
                }
                for (int i = 1; i < cols; i++)
                {
                    list.Add(new Box(new Vector3(xa + inward * cw * i, (py0 + py1) / 2, face + side * 0.021f), new Vector3(0.16f, py1 - py0, 0.044f), "dark"));
                }
                for (int j = 0; j <= rows; j++)
                {
                    float by = py0 + ch * j;
                    for (int i = 0; i < cols; i++)
                    {
                        float bcx = xa + inward * cw * (i + 0.5f);
                        list.Add(Box.Chamfer(new Vector3(bcx, by, face + side * 0.024f), new Vector3(cw - 0.02f, 0.2f, 0.05f), 0.04f, "dark"));
                        for (float t = -cw / 2 + 0.3f; t < cw / 2 - 0.2f; t += pitch)
                        {
                            if (j == rows && Mathf.Abs(t) < 0.14f) continue;
                            list.Add(Box.Chamfer(new Vector3(bcx + t, by, face + side * 0.054f), new Vector3(0.06f, 0.06f, 0.02f), 0.018f, "steel"));
                        }
                        if (j == rows)
                        {
                            list.Add(new Box(Disc(bcx, by, 0.075f, 12), face + side * 0.056f, 0.016f, "steel"));
                            list.Add(new Box(Disc(bcx, by, 0.03f, 8), face + side * 0.067f, 0.008f, "black"));
                        }
                    }
                }
                list.Add(Box.Chamfer(new Vector3(outer - inward * 0.08f, (py0 + py1) / 2, face + side * 0.019f), new Vector3(0.14f, py1 - py0 + 0.2f, 0.04f), 0.03f, "dark"));
                list.Add(Box.Chamfer(new Vector3(edge + inward * 0.07f, (py0 + py1) / 2, face + side * 0.019f), new Vector3(0.1f, py1 - py0 + 0.2f, 0.04f), 0.03f, "dark"));
                for (float y = py0 + 0.1f; y < py1 - 0.05f; y += pitch)
                {
                    list.Add(Box.Chamfer(new Vector3(outer - inward * 0.08f, y, face + side * 0.044f), nut, 0.015f, "steel"));
                    if (y < y0 + 0.85f || y > y0 + 1.75f) list.Add(Box.Chamfer(new Vector3(edge + inward * 0.07f, y, face + side * 0.044f), nut, 0.015f, "steel"));
                }
                float hx = edge + inward * 0.07f;
                foreach (float sy in new[] { y0 + 1f, y0 + 1.6f })
                {
                    list.Add(Box.Chamfer(new Vector3(hx, sy, face + side * 0.058f), new Vector3(0.05f, 0.05f, 0.04f), 0.015f, "steel"));
                }
                list.Add(Box.Chamfer(new Vector3(hx, y0 + 1.3f, face + side * 0.092f), new Vector3(0.045f, 0.76f, 0.03f), 0.015f, "steel"));
                for (float x = Mathf.Min(x0, x1) + 0.2f; x < Mathf.Max(x0, x1) - 0.15f; x += pitch)
                {
                    list.Add(Box.Chamfer(new Vector3(x, y0 + 0.17f, face + side * 0.035f), nut, 0.015f, "steel"));
                }
            }
            list.Add(Box.Chamfer(new Vector3(cx, y0 + 0.13f, z), new Vector3(wl, 0.26f, d + 0.06f), 0.04f, "orange"));
            list.Add(new Box(new Vector3(cx, y0 + 0.03f, z), new Vector3(wl, 0.06f, d + 0.08f), "rubber"));
            list.Add(new Box(new Vector3(edge + inward * 0.02f, cy, z), new Vector3(0.04f, hl, d + 0.06f), "black"));
            return list;
        }

        private static List<Box> JawHalf(float x0, float x1, float y0, float y1, float z, float d, bool top)
        {
            var list = new List<Box>();
            float cx = (x0 + x1) / 2, cy = (y0 + y1) / 2, wl = x1 - x0, hl = y1 - y0;
            float edge = top ? y0 : y1, inward = top ? 1f : -1f;
            list.Add(new Box(new Vector3(cx, cy, z), new Vector3(wl, hl - 0.01f, d), "light"));
            int teeth = Mathf.Max(4, Mathf.RoundToInt(wl / 0.5f));
            float tw = wl / teeth;
            for (int i = 0; i < teeth; i++)
            {
                if ((i % 2 == 0) == top)
                {
                    float tx = x0 + tw * (i + 0.5f);
                    list.Add(Box.Chamfer(new Vector3(tx, edge - inward * 0.06f, z), new Vector3(tw - 0.02f, 0.13f, d), 0.03f, "grey"));
                }
            }
            int cols = Mathf.Max(1, Mathf.RoundToInt((wl - 0.2f) / 0.7f));
            float pTop = top ? y1 - 0.2f : y1 - 0.24f, pBottom = top ? y0 + 0.24f : y0 + 0.2f;
            float edgeRail = edge + inward * 0.12f, outerRail = top ? y1 - 0.1f : y0 + 0.1f;
            foreach (float side in new[] { -1f, 1f })
            {
                float face = z + side * d / 2, fz = z + side * (d / 2 + 0.005f);
                list.Add(Box.Chamfer(new Vector3(cx, edgeRail, fz), new Vector3(wl - 0.04f, 0.14f, 0.012f), 0.03f, "grey"));
                list.Add(Box.Chamfer(new Vector3(cx, outerRail, fz), new Vector3(wl - 0.04f, 0.14f, 0.012f), 0.03f, "grey"));
                for (float x = x0 + 0.2f; x < x1 - 0.15f; x += 0.35f)
                {
                    foreach (float ry in new[] { edgeRail, outerRail })
                    {
                        list.Add(Box.Chamfer(new Vector3(x, ry, face + side * 0.015f), new Vector3(0.03f, 0.03f, 0.01f), 0.01f, "steel"));
                    }
                }
                Lattice(list, x0 + 0.1f, x1 - 0.1f, pBottom, pTop, face, side, cols, 1, 0.035f);
                foreach (float sx in new[] { x0 + 0.05f, x1 - 0.05f })
                {
                    list.Add(Box.Chamfer(new Vector3(sx, cy, face + side * 0.007f), new Vector3(0.08f, Mathf.Min(0.36f, hl * 0.35f), 0.016f), 0.02f, "steel"));
                }
            }
            return list;
        }

        private static void AddLeaf(Transform model, Material material, MeshRenderer look, List<Box> boxes, char axis, int dir, float distance,
            float clipLo, float clipHi, float lo, float hi)
        {
            int count = Mathf.Max(1, Mathf.CeilToInt((hi - lo) / Strip));
            float step = (hi - lo) / count;
            for (int i = 0; i < count; i++)
            {
                float s0 = lo + i * step, s1 = s0 + step, sc = (s0 + s1) / 2;
                var m = new MeshBuilder();
                foreach (Box box in boxes)
                {
                    m.Add(box, axis == 'x' ? 0 : 1, s0, s1, axis == 'x' ? new Vector2(sc, 0f) : new Vector2(0f, sc));
                }
                string name = string.Format(CultureInfo.InvariantCulture, "P|{0}|{1}|{2}|{3}|{4}|{5}", axis, dir, distance, clipLo, clipHi, step);
                GameObject piece = PartFactory.CreateRenderer(name, model, m.ToMesh("BigDoorStrip"), material, look);
                piece.transform.localPosition = axis == 'x' ? new Vector3(sc, 0, 0) : new Vector3(0, sc, 0);
            }
        }

        private const int Grid = 4;
        private const float Margin = 26f / 256f, Inset = 1.5f / 256f, Border = 0.03f;

        private const float Tile = 1f;

        private static int Tiles(float length) => Mathf.Max(1, Mathf.RoundToInt((length - 2f * Border) / Tile));

        private static float Slice(float t, float length, float mid)
        {
            if (length < Border * 2.5f) return Margin + (1f - 2f * Margin) * Mathf.Clamp01(t / length);
            if (mid <= Border) return Margin * t / Border;
            if (mid >= length - Border) return 1f - Margin * (length - t) / Border;
            float tile = (length - 2f * Border) / Tiles(length);
            int k = Mathf.Clamp(Mathf.FloorToInt((mid - Border) / tile), 0, Tiles(length) - 1);
            return Margin + (1f - 2f * Margin) * Mathf.Clamp01((t - Border - k * tile) / tile);
        }

        private static float[] Cuts(float length)
        {
            if (length < Border * 2.5f) return new[] { 0f, length };
            int n = Tiles(length);
            var cuts = new List<float> { 0f, Border };
            for (int k = 1; k < n; k++) cuts.Add(Border + (length - 2f * Border) * k / n);
            cuts.Add(length - Border);
            cuts.Add(length);
            return cuts.ToArray();
        }

        private static Vector2 CellUv(int cell, float fu, float fv)
        {
            float cx = cell % Grid, cy = cell / Grid;
            fu = Inset + fu * (1f - 2f * Inset);
            fv = Inset + fv * (1f - 2f * Inset);
            return new Vector2((cx + fu) / Grid, 1f - (cy + 1f - fv) / Grid);
        }

        private sealed class MeshBuilder
        {
            private readonly List<Vector3> _v = new List<Vector3>();
            private readonly List<Vector3> _n = new List<Vector3>();
            private readonly List<Vector2> _uv = new List<Vector2>();
            private readonly List<int> _t = new List<int>();

            public void Box(Vector3 center, Vector3 size, string color) => Add(new Box(center, size, color), -1, 0f, 0f, Vector2.zero);

            public void Chamfer(Vector3 center, Vector3 size, float r, string color) => Add(BigDoorModel.Box.Chamfer(center, size, r, color), -1, 0f, 0f, Vector2.zero);

            public void Poly(List<Vector2> poly, float z, float depth, string color) => Add(new Box(poly, z, depth, color), -1, 0f, 0f, Vector2.zero);

            public void Add(Box box, int axis, float lo, float hi, Vector2 shift)
            {
                if (axis >= 0 && (box.Bounds.max[axis] <= lo || box.Bounds.min[axis] >= hi)) return;
                List<Vector2> poly = axis < 0 ? box.Poly : Clip(box.Poly, axis, lo, hi);
                if (poly.Count < 3 || Mathf.Abs(Area(poly)) < 1e-7f) return;
                if (axis >= 0 && poly.Max(p => p[axis]) - poly.Min(p => p[axis]) < 0.0005f) return;
                int cell = Mathf.Max(0, System.Array.IndexOf(Names, HullStyle(box.Color)));
                Rect r = box.Bounds;
                float[] cx = Cuts(r.width), cy = Cuts(r.height);
                foreach (float z in new[] { box.Z0, box.Z1 })
                {
                    var n = new Vector3(0f, 0f, z == box.Z0 ? -1f : 1f);
                    for (int i = 0; i + 1 < cx.Length; i++)
                    {
                        for (int j = 0; j + 1 < cy.Length; j++)
                        {
                            List<Vector2> part = Clip(Clip(poly, 0, r.xMin + cx[i], r.xMin + cx[i + 1]), 1, r.yMin + cy[j], r.yMin + cy[j + 1]);
                            if (part.Count < 3) continue;
                            int start = _v.Count;
                            float mx = (cx[i] + cx[i + 1]) / 2, my = (cy[j] + cy[j + 1]) / 2;
                            foreach (Vector2 p in part)
                            {
                                Vertex(new Vector3(p.x - shift.x, p.y - shift.y, z), n, CellUv(cell, Slice(p.x - r.xMin, r.width, mx), Slice(p.y - r.yMin, r.height, my)));
                            }
                            for (int k = 1; k + 1 < part.Count; k++) Tri(start, start + k, start + k + 1, n);
                        }
                    }
                }
                float depth = box.Z1 - box.Z0;
                float[] cz = Cuts(depth);
                for (int e = 0; e < poly.Count; e++)
                {
                    Vector2 a = poly[e], b = poly[(e + 1) % poly.Count], d = b - a;
                    if (d.sqrMagnitude < 1e-12f) continue;
                    var n = new Vector3(d.y, -d.x, 0f).normalized;
                    int k = Mathf.Abs(d.x) >= Mathf.Abs(d.y) ? 0 : 1;
                    float origin = k == 0 ? r.xMin : r.yMin, length = k == 0 ? r.width : r.height;
                    var ts = new List<float> { 0f, 1f };
                    foreach (float c in Cuts(length))
                    {
                        float t = (origin + c - a[k]) / d[k];
                        if (t > 1e-4f && t < 1f - 1e-4f) ts.Add(t);
                    }
                    ts.Sort();
                    for (int i = 0; i + 1 < ts.Count; i++)
                    {
                        Vector2 p0 = Vector2.Lerp(a, b, ts[i]) - shift, p1 = Vector2.Lerp(a, b, ts[i + 1]) - shift;
                        float um = (p0[k] + p1[k]) / 2 + shift[k] - origin;
                        float u0 = Slice(p0[k] + shift[k] - origin, length, um), u1 = Slice(p1[k] + shift[k] - origin, length, um);
                        for (int j = 0; j + 1 < cz.Length; j++)
                        {
                            float z0 = box.Z0 + cz[j], z1 = box.Z0 + cz[j + 1], vm = (cz[j] + cz[j + 1]) / 2, v0 = Slice(cz[j], depth, vm), v1 = Slice(cz[j + 1], depth, vm);
                            int start = _v.Count;
                            Vertex(new Vector3(p0.x, p0.y, z0), n, CellUv(cell, u0, v0));
                            Vertex(new Vector3(p1.x, p1.y, z0), n, CellUv(cell, u1, v0));
                            Vertex(new Vector3(p1.x, p1.y, z1), n, CellUv(cell, u1, v1));
                            Vertex(new Vector3(p0.x, p0.y, z1), n, CellUv(cell, u0, v1));
                            Tri(start, start + 1, start + 2, n);
                            Tri(start, start + 2, start + 3, n);
                        }
                    }
                }
            }

            private void Vertex(Vector3 p, Vector3 n, Vector2 uv)
            {
                _v.Add(p);
                _n.Add(n);
                _uv.Add(uv);
            }

            private void Tri(int a, int b, int c, Vector3 n)
            {
                bool flip = Vector3.Dot(Vector3.Cross(_v[b] - _v[a], _v[c] - _v[a]), n) < 0f;
                _t.Add(a);
                _t.Add(flip ? c : b);
                _t.Add(flip ? b : c);
            }

            public Mesh ToMesh(string name)
            {
                ZFight.Separate(_v, _n, _uv, _t);
                var mesh = new Mesh { name = name, indexFormat = _v.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
                mesh.SetVertices(_v);
                mesh.SetNormals(_n);
                mesh.SetUVs(0, _uv);
                var palette = new Vector2[_v.Count];
                for (int i = 0; i < palette.Length; i++) palette[i] = ModAssets.PaletteUv;
                mesh.uv2 = palette;
                mesh.SetTriangles(_t, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }

    internal class BigDoorVisuals : DefaultShipPartVisuals
    {
        private float _seconds = 1.6f;
        private float _pitch = 1f, _hear = 40f;
        private bool _moving;
        private string _soundName = "bigdoor";
        private Audio.LoopEmitter _sound;

        private struct Piece
        {
            public Transform T;
            public Renderer R;
            public bool AxisX;
            public float Closed, Width, Distance, Lo, Hi;
            public int Dir;
        }

        private readonly List<Piece> _pieces = new List<Piece>();
        private float _t;
        private float _target;

        private void Awake()
        {
            Transform model = transform.Find(BigDoorModel.ModelName);
            if (model != null && model.Find(BigDoorModel.NarrowMarker) != null)
            {
                _soundName = "smalldoor";
            }
            _sound = new Audio.LoopEmitter(_soundName, 45f, BuildingMod.DoorVolume);
            if (model == null)
            {
                return;
            }
            foreach (Transform child in model)
            {
                string[] p = child.name.Split('|');
                if (p.Length != 7 || p[0] != "P")
                {
                    continue;
                }
                var piece = new Piece
                {
                    T = child,
                    R = child.GetComponent<Renderer>(),
                    AxisX = p[1] == "x",
                    Dir = int.Parse(p[2], CultureInfo.InvariantCulture),
                    Distance = float.Parse(p[3], CultureInfo.InvariantCulture),
                    Lo = float.Parse(p[4], CultureInfo.InvariantCulture),
                    Hi = float.Parse(p[5], CultureInfo.InvariantCulture),
                    Width = float.Parse(p[6], CultureInfo.InvariantCulture),
                };
                piece.Closed = piece.AxisX ? child.localPosition.x : child.localPosition.y;
                _pieces.Add(piece);
            }
            float travel = 0f;
            foreach (Piece p in _pieces) travel = Mathf.Max(travel, p.Distance);
            _seconds = 1.2f + 0.5f * travel;
            _pitch = Mathf.Clamp(1.1f - 0.045f * travel, 0.7f, 1.1f);
            _hear = 35f + 4f * travel;
        }

        public override void SetNetworkState(IStatefulPartState state)
        {
            _target = _t = ((IDoorState)state).IsOpen ? 1f : 0f;
            Apply();
        }

        public override void ReactToNetworkStateChange(StatefulPartStateChangeEvent change)
        {
            bool wasOpen = ((IDoorState)change.PreviousState).IsOpen, open = ((IDoorState)change.NewState).IsOpen;
            if (wasOpen != open)
            {
                Audio.OneShot.Play(_soundName + "_start", transform.position, _hear, BuildingMod.DoorVolume.Value, _pitch);
            }
            _target = open ? 1f : 0f;
        }

        private void Update()
        {
            if (Mathf.Approximately(_t, _target))
            {
                if (_moving)
                {
                    _moving = false;
                    _sound?.Stop();
                    Audio.OneShot.Play(_soundName + "_stop", transform.position, _hear, BuildingMod.DoorVolume.Value, _pitch);
                }
                return;
            }
            _moving = true;
            _t = Mathf.MoveTowards(_t, _target, Time.deltaTime / _seconds);
            float edge = Mathf.Min(Mathf.Abs(_t - _target), 1f - Mathf.Abs(_t - _target));
            _sound?.Update(transform.position, Mathf.Clamp01(0.35f + edge * 4f), _pitch);
            Apply();
        }

        private void OnDisable() => _sound?.Stop();

        private void Apply()
        {
            float x = Mathf.Clamp01(_t);
            float k = x * x * x * (x * (x * 6f - 15f) + 10f);
            foreach (Piece p in _pieces)
            {
                if (p.T == null) continue;
                float center = p.Closed + p.Dir * p.Distance * k;
                float a = Mathf.Max(center - p.Width / 2, p.Lo), b = Mathf.Min(center + p.Width / 2, p.Hi);
                bool visible = b - a > 0.002f;
                if (p.R != null) p.R.enabled = visible;
                if (!visible) continue;
                Vector3 pos = p.T.localPosition, scale = p.T.localScale;
                float mid = (a + b) / 2, s = (b - a) / p.Width;
                if (p.AxisX) { pos.x = mid; scale.x = s; }
                else { pos.y = mid; scale.y = s; }
                p.T.localPosition = pos;
                p.T.localScale = scale;
            }
        }
    }
}
