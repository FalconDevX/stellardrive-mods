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
    // a do pracy potrzebna jest woda do chłodzenia i sygnał włączenia. Gniazda: 0 i 1 = prąd (połączone), 2 = woda,
    // 3 = sygnał (1 = praca). Logika: Server/ReactorServer. Model z GLB (AssetSource/build_reactor_glb.py), rdzenie w tubach
    // i wirnik na kopule obracają się, a szkło i ekrany świecą przy pracy (Content/ReactorVisuals).
    internal static class Reactor
    {
        internal const ushort PartId = 203;
        internal const byte PowerSocket = 0, WaterSocket = 2, SignalSocket = 3;
        internal static readonly Vector3[] CorePositions = { new Vector3(0f, 0.64f, -0.45f), new Vector3(0f, 0.64f, 0.45f) }; // osie szklanych tub
        internal static bool HasSignalSocket; // bez gniazda (gra bez gniazd sygnałowych) reaktor nie czeka na sygnał
        internal static readonly Vector3 RotorPosition = new Vector3(0f, 1.525f, 0f); // środek kopuły
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
            Mesh rotor = ModAssets.LoadMesh("reactor_rotor.bin");

            Transform visual = root.transform.Find("Visual");
            MeshRenderer look = visual.GetComponentInChildren<MeshRenderer>(true);
            // korpus: kolor z GLB, świecenie tylko w miejscach z maski (szkło tub, ekrany)
            Material material = PartFactory.CreateMaterial(look.sharedMaterial, ModAssets.LoadTexture("reactor_albedo.jpg"), "NuclearReactor");
            material.SetTexture("_EmissionMap", ModAssets.LoadTexture("reactor_emission.png"));
            material.SetColor("_EmissionColor", Color.black);
            material.EnableKeyword("_EMISSION");
            // rdzenie i wirnik: paleta low-poly; rdzeń świeci własnymi kolorami (mapa emisji = paleta)
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
            Object.DestroyImmediate(lookCopy.gameObject);

            PartFactory.SetBounds(root, body.bounds);

            // Okno paliwa otwiera się na przednim panelu (musi wystawać przed obrys reaktora).
            var box = root.transform.Find("Interactions").GetComponentInChildren<BoxCollider>(true);
            box.center = new Vector3(0f, 0.75f, -1.0f);   // model sięga do z = -0.944, strefa do -1.15
            box.size = new Vector3(1.0f, 0.9f, 0.3f);

            SocketObject power = PartFactory.FindSocket(generator, CableType.Power);
            SocketObject fluid = PartFactory.FindSocket(distiller, CableType.Fluid);
            SocketObject p0 = PartFactory.CloneSocket(power, root.transform, 0, new Vector3(-1.05f, 0.42f, 0.55f), 90f);
            SocketObject p1 = PartFactory.CloneSocket(power, root.transform, 1, new Vector3(1.05f, 0.42f, 0.55f), 270f);
            HarmonyLib.Traverse.Create(p0).Field("relayedSockets").SetValue(new[] { p1 });
            HarmonyLib.Traverse.Create(p1).Field("relayedSockets").SetValue(new[] { p0 });
            PartFactory.CloneSocket(fluid, root.transform, WaterSocket, new Vector3(0f, 0.42f, 1.05f), 180f);
            SocketObject signalSource = Resources.FindObjectsOfTypeAll<SocketObject>().FirstOrDefault(s => s.Type == CableType.Signal && s.visualObject != null && s.interactive != null);
            HasSignalSocket = signalSource != null;
            if (signalSource != null)
            {
                PartFactory.CloneSocket(signalSource, root.transform, SignalSocket, new Vector3(-0.45f, 0.42f, 1.05f), 180f);
            }
            else
            {
                Melon<MiningMod>.Logger.Warning("Nie znaleziono gniazda sygnałowego do skopiowania - reaktor bez wejścia sygnału (działa bez niego)");
            }

            var settings = ScriptableObject.CreateInstance<PartSettings>();
            settings.name = "MiningNuclearReactor";
            settings.hideFlags = HideFlags.DontUnloadUnusedAsset;
            settings.id = PartId;
            settings.fullLabel = "Nuclear Reactor";
            settings.internalStateClass = generator.internalStateClass; // WoodPowerGeneratorState (slot na paliwo)
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
