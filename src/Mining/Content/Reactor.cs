using System;
using System.Collections.Generic;
using System.Linq;
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
    internal static class Reactor
    {
        internal const ushort PartId = 203;
        internal const byte PowerSocket = 0, WaterSocket = 2, SignalSocket = 3;
        internal static readonly Vector3[] CorePositions = { new Vector3(0f, 0.64f, -0.45f), new Vector3(0f, 0.64f, 0.45f) };
        internal static bool HasSignalSocket;
        internal static readonly Vector3 RotorPosition = new Vector3(0f, 1.525f, 0f);
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
                Melon<MiningMod>.Logger.Error($"Could not build the reactor: {e}");
            }
            return _part;
        }

        private static PartSettings Build(PartSettings chest, PartSettings generator, PartSettings distiller)
        {
            GameObject root = Object.Instantiate(chest.part, PartFactory.Holder);
            root.name = "NuclearReactor";
            Mesh body = ModAssets.LoadMesh("reactor_body.bin");
            Mesh core = ModAssets.LoadMesh("reactor_core.bin");
            Mesh rotor = ModAssets.LoadMesh("reactor_rotor.bin");

            Transform visual = root.transform.Find("Visual");
            MeshRenderer look = visual.GetComponentInChildren<MeshRenderer>(true);
            Material material = PartFactory.CreateMaterial(look.sharedMaterial, ModAssets.LoadTexture("reactor_albedo.jpg"), "NuclearReactor");
            material.SetTexture("_EmissionMap", ModAssets.LoadTexture("reactor_emission.png"));
            material.SetColor("_EmissionColor", Color.black);
            material.EnableKeyword("_EMISSION");
            Texture2D parts = ModAssets.LoadTexture("reactor_parts.png");
            Material partsMaterial = PartFactory.CreateMaterial(look.sharedMaterial, parts, "NuclearReactorParts");
            Material coreMaterial = PartFactory.CreateMaterial(look.sharedMaterial, parts, "NuclearReactorCore");
            coreMaterial.SetTexture("_EmissionMap", parts);
            coreMaterial.SetColor("_EmissionColor", Color.black);
            coreMaterial.EnableKeyword("_EMISSION");
            var lookCopy = new GameObject("look").AddComponent<MeshRenderer>();
            lookCopy.gameObject.layer = look.gameObject.layer;
            lookCopy.shadowCastingMode = look.shadowCastingMode;
            lookCopy.renderingLayerMask = look.renderingLayerMask;
            foreach (Transform child in visual.Cast<Transform>().ToArray())
            {
                Object.DestroyImmediate(child.gameObject);
            }
            Object.DestroyImmediate(visual.GetComponent<DefaultShipPartVisuals>());
            visual.gameObject.AddComponent<ReactorVisuals>();
            PartFactory.CreateRenderer("Body", visual, body, material, lookCopy);
            for (int i = 0; i < CorePositions.Length; i++)
            {
                PartFactory.CreateRenderer("Core" + i, visual, core, coreMaterial, lookCopy).transform.localPosition = CorePositions[i];
            }
            PartFactory.CreateRenderer("Rotor", visual, rotor, partsMaterial, lookCopy).transform.localPosition = RotorPosition;
            PartFactory.CreateRenderer("Mounts", visual, ModAssets.LoadMesh("reactor_mounts.bin"), partsMaterial, lookCopy);
            Object.DestroyImmediate(lookCopy.gameObject);

            PartFactory.SetBounds(root, body.bounds);

            var box = root.transform.Find("Interactions").GetComponentInChildren<BoxCollider>(true);
            box.center = new Vector3(0f, 0.75f, -1.0f);
            box.size = new Vector3(1.0f, 0.9f, 0.3f);

            SocketObject power = PartFactory.FindSocket(generator, CableType.Power);
            SocketObject fluid = PartFactory.FindSocket(distiller, CableType.Fluid);
            SocketObject p0 = PartFactory.CloneSocket(power, root.transform, 0, new Vector3(-1.00f, 0.50f, -0.60f), 90f);
            SocketObject p1 = PartFactory.CloneSocket(power, root.transform, 1, new Vector3(1.00f, 0.50f, 0.55f), 270f);
            HarmonyLib.Traverse.Create(p0).Field("relayedSockets").SetValue(new[] { p1 });
            HarmonyLib.Traverse.Create(p1).Field("relayedSockets").SetValue(new[] { p0 });
            PartFactory.CloneSocket(fluid, root.transform, WaterSocket, new Vector3(0.75f, 0.50f, 0.97f), 180f);
            SocketObject signalSource = Resources.FindObjectsOfTypeAll<SocketObject>().FirstOrDefault(s => s.Type == CableType.Signal && s.visualObject != null && s.interactive != null);
            HasSignalSocket = signalSource != null;
            if (signalSource != null)
            {
                PartFactory.CloneSocket(signalSource, root.transform, SignalSocket, new Vector3(-0.75f, 0.22f, 0.97f), 180f);
            }
            else
            {
                Melon<MiningMod>.Logger.Warning("No signal socket found to copy - reactor without a signal input (runs without it)");
            }

            var settings = ScriptableObject.CreateInstance<PartSettings>();
            settings.name = "MiningNuclearReactor";
            settings.hideFlags = HideFlags.DontUnloadUnusedAsset;
            settings.id = PartId;
            settings.fullLabel = "Nuclear Reactor";
            settings.internalStateClass = generator.internalStateClass;
            settings.snappingStyle = SnappingStyle.PreciseOnFloor;
            settings.description = $"Makes {MiningMod.ReactorPower.Value:0} W of power from uranium (put it in the slot) while cooled by water from a pipe (socket on the back) " +
                                   "and switched on by signal 1 (signal socket on the back, e.g. from a lever). Power sockets on both sides. Without water or signal it stops.";
            settings.localizedDescription = new UnityEngine.Localization.LocalizedString();
            settings.part = root;
            settings.thumbnailRotation = Quaternion.Euler(0f, 150f, 0f);
            settings.mass = 400f;
            settings.size = new Vector3(2f, 2f, 2f);
            PartFactory.SetCost(settings, (Items.Model.ItemsConstants.IronItemId, 40), (112, 20), (Items.Model.ItemsConstants.GlassItemId, 10));
            return settings;
        }
    }
}
