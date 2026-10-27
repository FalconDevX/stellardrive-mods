using Odyssey.Content;
using UnityEngine;

namespace Odyssey.Blueprints
{
    internal static class WorkbenchModel
    {
        internal const float HalfWidth = 0.95f, HalfDepth = 0.43f, Height = 1.52f;
        private const float TopY = 0.92f, TopThickness = 0.06f, Leg = 0.07f;
        private const float LegX = HalfWidth - 0.07f, LegZ = HalfDepth - 0.06f;
        private const float PanelZ = HalfDepth - 0.04f;

        internal static Bounds Bounds => new Bounds(new Vector3(0f, Height / 2f, 0f), new Vector3(HalfWidth * 2f, Height, HalfDepth * 2f));

        internal static Mesh Build()
        {
            var b = new MeshBuilder();
            Frame(b);
            Worktop(b);
            Drawers(b);
            Shelf(b);
            BackPanel(b);
            Tools(b);
            Vise(b);
            return b.Build("ToolWorkbench");
        }

        private static void Frame(MeshBuilder b)
        {
            float bottom = TopY - TopThickness;
            foreach (float x in new[] { -LegX, LegX })
            {
                foreach (float z in new[] { -LegZ, LegZ })
                {
                    float top = z > 0f ? Height - 0.06f : bottom + 0.01f;
                    b.Box(new Vector3(x, top / 2f, z), new Vector3(Leg, top, Leg), "dark");
                    b.Box(new Vector3(x, 0.006f, z), new Vector3(Leg + 0.05f, 0.012f, Leg + 0.05f), "steel");
                    foreach (float sx in new[] { -1f, 1f })
                    {
                        b.Cylinder(new Vector3(x + sx * 0.042f, 0.008f, z - 0.042f), new Vector3(x + sx * 0.042f, 0.02f, z - 0.042f), 0.008f, "grey", 8);
                    }
                }
                b.Box(new Vector3(x, bottom - 0.04f, 0f), new Vector3(Leg - 0.02f, 0.08f, LegZ * 2f), "dark");
                b.Box(new Vector3(x, 0.2f, 0f), new Vector3(Leg - 0.02f, 0.05f, LegZ * 2f), "dark");
            }
            foreach (float z in new[] { -LegZ, LegZ })
            {
                b.Box(new Vector3(0f, bottom - 0.04f, z), new Vector3(LegX * 2f, 0.08f, Leg - 0.02f), "dark");
                b.Box(new Vector3(0f, 0.2f, z), new Vector3(LegX * 2f, 0.05f, Leg - 0.02f), "dark");
            }
            b.Box(new Vector3(0f, 0.235f, 0f), new Vector3(LegX * 2f - Leg, 0.02f, LegZ * 2f), "grey");
            for (float x = -0.6f; x <= 0.61f; x += 0.3f)
            {
                b.Box(new Vector3(x, 0.22f, 0f), new Vector3(0.04f, 0.02f, LegZ * 2f), "dark");
            }
        }

        private static void Worktop(MeshBuilder b)
        {
            float y = TopY - TopThickness / 2f, depth = PanelZ - 0.01f + HalfDepth;
            float z = (PanelZ - 0.01f - HalfDepth) / 2f;
            b.Box(new Vector3(0f, y, z), new Vector3(HalfWidth * 2f, TopThickness, depth), "grey");
            b.Box(new Vector3(0f, y, -HalfDepth + 0.006f), new Vector3(HalfWidth * 2f + 0.008f, TopThickness + 0.008f, 0.014f), "steel");
            foreach (float sx in new[] { -1f, 1f })
            {
                b.Box(new Vector3(sx * (HalfWidth - 0.006f), y, z), new Vector3(0.014f, TopThickness + 0.008f, depth), "steel");
            }
            b.Box(new Vector3(-0.3f, TopY + 0.002f, -0.04f), new Vector3(1.0f, 0.008f, 0.6f), "rubber");
            for (float x = -0.75f; x <= 0.16f; x += 0.1f)
            {
                b.Box(new Vector3(x, TopY + 0.006f, -0.04f), new Vector3(0.006f, 0.004f, 0.56f), "black");
            }
        }

        private static void Drawers(MeshBuilder b)
        {
            const float x0 = -LegX + Leg / 2f, x1 = -0.3f, y0 = 0.3f, y1 = TopY - TopThickness - 0.08f;
            float front = -LegZ - Leg / 2f + 0.01f, back = LegZ - Leg / 2f;
            float cx = (x0 + x1) / 2f, w = x1 - x0;
            b.Box(new Vector3(cx, (y0 + y1) / 2f, (front + back) / 2f), new Vector3(w, y1 - y0, back - front), "grey");
            b.Box(new Vector3(cx, y0 + 0.01f, (front + back) / 2f), new Vector3(w + 0.01f, 0.02f, back - front + 0.01f), "dark");
            float h = (y1 - y0 - 0.04f) / 3f;
            for (int i = 0; i < 3; i++)
            {
                float y = y0 + 0.02f + h * (i + 0.5f);
                b.Box(new Vector3(cx, y, front - 0.007f), new Vector3(w - 0.04f, h - 0.02f, 0.016f), "light");
                b.Box(new Vector3(cx, y + h * 0.28f, front - 0.016f), new Vector3(0.12f, 0.03f, 0.004f), "white");
                foreach (float sx in new[] { -0.08f, 0.08f })
                {
                    b.Cylinder(new Vector3(cx + sx, y, front - 0.014f), new Vector3(cx + sx, y, front - 0.04f), 0.008f, "steel", 8);
                }
                b.Cylinder(new Vector3(cx - 0.1f, y, front - 0.04f), new Vector3(cx + 0.1f, y, front - 0.04f), 0.011f, "steel", 10);
            }
        }

        private static void Shelf(MeshBuilder b)
        {
            const float y = 1.3f, x0 = -0.2f, x1 = LegX - Leg / 2f;
            float z0 = PanelZ - 0.16f, z1 = PanelZ - 0.01f;
            b.Box(new Vector3((x0 + x1) / 2f, y, (z0 + z1) / 2f), new Vector3(x1 - x0, 0.02f, z1 - z0), "grey");
            b.Box(new Vector3((x0 + x1) / 2f, y + 0.02f, z0 + 0.005f), new Vector3(x1 - x0, 0.03f, 0.01f), "steel");
            foreach (float x in new[] { x0 + 0.06f, x1 - 0.06f })
            {
                b.Box(new Vector3(x, y - 0.06f, z1 - 0.012f), new Vector3(0.03f, 0.12f, 0.024f), "dark");
                b.Box(new Vector3(x, y - 0.025f, (z0 + z1) / 2f), new Vector3(0.03f, 0.03f, z1 - z0), "dark");
            }
            string[] colors = { "orange", "blue", "yellow" };
            for (int i = 0; i < 3; i++)
            {
                float x = x0 + 0.17f + i * 0.28f;
                b.Box(new Vector3(x, y + 0.06f, (z0 + z1) / 2f + 0.005f), new Vector3(0.22f, 0.1f, z1 - z0 - 0.03f), colors[i]);
                b.Box(new Vector3(x, y + 0.08f, z0 + 0.018f), new Vector3(0.08f, 0.03f, 0.008f), "white");
            }
        }

        private static void BackPanel(MeshBuilder b)
        {
            float y0 = TopY - 0.01f, y1 = Height - 0.06f;
            b.Box(new Vector3(0f, (y0 + y1) / 2f, PanelZ), new Vector3(LegX * 2f - Leg + 0.01f, y1 - y0, 0.02f), "grey");
            b.Box(new Vector3(0f, Height - 0.03f, LegZ), new Vector3(LegX * 2f + Leg, 0.06f, Leg + 0.01f), "dark");
            for (float y = TopY + 0.06f; y < y1 - 0.03f; y += 0.075f)
            {
                for (float x = -LegX + 0.08f; x < LegX - 0.06f; x += 0.075f)
                {
                    if (y > 1.24f && y < 1.4f && x > -0.22f) continue;
                    b.Box(new Vector3(x, y, PanelZ - 0.009f), new Vector3(0.012f, 0.012f, 0.004f), "black");
                }
            }
        }

        private static void Hook(MeshBuilder b, float x, float y)
        {
            float z = PanelZ - 0.01f;
            b.Cylinder(new Vector3(x, y, z + 0.004f), new Vector3(x, y, z - 0.05f), 0.006f, "steel", 8);
            b.Cylinder(new Vector3(x, y - 0.006f, z - 0.05f), new Vector3(x, y + 0.025f, z - 0.05f), 0.006f, "steel", 8);
        }

        private static void Tools(MeshBuilder b)
        {
            float z = PanelZ - 0.05f;
            float[] wrenches = { -0.78f, -0.7f, -0.62f };
            for (int i = 0; i < wrenches.Length; i++)
            {
                float x = wrenches[i], len = 0.18f + i * 0.03f, top = 1.38f;
                Hook(b, x, top);
                b.Box(new Vector3(x, top - 0.012f, z), new Vector3(0.05f, 0.03f, 0.012f), "steel");
                b.Box(new Vector3(x, top - 0.027f - len / 2f, z), new Vector3(0.022f, len, 0.01f), "steel");
                b.Box(new Vector3(x, top - 0.027f - len, z), new Vector3(0.04f, 0.035f, 0.01f), "steel");
            }
            Hook(b, -0.46f, 1.36f);
            b.Box(new Vector3(-0.46f, 1.21f, z), new Vector3(0.03f, 0.3f, 0.026f), "orange");
            b.Box(new Vector3(-0.46f, 1.37f, z), new Vector3(0.13f, 0.04f, 0.04f), "dark");
            Hook(b, -0.3f, 1.33f);
            b.Box(new Vector3(-0.3f, 1.27f, z), new Vector3(0.034f, 0.13f, 0.034f), "yellow");
            b.Cylinder(new Vector3(-0.3f, 1.21f, z), new Vector3(-0.3f, 1.09f, z), 0.007f, "steel", 8);
            Hook(b, 0.05f, 1.18f);
            Hook(b, 0.25f, 1.18f);
            b.Box(new Vector3(0.15f, 1.13f, z), new Vector3(0.3f, 0.1f, 0.02f), "dark");
            b.Box(new Vector3(0.15f, 1.13f, z - 0.011f), new Vector3(0.24f, 0.05f, 0.004f), "white");
        }

        private static void Vise(MeshBuilder b)
        {
            const float x = 0.68f, z = -0.33f;
            b.Box(new Vector3(x, TopY + 0.015f, z), new Vector3(0.2f, 0.03f, 0.16f), "dark");
            foreach (float sx in new[] { -0.075f, 0.075f })
            {
                foreach (float sz in new[] { -0.055f, 0.055f })
                {
                    b.Cylinder(new Vector3(x + sx, TopY + 0.025f, z + sz), new Vector3(x + sx, TopY + 0.04f, z + sz), 0.01f, "steel", 8);
                }
            }
            b.Box(new Vector3(x, TopY + 0.07f, z + 0.03f), new Vector3(0.14f, 0.08f, 0.1f), "blue");
            b.Box(new Vector3(x, TopY + 0.13f, z + 0.045f), new Vector3(0.16f, 0.05f, 0.04f), "blue");
            b.Box(new Vector3(x, TopY + 0.13f, z - 0.035f), new Vector3(0.16f, 0.05f, 0.04f), "blue");
            b.Box(new Vector3(x, TopY + 0.088f, z - 0.035f), new Vector3(0.06f, 0.04f, 0.04f), "blue");
            foreach (float jz in new[] { z + 0.023f, z - 0.013f })
            {
                b.Box(new Vector3(x, TopY + 0.14f, jz), new Vector3(0.15f, 0.035f, 0.006f), "dark");
            }
            b.Cylinder(new Vector3(x, TopY + 0.09f, z + 0.07f), new Vector3(x, TopY + 0.09f, z - 0.14f), 0.012f, "steel", 10);
            b.Cylinder(new Vector3(x - 0.09f, TopY + 0.09f, z - 0.13f), new Vector3(x + 0.09f, TopY + 0.09f, z - 0.13f), 0.007f, "steel", 8);
            foreach (float sx in new[] { -0.095f, 0.095f })
            {
                b.Sphere(new Vector3(x + sx, TopY + 0.09f, z - 0.13f), 0.012f, "black", 10);
            }
        }
    }
}
