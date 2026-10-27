using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MelonLoader;
using Ships.Interface.Model;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Settings;
using Ships.Parts.Common;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Odyssey.Content
{
    internal static class ElectrolyzerPart
    {
        internal const ushort PartId = 202;
        internal const byte OxygenSocket = 0, PowerSocket = 1, HydrogenSocket = 2, WaterSocket = 3, SignalSocket = 4;

        private const ushort ChestPartId = 30, GeneratorPartId = 60, DistillerPartId = 80;
        private static PartSettings _part;
        private static bool _failed;

        internal static PartSettings Part => _part;

        internal static bool Is(PartSettings settings) => settings != null && settings.id == PartId;

        internal static PartSettings GetOrCreatePart(IEnumerable<PartSettings> gameParts)
        {
            if (_part != null || _failed)
            {
                return _part;
            }
            List<PartSettings> parts = gameParts.ToList();
            PartSettings chest = PartFactory.Find(parts, ChestPartId), generator = PartFactory.Find(parts, GeneratorPartId), distiller = PartFactory.Find(parts, DistillerPartId);
            if (chest?.part == null || generator?.part == null || distiller?.part == null)
            {
                return null;
            }
            try
            {
                _part = Build(chest, generator, distiller, 1);
                Melon<PowerMod>.Logger.Msg($"Added part '{_part.fullLabel}' (id {PartId})");
            }
            catch (Exception e)
            {
                _failed = true;
                Melon<PowerMod>.Logger.Error($"Could not build the electrolyzer: {e}");
            }
            return _part;
        }

        private const float Wall = 0.4f, HousingTop = 0.42f, SocketY = 0.2f, LabelY = 0.33f;
        private static readonly Vector3 HydrogenSocketPos = new Vector3(-0.36f, SocketY, 0.5f);
        private static readonly Vector3 OxygenSocketPos = new Vector3(0.36f, SocketY, 0.5f);
        private static readonly Vector3 WaterSocketPos = new Vector3(0.18f, SocketY, 0.5f);
        private static readonly Vector3 PowerSocketPos = new Vector3(-0.18f, SocketY, 0.5f);
        private static readonly Vector3 SignalSocketPos = new Vector3(0f, SocketY, 0.5f);

        private static void ServicePanel(MeshBuilder b, Vector3 offset, float face, (float x, string color)[] sockets)
        {
            float x0 = sockets.Min(k => k.x) - 0.12f, x1 = sockets.Max(k => k.x) + 0.12f, cx = (x0 + x1) * 0.5f, w = x1 - x0;
            b.Box(offset + new Vector3(cx, SocketY, face - 0.03f), new Vector3(w, 0.22f, 0.03f), "dark");
            b.Box(offset + new Vector3(cx, SocketY + 0.115f, face - 0.025f), new Vector3(w + 0.02f, 0.02f, 0.04f), "steel");
            b.Box(offset + new Vector3(cx, SocketY - 0.115f, face - 0.025f), new Vector3(w + 0.02f, 0.02f, 0.04f), "steel");
            foreach (var (x, color) in sockets)
            {
                Vector3 c = offset + new Vector3(x, SocketY, 0f);
                b.Cylinder(c + new Vector3(0f, 0f, face - 0.015f), c + new Vector3(0f, 0f, face - 0.005f), 0.07f, "grey", 20);
                b.Cylinder(c + new Vector3(0f, 0f, face - 0.005f), c + new Vector3(0f, 0f, face - 0.001f), 0.055f, color, 20);
                b.Cylinder(c + new Vector3(0f, 0f, face - 0.001f), c + new Vector3(0f, 0f, face + 0.001f), 0.035f, "black", 14);
                b.Box(c + new Vector3(0f, 0.09f, face - 0.013f), new Vector3(0.1f, 0.016f, 0.006f), color);
            }
        }

        private static Mesh BodyMesh()
        {
            var b = new MeshBuilder();
            b.Box(new Vector3(0f, 0.04f, 0f), new Vector3(0.92f, 0.08f, 0.92f), "dark");
            b.Box(new Vector3(0f, (0.08f + HousingTop) * 0.5f, 0f), new Vector3(Wall * 2f, HousingTop - 0.08f, Wall * 2f), "light");
            b.Box(new Vector3(0f, HousingTop + 0.015f, 0f), new Vector3(Wall * 2f - 0.06f, 0.03f, Wall * 2f - 0.06f), "grey");

            foreach (var (x, cap) in new[] { (-0.22f, "orange"), (0.22f, "cyan") })
            {
                b.Cylinder(new Vector3(x, HousingTop, 0.12f), new Vector3(x, HousingTop + 0.04f, 0.12f), 0.12f, "dark", 20);
                b.Cylinder(new Vector3(x, HousingTop + 0.04f, 0.12f), new Vector3(x, 0.86f, 0.12f), 0.095f, cap, 20);
                b.Cylinder(new Vector3(x, 0.6f, 0.12f), new Vector3(x, 0.63f, 0.12f), 0.104f, "dark", 20);
                b.Cylinder(new Vector3(x, 0.86f, 0.12f), new Vector3(x, 0.92f, 0.12f), 0.11f, "dark", 20);
                b.Cylinder(new Vector3(x, 0.92f, 0.12f), new Vector3(x, 0.95f, 0.12f), 0.05f, "steel", 12);
                float side = Mathf.Sign(x);
                float px = side * 0.45f;
                b.Cylinder(new Vector3(x, 0.935f, 0.12f), new Vector3(px, 0.935f, 0.12f), 0.03f, "steel", 12);
                b.Cylinder(new Vector3(px, 0.95f, 0.12f), new Vector3(px, SocketY, 0.12f), 0.03f, "steel", 12);
                b.Sphere(new Vector3(px, SocketY, 0.12f), 0.032f, "steel", 10);
                b.Cylinder(new Vector3(px, SocketY, 0.12f), new Vector3(px, SocketY, 0.44f), 0.03f, "steel", 12);
                b.Box(new Vector3(px, 0.62f, 0.12f), new Vector3(0.05f, 0.04f, 0.08f), "dark");
            }
            b.Cylinder(new Vector3(-0.22f, 0.5f, 0.12f), new Vector3(0.22f, 0.5f, 0.12f), 0.035f, "steel", 12);

            b.Cylinder(new Vector3(0f, HousingTop, 0.24f), new Vector3(0f, 0.66f, 0.24f), 0.085f, "blue", 18);
            b.Cylinder(new Vector3(0f, 0.66f, 0.24f), new Vector3(0f, 0.69f, 0.24f), 0.095f, "dark", 18);
            b.Cylinder(new Vector3(0f, 0.55f, 0.24f), new Vector3(WaterSocketPos.x, 0.55f, 0.24f), 0.03f, "steel", 12);
            b.Cylinder(new Vector3(WaterSocketPos.x, 0.55f, 0.24f), new Vector3(WaterSocketPos.x, 0.55f, 0.46f), 0.03f, "steel", 12);
            b.Cylinder(new Vector3(WaterSocketPos.x, 0.55f, 0.46f), new Vector3(WaterSocketPos.x, SocketY + 0.09f, 0.46f), 0.03f, "steel", 12);

            b.Box(new Vector3(PowerSocketPos.x, 0.5f, 0.26f), new Vector3(0.16f, 0.16f, 0.14f), "dark");
            b.Box(new Vector3(PowerSocketPos.x, 0.56f, 0.26f), new Vector3(0.165f, 0.025f, 0.145f), "grey");
            b.Box(new Vector3(PowerSocketPos.x, 0.5f, 0.38f), new Vector3(0.05f, 0.05f, 0.12f), "steel");
            b.Box(new Vector3(PowerSocketPos.x, (0.5f + SocketY + 0.06f) * 0.5f, 0.43f), new Vector3(0.05f, 0.5f - SocketY - 0.06f, 0.05f), "steel");

            b.Box(new Vector3(0f, 0.53f, -0.27f), new Vector3(0.5f, 0.22f, 0.26f), "grey");
            b.Box(new Vector3(0f, 0.645f, -0.27f), new Vector3(0.42f, 0.01f, 0.18f), "black");
            b.Box(new Vector3(0f, 0.6f, -0.401f), new Vector3(0.3f, 0.06f, 0.01f), "white");
            b.Box(new Vector3(0f, 0.25f, -0.402f), new Vector3(0.6f, 0.2f, 0.01f), "dark");

            b.Box(new Vector3(0f, SocketY, 0.43f), new Vector3(0.9f, 0.2f, 0.06f), "grey");
            b.Box(new Vector3(SignalSocketPos.x, 0.36f, 0.43f), new Vector3(0.1f, 0.1f, 0.06f), "dark");
            b.Cylinder(new Vector3(SignalSocketPos.x, 0.36f, 0.43f), new Vector3(SignalSocketPos.x, HousingTop, 0.43f), 0.015f, "white", 8);
            ServicePanel(b, Vector3.zero, 0.5f, new[] { (HydrogenSocketPos.x, "orange"), (PowerSocketPos.x, "yellow"), (SignalSocketPos.x, "white"), (WaterSocketPos.x, "blue"), (OxygenSocketPos.x, "cyan") });

            return b.Build("ElectrolyzerBody");
        }

        private static Mesh LabelMesh(Vector3 at)
        {
            var b = new MeshBuilder();
            Glyph(b, 'I', at, new Vector3(0f, 0f, -1f));
            return b.Build("ElectrolyzerLabels");
        }

        private struct Layout
        {
            public int N;
            public float Half, TankZ, TankTop, StackZ, StackTop, HopperX;
            public Vector3 Hydrogen, Oxygen, Water, Power, Signal, Label;
            public Vector3 Offset => new Vector3((N - 1) * 0.5f, 0f, (N - 1) * 0.5f);
        }

        private static Layout LargeLayout(int n)
        {
            float half = n * 0.5f;
            var l = new Layout
            {
                N = n,
                Half = half,
                TankZ = half - 0.5f,
                TankTop = 0.62f * n + 0.1f,
                StackZ = -half + 0.62f,
                StackTop = 0.18f + 0.26f * n,
                HopperX = -half + 0.42f,
            };
            float spacing = Mathf.Min(0.45f, 0.15f * n);
            l.Hydrogen = new Vector3(-2f * spacing, SocketY, half);
            l.Power = new Vector3(-spacing, SocketY, half);
            l.Signal = new Vector3(0f, SocketY, half);
            l.Water = new Vector3(spacing, SocketY, half);
            l.Oxygen = new Vector3(2f * spacing, SocketY, half);
            l.Label = new Vector3(l.HopperX, 0.6f, -half + 0.067f);
            return l;
        }

        private static Mesh LargeBodyMesh(Layout l)
        {
            var b = new MeshBuilder();
            int n = l.N;
            float h = l.Half, w = n - 0.3f;
            Vector3 o = l.Offset;
            Vector3 P(float x, float y, float z) => new Vector3(x, y, z) + o;
            void Pipe(float r, string color, params Vector3[] pts)
            {
                for (int i = 0; i + 1 < pts.Length; i++)
                {
                    b.Cylinder(pts[i] + o, pts[i + 1] + o, r, color, 12);
                }
                for (int i = 1; i + 1 < pts.Length; i++)
                {
                    b.Sphere(pts[i] + o, r * 1.05f, color, 10);
                }
            }
            void Hex(Vector3 from, Vector3 to, float r) => b.Cylinder(from + o, to + o, r, "dark", 6);
            void Flange(Vector3 at, Vector3 dir, float r)
            {
                float t = Mathf.Max(0.012f, r * 0.4f);
                b.Cylinder(at - dir * (t * 0.5f) + o, at + dir * (t * 0.5f) + o, r * 1.8f, "steel", 14);
                Vector3 side = Vector3.Cross(dir, Mathf.Abs(dir.y) < 0.9f ? Vector3.up : Vector3.right).normalized, side2 = Vector3.Cross(dir, side);
                for (int i = 0; i < 4; i++)
                {
                    float a = (45f + 90f * i) * Mathf.Deg2Rad;
                    Vector3 p = at + (side * Mathf.Cos(a) + side2 * Mathf.Sin(a)) * r * 1.35f;
                    Hex(p - dir * (t * 0.5f + 0.006f), p + dir * (t * 0.5f + 0.006f), 0.006f);
                }
            }
            void Post(float x, float z, float top, float plate)
            {
                b.Box(P(x, (0.098f + top) * 0.5f, z), new Vector3(0.035f, top - 0.098f, 0.035f), "steel");
                b.Box(P(x, 0.106f, z), new Vector3(plate, 0.012f, plate), "dark");
            }

            b.Box(P(0f, 0.05f, 0f), new Vector3(n - 0.06f, 0.1f, n - 0.06f), "dark");
            foreach (float z in new[] { -h + 0.028f, h - 0.028f })
            {
                foreach (float x in new[] { -h * 0.5f, h * 0.5f })
                {
                    b.Box(P(x, 0.05f, z), new Vector3(0.26f, 0.05f, 0.008f), "black");
                }
            }
            int stripes = n * 6;
            for (int i = 0; i < stripes; i++)
            {
                float x = -h + 0.03f + (i + 0.5f) * (n - 0.06f) / stripes;
                b.Box(P(x, 0.106f, -h + 0.07f), new Vector3((n - 0.06f) / stripes - 0.002f, 0.012f, 0.08f), i % 2 == 0 ? "yellow" : "black");
            }
            foreach (float x in new[] { -h + 0.06f, h - 0.06f })
            {
                b.Box(P(x, 0.13f, 0.1f), new Vector3(0.06f, 0.06f, n - 0.3f), "steel");
            }

            int tanks = n;
            float pitch = (w * 0.5f - 0.2f) / tanks;
            float radius = Mathf.Min(0.2f, pitch * 0.42f);
            float headerY = l.TankTop + radius + 0.08f;
            float strap = radius * 1.06f, rackZ = l.TankZ + strap + 0.05f;
            foreach (var (sign, color, socket) in new[] { (-1f, "orange", l.Hydrogen), (1f, "cyan", l.Oxygen) })
            {
                float inner = 0.12f * sign;
                for (int i = 0; i < tanks; i++)
                {
                    float x = inner + sign * (i + 0.5f) * pitch;
                    b.Cylinder(P(x, 0.095f, l.TankZ), P(x, 0.16f, l.TankZ), radius * 1.15f, "dark", 20);
                    for (int j = 0; j < 4; j++)
                    {
                        float a = (45f + 90f * j) * Mathf.Deg2Rad;
                        var bolt = new Vector3(x + Mathf.Cos(a) * radius * 1.08f, 0.157f, l.TankZ + Mathf.Sin(a) * radius * 1.08f);
                        Hex(bolt, bolt + Vector3.up * 0.018f, 0.008f);
                    }
                    b.Cylinder(P(x, 0.16f, l.TankZ), P(x, l.TankTop, l.TankZ), radius, color, 20);
                    b.Sphere(P(x, l.TankTop, l.TankZ), radius * 0.98f, color, 18);
                    for (float y = 0.4f; y < l.TankTop - 0.1f; y += 0.45f)
                    {
                        b.Cylinder(P(x, y, l.TankZ), P(x, y + 0.03f, l.TankZ), strap, "dark", 20);
                        b.Box(P(x, y + 0.015f, l.TankZ + strap + 0.0125f), new Vector3(0.05f, 0.03f, 0.035f), "dark");
                        Hex(new Vector3(x - 0.032f, y + 0.015f, l.TankZ + strap + 0.014f), new Vector3(x + 0.032f, y + 0.015f, l.TankZ + strap + 0.014f), 0.007f);
                    }
                    Pipe(0.028f, "steel", new Vector3(x, l.TankTop + radius * 0.7f, l.TankZ), new Vector3(x, headerY, l.TankZ));
                    float valveY = l.TankTop + radius + 0.035f;
                    b.Cylinder(P(x, l.TankTop + radius * 0.9f, l.TankZ), P(x, l.TankTop + radius + 0.012f, l.TankZ), 0.04f, "steel", 14);
                    b.Box(P(x, valveY, l.TankZ), new Vector3(0.07f, 0.05f, 0.07f), "dark");
                    b.Cylinder(P(x, valveY, l.TankZ - 0.034f), P(x, valveY, l.TankZ - 0.058f), 0.006f, "steel", 6);
                    b.Cylinder(P(x, valveY, l.TankZ - 0.055f), P(x, valveY, l.TankZ - 0.064f), 0.03f, "red", 14);
                    b.Box(P(x, l.TankTop * 0.55f, l.TankZ - radius - 0.005f), new Vector3(0.07f, 0.07f, 0.02f), "white");
                    float gaugeY = l.TankTop * 0.72f, front = l.TankZ - radius;
                    b.Cylinder(P(x, gaugeY, front + 0.01f), P(x, gaugeY, front - 0.021f), 0.012f, "steel", 8);
                    b.Cylinder(P(x, gaugeY, front - 0.02f), P(x, gaugeY, front - 0.034f), 0.042f, "dark", 16);
                    b.Cylinder(P(x, gaugeY, front - 0.034f), P(x, gaugeY, front - 0.0365f), 0.034f, "white", 16);
                    b.Box(P(x + 0.007f, gaugeY + 0.007f, front - 0.038f), new Vector3(0.004f, 0.026f, 0.004f), "black", Quaternion.Euler(0f, 0f, -45f));
                }
                float edge = sign * (h - 0.12f);
                Pipe(0.04f, "steel",
                    new Vector3(inner, headerY, l.TankZ),
                    new Vector3(edge, headerY, l.TankZ),
                    new Vector3(edge, socket.y, l.TankZ),
                    new Vector3(edge, socket.y, h - 0.12f),
                    new Vector3(socket.x, socket.y, h - 0.12f),
                    new Vector3(socket.x, socket.y, h - 0.04f));
                Flange(new Vector3(edge - sign * 0.09f, headerY, l.TankZ), Vector3.right, 0.04f);
                Flange(new Vector3(edge, headerY - 0.09f, l.TankZ), Vector3.up, 0.04f);
                Flange(new Vector3(socket.x, socket.y, h - 0.075f), Vector3.forward, 0.04f);
                Post(edge, l.TankZ, socket.y - 0.038f, 0.06f);
                Post(edge, h - 0.12f, socket.y - 0.038f, 0.06f);
                b.Box(P(edge, l.TankTop * 0.5f, l.TankZ), new Vector3(0.1f, 0.12f, 0.1f), "dark");
                b.Cylinder(P(edge, l.TankTop * 0.5f, l.TankZ - 0.05f), P(edge, l.TankTop * 0.5f, l.TankZ - 0.09f), 0.035f, color, 12);
            }

            float rackX = w * 0.5f - 0.08f, rackTop = 0.2f;
            for (float y = 0.4f; y < l.TankTop - 0.1f; y += 0.45f)
            {
                b.Box(P(0f, y + 0.015f, rackZ), new Vector3(rackX * 2f + 0.04f, 0.04f, 0.04f), "steel");
                rackTop = y + 0.035f;
            }
            foreach (float x in new[] { -rackX, 0f, rackX })
            {
                Post(x, rackZ, rackTop, 0.09f);
                foreach (float dx in new[] { -0.03f, 0.03f })
                {
                    Hex(new Vector3(x + dx, 0.11f, rackZ - 0.03f), new Vector3(x + dx, 0.124f, rackZ - 0.03f), 0.008f);
                }
            }

            float stackW = w - 0.9f, stackX = 0.25f;
            int plates = Mathf.RoundToInt(stackW / 0.05f);
            float stackH = l.StackTop - 0.14f, stackD = 0.56f, endTop = l.StackTop + 0.03f, endOuter = stackW * 0.5f + 0.08f;
            b.Box(P(stackX, 0.1225f, l.StackZ), new Vector3(stackW, 0.045f, stackD - 0.06f), "dark");
            foreach (float side in new[] { -1f, 1f })
            {
                float x = stackX + side * (stackW * 0.5f + 0.04f);
                b.Box(P(x, (0.1f + endTop) * 0.5f, l.StackZ), new Vector3(0.08f, endTop - 0.1f, stackD + 0.08f), "steel");
                b.Box(P(x + side * 0.04f, 0.1125f, l.StackZ), new Vector3(0.16f, 0.025f, stackD + 0.16f), "dark");
                foreach (float z in new[] { -1f, 1f })
                {
                    var bolt = new Vector3(x + side * 0.085f, 0.122f, l.StackZ + z * (stackD * 0.5f + 0.055f));
                    Hex(bolt, bolt + Vector3.up * 0.02f, 0.012f);
                }
                b.Box(P(x, endTop + 0.02f, l.StackZ), new Vector3(0.07f, 0.04f, 0.1f), side < 0f ? "orange" : "dark");
            }
            for (int i = 0; i < plates; i++)
            {
                float x = stackX - stackW * 0.5f + (i + 0.5f) * stackW / plates;
                b.Box(P(x, 0.14f + stackH * 0.5f, l.StackZ), new Vector3(stackW / plates - 0.006f, stackH, stackD), i % 2 == 0 ? "grey" : "dark");
            }
            foreach (float y in new[] { 0.2f, l.StackTop - 0.06f })
            {
                foreach (float z in new[] { l.StackZ - stackD * 0.5f - 0.02f, l.StackZ + stackD * 0.5f + 0.02f })
                {
                    b.Cylinder(P(stackX - endOuter - 0.04f, y, z), P(stackX + endOuter + 0.04f, y, z), 0.018f, "steel", 10);
                    foreach (float side in new[] { -1f, 1f })
                    {
                        Hex(new Vector3(stackX + side * (endOuter - 0.002f), y, z), new Vector3(stackX + side * (endOuter + 0.025f), y, z), 0.032f);
                    }
                }
            }
            int fins = Mathf.RoundToInt(stackW / 0.09f);
            for (int i = 0; i < fins; i++)
            {
                float x = stackX - stackW * 0.5f + (i + 0.5f) * stackW / fins;
                b.Box(P(x, l.StackTop + 0.09f, l.StackZ), new Vector3(0.012f, 0.18f, stackD - 0.04f), "light");
            }
            b.Box(P(stackX, l.StackTop + 0.005f, l.StackZ), new Vector3(stackW, 0.012f, stackD), "dark");

            float riseZ = l.StackZ + stackD * 0.5f + 0.12f, outY = l.StackTop - 0.16f, backFace = l.StackZ + stackD * 0.5f;
            Pipe(0.035f, "orange",
                new Vector3(stackX - stackW * 0.25f, outY, backFace),
                new Vector3(stackX - stackW * 0.25f, outY, riseZ),
                new Vector3(-0.08f, outY, riseZ),
                new Vector3(-0.08f, headerY, riseZ),
                new Vector3(-0.08f, headerY, l.TankZ),
                new Vector3(-0.12f, headerY, l.TankZ));
            Pipe(0.035f, "cyan",
                new Vector3(stackX + stackW * 0.25f, outY, backFace),
                new Vector3(stackX + stackW * 0.25f, outY, riseZ + 0.08f),
                new Vector3(0.08f, outY, riseZ + 0.08f),
                new Vector3(0.08f, headerY, riseZ + 0.08f),
                new Vector3(0.08f, headerY, l.TankZ),
                new Vector3(0.12f, headerY, l.TankZ));
            foreach (float x in new[] { stackX - stackW * 0.25f, stackX + stackW * 0.25f })
            {
                Flange(new Vector3(x, outY, backFace + 0.008f), Vector3.forward, 0.035f);
            }
            Post(-0.08f, riseZ, outY - 0.033f, 0.07f);
            Post(0.08f, riseZ + 0.08f, outY - 0.033f, 0.07f);
            Flange(new Vector3(-0.08f, outY + 0.12f, riseZ), Vector3.up, 0.035f);
            Flange(new Vector3(0.08f, outY + 0.12f, riseZ + 0.08f), Vector3.up, 0.035f);

            float backZ = h - 0.12f;
            float sideX = stackX + stackW * 0.5f + 0.2f;
            Pipe(0.04f, "blue",
                new Vector3(l.Water.x, l.Water.y, h - 0.02f),
                new Vector3(l.Water.x, l.Water.y, backZ),
                new Vector3(l.Water.x, 0.3f, backZ),
                new Vector3(sideX, 0.3f, backZ),
                new Vector3(sideX, 0.3f, l.StackZ),
                new Vector3(stackX + endOuter, 0.3f, l.StackZ));
            b.Cylinder(P(l.Water.x, 0.33f, backZ), P(l.Water.x, 0.4f, backZ), 0.06f, "dark", 14);
            Flange(new Vector3(stackX + endOuter + 0.007f, 0.3f, l.StackZ), Vector3.right, 0.04f);
            Post(sideX, backZ, 0.262f, 0.07f);
            Post(sideX, l.StackZ, 0.262f, 0.07f);

            b.Box(P(l.Power.x, 0.45f, backZ - 0.05f), new Vector3(0.24f, 0.3f, 0.14f), "dark");
            b.Box(P(l.Power.x, 0.61f, backZ - 0.05f), new Vector3(0.25f, 0.025f, 0.15f), "grey");
            b.Box(P(l.Power.x, 0.5f, backZ + 0.022f), new Vector3(0.12f, 0.06f, 0.005f), "yellow");
            foreach (float dx in new[] { -0.09f, 0.09f })
            {
                Post(l.Power.x + dx, backZ - 0.1f, 0.302f, 0.06f);
            }
            Pipe(0.026f, "steel", new Vector3(l.Power.x, l.Power.y, h - 0.02f), new Vector3(l.Power.x, l.Power.y, backZ), new Vector3(l.Power.x, 0.3f, backZ));
            float leftX = stackX - stackW * 0.5f - 0.04f, wireY = Mathf.Max(l.StackTop + 0.3f, headerY + 0.1f);
            Pipe(0.026f, "steel",
                new Vector3(l.Power.x, 0.62f, backZ - 0.05f),
                new Vector3(l.Power.x, wireY, backZ - 0.05f),
                new Vector3(leftX, wireY, backZ - 0.05f),
                new Vector3(leftX, wireY, l.StackZ),
                new Vector3(leftX, l.StackTop + 0.03f, l.StackZ));

            float hx = l.HopperX, hz = -h + 0.3f;
            b.Box(P(hx, 0.4f, hz), new Vector3(0.5f, 0.6f, 0.44f), "grey");
            b.Box(P(hx, 0.72f, hz), new Vector3(0.56f, 0.06f, 0.5f), "dark");
            b.Box(P(hx, 0.756f, hz), new Vector3(0.44f, 0.012f, 0.36f), "black");
            b.Box(P(hx, 0.6f, -h + 0.075f), new Vector3(0.34f, 0.08f, 0.01f), "white");
            b.Box(P(hx, 0.3f, -h + 0.075f), new Vector3(0.4f, 0.24f, 0.01f), "dark");
            foreach (float y in new[] { 0.23f, 0.37f })
            {
                b.Cylinder(P(hx - 0.2f, y - 0.022f, -h + 0.068f), P(hx - 0.2f, y + 0.022f, -h + 0.068f), 0.01f, "steel", 8);
            }
            b.Box(P(hx + 0.16f, 0.3f, -h + 0.062f), new Vector3(0.02f, 0.1f, 0.02f), "steel");
            foreach (float side in new[] { -1f, 1f })
            {
                for (int i = 0; i < 6; i++)
                {
                    b.Box(P(hx + side * 0.252f, 0.27f + i * 0.045f, hz), new Vector3(0.014f, 0.012f, 0.3f), "dark", Quaternion.Euler(0f, 0f, side * 35f));
                }
            }
            b.Cylinder(P(hx - 0.14f, 0.755f, hz + 0.18f), P(hx + 0.14f, 0.755f, hz + 0.18f), 0.012f, "steel", 10);
            b.Box(P(hx, 0.86f, hz + 0.18f), new Vector3(0.3f, 0.2f, 0.03f), "dark");
            b.Box(P(hx, 0.865f, hz + 0.163f), new Vector3(0.26f, 0.15f, 0.005f), "screen");
            foreach (var (dx, c) in new[] { (-0.08f, "green"), (0f, "yellow"), (0.08f, "red") })
            {
                b.Box(P(hx + dx, 0.46f, -h + 0.075f), new Vector3(0.035f, 0.035f, 0.012f), c);
            }
            b.Box(P(l.Signal.x, 0.42f, backZ - 0.02f), new Vector3(0.12f, 0.12f, 0.1f), "dark");
            Post(l.Signal.x, backZ - 0.05f, 0.362f, 0.06f);
            Pipe(0.016f, "white", new Vector3(l.Signal.x, l.Signal.y, h - 0.04f), new Vector3(l.Signal.x, l.Signal.y, backZ), new Vector3(l.Signal.x, 0.36f, backZ));
            ServicePanel(b, o, h, new[] { (l.Hydrogen.x, "orange"), (l.Power.x, "yellow"), (l.Signal.x, "white"), (l.Water.x, "blue"), (l.Oxygen.x, "cyan") });
            return b.Build("ElectrolyzerLargeBody");
        }

        internal static PartSettings BuildScaled(IEnumerable<PartSettings> gameParts, int n)
        {
            List<PartSettings> parts = gameParts.ToList();
            PartSettings chest = PartFactory.Find(parts, ChestPartId), generator = PartFactory.Find(parts, GeneratorPartId), distiller = PartFactory.Find(parts, DistillerPartId);
            return chest?.part == null || generator?.part == null || distiller?.part == null ? null : Build(chest, generator, distiller, n);
        }

        private static void Glyph(MeshBuilder b, char c, Vector3 center, Vector3 normal)
        {
            const float h = 0.085f, w = 0.06f, s = 0.016f, d = 0.006f;
            Quaternion face = Quaternion.LookRotation(-normal, Vector3.up);
            void Bar(float x, float y, float sx, float sy, float angle = 0f)
            {
                Quaternion rot = face * Quaternion.Euler(0f, 0f, angle);
                b.Box(center + face * new Vector3(x, y, 0f), new Vector3(sx, sy, d), rot);
            }
            switch (c)
            {
                case 'H':
                    Bar(-w * 0.5f + s * 0.5f, 0f, s, h);
                    Bar(w * 0.5f - s * 0.5f, 0f, s, h);
                    Bar(0f, 0f, w, s);
                    break;
                case 'O':
                    Bar(-w * 0.5f + s * 0.5f, 0f, s, h);
                    Bar(w * 0.5f - s * 0.5f, 0f, s, h);
                    Bar(0f, h * 0.5f - s * 0.5f, w, s);
                    Bar(0f, -h * 0.5f + s * 0.5f, w, s);
                    break;
                case 'W':
                    Bar(-w * 0.6f + s * 0.5f, 0f, s, h);
                    Bar(w * 0.6f - s * 0.5f, 0f, s, h);
                    Bar(0f, -h * 0.2f, s, h * 0.6f);
                    Bar(0f, -h * 0.5f + s * 0.5f, w * 1.2f, s);
                    break;
                case 'Z':
                    Bar(0.012f, h * 0.22f, s, h * 0.6f, -25f);
                    Bar(0f, 0f, w * 0.8f, s);
                    Bar(-0.012f, -h * 0.22f, s, h * 0.6f, -25f);
                    break;
                case 'I':
                    for (int i = -1; i <= 1; i++)
                    {
                        Bar(i * 0.07f, 0f, 0.035f, 0.035f, 45f);
                    }
                    break;
            }
        }

        private static Material Emissive(Material source, string name, Color color)
        {
            var m = new Material(source) { name = name };
            m.SetTexture("_Albedo", Texture2D.whiteTexture);
            m.SetTexture("_EmissionMap", Texture2D.whiteTexture);
            m.SetColor("_EmissionColor", color);
            m.EnableKeyword("_EMISSION");
            return m;
        }

        private static PartSettings Build(PartSettings chest, PartSettings generator, PartSettings distiller, int n)
        {
            Layout l = n > 1 ? LargeLayout(n) : default;
            GameObject root = Object.Instantiate(chest.part, PartFactory.Holder);
            root.name = "Electrolyzer";

            Transform visual = root.transform.Find("Visual");
            MeshRenderer look = visual.GetComponentInChildren<MeshRenderer>(true);
            Material material = PartFactory.CreateMaterial(look.sharedMaterial, ModAssets.LoadTexture("door_albedo.png"), "Electrolyzer");
            var lookCopy = new GameObject("look").AddComponent<MeshRenderer>();
            lookCopy.gameObject.layer = look.gameObject.layer;
            lookCopy.shadowCastingMode = look.shadowCastingMode;
            lookCopy.renderingLayerMask = look.renderingLayerMask;
            foreach (Transform child in visual.Cast<Transform>().ToArray())
            {
                Object.DestroyImmediate(child.gameObject);
            }
            foreach (DefaultShipPartVisuals v in root.GetComponentsInChildren<DefaultShipPartVisuals>(true))
            {
                Object.DestroyImmediate(v);
            }
            visual.gameObject.AddComponent<ElectrolyzerSoundVisuals>();
            Mesh mesh = n > 1 ? LargeBodyMesh(l) : BodyMesh();
            PartFactory.CreateRenderer("Body", visual, mesh, material, lookCopy);
            Vector3 labelAt = n > 1 ? l.Label + l.Offset : new Vector3(0f, 0.6f, -0.408f);
            GameObject labels = PartFactory.CreateRenderer("Labels", visual, LabelMesh(labelAt), Emissive(material, "ElectrolyzerLabels", new Color(1.4f, 1.4f, 1.3f)), lookCopy);
            labels.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Object.DestroyImmediate(lookCopy.gameObject);

            PartFactory.SetBounds(root, mesh.bounds);

            var box = root.transform.Find("Interactions").GetComponentInChildren<BoxCollider>(true);
            if (n > 1)
            {
                box.center = l.Offset + new Vector3(l.HopperX, 0.45f, -l.Half + 0.3f);
                box.size = new Vector3(0.6f, 0.8f, 0.5f);
            }
            else
            {
                box.center = new Vector3(0f, 0.53f, -0.27f);
                box.size = new Vector3(0.56f, 0.3f, 0.3f);
            }

            SocketObject fluid = PartFactory.FindSocket(distiller, CableType.Fluid);
            SocketObject power = PartFactory.FindSocket(generator, CableType.Power);
            PartFactory.CloneSocket(fluid, root.transform, OxygenSocket, n > 1 ? l.Oxygen + l.Offset : OxygenSocketPos, 180f);
            PartFactory.CloneSocket(power, root.transform, PowerSocket, n > 1 ? l.Power + l.Offset : PowerSocketPos, 180f);
            PartFactory.CloneSocket(fluid, root.transform, HydrogenSocket, n > 1 ? l.Hydrogen + l.Offset : HydrogenSocketPos, 180f);
            PartFactory.CloneSocket(fluid, root.transform, WaterSocket, n > 1 ? l.Water + l.Offset : WaterSocketPos, 180f);
            SocketObject signalSource = Resources.FindObjectsOfTypeAll<SocketObject>().FirstOrDefault(k => k.Type == CableType.Signal && k.visualObject != null && k.interactive != null);
            if (signalSource != null)
            {
                PartFactory.CloneSocket(signalSource, root.transform, SignalSocket, n > 1 ? l.Signal + l.Offset : SignalSocketPos, 180f);
            }
            else
            {
                Melon<PowerMod>.Logger.Warning("No signal socket found to copy - electrolyzer without a signal input (runs without it)");
            }

            var settings = ScriptableObject.CreateInstance<PartSettings>();
            settings.name = "MiningElectrolyzer";
            settings.hideFlags = HideFlags.DontUnloadUnusedAsset;
            settings.id = PartId;
            settings.fullLabel = "Electrolyzer";
            settings.internalStateClass = "ElectrolyzerState";
            settings.snappingStyle = SnappingStyle.PreciseOnFloor;
            settings.description = "Splits ice (put it in the hopper on the front) into oxygen, hydrogen and water, or - without ice - water from a pipe into hydrogen and oxygen. " +
                                   "Needs power. All sockets are on the service panel at the back: hydrogen out (orange), power (yellow), signal (white), water (blue: in, or out while splitting ice) and oxygen out (light blue). " +
                                   "Without a signal cable it always runs, with one it runs only while the signal is above 0.";
            settings.localizedDescription = new UnityEngine.Localization.LocalizedString();
            settings.part = root;
            settings.thumbnailRotation = Quaternion.Euler(0f, 150f, 0f);
            settings.mass = 40f;
            settings.size = Vector3.one * n;
            PartFactory.SetCost(settings, (Items.Model.ItemsConstants.IronItemId, 12), (112, 8), (Items.Model.ItemsConstants.GlassItemId, 6));
            return settings;
        }
    }
}
