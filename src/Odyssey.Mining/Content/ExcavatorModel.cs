using System.Collections.Generic;
using UnityEngine;

namespace Odyssey.Content
{
    internal static class ExcavatorModel
    {
        private const float LegX = 0.72f, LegZ = 0.62f, DeckY = 0.74f, DeckHalf = 0.84f;
        private const float DrillX = 0.25f, DrillZ = 0.1f, Hole = 0.2f;
        private const float HousingTop = 1.42f, TowerTop = 2.0f;
        private static readonly Vector3 ExhaustTop = new Vector3(-0.44f, 1.99f, 0.34f);

        internal static Mesh Body()
        {
            var b = new MeshBuilder();
            Legs(b);
            Frame(b);
            Deck(b);
            Housing(b);
            Hopper(b);
            Engine(b);
            Tower(b);
            SocketMounts(b);
            return b.Build("ExcavatorBody");
        }

        internal static Mesh Drill()
        {
            var b = new MeshBuilder();
            b.Cylinder(new Vector3(0f, 0f, 0f), new Vector3(0f, -0.08f, 0f), 0.075f, "dark", 16);
            foreach (float y in new[] { -0.02f, -0.06f })
            {
                for (int i = 0; i < 6; i++)
                {
                    float a = i * Mathf.PI / 3f;
                    b.Cylinder(new Vector3(Mathf.Cos(a) * 0.06f, y + 0.008f, Mathf.Sin(a) * 0.06f), new Vector3(Mathf.Cos(a) * 0.06f, y - 0.008f, Mathf.Sin(a) * 0.06f), 0.019f, "steel", 6);
                }
            }
            b.Cylinder(new Vector3(0f, -0.08f, 0f), new Vector3(0f, -0.82f, 0f), 0.055f, "grey", 14);
            foreach (float y in new[] { -0.25f, -0.5f, -0.75f })
            {
                b.Cylinder(new Vector3(0f, y + 0.015f, 0f), new Vector3(0f, y - 0.015f, 0f), 0.064f, "dark", 14);
            }
            b.Cylinder(new Vector3(0f, -0.81f, 0f), new Vector3(0f, -0.86f, 0f), 0.078f, "dark", 16);
            b.Cylinder(new Vector3(0f, -0.86f, 0f), new Vector3(0f, -1.76f, 0f), 0.05f, "steel", 12);
            b.Lathe(new Vector3(0f, -1.9f, 0f), Vector3.up, new[] { new Vector2(0f, 0f), new Vector2(0.056f, 0.14f) }, "steel", 12);
            for (int i = 0; i < 3; i++)
            {
                float a = i * Mathf.PI * 2f / 3f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                b.Box(new Vector3(0f, -1.8f, 0f) + dir * 0.045f, new Vector3(0.03f, 0.05f, 0.012f), "dark", Quaternion.AngleAxis(-a * Mathf.Rad2Deg, Vector3.up));
            }
            Flights(b, -0.87f, -1.73f, 5f, 26);
            return b.Build("ExcavatorDrill");
        }

        private static void Flights(MeshBuilder b, float top, float bottom, float turns, int perTurn)
        {
            int steps = Mathf.RoundToInt(turns * perTurn);
            float da = Mathf.PI * 2f / perTurn, dy = (top - bottom) / steps;
            for (int k = 0; k < steps; k++)
            {
                float t = (k + 0.5f) / steps, a = (k + 0.5f) * da;
                float rIn = 0.045f, rOut = Mathf.Lerp(0.15f, 0.11f, t), mid = (rIn + rOut) / 2f;
                float y = Mathf.Lerp(top, bottom, t);
                float tilt = Mathf.Atan2(dy, mid * da) * Mathf.Rad2Deg;
                Quaternion r = Quaternion.AngleAxis(-a * Mathf.Rad2Deg, Vector3.up) * Quaternion.AngleAxis(tilt, Vector3.right);
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                b.Box(new Vector3(0f, y, 0f) + dir * mid, new Vector3(rOut - rIn, 0.022f, rOut * da * 1.15f), k % 2 == 0 ? "steel" : "grey", r);
            }
        }

        private static void Ring(MeshBuilder b, Vector3 center, float rIn, float rOut, float y0, float y1, string color, int segments = 24)
        {
            var outline = new List<Vector2>
            {
                new Vector2(rOut, y0), new Vector2(rOut, y0), new Vector2(rOut, y1), new Vector2(rOut, y1),
                new Vector2(rIn, y1), new Vector2(rIn, y1), new Vector2(rIn, y0), new Vector2(rIn, y0), new Vector2(rOut, y0),
            };
            b.Lathe(center, Vector3.up, outline, color, segments);
        }

        private static void Bolts(MeshBuilder b, Vector3 center, Vector3 normal, float du, float dv, float radius = 0.012f)
        {
            Vector3 u = Mathf.Abs(normal.y) > 0.5f ? Vector3.right : Vector3.up;
            Vector3 v = Vector3.Cross(normal, u);
            foreach (float su in new[] { -1f, 1f })
            {
                foreach (float sv in new[] { -1f, 1f })
                {
                    Vector3 p = center + u * (su * du) + v * (sv * dv);
                    b.Cylinder(p - normal * 0.004f, p + normal * 0.01f, radius, "steel", 6);
                }
            }
        }

        private static void Strut(MeshBuilder b, Vector3 from, Vector3 to, float thickness, string color)
        {
            Vector3 d = to - from;
            b.Box((from + to) / 2f, new Vector3(thickness, d.magnitude, thickness), color, Quaternion.FromToRotation(Vector3.up, d));
        }

        private static void Legs(MeshBuilder b)
        {
            foreach (float sx in new[] { -1f, 1f })
            {
                foreach (float sz in new[] { -1f, 1f })
                {
                    float x = sx * LegX, z = sz * LegZ;
                    b.Box(new Vector3(x, 0.02f, z), new Vector3(0.44f, 0.04f, 0.44f), "dark");
                    b.Box(new Vector3(x, 0.055f, z), new Vector3(0.34f, 0.03f, 0.34f), "grey");
                    Bolts(b, new Vector3(x, 0.04f, z), Vector3.up, 0.18f, 0.18f, 0.014f);
                    for (int i = 0; i < 4; i++)
                    {
                        Quaternion r = Quaternion.AngleAxis(i * 90f, Vector3.up);
                        b.Box(new Vector3(x, 0.1f, z) + r * new Vector3(0.1f, 0f, 0f), new Vector3(0.1f, 0.06f, 0.025f), "dark", r);
                    }
                    b.Sphere(new Vector3(x, 0.12f, z), 0.07f, "dark", 14);
                    b.Cylinder(new Vector3(x, 0.12f, z), new Vector3(x, 0.38f, z), 0.05f, "steel", 14);
                    b.Cylinder(new Vector3(x, 0.34f, z), new Vector3(x, 0.39f, z), 0.09f, "dark", 16);
                    b.Box(new Vector3(x, 0.58f, z), new Vector3(0.26f, 0.42f, 0.26f), "yellow");
                    foreach (float cx in new[] { -1f, 1f })
                    {
                        foreach (float cz in new[] { -1f, 1f })
                        {
                            b.Box(new Vector3(x + cx * 0.122f, 0.58f, z + cz * 0.122f), new Vector3(0.03f, 0.42f, 0.03f), "dark");
                        }
                    }
                    b.Box(new Vector3(x + sx * 0.132f, 0.56f, z), new Vector3(0.012f, 0.26f, 0.16f), "grey");
                    Bolts(b, new Vector3(x + sx * 0.138f, 0.56f, z), new Vector3(sx, 0f, 0f), 0.1f, 0.055f);
                    b.Box(new Vector3(x, 0.4f, z), new Vector3(0.28f, 0.03f, 0.28f), "dark");
                    b.Box(new Vector3(x, DeckY + 0.06f, z), new Vector3(0.3f, 0.04f, 0.3f), "grey");
                    b.Cylinder(new Vector3(x, DeckY + 0.08f, z), new Vector3(x, DeckY + 0.11f, z), 0.06f, "dark", 12);
                    Vector3 port = new Vector3(x - sx * 0.13f, 0.7f, z - sz * 0.06f);
                    b.Pipe(new[] { port, port + new Vector3(-sx * 0.08f, 0f, 0f), new Vector3(x - sx * 0.3f, DeckY - 0.06f, z - sz * 0.06f) }, 0.016f, "black", 8);
                }
            }
        }

        private static void Frame(MeshBuilder b)
        {
            foreach (float sz in new[] { -1f, 1f })
            {
                float z = sz * LegZ;
                b.Box(new Vector3(0f, 0.63f, z), new Vector3(LegX * 2f, 0.13f, 0.03f), "grey");
                b.Box(new Vector3(0f, 0.7f, z), new Vector3(LegX * 2f, 0.016f, 0.12f), "dark");
                b.Box(new Vector3(0f, 0.56f, z), new Vector3(LegX * 2f, 0.016f, 0.12f), "dark");
                foreach (float x in new[] { -0.36f, 0f, 0.36f })
                {
                    b.Box(new Vector3(x, 0.63f, z - sz * 0.03f), new Vector3(0.016f, 0.12f, 0.05f), "grey");
                }
            }
            foreach (float sx in new[] { -1f, 1f })
            {
                float x = sx * LegX;
                b.Box(new Vector3(x, 0.63f, 0f), new Vector3(0.03f, 0.13f, LegZ * 2f), "grey");
                b.Box(new Vector3(x, 0.7f, 0f), new Vector3(0.12f, 0.016f, LegZ * 2f), "dark");
                b.Box(new Vector3(x, 0.56f, 0f), new Vector3(0.12f, 0.016f, LegZ * 2f), "dark");
            }
            foreach (float x in new[] { DrillX - Hole - 0.04f, DrillX + Hole + 0.04f })
            {
                b.Box(new Vector3(x, 0.65f, 0f), new Vector3(0.08f, 0.1f, LegZ * 2f), "dark");
            }
            foreach (float sz in new[] { -1f, 1f })
            {
                Strut(b, new Vector3(-LegX + 0.15f, 0.57f, sz * LegZ), new Vector3(-LegX + 0.02f, 0.4f, sz * LegZ), 0.035f, "dark");
                Strut(b, new Vector3(LegX - 0.15f, 0.57f, sz * LegZ), new Vector3(LegX - 0.02f, 0.4f, sz * LegZ), 0.035f, "dark");
            }
            Ring(b, new Vector3(DrillX, 0f, DrillZ), 0.17f, 0.25f, 0.5f, 0.7f, "dark");
            Ring(b, new Vector3(DrillX, 0f, DrillZ), 0.17f, 0.27f, 0.47f, 0.51f, "grey");
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f + Mathf.PI / 8f;
                var p = new Vector3(DrillX + Mathf.Cos(a) * 0.255f, 0.6f, DrillZ + Mathf.Sin(a) * 0.255f);
                b.Box(p, new Vector3(0.03f, 0.16f, 0.03f), "grey", Quaternion.AngleAxis(-a * Mathf.Rad2Deg, Vector3.up));
            }
        }

        private static void Deck(MeshBuilder b)
        {
            float y = DeckY - 0.02f, h = 0.04f;
            float hx0 = DrillX - Hole, hx1 = DrillX + Hole, hz0 = DrillZ - Hole, hz1 = DrillZ + Hole;
            void Plate(float x0, float x1, float z0, float z1) =>
                b.Box(new Vector3((x0 + x1) / 2f, y, (z0 + z1) / 2f), new Vector3(x1 - x0, h, z1 - z0), "steel");
            Plate(-DeckHalf, hx0, -DeckHalf, DeckHalf);
            Plate(hx1, DeckHalf, -DeckHalf, DeckHalf);
            Plate(hx0, hx1, -DeckHalf, hz0);
            Plate(hx0, hx1, hz1, DeckHalf);
            foreach (float s in new[] { -1f, 1f })
            {
                b.Box(new Vector3(0f, y, s * (DeckHalf + 0.01f)), new Vector3(DeckHalf * 2f + 0.04f, h + 0.02f, 0.02f), "dark");
                b.Box(new Vector3(s * (DeckHalf + 0.01f), y, 0f), new Vector3(0.02f, h + 0.02f, DeckHalf * 2f), "dark");
            }
            for (float x = -DeckHalf + 0.12f; x < DeckHalf; x += 0.24f)
            {
                foreach (float s in new[] { -1f, 1f })
                {
                    b.Cylinder(new Vector3(x, DeckY - 0.002f, s * (DeckHalf - 0.04f)), new Vector3(x, DeckY + 0.008f, s * (DeckHalf - 0.04f)), 0.011f, "grey", 6);
                }
            }
        }

        private static void Housing(MeshBuilder b)
        {
            const float x0 = -0.12f, x1 = 0.62f, z0 = -0.23f, z1 = 0.43f;
            float cx = (x0 + x1) / 2f, cz = (z0 + z1) / 2f, y0 = DeckY, h = HousingTop - DeckY;
            b.Box(new Vector3(cx, y0 + 0.03f, cz), new Vector3(x1 - x0 + 0.04f, 0.06f, z1 - z0 + 0.04f), "dark");
            b.Box(new Vector3(cx, y0 + h / 2f, cz), new Vector3(x1 - x0, h, z1 - z0), "yellow");
            b.Box(new Vector3(cx, HousingTop + 0.015f, cz), new Vector3(x1 - x0 + 0.04f, 0.03f, z1 - z0 + 0.04f), "grey");
            foreach (float sx in new[] { x0, x1 })
            {
                foreach (float sz in new[] { z0, z1 })
                {
                    b.Box(new Vector3(sx, y0 + h / 2f, sz), new Vector3(0.035f, h, 0.035f), "dark");
                }
            }
            float side = x1 + 0.005f;
            b.Box(new Vector3(side, 1.1f, cz), new Vector3(0.012f, 0.38f, 0.48f), "dark");
            for (int i = 0; i < 7; i++)
            {
                float y = 0.95f + i * 0.05f;
                b.Box(new Vector3(side + 0.012f, y, cz), new Vector3(0.02f, 0.016f, 0.44f), "grey", Quaternion.AngleAxis(-25f, Vector3.forward));
            }
            Bolts(b, new Vector3(side + 0.006f, 1.1f, cz), Vector3.right, 0.17f, 0.22f, 0.01f);

            float front = z0 - 0.005f, door = 0.42f;
            b.Box(new Vector3(door, 1.04f, front), new Vector3(0.34f, 0.46f, 0.012f), "grey");
            b.Box(new Vector3(door, 1.04f, front - 0.008f), new Vector3(0.28f, 0.4f, 0.006f), "yellow");
            foreach (float y in new[] { 0.9f, 1.18f })
            {
                b.Cylinder(new Vector3(door - 0.155f, y - 0.04f, front - 0.012f), new Vector3(door - 0.155f, y + 0.04f, front - 0.012f), 0.013f, "dark", 8);
            }
            b.Box(new Vector3(door + 0.11f, 1.04f, front - 0.032f), new Vector3(0.02f, 0.12f, 0.016f), "steel");
            foreach (float y in new[] { 0.99f, 1.09f })
            {
                b.Box(new Vector3(door + 0.11f, y, front - 0.02f), new Vector3(0.016f, 0.016f, 0.026f), "steel");
            }
            b.Box(new Vector3(door, 1.32f, front - 0.004f), new Vector3(0.2f, 0.07f, 0.008f), "dark");
            b.Box(new Vector3(door, 1.32f, front - 0.009f), new Vector3(0.16f, 0.045f, 0.004f), "screen");

            Vector3 lamp = new Vector3(x1 - 0.06f, HousingTop + 0.03f, z0 + 0.06f);
            b.Cylinder(lamp, lamp + Vector3.up * 0.03f, 0.045f, "dark", 12);
            b.Cylinder(lamp + Vector3.up * 0.03f, lamp + Vector3.up * 0.07f, 0.035f, "orange", 12);
            b.Sphere(lamp + Vector3.up * 0.07f, 0.035f, "orange", 12);
        }

        private static void Hopper(MeshBuilder b)
        {
            const float x0 = -0.82f, x1 = 0.22f, z0 = -0.67f, z1 = -0.05f, top = 1.26f;
            float cx = (x0 + x1) / 2f, cz = (z0 + z1) / 2f, h = top - DeckY;
            b.Box(new Vector3(cx, DeckY + h / 2f, cz), new Vector3(x1 - x0, h, z1 - z0), "yellow");
            b.Box(new Vector3(cx, DeckY + 0.03f, cz), new Vector3(x1 - x0 + 0.03f, 0.06f, z1 - z0 + 0.03f), "dark");
            for (int i = 0; i <= 4; i++)
            {
                float x = x0 + 0.03f + i * (x1 - x0 - 0.06f) / 4f;
                b.Box(new Vector3(x, DeckY + h / 2f, z0 - 0.01f), new Vector3(0.04f, h, 0.02f), "dark");
            }
            b.Box(new Vector3(x0 - 0.01f, DeckY + h / 2f, cz), new Vector3(0.02f, h, 0.04f), "dark");
            b.Box(new Vector3(x0 - 0.008f, 1.03f, cz), new Vector3(0.012f, 0.26f, 0.38f), "grey");
            Bolts(b, new Vector3(x0 - 0.014f, 1.03f, cz), Vector3.left, 0.11f, 0.16f, 0.009f);
            b.Box(new Vector3(x0 - 0.016f, 1.06f, cz), new Vector3(0.006f, 0.08f, 0.22f), "white");

            b.Box(new Vector3(cx, top + 0.015f, cz), new Vector3(x1 - x0 + 0.04f, 0.03f, z1 - z0 + 0.04f), "grey");
            b.Box(new Vector3(cx, top + 0.04f, cz + 0.01f), new Vector3(x1 - x0 - 0.12f, 0.02f, z1 - z0 - 0.1f), "dark");
            for (int i = 0; i < 4; i++)
            {
                float x = x0 + 0.17f + i * 0.235f;
                b.Box(new Vector3(x, top + 0.055f, cz + 0.01f), new Vector3(0.03f, 0.012f, z1 - z0 - 0.14f), "grey");
            }
            foreach (float x in new[] { cx - 0.3f, cx + 0.3f })
            {
                b.Cylinder(new Vector3(x - 0.08f, top + 0.04f, z1 - 0.01f), new Vector3(x + 0.08f, top + 0.04f, z1 - 0.01f), 0.02f, "dark", 10);
                b.Box(new Vector3(x - 0.06f, top + 0.06f, z0 - 0.01f), new Vector3(0.016f, 0.05f, 0.016f), "steel");
                b.Box(new Vector3(x + 0.06f, top + 0.06f, z0 - 0.01f), new Vector3(0.016f, 0.05f, 0.016f), "steel");
                b.Cylinder(new Vector3(x - 0.07f, top + 0.085f, z0 - 0.01f), new Vector3(x + 0.07f, top + 0.085f, z0 - 0.01f), 0.011f, "steel", 8);
            }
        }

        private static void Engine(MeshBuilder b)
        {
            const float x0 = -0.8f, x1 = -0.16f, z0 = -0.01f, z1 = 0.62f, top = 1.24f;
            float cx = (x0 + x1) / 2f, cz = (z0 + z1) / 2f, h = top - DeckY;
            b.Box(new Vector3(cx, DeckY + 0.02f, cz), new Vector3(x1 - x0 + 0.06f, 0.04f, z1 - z0 + 0.04f), "dark");
            foreach (float sz in new[] { z0 + 0.1f, z1 - 0.1f })
            {
                foreach (float sx in new[] { x0 + 0.1f, x1 - 0.1f })
                {
                    b.Cylinder(new Vector3(sx, DeckY + 0.04f, sz), new Vector3(sx, DeckY + 0.08f, sz), 0.04f, "rubber", 10);
                }
            }
            b.Box(new Vector3(cx, DeckY + 0.08f + (h - 0.08f) / 2f, cz), new Vector3(x1 - x0, h - 0.08f, z1 - z0), "olive");
            b.Box(new Vector3(cx, top + 0.012f, cz), new Vector3(x1 - x0 + 0.03f, 0.024f, z1 - z0 + 0.03f), "dark");
            foreach (float sx in new[] { x0, x1 })
            {
                foreach (float sz in new[] { z0, z1 })
                {
                    b.Box(new Vector3(sx, DeckY + 0.08f + (h - 0.08f) / 2f, sz), new Vector3(0.03f, h - 0.08f, 0.03f), "dark");
                }
            }
            float face = x0 - 0.006f;
            b.Box(new Vector3(face, 1.0f, cz), new Vector3(0.012f, 0.3f, 0.48f), "dark");
            for (int i = 0; i < 9; i++)
            {
                float z = cz - 0.2f + i * 0.05f;
                b.Box(new Vector3(face - 0.01f, 1.0f, z), new Vector3(0.02f, 0.26f, 0.012f), "grey");
            }
            float back = z1 + 0.006f;
            b.Box(new Vector3(cx, 1.0f, back), new Vector3(0.4f, 0.24f, 0.012f), "dark");
            for (int i = 0; i < 6; i++)
            {
                b.Box(new Vector3(cx, 0.9f + i * 0.04f, back + 0.01f), new Vector3(0.36f, 0.014f, 0.02f), "grey", Quaternion.AngleAxis(25f, Vector3.right));
            }

            Vector3 filter = new Vector3(-0.66f, top + 0.024f, 0.14f);
            b.Cylinder(filter, filter + Vector3.up * 0.14f, 0.07f, "dark", 14);
            b.Cylinder(filter + Vector3.up * 0.14f, filter + Vector3.up * 0.16f, 0.075f, "grey", 14);
            b.Cylinder(filter + new Vector3(0f, 0.02f, 0f), filter + new Vector3(0f, 0.02f, 0f) + Vector3.up * 0.1f, 0.072f, "grey", 14);
            b.Pipe(new[] { filter + new Vector3(0.06f, 0.07f, 0f), filter + new Vector3(0.16f, 0.07f, 0f), new Vector3(-0.5f, top + 0.024f, 0.14f) }, 0.022f, "black", 8);

            Vector3 tank = new Vector3(-0.3f, top + 0.024f + 0.075f, 0.42f);
            b.Cylinder(tank - Vector3.forward * 0.16f, tank + Vector3.forward * 0.16f, 0.075f, "red", 16);
            foreach (float s in new[] { -0.1f, 0.1f })
            {
                b.Box(new Vector3(tank.x, top + 0.04f, tank.z + s), new Vector3(0.16f, 0.035f, 0.03f), "dark");
            }
            b.Cylinder(tank + new Vector3(0f, 0.07f, 0.08f), tank + new Vector3(0f, 0.1f, 0.08f), 0.022f, "dark", 10);

            Vector3 baseP = new Vector3(ExhaustTop.x, top + 0.024f, ExhaustTop.z);
            b.Cylinder(baseP, baseP + Vector3.up * 0.04f, 0.075f, "dark", 14);
            b.Cylinder(baseP, new Vector3(ExhaustTop.x, ExhaustTop.y - 0.03f, ExhaustTop.z), 0.04f, "dark", 12);
            b.Cylinder(new Vector3(ExhaustTop.x, 1.42f, ExhaustTop.z), new Vector3(ExhaustTop.x, 1.68f, ExhaustTop.z), 0.07f, "steel", 16);
            foreach (float y in new[] { 1.45f, 1.65f })
            {
                b.Cylinder(new Vector3(ExhaustTop.x, y - 0.012f, ExhaustTop.z), new Vector3(ExhaustTop.x, y + 0.012f, ExhaustTop.z), 0.076f, "dark", 16);
            }
            b.Cylinder(new Vector3(ExhaustTop.x, ExhaustTop.y - 0.04f, ExhaustTop.z), new Vector3(ExhaustTop.x, ExhaustTop.y, ExhaustTop.z), 0.05f, "black", 12);
            Strut(b, new Vector3(ExhaustTop.x + 0.07f, 1.55f, ExhaustTop.z), new Vector3(0.62f - 0.73f, HousingTop + 0.03f, ExhaustTop.z), 0.025f, "dark");
        }

        private static void Tower(MeshBuilder b)
        {
            const float ox = 0.22f, oz = 0.18f;
            float y0 = HousingTop + 0.03f;
            var posts = new List<Vector3>();
            foreach (float sx in new[] { -1f, 1f })
            {
                foreach (float sz in new[] { -1f, 1f })
                {
                    var p = new Vector3(DrillX + sx * ox, 0f, DrillZ + sz * oz);
                    posts.Add(p);
                    b.Box(new Vector3(p.x, (y0 + TowerTop) / 2f, p.z), new Vector3(0.07f, TowerTop - y0, 0.07f), "grey");
                    b.Box(new Vector3(p.x, y0 + 0.015f, p.z), new Vector3(0.12f, 0.03f, 0.12f), "dark");
                    Bolts(b, new Vector3(p.x, y0 + 0.03f, p.z), Vector3.up, 0.04f, 0.04f, 0.008f);
                }
            }
            foreach (float sx in new[] { -1f, 1f })
            {
                float x = DrillX + sx * ox;
                Strut(b, new Vector3(x, y0 + 0.05f, DrillZ - oz), new Vector3(x, TowerTop - 0.05f, DrillZ + oz), 0.03f, "dark");
                Strut(b, new Vector3(x, y0 + 0.05f, DrillZ + oz), new Vector3(x, TowerTop - 0.05f, DrillZ - oz), 0.03f, "dark");
            }
            float mid = (y0 + TowerTop) / 2f;
            foreach (float sz in new[] { -1f, 1f })
            {
                b.Box(new Vector3(DrillX, mid, DrillZ + sz * oz), new Vector3(ox * 2f, 0.04f, 0.04f), "dark");
            }
            b.Box(new Vector3(DrillX, TowerTop + 0.03f, DrillZ), new Vector3(ox * 2f + 0.14f, 0.06f, oz * 2f + 0.14f), "grey");
            b.Box(new Vector3(DrillX, TowerTop + 0.07f, DrillZ), new Vector3(ox * 2f + 0.02f, 0.02f, oz * 2f + 0.02f), "dark");
            Vector3 motor = new Vector3(DrillX, TowerTop + 0.08f, DrillZ);
            b.Cylinder(motor, motor + Vector3.up * 0.16f, 0.15f, "dark", 20);
            for (int i = 0; i < 5; i++)
            {
                float y = 0.025f + i * 0.028f;
                b.Cylinder(motor + Vector3.up * (y - 0.006f), motor + Vector3.up * (y + 0.006f), 0.162f, "grey", 20);
            }
            b.Cylinder(motor + Vector3.up * 0.16f, motor + Vector3.up * 0.2f, 0.155f, "orange", 20);
            b.Cylinder(motor + Vector3.up * 0.2f, motor + Vector3.up * 0.24f, 0.12f, "dark", 18);
            b.Cylinder(motor + Vector3.up * 0.24f, motor + Vector3.up * 0.26f, 0.05f, "steel", 12);
            Vector3 box = motor + new Vector3(0.2f, 0.06f, 0f);
            b.Box(box, new Vector3(0.1f, 0.12f, 0.14f), "dark");
            b.Pipe(new[] { box + new Vector3(0.03f, -0.06f, 0.04f), new Vector3(box.x + 0.03f, TowerTop + 0.06f, box.z + 0.04f) }, 0.015f, "black", 8);
            b.Pipe(new[] { new Vector3(DrillX + ox + 0.06f, TowerTop, DrillZ - 0.05f), new Vector3(DrillX + ox + 0.06f, HousingTop + 0.02f, DrillZ - 0.05f) }, 0.016f, "black", 8);
            b.Pipe(new[] { new Vector3(DrillX + ox + 0.1f, TowerTop, DrillZ + 0.05f), new Vector3(DrillX + ox + 0.1f, HousingTop + 0.02f, DrillZ + 0.05f) }, 0.016f, "black", 8);
        }

        private static void SocketMounts(MeshBuilder b)
        {
            foreach ((float sx, float sz, string cap) in new[] { (1f, -1f, "grey"), (-1f, 1f, "grey"), (-1f, -1f, "yellow") })
            {
                float x = sx * (LegX + 0.17f), z = sz * LegZ;
                b.Box(new Vector3(x, 0.42f, z), new Vector3(0.08f, 0.2f, 0.18f), "dark");
                b.Box(new Vector3(x, 0.53f, z), new Vector3(0.09f, 0.02f, 0.19f), cap);
                b.Box(new Vector3(sx * (LegX + 0.1f), 0.42f, z), new Vector3(0.08f, 0.12f, 0.1f), "grey");
                b.Pipe(new[] { new Vector3(x, 0.33f, z + 0.05f), new Vector3(x, 0.26f, z + 0.05f), new Vector3(sx * (LegX + 0.06f), 0.26f, z + 0.05f), new Vector3(sx * (LegX + 0.06f), 0.4f, z + 0.05f) }, 0.014f, "black", 8);
            }
        }
    }
}
