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
    // Reaktor jądrowy: stan generatora na drewno (slot na paliwo, poziom produkcji, okno generatora), ale paliwem jest uran,
    // a do pracy potrzebna jest woda do chłodzenia. Gniazda: 0 i 1 = prąd (połączone), 2 = woda. Logika: Server/ReactorServer.
    internal static class Reactor
    {
        internal const ushort PartId = 203;
        internal const byte PowerSocket = 0, WaterSocket = 2;
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
                Melon<MiningMod>.Logger.Msg($"Dodano część '{_part.fullLabel}' (id {PartId})");
            }
            catch (Exception e)
            {
                _failed = true;
                Melon<MiningMod>.Logger.Error($"Nie udało się zbudować reaktora: {e}");
            }
            return _part;
        }

        private static PartSettings Build(PartSettings chest, PartSettings generator, PartSettings distiller)
        {
            GameObject root = Object.Instantiate(chest.part, PartFactory.Holder);
            root.name = "NuclearReactor";
            Mesh body = ModAssets.LoadMesh("reactor_body.bin");
            Mesh core = ModAssets.LoadMesh("reactor_core.bin");
            Texture2D albedo = ModAssets.LoadTexture("reactor_albedo.png");

            Transform visual = root.transform.Find("Visual");
            MeshRenderer look = visual.GetComponentInChildren<MeshRenderer>(true);
            Material material = PartFactory.CreateMaterial(look.sharedMaterial, albedo, "NuclearReactor");
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
            PartFactory.CreateRenderer("Core", visual, core, new Material(material) { name = "NuclearReactorCore" }, lookCopy);
            Object.DestroyImmediate(lookCopy.gameObject);

            PartFactory.SetBounds(root, body.bounds);

            // Okno paliwa otwiera się na przednim panelu (musi wystawać przed obrys reaktora).
            var box = root.transform.Find("Interactions").GetComponentInChildren<BoxCollider>(true);
            box.center = new Vector3(0f, 0.95f, -1.0f);   // model sięga do z = -1.1, strefa do -1.17
            box.size = new Vector3(1.0f, 1.0f, 0.34f);

            SocketObject power = PartFactory.FindSocket(generator, CableType.Power);
            SocketObject fluid = PartFactory.FindSocket(distiller, CableType.Fluid);
            SocketObject p0 = PartFactory.CloneSocket(power, root.transform, 0, new Vector3(-1.05f, 0.42f, 0.55f), 90f);
            SocketObject p1 = PartFactory.CloneSocket(power, root.transform, 1, new Vector3(1.05f, 0.42f, 0.55f), 270f);
            HarmonyLib.Traverse.Create(p0).Field("relayedSockets").SetValue(new[] { p1 });
            HarmonyLib.Traverse.Create(p1).Field("relayedSockets").SetValue(new[] { p0 });
            PartFactory.CloneSocket(fluid, root.transform, WaterSocket, new Vector3(0f, 0.42f, 1.05f), 180f);

            var settings = ScriptableObject.CreateInstance<PartSettings>();
            settings.name = "MiningNuclearReactor";
            settings.hideFlags = HideFlags.DontUnloadUnusedAsset;
            settings.id = PartId;
            settings.fullLabel = "Nuclear Reactor";
            settings.internalStateClass = generator.internalStateClass; // WoodPowerGeneratorState (slot na paliwo)
            settings.snappingStyle = SnappingStyle.PreciseOnFloor;
            settings.description = $"Makes {MiningMod.ReactorPower.Value:0} W of power from uranium (put it in the slot) while cooled by water from a pipe (socket on the back). " +
                                   "Power sockets on both sides. Without water it stops.";
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
