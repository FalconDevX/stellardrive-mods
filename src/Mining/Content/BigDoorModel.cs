using System.Collections.Generic;
using System.Globalization;
using Audio.Interface;
using Core.Services;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.StateTypes;
using Ships.Parts.Common;
using UnityEngine;

namespace Mining.Content
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
                m.Box(new Vector3(x, y, z + side * (depth / 2 + PlateDepth / 2)), new Vector3(frame * 0.92f, 0.22f, PlateDepth), "yellow");
                m.Box(new Vector3(x, y, z + side * (depth / 2 + PlateDepth / 2 + 0.001f)), new Vector3(frame * 0.6f, 0.15f, PlateDepth), "black");
            }
        }
        private const float Sill = 0.08f;
        private const float Strip = 0.25f;

        private enum Style { Slide, Jaw, Blast }

        private struct Box
        {
            public Vector3 C, S;
            public string Color;
            public Box(Vector3 c, Vector3 s, string color) { C = c; S = s; Color = color; }
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
            BuildFrame(model, material, look, min, max, z, depth, style, ix0, ix1);

            float leaf = Mathf.Min(style == Style.Blast ? 0.2f : 0.14f, depth * 0.7f);
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
            float ix0, float ix1)
        {
            float w = max.x - min.x, h = max.y - min.y, cx = (min.x + max.x) / 2, cy = (min.y + max.y) / 2;
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
                    foreach (float side in new[] { -1f, 1f })
                    {
                        m.Box(new Vector3(sx, y + 0.25f, z + side * (depth / 2 + 0.012f)), new Vector3(0.04f, 0.04f, 0.02f), "steel");
                    }
                }
            }
            m.Box(new Vector3(cx, max.y - Frame / 2, z), new Vector3(w, Frame, depth), "dark");
            m.Box(new Vector3(cx, max.y - Frame - 0.03f, z), new Vector3(w - 2 * Frame, 0.06f, depth * 0.8f), "grey");
            for (float x = min.x + 0.5f; x < max.x - 0.4f; x += 0.5f)
            {
                foreach (float side in new[] { -1f, 1f })
                {
                    m.Box(new Vector3(x, max.y - Frame / 2, z + side * (depth / 2 + 0.012f)), new Vector3(0.04f, 0.04f, 0.02f), "steel");
                }
            }
            foreach (float lx in style == Style.Slide ? new[] { cx } : new[] { min.x + w * 0.25f, max.x - w * 0.25f })
            {
                foreach (float side in new[] { -1f, 1f })
                {
                    float lz = z + side * (depth / 2 + 0.05f);
                    m.Box(new Vector3(lx, max.y - Frame / 2, lz), new Vector3(0.24f, 0.14f, 0.1f), "black");
                    m.Box(new Vector3(lx, max.y - Frame / 2, lz + side * 0.04f), new Vector3(0.18f, 0.1f, 0.04f), "orange");
                    m.Box(new Vector3(lx, max.y - Frame / 2, lz + side * 0.06f), new Vector3(0.1f, 0.06f, 0.02f), "yellow");
                }
            }
            m.Box(new Vector3(cx, min.y + Sill / 2, z), new Vector3(w - 2 * Frame, Sill, depth * 0.9f), "black");
            for (float x = ix0; x < ix1 - 0.01f; x += 0.3f)
            {
                float x1 = Mathf.Min(x + 0.15f, ix1);
                foreach (float side in new[] { -1f, 1f })
                {
                    m.Box(new Vector3((x + x1) / 2, min.y + Sill / 2, z + side * depth * 0.45f), new Vector3(x1 - x, Sill * 0.9f, 0.012f), "yellow");
                }
            }
            foreach (float side in new[] { -1f, 1f })
            {
                float pz = z + side * (depth / 2 + 0.04f);
                float px = max.x - Frame / 2, py = min.y + Mathf.Min(1.3f, h * 0.45f);
                m.Box(new Vector3(px, py, pz), new Vector3(Frame * 0.85f, 0.42f, 0.07f), "white");
                m.Box(new Vector3(px, py + 0.09f, pz + side * 0.036f), new Vector3(Frame * 0.6f, 0.13f, 0.01f), "screen");
                m.Box(new Vector3(px - 0.04f, py - 0.08f, pz + side * 0.04f), new Vector3(0.05f, 0.05f, 0.02f), "orange");
                m.Box(new Vector3(px + 0.04f, py - 0.08f, pz + side * 0.04f), new Vector3(0.05f, 0.05f, 0.02f), "yellow");
                m.Box(new Vector3(px, py - 0.16f, pz + side * 0.038f), new Vector3(Frame * 0.6f, 0.03f, 0.012f), "dark");
            }
            if (style != Style.Slide)
            {
                foreach (float sx in new[] { min.x + Frame + 0.08f, max.x - Frame - 0.08f })
                {
                    foreach (float side in new[] { -1f, 1f })
                    {
                        float pz = z + side * (depth / 2 + 0.05f);
                        m.Box(new Vector3(sx, max.y - Frame - 0.5f, pz), new Vector3(0.12f, 0.9f, 0.1f), "steel");
                        m.Box(new Vector3(sx, max.y - Frame - 1.15f, pz), new Vector3(0.06f, 0.5f, 0.06f), "light");
                        m.Box(new Vector3(sx, max.y - Frame - 0.06f, pz), new Vector3(0.16f, 0.1f, 0.12f), "orange");
                    }
                }
            }
            PartFactory.CreateRenderer("Frame", model, m.ToMesh("BigDoorFrame"), material, look);
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
            PartFactory.CreateRenderer("Frame", model, m.ToMesh("BigDoorFrame"), material, look);

            float d = Mathf.Min(0.1f, depth * 0.6f), wl = ix1 - ix0 - 0.01f, hl = iy1 - iy0, lcx = (ix0 + ix1) / 2, lcy = (iy0 + iy1) / 2;
            int slats = Mathf.Max(1, Mathf.CeilToInt(hl / Strip));
            float pitch = hl / slats;
            var leaf = new List<Box> { new Box(new Vector3(lcx, lcy, z), new Vector3(wl, hl, d * 0.6f), "dark") };
            for (int i = 0; i < slats; i++)
            {
                float sy = iy0 + pitch * (i + 0.5f);
                leaf.Add(new Box(new Vector3(lcx, sy, z), new Vector3(wl, pitch - 0.03f, d), "light"));
                foreach (float side in new[] { -1f, 1f })
                {
                    float fz = z + side * (d / 2 + 0.004f);
                    leaf.Add(new Box(new Vector3(lcx, sy + pitch * 0.18f, fz), new Vector3(wl - 0.04f, 0.022f, 0.008f), "grey"));
                    leaf.Add(new Box(new Vector3(lcx, sy - pitch * 0.18f, fz), new Vector3(wl - 0.04f, 0.022f, 0.008f), "grey"));
                }
            }
            foreach (float side in new[] { -1f, 1f })
            {
                float fz = z + side * (d / 2 + 0.008f);
                leaf.Add(new Box(new Vector3(lcx, iy0 + pitch * 0.5f, fz), new Vector3(0.22f, 0.05f, 0.03f), "steel"));
            }
            leaf.Add(new Box(new Vector3(lcx, iy0 + 0.02f, z), new Vector3(wl, 0.04f, d + 0.02f), "rubber"));
            AddLeaf(model, material, look, leaf, 'y', 1, hl, iy0, iy1, iy0, iy1);
            new GameObject(NarrowMarker).transform.SetParent(model, false);
        }

        private static List<Box> SlideLeaf(float x0, float x1, float y0, float y1, float z, float d, float mid)
        {
            var list = new List<Box>();
            float cx = (x0 + x1) / 2, cy = (y0 + y1) / 2, wl = x1 - x0 - 0.01f, hl = y1 - y0;
            bool left = x1 <= mid + 0.001f;
            float edge = left ? x1 : x0, outer = left ? x0 : x1, inward = left ? -1f : 1f;
            list.Add(new Box(new Vector3(cx, cy, z), new Vector3(wl, hl, d), "light"));
            foreach (float side in new[] { -1f, 1f })
            {
                float fz = z + side * (d / 2 + 0.006f);
                list.Add(new Box(new Vector3(cx, y1 - 0.05f, fz), new Vector3(wl, 0.1f, 0.012f), "grey"));
                list.Add(new Box(new Vector3(cx, y0 + 0.28f, fz), new Vector3(wl, 0.04f, 0.012f), "grey"));
                list.Add(new Box(new Vector3(outer - inward * 0.05f, cy, fz), new Vector3(0.1f, hl, 0.012f), "grey"));
                for (float y = y0 + 0.9f; y < y1 - 0.5f; y += 0.9f)
                {
                    list.Add(new Box(new Vector3(cx, y, fz), new Vector3(wl - 0.2f, 0.025f, 0.012f), "grey"));
                }
                if (hl > 2.2f)
                {
                    float wy = y1 - 0.65f, ww = wl * 0.42f, wx = cx - inward * 0.05f;
                    list.Add(new Box(new Vector3(wx, wy, fz), new Vector3(ww + 0.08f, 0.5f, 0.016f), "dark"));
                    list.Add(new Box(new Vector3(wx, wy, fz + side * 0.004f), new Vector3(ww, 0.42f, 0.012f), "screen"));
                }
                list.Add(new Box(new Vector3(edge + inward * 0.2f, cy, fz), new Vector3(0.3f, hl, 0.014f), "black"));
                for (float y = y0; y < y1 - 0.05f; y += 0.1f)
                {
                    int step = Mathf.RoundToInt((y - y0) / 0.1f) % 6;
                    if (step < 3)
                    {
                        float kx = edge + inward * (0.07f + step * 0.1f);
                        list.Add(new Box(new Vector3(kx, y + 0.05f, fz + side * 0.003f), new Vector3(0.1f, 0.1f, 0.014f), "yellow"));
                    }
                }
                float hx = edge + inward * 0.5f, hy = y0 + Mathf.Min(1.1f, hl * 0.4f);
                list.Add(new Box(new Vector3(hx, hy, fz), new Vector3(0.14f, 0.5f, 0.012f), "dark"));
                list.Add(new Box(new Vector3(hx, hy, fz + side * 0.03f), new Vector3(0.04f, 0.4f, 0.05f), "steel"));
                list.Add(new Box(new Vector3(cx - inward * 0.15f, y1 - 0.25f, fz), new Vector3(0.3f, 0.12f, 0.014f), "yellow"));
                for (float x = Mathf.Min(x0, x1) + 0.15f; x < Mathf.Max(x0, x1) - 0.1f; x += 0.3f)
                {
                    list.Add(new Box(new Vector3(x, y0 + 0.15f, fz + side * 0.006f), new Vector3(0.035f, 0.035f, 0.016f), "steel"));
                }
            }
            list.Add(new Box(new Vector3(cx, y0 + 0.12f, z), new Vector3(wl, 0.24f, d + 0.02f), "orange"));
            list.Add(new Box(new Vector3(edge + inward * 0.015f, cy, z), new Vector3(0.03f, hl, d + 0.03f), "rubber"));
            return list;
        }

        private static List<Box> BlastLeaf(float x0, float x1, float y0, float y1, float z, float d, float mid)
        {
            var list = new List<Box>();
            float cx = (x0 + x1) / 2, cy = (y0 + y1) / 2, wl = x1 - x0 - 0.01f, hl = y1 - y0;
            bool left = x1 <= mid + 0.001f;
            float edge = left ? x1 : x0, inward = left ? -1f : 1f;
            list.Add(new Box(new Vector3(cx, cy, z), new Vector3(wl, hl, d), "grey"));
            foreach (float side in new[] { -1f, 1f })
            {
                float fz = z + side * (d / 2 + 0.01f);
                for (float x = Mathf.Min(x0, x1) + 0.4f; x < Mathf.Max(x0, x1) - 0.6f; x += 0.8f)
                {
                    list.Add(new Box(new Vector3(x, cy, fz), new Vector3(0.14f, hl - 0.2f, 0.06f), "dark"));
                    for (float y = y0 + 0.3f; y < y1 - 0.2f; y += 0.6f)
                    {
                        list.Add(new Box(new Vector3(x, y, fz + side * 0.035f), new Vector3(0.05f, 0.05f, 0.02f), "steel"));
                    }
                }
                foreach (float y in new[] { y0 + 0.5f, cy, y1 - 0.5f })
                {
                    list.Add(new Box(new Vector3(cx, y, fz + side * 0.02f), new Vector3(wl, 0.18f, 0.04f), "dark"));
                }
                const float band = 0.9f;
                list.Add(new Box(new Vector3(edge + inward * band / 2, cy, fz + side * 0.03f), new Vector3(band, hl, 0.02f), "black"));
                for (float y = y0; y < y1 - 0.1f; y += 0.15f)
                {
                    int step = Mathf.RoundToInt((y - y0) / 0.15f);
                    for (int k = 0; k < 6; k++)
                    {
                        if ((k + step) % 6 < 3)
                        {
                            float kx = edge + inward * (0.075f + k * 0.15f);
                            list.Add(new Box(new Vector3(kx, y + 0.075f, fz + side * 0.035f), new Vector3(0.15f, 0.15f, 0.02f), "yellow"));
                        }
                    }
                }
                list.Add(new Box(new Vector3(edge + inward * 1.8f, y1 - 0.9f, fz + side * 0.02f), new Vector3(0.9f, 0.5f, 0.03f), "orange"));
                list.Add(new Box(new Vector3(edge + inward * 1.8f, y1 - 0.9f, fz + side * 0.03f), new Vector3(0.5f, 0.25f, 0.02f), "white"));
                list.Add(new Box(new Vector3(edge + inward * 1.8f, cy - 0.2f, fz + side * 0.02f), new Vector3(0.7f, 0.3f, 0.03f), "black"));
                list.Add(new Box(new Vector3(edge + inward * 1.8f, cy - 0.2f, fz + side * 0.03f), new Vector3(0.6f, 0.2f, 0.02f), "screen"));
            }
            list.Add(new Box(new Vector3(cx, y0 + 0.06f, z), new Vector3(wl, 0.12f, d + 0.04f), "rubber"));
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
                    list.Add(new Box(new Vector3(tx, edge - inward * 0.06f, z), new Vector3(tw - 0.02f, 0.12f, d), "yellow"));
                    list.Add(new Box(new Vector3(tx, edge - inward * 0.11f, z), new Vector3(tw - 0.06f, 0.02f, d + 0.01f), "black"));
                }
            }
            foreach (float side in new[] { -1f, 1f })
            {
                float fz = z + side * (d / 2 + 0.008f);
                list.Add(new Box(new Vector3(cx, edge + inward * 0.2f, fz), new Vector3(wl, 0.16f, 0.014f), "black"));
                for (float x = x0; x < x1 - 0.05f; x += 0.2f)
                {
                    list.Add(new Box(new Vector3(x + 0.05f, edge + inward * 0.2f, fz + side * 0.003f), new Vector3(0.1f, 0.16f, 0.014f), "yellow"));
                }
                for (float x = x0 + 0.5f; x < x1 - 0.3f; x += 1f)
                {
                    list.Add(new Box(new Vector3(x, cy + inward * 0.1f, fz), new Vector3(0.1f, hl - 0.4f, 0.03f), "grey"));
                }
                list.Add(new Box(new Vector3(cx, top ? y1 - 0.12f : y0 + 0.12f, fz), new Vector3(wl, 0.08f, 0.02f), "dark"));
                if (top)
                {
                    for (float x = x0 + 1f; x < x1 - 0.8f; x += 2f)
                    {
                        list.Add(new Box(new Vector3(x + 0.5f, cy + 0.15f, fz), new Vector3(0.6f, 0.3f, 0.016f), "dark"));
                        list.Add(new Box(new Vector3(x + 0.5f, cy + 0.15f, fz + side * 0.004f), new Vector3(0.52f, 0.22f, 0.012f), "screen"));
                    }
                }
                else
                {
                    list.Add(new Box(new Vector3(cx, cy - 0.1f, fz), new Vector3(0.6f, 0.2f, 0.016f), "orange"));
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
                    float b0 = (axis == 'x' ? box.C.x : box.C.y) - (axis == 'x' ? box.S.x : box.S.y) / 2;
                    float b1 = b0 + (axis == 'x' ? box.S.x : box.S.y);
                    float c0 = Mathf.Max(b0, s0), c1 = Mathf.Min(b1, s1);
                    if (c1 - c0 < 0.0005f) continue;
                    Vector3 c = box.C, s = box.S;
                    if (axis == 'x') { c.x = (c0 + c1) / 2 - sc; s.x = c1 - c0; }
                    else { c.y = (c0 + c1) / 2 - sc; s.y = c1 - c0; }
                    m.Box(c, s, box.Color);
                }
                string name = string.Format(CultureInfo.InvariantCulture, "P|{0}|{1}|{2}|{3}|{4}|{5}", axis, dir, distance, clipLo, clipHi, step);
                GameObject piece = PartFactory.CreateRenderer(name, model, m.ToMesh("BigDoorStrip"), material, look);
                piece.transform.localPosition = axis == 'x' ? new Vector3(sc, 0, 0) : new Vector3(0, sc, 0);
            }
        }

        private const int Grid = 4;
        private const float SolidU = 0.55f, SolidV = 0.58f;

        private sealed class MeshBuilder
        {
            private readonly List<Vector3> _v = new List<Vector3>();
            private readonly List<Vector3> _n = new List<Vector3>();
            private readonly List<Vector2> _uv = new List<Vector2>();
            private readonly List<int> _t = new List<int>();

            public void Box(Vector3 center, Vector3 size, string color)
            {
                int cell = System.Array.IndexOf(Names, HullStyle(color));
                Vector3 min = center - size * 0.5f, max = center + size * 0.5f;
                for (int k = 0; k < 3; k++)
                {
                    int a = k == 0 ? 2 : 0, b = k == 1 ? 2 : 1;
                    foreach (int sign in new[] { -1, 1 })
                    {
                        var n = new Vector3();
                        n[k] = sign;
                        Quad(n, k, a, b, sign > 0 ? max[k] : min[k], min[a], max[a], min[b], max[b], cell);
                    }
                }
            }

            private void Quad(Vector3 n, int k, int a, int b, float plane, float a0, float a1, float b0, float b1, int cell)
            {
                int start = _v.Count;
                var uv = new Vector2((cell % Grid + SolidU) / Grid, 1f - (cell / Grid + 1f - SolidV) / Grid);
                foreach (var (pa, pb) in new[] { (a0, b0), (a0, b1), (a1, b1), (a1, b0) })
                {
                    var p = new Vector3();
                    p[k] = plane; p[a] = pa; p[b] = pb;
                    _v.Add(p);
                    _n.Add(n);
                    _uv.Add(uv);
                }
                bool flip = Vector3.Dot(Vector3.Cross(_v[start + 1] - _v[start], _v[start + 2] - _v[start]), n) < 0f;
                if (flip)
                {
                    _t.Add(start); _t.Add(start + 2); _t.Add(start + 1);
                    _t.Add(start); _t.Add(start + 3); _t.Add(start + 2);
                }
                else
                {
                    _t.Add(start); _t.Add(start + 1); _t.Add(start + 2);
                    _t.Add(start); _t.Add(start + 2); _t.Add(start + 3);
                }
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
            _sound = new Audio.LoopEmitter(_soundName, 45f, MiningMod.DoorVolume);
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
                Audio.OneShot.Play(_soundName + "_start", transform.position, _hear, MiningMod.DoorVolume.Value, _pitch);
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
                    Audio.OneShot.Play(_soundName + "_stop", transform.position, _hear, MiningMod.DoorVolume.Value, _pitch);
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
