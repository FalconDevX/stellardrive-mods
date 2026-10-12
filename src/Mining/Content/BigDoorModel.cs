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
    // Własny model dużych drzwi, budowany w kodzie dla każdego rozmiaru (rama zawsze tej samej grubości, nie rozciągnięta).
    // Tekstura door_albedo.png w stylu gry (kontur, wytarte krawędzie, kreskowanie). Trzy style:
    //  - Slide (3x3, 4x4): dwa skrzydła rozsuwane na boki,
    //  - Jaw (6x3): "szczęki" - górna połowa w górę, dolna w dół, zęby na styku,
    //  - Blast (10x5): pancerne drzwi hangarowe - dwie ciężkie połowy na boki, żebra, wielkie pasy ostrzegawcze.
    // Skrzydła są pocięte na wąskie paski: pasek, który wjeżdża w ramę, jest przycinany i znika (nic nie wystaje).
    internal static class BigDoorModel
    {
        private static readonly string[] Names =
        {
            "yellow", "yellow_dark", "dark", "grey", "light", "orange", "steel", "screen", "black", "rubber", "olive", "white", "cyan", "blue",
        };

        internal const string ModelName = "BigDoorModel";
        private const float Frame = 0.22f;
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
            float depth = Mathf.Clamp(b.size.z, 0.25f, 0.35f); // grubość ściany (obrys drzwi z gry bywa na całe pole)
            Style style = w >= 8f ? Style.Blast : w > h ? Style.Jaw : Style.Slide;

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

        // --- rama: słupy z żebrami i nitami, nadproże z lampami, próg w pasy, panele sterowania, siłowniki ---
        private static void BuildFrame(Transform model, Material material, MeshRenderer look, Vector3 min, Vector3 max, float z, float depth, Style style,
            float ix0, float ix1)
        {
            float w = max.x - min.x, h = max.y - min.y, cx = (min.x + max.x) / 2, cy = (min.y + max.y) / 2;
            var m = new MeshBuilder();
            foreach (float sx in new[] { min.x + Frame / 2, max.x - Frame / 2 })
            {
                m.Box(new Vector3(sx, cy, z), new Vector3(Frame, h, depth), "dark");
                m.Box(new Vector3(sx, cy, z), new Vector3(Frame * 0.5f, h - 0.3f, depth + 0.02f), "black");        // rowek w słupie
                m.Box(new Vector3(sx, max.y - 0.18f, z), new Vector3(Frame + 0.06f, 0.36f, depth + 0.06f), "orange");
                m.Box(new Vector3(sx, max.y - 0.18f, z), new Vector3(Frame + 0.07f, 0.06f, depth + 0.07f), "black");
                m.Box(new Vector3(sx, min.y + 0.16f, z), new Vector3(Frame + 0.06f, 0.32f, depth + 0.06f), "orange");
                for (float y = min.y + 0.55f; y < max.y - 0.5f; y += 0.5f)
                {
                    m.Box(new Vector3(sx, y, z), new Vector3(Frame + 0.02f, 0.05f, depth + 0.02f), "grey");          // żebra
                    foreach (float side in new[] { -1f, 1f })
                    {
                        m.Box(new Vector3(sx, y + 0.25f, z + side * (depth / 2 + 0.012f)), new Vector3(0.04f, 0.04f, 0.02f), "steel"); // nity
                    }
                }
            }
            m.Box(new Vector3(cx, max.y - Frame / 2, z), new Vector3(w, Frame, depth), "dark");
            m.Box(new Vector3(cx, max.y - Frame - 0.03f, z), new Vector3(w - 2 * Frame, 0.06f, depth * 0.8f), "grey"); // prowadnica
            for (float x = min.x + 0.5f; x < max.x - 0.4f; x += 0.5f)
            {
                foreach (float side in new[] { -1f, 1f })
                {
                    m.Box(new Vector3(x, max.y - Frame / 2, z + side * (depth / 2 + 0.012f)), new Vector3(0.04f, 0.04f, 0.02f), "steel");
                }
            }
            // lampy ostrzegawcze na nadprożu (po obu stronach ściany)
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
            // próg: czarny z żółtymi pasami (po obu stronach)
            m.Box(new Vector3(cx, min.y + Sill / 2, z), new Vector3(w - 2 * Frame, Sill, depth * 0.9f), "black");
            for (float x = ix0; x < ix1 - 0.01f; x += 0.3f)
            {
                float x1 = Mathf.Min(x + 0.15f, ix1);
                foreach (float side in new[] { -1f, 1f })
                {
                    m.Box(new Vector3((x + x1) / 2, min.y + Sill / 2, z + side * depth * 0.45f), new Vector3(x1 - x, Sill * 0.9f, 0.012f), "yellow");
                }
            }
            // panele sterowania na prawym słupie (przód i tył): obudowa, ekran, przyciski
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
            // siłowniki przy większych drzwiach (pionowe tłoki przy słupach, po obu stronach ściany)
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

        // --- skrzydło rozsuwane (3x3, 4x4) ---
        private static List<Box> SlideLeaf(float x0, float x1, float y0, float y1, float z, float d, float mid)
        {
            var list = new List<Box>();
            float cx = (x0 + x1) / 2, cy = (y0 + y1) / 2, wl = x1 - x0 - 0.01f, hl = y1 - y0;
            bool left = x1 <= mid + 0.001f;
            float edge = left ? x1 : x0, outer = left ? x0 : x1, inward = left ? -1f : 1f; // inward: od krawędzi środkowej w głąb skrzydła
            list.Add(new Box(new Vector3(cx, cy, z), new Vector3(wl, hl, d), "light"));
            foreach (float side in new[] { -1f, 1f })
            {
                float fz = z + side * (d / 2 + 0.006f);
                // obramowanie skrzydła i szwy
                list.Add(new Box(new Vector3(cx, y1 - 0.05f, fz), new Vector3(wl, 0.1f, 0.012f), "grey"));
                list.Add(new Box(new Vector3(cx, y0 + 0.28f, fz), new Vector3(wl, 0.04f, 0.012f), "grey"));
                list.Add(new Box(new Vector3(outer - inward * 0.05f, cy, fz), new Vector3(0.1f, hl, 0.012f), "grey"));
                for (float y = y0 + 0.9f; y < y1 - 0.5f; y += 0.9f)
                {
                    list.Add(new Box(new Vector3(cx, y, fz), new Vector3(wl - 0.2f, 0.025f, 0.012f), "grey"));
                }
                // okno w ciemnej ramce
                if (hl > 2.2f)
                {
                    float wy = y1 - 0.65f, ww = wl * 0.42f, wx = cx - inward * 0.05f;
                    list.Add(new Box(new Vector3(wx, wy, fz), new Vector3(ww + 0.08f, 0.5f, 0.016f), "dark"));
                    list.Add(new Box(new Vector3(wx, wy, fz + side * 0.004f), new Vector3(ww, 0.42f, 0.012f), "screen"));
                }
                // pasy ukośne przy krawędzi środkowej (schodkowo: żółte kostki na czarnym)
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
                // uchwyt we wnęce, tabliczka, nity
                float hx = edge + inward * 0.5f, hy = y0 + Mathf.Min(1.1f, hl * 0.4f);
                list.Add(new Box(new Vector3(hx, hy, fz), new Vector3(0.14f, 0.5f, 0.012f), "dark"));
                list.Add(new Box(new Vector3(hx, hy, fz + side * 0.03f), new Vector3(0.04f, 0.4f, 0.05f), "steel"));
                list.Add(new Box(new Vector3(cx - inward * 0.15f, y1 - 0.25f, fz), new Vector3(0.3f, 0.12f, 0.014f), "yellow"));
                for (float x = Mathf.Min(x0, x1) + 0.15f; x < Mathf.Max(x0, x1) - 0.1f; x += 0.3f)
                {
                    list.Add(new Box(new Vector3(x, y0 + 0.15f, fz + side * 0.006f), new Vector3(0.035f, 0.035f, 0.016f), "steel"));
                }
            }
            list.Add(new Box(new Vector3(cx, y0 + 0.12f, z), new Vector3(wl, 0.24f, d + 0.02f), "orange"));            // listwa dolna
            list.Add(new Box(new Vector3(edge + inward * 0.015f, cy, z), new Vector3(0.03f, hl, d + 0.03f), "rubber"));  // uszczelka
            return list;
        }

        // --- pancerna połowa drzwi hangarowych (10x5) ---
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
                // pionowe żebra co 0.8 m z nitami
                for (float x = Mathf.Min(x0, x1) + 0.4f; x < Mathf.Max(x0, x1) - 0.6f; x += 0.8f)
                {
                    list.Add(new Box(new Vector3(x, cy, fz), new Vector3(0.14f, hl - 0.2f, 0.06f), "dark"));
                    for (float y = y0 + 0.3f; y < y1 - 0.2f; y += 0.6f)
                    {
                        list.Add(new Box(new Vector3(x, y, fz + side * 0.035f), new Vector3(0.05f, 0.05f, 0.02f), "steel"));
                    }
                }
                // poziome pasy wzmocnień
                foreach (float y in new[] { y0 + 0.5f, cy, y1 - 0.5f })
                {
                    list.Add(new Box(new Vector3(cx, y, fz + side * 0.02f), new Vector3(wl, 0.18f, 0.04f), "dark"));
                }
                // wielkie pasy ostrzegawcze przy krawędzi środkowej (schodkowo, żółte na czarnym)
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
                // tabliczka z numerem hangaru i okienko inspekcyjne
                list.Add(new Box(new Vector3(edge + inward * 1.8f, y1 - 0.9f, fz + side * 0.02f), new Vector3(0.9f, 0.5f, 0.03f), "orange"));
                list.Add(new Box(new Vector3(edge + inward * 1.8f, y1 - 0.9f, fz + side * 0.03f), new Vector3(0.5f, 0.25f, 0.02f), "white"));
                list.Add(new Box(new Vector3(edge + inward * 1.8f, cy - 0.2f, fz + side * 0.02f), new Vector3(0.7f, 0.3f, 0.03f), "black"));
                list.Add(new Box(new Vector3(edge + inward * 1.8f, cy - 0.2f, fz + side * 0.03f), new Vector3(0.6f, 0.2f, 0.02f), "screen"));
            }
            list.Add(new Box(new Vector3(cx, y0 + 0.06f, z), new Vector3(wl, 0.12f, d + 0.04f), "rubber"));             // uszczelka dolna
            list.Add(new Box(new Vector3(edge + inward * 0.02f, cy, z), new Vector3(0.04f, hl, d + 0.06f), "black"));    // krawędź styku
            return list;
        }

        // --- połowa drzwi "szczęk" (6x3): zęby na styku zazębiają się ---
        private static List<Box> JawHalf(float x0, float x1, float y0, float y1, float z, float d, bool top)
        {
            var list = new List<Box>();
            float cx = (x0 + x1) / 2, cy = (y0 + y1) / 2, wl = x1 - x0, hl = y1 - y0;
            float edge = top ? y0 : y1, inward = top ? 1f : -1f;
            list.Add(new Box(new Vector3(cx, cy, z), new Vector3(wl, hl - 0.01f, d), "light"));
            // zęby: na górnej połowie co drugi, na dolnej - pozostałe (zachodzą na siebie 0.12 m)
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
                list.Add(new Box(new Vector3(cx, edge + inward * 0.2f, fz), new Vector3(wl, 0.16f, 0.014f), "black"));      // pas przy styku
                for (float x = x0; x < x1 - 0.05f; x += 0.2f)
                {
                    list.Add(new Box(new Vector3(x + 0.05f, edge + inward * 0.2f, fz + side * 0.003f), new Vector3(0.1f, 0.16f, 0.014f), "yellow"));
                }
                for (float x = x0 + 0.5f; x < x1 - 0.3f; x += 1f)                                                         // pionowe żebra
                {
                    list.Add(new Box(new Vector3(x, cy + inward * 0.1f, fz), new Vector3(0.1f, hl - 0.4f, 0.03f), "grey"));
                }
                list.Add(new Box(new Vector3(cx, top ? y1 - 0.12f : y0 + 0.12f, fz), new Vector3(wl, 0.08f, 0.02f), "dark"));
                if (top)
                {
                    for (float x = x0 + 1f; x < x1 - 0.8f; x += 2f)                                                       // okienka w górnej połowie
                    {
                        list.Add(new Box(new Vector3(x + 0.5f, cy + 0.15f, fz), new Vector3(0.6f, 0.3f, 0.016f), "dark"));
                        list.Add(new Box(new Vector3(x + 0.5f, cy + 0.15f, fz + side * 0.004f), new Vector3(0.52f, 0.22f, 0.012f), "screen"));
                    }
                }
                else
                {
                    list.Add(new Box(new Vector3(cx, cy - 0.1f, fz), new Vector3(0.6f, 0.2f, 0.016f), "orange"));            // tabliczka
                }
            }
            return list;
        }

        // Skrzydło pocięte na paski wzdłuż osi ruchu; każdy pasek to osobny obiekt (nazwa niesie dane do przycinania).
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
                    Vector3 c = box.C, s = box.S, shift = axis == 'x' ? new Vector3(sc, 0, 0) : new Vector3(0, sc, 0);
                    if (axis == 'x') { c.x = (c0 + c1) / 2 - sc; s.x = c1 - c0; }
                    else { c.y = (c0 + c1) / 2 - sc; s.y = c1 - c0; }
                    m.Box(c, s, box.Color, box.C - box.S * 0.5f - shift, box.C + box.S * 0.5f - shift);
                }
                string name = string.Format(CultureInfo.InvariantCulture, "P|{0}|{1}|{2}|{3}|{4}|{5}", axis, dir, distance, clipLo, clipHi, step);
                GameObject piece = PartFactory.CreateRenderer(name, model, m.ToMesh("BigDoorStrip"), material, look);
                piece.transform.localPosition = axis == 'x' ? new Vector3(sc, 0, 0) : new Vector3(0, sc, 0);
            }
        }

        // Tekstura door_albedo.png (AssetSource/build_door_texture.py): pola GRID x GRID w kolejności Names, każde z czarnym
        // konturem, wytartą krawędzią i kreskowaniem jak w teksturach gry. Ścianki mapujemy "9-slice": brzeg pola (Margin)
        // trafia na pas szerokości Border metrów przy krawędzi bryły, więc kontur ma wszędzie tę samą grubość. Środek
        // dużej ścianki dzielimy na prawie kwadratowe kafle (0,6-1,5 m), żeby kreskowanie i plamki nie były rozciągnięte;
        // co drugi kafel w poziomie jest odbity, żeby wzór mniej się powtarzał.
        private const int Grid = 4;
        private const float Margin = 26f / 256f;
        private const float Border = 0.045f;
        private const float Inset = 1.5f / 256f;
        private const float MinTile = 0.6f, MaxTile = 1.5f;

        // Oś ścianki: obrys bryły przed przycięciem [F0, F1] i podział jej środka na kafle.
        private struct Axis
        {
            public float F0, F1, TileLen;
            public int Tiles;
            public bool Sliced => F1 - F0 >= Border * 2.5f;
            public float Inner => F1 - F0 - 2f * Border;

            public int TileAt(float p) => Mathf.Clamp(Mathf.FloorToInt((p - F0 - Border) / TileLen), 0, Tiles - 1);

            // położenie p -> współrzędna w polu (0..1), w kaflu `tile`
            public float Map(float p, int tile, bool mirror)
            {
                float l = F1 - F0, t = Mathf.Clamp(p - F0, 0f, l);
                if (!Sliced) return t / l;                                 // cienka ścianka: całe pole (sam kontur = obrys)
                if (t <= Border) return Margin * t / Border;
                if (t >= l - Border) return 1f - Margin * (l - t) / Border;
                float local = Mathf.Clamp01((t - Border - tile * TileLen) / TileLen);
                if (mirror && tile % 2 == 1) local = 1f - local;
                return Margin + (1f - 2f * Margin) * local;
            }
        }

        private sealed class MeshBuilder
        {
            private readonly List<Vector3> _v = new List<Vector3>();
            private readonly List<Vector3> _n = new List<Vector3>();
            private readonly List<Vector2> _uv = new List<Vector2>();
            private readonly List<int> _t = new List<int>();
            private readonly List<float> _ca = new List<float>(8), _cb = new List<float>(8);

            public void Box(Vector3 center, Vector3 size, string color) => Box(center, size, color, center - size * 0.5f, center + size * 0.5f);

            // fullMin/fullMax: obrys całej bryły przed przycięciem do paska skrzydła - tekstura liczona z niego, żeby na
            // styku pasków nie rysował się kontur.
            public void Box(Vector3 center, Vector3 size, string color, Vector3 fullMin, Vector3 fullMax)
            {
                int cell = System.Array.IndexOf(Names, color);
                Vector3 min = center - size * 0.5f, max = center + size * 0.5f;
                for (int k = 0; k < 3; k++)
                {
                    // osie w płaszczyźnie ścianki: na bokach v = Y (kreskowanie na dole), na górze i dole v = Z
                    int a = k == 0 ? 2 : 0, b = k == 1 ? 2 : 1;
                    var ax = new Axis { F0 = fullMin[a], F1 = fullMax[a] };
                    var bx = new Axis { F0 = fullMin[b], F1 = fullMax[b] };
                    float shorter = Mathf.Min(ax.Sliced ? ax.Inner : float.MaxValue, bx.Sliced ? bx.Inner : float.MaxValue);
                    float tile = Mathf.Clamp(shorter, MinTile, MaxTile);
                    ax = Tiled(ax, tile);
                    bx = Tiled(bx, tile);
                    Cuts(_ca, min[a], max[a], ax);
                    Cuts(_cb, min[b], max[b], bx);
                    foreach (int sign in new[] { -1, 1 })
                    {
                        var n = new Vector3();
                        n[k] = sign;
                        float plane = sign > 0 ? max[k] : min[k];
                        for (int i = 0; i + 1 < _ca.Count; i++)
                        {
                            for (int j = 0; j + 1 < _cb.Count; j++)
                            {
                                Quad(n, k, a, b, plane, _ca[i], _ca[i + 1], _cb[j], _cb[j + 1], cell, ax, bx);
                            }
                        }
                    }
                }
            }

            private static Axis Tiled(Axis axis, float tile)
            {
                axis.Tiles = axis.Sliced ? Mathf.Max(1, Mathf.RoundToInt(axis.Inner / tile)) : 1;
                axis.TileLen = axis.Sliced ? axis.Inner / axis.Tiles : 1f;
                return axis;
            }

            private static void Cuts(List<float> cuts, float lo, float hi, Axis axis)
            {
                cuts.Clear();
                cuts.Add(lo);
                if (axis.Sliced)
                {
                    for (int i = 0; i <= axis.Tiles; i++)
                    {
                        float c = axis.F0 + Border + i * axis.TileLen;
                        if (c > lo + 1e-4f && c < hi - 1e-4f) cuts.Add(c);
                    }
                }
                cuts.Add(hi);
            }

            private void Quad(Vector3 n, int k, int a, int b, float plane, float a0, float a1, float b0, float b1, int cell, Axis ax, Axis bx)
            {
                int start = _v.Count;
                float cx = cell % Grid, cy = cell / Grid;
                int ta = ax.TileAt((a0 + a1) / 2), tb = bx.TileAt((b0 + b1) / 2);
                foreach (var (pa, pb) in new[] { (a0, b0), (a0, b1), (a1, b1), (a1, b0) })
                {
                    var p = new Vector3();
                    p[k] = plane; p[a] = pa; p[b] = pb;
                    _v.Add(p);
                    _n.Add(n);
                    float fu = Inset + ax.Map(pa, ta, true) * (1f - 2f * Inset);
                    float fv = Inset + bx.Map(pb, tb, false) * (1f - 2f * Inset);
                    _uv.Add(new Vector2((cx + fu) / Grid, 1f - (cy + 1f - fv) / Grid));
                }
                // kolejność zgodna z ruchem wskazówek patrząc z zewnątrz (przód ścianki w Unity)
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

    // Animacja: paski skrzydeł przesuwają się (na boki albo w górę/dół), a część, która wjechała w ramę, jest
    // przycinana (pasek zwęża się do widocznego kawałka, a całkiem schowany - znika). Stan jak w drzwiach gry.
    internal class BigDoorVisuals : DefaultShipPartVisuals
    {
        private const float Seconds = 1.6f;

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
                ServiceLocator.GetService<IAudioPlayer>()?.PlayAudioEvent3D(open ? "Event:/Ships/DoorOpen" : "Event:/Ships/DoorClose", gameObject);
            }
            _target = open ? 1f : 0f;
        }

        private void Update()
        {
            if (Mathf.Approximately(_t, _target))
            {
                return;
            }
            _t = Mathf.MoveTowards(_t, _target, Time.deltaTime / Seconds);
            Apply();
        }

        private void Apply()
        {
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_t));
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
                // pasek ma środek w swoim punkcie; przycięty: przesuwamy środek do widocznej części i zwężamy
                if (p.AxisX) { pos.x = mid; scale.x = s; }
                else { pos.y = mid; scale.y = s; }
                p.T.localPosition = pos;
                p.T.localScale = scale;
            }
        }
    }
}
