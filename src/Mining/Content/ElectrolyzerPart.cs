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

        private static PartSettings Build(PartSettings chest, PartSettings generator, PartSettings distiller)
        {
            GameObject root = Object.Instantiate(chest.part, PartFactory.Holder);
            root.name = "Electrolyzer";
            Mesh mesh = ModAssets.LoadMesh("electrolyzer.bin");
            Texture2D albedo = ModAssets.LoadTexture("hydrogen_albedo.png");

            Transform visual = root.transform.Find("Visual");
            MeshRenderer look = visual.GetComponentInChildren<MeshRenderer>(true);
            Material material = PartFactory.CreateMaterial(look.sharedMaterial, albedo, "Electrolyzer");
            var lookCopy = new GameObject("look").AddComponent<MeshRenderer>();
            lookCopy.gameObject.layer = look.gameObject.layer;
            lookCopy.shadowCastingMode = look.shadowCastingMode;
            lookCopy.renderingLayerMask = look.renderingLayerMask;
            foreach (Transform child in visual.Cast<Transform>().ToArray())
            {
                Object.DestroyImmediate(child.gameObject);
            }
            PartFactory.CreateRenderer("Body", visual, mesh, material, lookCopy);
            Object.DestroyImmediate(lookCopy.gameObject);

            PartFactory.SetBounds(root, mesh.bounds);

            var box = root.transform.Find("Interactions").GetComponentInChildren<BoxCollider>(true);
            box.center = new Vector3(0f, 0.42f, -0.3f);
            box.size = new Vector3(0.8f, 0.55f, 0.36f);

            SocketObject fluid = PartFactory.FindSocket(distiller, CableType.Fluid);
            SocketObject power = PartFactory.FindSocket(generator, CableType.Power);
            PartFactory.CloneSocket(fluid, root.transform, OxygenSocket, new Vector3(0.5f, 0.25f, 0.16f), 270f);
            PartFactory.CloneSocket(power, root.transform, PowerSocket, new Vector3(0.5f, 0.25f, -0.2f), 270f);
            PartFactory.CloneSocket(fluid, root.transform, HydrogenSocket, new Vector3(-0.5f, 0.25f, 0.16f), 90f);
            PartFactory.CloneSocket(fluid, root.transform, WaterSocket, new Vector3(-0.5f, 0.25f, -0.2f), 90f);

            var settings = ScriptableObject.CreateInstance<PartSettings>();
            settings.name = "MiningElectrolyzer";
            settings.hideFlags = HideFlags.DontUnloadUnusedAsset;
            settings.id = PartId;
            settings.fullLabel = "Electrolyzer";
            settings.internalStateClass = "ElectrolyzerState";
            settings.snappingStyle = SnappingStyle.PreciseOnFloor;
            settings.description = "Splits ice (put it in the slot) into oxygen, hydrogen and water, or - without ice - water from a pipe into hydrogen and oxygen. Needs power. " +
                                   "Sockets: right side - oxygen out, power; left side - hydrogen out, water (out with ice, in without ice).";
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
