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
                _part = Build(chest, generator, distiller);
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

        private static Mesh LabelMesh()
        {
            var b = new MeshBuilder();
            Glyph(b, 'I', new Vector3(0f, 0.6f, -0.408f), new Vector3(0f, 0f, -1f));
            return b.Build("ElectrolyzerLabels");
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

        private static PartSettings Build(PartSettings chest, PartSettings generator, PartSettings distiller)
        {
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
            Mesh mesh = BodyMesh();
            PartFactory.CreateRenderer("Body", visual, mesh, material, lookCopy);
            GameObject labels = PartFactory.CreateRenderer("Labels", visual, LabelMesh(), Emissive(material, "ElectrolyzerLabels", new Color(1.4f, 1.4f, 1.3f)), lookCopy);
            labels.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Object.DestroyImmediate(lookCopy.gameObject);

            PartFactory.SetBounds(root, mesh.bounds);

            var box = root.transform.Find("Interactions").GetComponentInChildren<BoxCollider>(true);
            box.center = new Vector3(0f, 0.53f, -0.27f);
            box.size = new Vector3(0.56f, 0.3f, 0.3f);

            SocketObject fluid = PartFactory.FindSocket(distiller, CableType.Fluid);
            SocketObject power = PartFactory.FindSocket(generator, CableType.Power);
            PartFactory.CloneSocket(fluid, root.transform, OxygenSocket, OxygenSocketPos, 270f);
            PartFactory.CloneSocket(power, root.transform, PowerSocket, PowerSocketPos, 180f);
            PartFactory.CloneSocket(fluid, root.transform, HydrogenSocket, HydrogenSocketPos, 90f);
            PartFactory.CloneSocket(fluid, root.transform, WaterSocket, WaterSocketPos, 180f);

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
            settings.size = Vector3.one;
            PartFactory.SetCost(settings, (Items.Model.ItemsConstants.IronItemId, 12), (112, 8), (Items.Model.ItemsConstants.GlassItemId, 6));
            return settings;
        }
    }
}
