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

namespace Mining.Content
{
    internal static class ElectrolyzerPart
    {
        internal const ushort PartId = 202;
        internal const byte OxygenSocket = 0, PowerSocket = 1, HydrogenSocket = 2, WaterSocket = 3;

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
                Melon<MiningMod>.Logger.Msg($"Added part '{_part.fullLabel}' (id {PartId})");
            }
            catch (Exception e)
            {
                _failed = true;
                Melon<MiningMod>.Logger.Error($"Could not build the electrolyzer: {e}");
            }
            return _part;
        }

        private const float Wall = 0.4f, HousingTop = 0.42f, SocketY = 0.2f, LabelY = 0.33f;
        private static readonly Vector3 HydrogenSocketPos = new Vector3(-0.5f, SocketY, 0.12f);
        private static readonly Vector3 OxygenSocketPos = new Vector3(0.5f, SocketY, 0.12f);
        private static readonly Vector3 WaterSocketPos = new Vector3(0.18f, SocketY, 0.5f);
        private static readonly Vector3 PowerSocketPos = new Vector3(-0.18f, SocketY, 0.5f);

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
                b.Cylinder(new Vector3(px, 0.95f, 0.12f), new Vector3(px, SocketY + 0.09f, 0.12f), 0.03f, "steel", 12);
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
            public Vector3 Hydrogen, Oxygen, Water, Power, Label;
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
            l.Hydrogen = new Vector3(-half, SocketY, l.TankZ);
            l.Oxygen = new Vector3(half, SocketY, l.TankZ);
            l.Power = new Vector3(-half + 0.5f, SocketY, half);
            l.Water = new Vector3(half - 0.5f, SocketY, half);
            l.Label = new Vector3(l.HopperX, 0.6f, -half + 0.062f);
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

            b.Box(P(0f, 0.05f, 0f), new Vector3(n - 0.06f, 0.1f, n - 0.06f), "dark");
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
            foreach (var (sign, color, socket) in new[] { (-1f, "orange", l.Hydrogen), (1f, "cyan", l.Oxygen) })
            {
                float inner = 0.12f * sign;
                for (int i = 0; i < tanks; i++)
                {
                    float x = inner + sign * (i + 0.5f) * pitch;
                    b.Cylinder(P(x, 0.1f, l.TankZ), P(x, 0.16f, l.TankZ), radius * 1.15f, "dark", 20);
                    b.Cylinder(P(x, 0.16f, l.TankZ), P(x, l.TankTop, l.TankZ), radius, color, 20);
                    b.Sphere(P(x, l.TankTop, l.TankZ), radius * 0.98f, color, 18);
                    for (float y = 0.4f; y < l.TankTop - 0.1f; y += 0.45f)
                    {
                        b.Cylinder(P(x, y, l.TankZ), P(x, y + 0.03f, l.TankZ), radius * 1.06f, "dark", 20);
                    }
                    Pipe(0.028f, "steel", new Vector3(x, l.TankTop + radius * 0.7f, l.TankZ), new Vector3(x, headerY, l.TankZ));
                    b.Box(P(x, l.TankTop * 0.55f, l.TankZ - radius - 0.005f), new Vector3(0.07f, 0.07f, 0.02f), "white");
                }
                float edge = sign * (h - 0.12f);
                Pipe(0.04f, "steel",
                    new Vector3(inner, headerY, l.TankZ),
                    new Vector3(edge, headerY, l.TankZ),
                    new Vector3(edge, socket.y, l.TankZ),
                    new Vector3(socket.x - sign * 0.02f, socket.y, socket.z));
                b.Box(P(edge, l.TankTop * 0.5f, l.TankZ), new Vector3(0.1f, 0.12f, 0.1f), "dark");
                b.Cylinder(P(edge, l.TankTop * 0.5f, l.TankZ - 0.05f), P(edge, l.TankTop * 0.5f, l.TankZ - 0.09f), 0.035f, color, 12);
            }

            float stackW = w - 0.9f, stackX = 0.25f;
            int plates = Mathf.RoundToInt(stackW / 0.05f);
            float stackH = l.StackTop - 0.14f, stackD = 0.56f;
            foreach (float x in new[] { stackX - stackW * 0.5f - 0.04f, stackX + stackW * 0.5f + 0.04f })
            {
                b.Box(P(x, 0.14f + stackH * 0.5f, l.StackZ), new Vector3(0.08f, stackH + 0.06f, stackD + 0.08f), "steel");
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
                    b.Cylinder(P(stackX - stackW * 0.5f - 0.1f, y, z), P(stackX + stackW * 0.5f + 0.1f, y, z), 0.018f, "steel", 10);
                }
            }
            int fins = Mathf.RoundToInt(stackW / 0.09f);
            for (int i = 0; i < fins; i++)
            {
                float x = stackX - stackW * 0.5f + (i + 0.5f) * stackW / fins;
                b.Box(P(x, l.StackTop + 0.09f, l.StackZ), new Vector3(0.012f, 0.18f, stackD - 0.04f), "light");
            }
            b.Box(P(stackX, l.StackTop + 0.005f, l.StackZ), new Vector3(stackW, 0.012f, stackD), "dark");

            float riseZ = l.StackZ + stackD * 0.5f + 0.12f;
            Pipe(0.035f, "orange",
                new Vector3(stackX - stackW * 0.25f, l.StackTop - 0.1f, l.StackZ + stackD * 0.5f),
                new Vector3(stackX - stackW * 0.25f, l.StackTop - 0.1f, riseZ),
                new Vector3(-0.08f, l.StackTop - 0.1f, riseZ),
                new Vector3(-0.08f, headerY, riseZ),
                new Vector3(-0.08f, headerY, l.TankZ),
                new Vector3(-0.12f, headerY, l.TankZ));
            Pipe(0.035f, "cyan",
                new Vector3(stackX + stackW * 0.25f, l.StackTop - 0.1f, l.StackZ + stackD * 0.5f),
                new Vector3(stackX + stackW * 0.25f, l.StackTop - 0.1f, riseZ + 0.08f),
                new Vector3(0.08f, l.StackTop - 0.1f, riseZ + 0.08f),
                new Vector3(0.08f, headerY, riseZ + 0.08f),
                new Vector3(0.08f, headerY, l.TankZ),
                new Vector3(0.12f, headerY, l.TankZ));

            float backZ = h - 0.12f;
            float sideX = stackX + stackW * 0.5f + 0.2f;
            Pipe(0.04f, "blue",
                new Vector3(l.Water.x, l.Water.y, h - 0.02f),
                new Vector3(l.Water.x, l.Water.y, backZ),
                new Vector3(l.Water.x, 0.3f, backZ),
                new Vector3(sideX, 0.3f, backZ),
                new Vector3(sideX, 0.3f, l.StackZ),
                new Vector3(stackX + stackW * 0.5f + 0.08f, 0.3f, l.StackZ));
            b.Cylinder(P(l.Water.x, 0.34f, backZ), P(l.Water.x, 0.4f, backZ), 0.06f, "dark", 14);

            b.Box(P(l.Power.x, 0.45f, backZ - 0.05f), new Vector3(0.24f, 0.3f, 0.14f), "dark");
            b.Box(P(l.Power.x, 0.61f, backZ - 0.05f), new Vector3(0.25f, 0.025f, 0.15f), "grey");
            b.Box(P(l.Power.x, 0.5f, backZ + 0.022f), new Vector3(0.12f, 0.06f, 0.005f), "yellow");
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
            b.Box(P(hx, 0.86f, hz + 0.18f), new Vector3(0.3f, 0.2f, 0.03f), "dark");
            b.Box(P(hx, 0.865f, hz + 0.163f), new Vector3(0.26f, 0.15f, 0.005f), "screen");
            foreach (var (dx, c) in new[] { (-0.08f, "green"), (0f, "yellow"), (0.08f, "red") })
            {
                b.Box(P(hx + dx, 0.46f, -h + 0.075f), new Vector3(0.035f, 0.035f, 0.012f), c);
            }
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
            PartFactory.CloneSocket(fluid, root.transform, OxygenSocket, n > 1 ? l.Oxygen + l.Offset : OxygenSocketPos, 270f);
            PartFactory.CloneSocket(power, root.transform, PowerSocket, n > 1 ? l.Power + l.Offset : PowerSocketPos, 180f);
            PartFactory.CloneSocket(fluid, root.transform, HydrogenSocket, n > 1 ? l.Hydrogen + l.Offset : HydrogenSocketPos, 90f);
            PartFactory.CloneSocket(fluid, root.transform, WaterSocket, n > 1 ? l.Water + l.Offset : WaterSocketPos, 180f);

            var settings = ScriptableObject.CreateInstance<PartSettings>();
            settings.name = "MiningElectrolyzer";
            settings.hideFlags = HideFlags.DontUnloadUnusedAsset;
            settings.id = PartId;
            settings.fullLabel = "Electrolyzer";
            settings.internalStateClass = "ElectrolyzerState";
            settings.snappingStyle = SnappingStyle.PreciseOnFloor;
            settings.description = "Splits ice (put it in the hopper on the front) into oxygen, hydrogen and water, or - without ice - water from a pipe into hydrogen and oxygen. " +
                                   "Needs power. Outputs on the sides: hydrogen next to the orange tank (left), oxygen next to the light blue tank (right). Inputs on the back: power " +
                                   "and water next to the navy tank (water in, or water out while splitting ice).";
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
