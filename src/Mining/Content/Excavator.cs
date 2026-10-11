using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Items.Model;
using MelonLoader;
using Research.Interface.Model;
using Research.Interface.Values;
using Ships.Interface.Model;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Settings;
using Ships.Parts.Common;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mining.Content
{
    // Koparka to nowa część statku zbudowana na bazie skrzyni (Storage Chest):
    //  - ten sam stan (StorageState), więc zapis gry, sieć i okno ekwipunku działają bez zmian,
    //  - własny wygląd (model z GLB) i wiertło, które kręci się podczas pracy,
    //  - gniazda prądu skopiowane z generatora na drewno,
    //  - nowe badanie "Mining" w drzewie badań.
    internal static class Excavator
    {
        internal const ushort PartId = 200;
        internal const ushort ResearchId = 200;
        internal const string Label = "Excavator";
        internal const int SlotCount = 4;
        internal static readonly Vector3 DrillPivot = new Vector3(0.1955f, 0.36f, 0.322f);
        internal const float DrillLength = 0.68f; // od osi mocowania do czubka (m)

        private const ushort ChestPartId = 30;
        private const ushort GeneratorPartId = 60;
        private const ushort RequiredResearchId = 20; // Chemical Propulsion (3,4) - koparka stoi obok w tym samym rzędzie

        private static PartSettings _part;
        private static ResearchSettings _research;
        private static GameObject _holder;
        private static bool _failed;

        internal static PartSettings Part => _part;

        internal static bool IsExcavator(PartSettings settings) => settings != null && settings.id == PartId;

        // Tworzy część przy pierwszym wywołaniu, gdy skrzynia i generator z gry są już wczytane.
        internal static PartSettings GetOrCreatePart(IEnumerable<PartSettings> gameParts)
        {
            if (_part != null || _failed)
            {
                return _part;
            }
            PartSettings chest = null, generator = null;
            foreach (PartSettings p in gameParts)
            {
                if (p == null) continue;
                if (p.id == ChestPartId) chest = p;
                if (p.id == GeneratorPartId) generator = p;
                if (p.id == PartId && p.name != "MiningExcavator")
                {
                    Melon<MiningMod>.Logger.Error($"Id części {PartId} jest już zajęte przez '{p.fullLabel}' - koparka wyłączona");
                    _failed = true;
                    return null;
                }
            }
            if (chest?.part == null || generator?.part == null)
            {
                return null;
            }
            try
            {
                _part = Build(chest, generator);
                Melon<MiningMod>.Logger.Msg($"Dodano część '{Label}' (id {PartId})");
            }
            catch (Exception e)
            {
                _failed = true;
                Melon<MiningMod>.Logger.Error($"Nie udało się zbudować koparki: {e}");
            }
            return _part;
        }

        private static PartSettings Build(PartSettings chest, PartSettings generator)
        {
            _holder = new GameObject("MiningPrefabs");
            _holder.SetActive(false);
            _holder.hideFlags = HideFlags.HideAndDontSave;
            Object.DontDestroyOnLoad(_holder);

            // Prefab: kopia skrzyni (Bounds, Visual, Interactions) z podmienionym wyglądem.
            GameObject root = Object.Instantiate(chest.part, _holder.transform);
            root.name = "Excavator";

            Mesh bodyMesh = ModAssets.LoadMesh("excavator_body.bin");
            Mesh drillMesh = ModAssets.LoadMesh("excavator_drill.bin");
            Texture2D albedo = ModAssets.LoadTexture("excavator_albedo.jpg");

            Transform visual = root.transform.Find("Visual");
            MeshRenderer chestRenderer = visual.GetComponentInChildren<MeshRenderer>(true);
            var material = new Material(chestRenderer.sharedMaterial) { name = "Excavator" };
            material.SetTexture("_Albedo", albedo);
            material.SetFloat("_CullMode", 0f); // model z Meshy ma miejscami odwrócone ścianki
            var look = (chestRenderer.gameObject.layer, chestRenderer.shadowCastingMode, chestRenderer.renderingLayerMask);

            // Usuwamy model skrzyni i wstawiamy korpus + wiertło (na osobnym obiekcie, żeby się kręciło).
            foreach (Transform child in visual.Cast<Transform>().ToArray())
            {
                Object.DestroyImmediate(child.gameObject);
            }
            Object.DestroyImmediate(visual.GetComponent<DefaultShipPartVisuals>());
            visual.gameObject.AddComponent<ExcavatorVisuals>();

            CreateRenderer("Body", visual, bodyMesh, material, look);
            var pivot = new GameObject("DrillPivot").transform;
            pivot.SetParent(visual, false);
            pivot.localPosition = DrillPivot;
            CreateRenderer("Drill", pivot, drillMesh, material, look);

            Bounds b = bodyMesh.bounds;
            var bounds = root.GetComponent<ShipPartBounds>();
            bounds.center = b.center;
            bounds.bounds = b.size;

            // Interakcja (otwieranie jak skrzynię) na całym korpusie.
            var box = root.transform.Find("Interactions").GetComponentInChildren<BoxCollider>(true);
            box.center = b.center;
            box.size = b.size;
            var interaction = box.GetComponent<PartInteraction>();
            if (interaction != null)
            {
                Traverse.Create(interaction).Field("detectionDistance").SetValue(2.5f);
            }

            AddPowerSockets(root, generator.part, b);

            var settings = ScriptableObject.CreateInstance<PartSettings>();
            settings.name = "MiningExcavator";
            settings.hideFlags = HideFlags.DontUnloadUnusedAsset;
            settings.id = PartId;
            settings.fullLabel = Label;
            settings.internalStateClass = chest.internalStateClass; // StorageState
            settings.snappingStyle = SnappingStyle.PreciseOnFloor;
            settings.description = $"Wydobywa surowiec spod siebie, gdy stoi na złożu (płaskie pole kamieni z rudą) i ma prąd. " +
                                   $"Zbiera do {SlotCount} stosów po 100 - otwórz jak skrzynię, żeby wyjąć urobek.";
            settings.localizedDescription = new UnityEngine.Localization.LocalizedString();
            settings.part = root;
            settings.thumbnailRotation = Quaternion.Euler(0f, 150f, 0f);
            settings.mass = 300f;
            settings.size = new Vector3(2f, 2f, 2f);
            Traverse.Create(settings).Field("buildingCost").SetValue(new List<ItemInstance>
            {
                CreateItemInstance(ItemsConstants.IronItemId, 30),
                CreateItemInstance(112, 10), // miedź
            });
            return settings;
        }

        private static void CreateRenderer(string name, Transform parent, Mesh mesh, Material material,
            (int layer, UnityEngine.Rendering.ShadowCastingMode shadows, uint renderingLayers) look)
        {
            var go = new GameObject(name);
            go.layer = look.layer;
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = look.shadows;
            r.renderingLayerMask = look.renderingLayers;
        }

        // Dwa gniazda prądu (połączone ze sobą, jak w generatorze) po bokach korpusu, przy podłodze.
        private static void AddPowerSockets(GameObject root, GameObject generator, Bounds body)
        {
            SocketObject[] source = generator.GetComponentsInChildren<SocketObject>(true).Where(s => s.Type == CableType.Power).OrderBy(s => s.Id).ToArray();
            if (source.Length < 2)
            {
                throw new InvalidOperationException("Generator nie ma dwóch gniazd prądu");
            }
            var copies = new SocketObject[2];
            for (int i = 0; i < 2; i++)
            {
                SocketObject copy = Object.Instantiate(source[i].gameObject, root.transform).GetComponent<SocketObject>();
                copy.name = source[i].name;
                float side = i == 0 ? -1f : 1f;
                copy.transform.localPosition = new Vector3(side * 0.62f, 0.07f, body.center.z - 0.15f);
                copy.transform.localRotation = Quaternion.Euler(0f, i == 0 ? 90f : 270f, 0f);
                copies[i] = copy;
            }
            var relayed = Traverse.Create(copies[0]).Field("relayedSockets");
            relayed.SetValue(new[] { copies[1] });
            Traverse.Create(copies[1]).Field("relayedSockets").SetValue(new[] { copies[0] });
        }

        internal static ItemInstance CreateItemInstance(uint itemId, int quantity)
        {
            ItemSettings item = Resources.FindObjectsOfTypeAll<ItemSettings>().FirstOrDefault(i => i.id == itemId);
            var instance = new ItemInstance();
            Traverse.Create(instance).Field("itemSettings").SetValue(item);
            Traverse.Create(instance).Field("quantity").SetValue(quantity);
            return instance;
        }

        // Badanie "Mining" dokładamy do każdej listy badań gry (klient i serwer korzystają z tego samego zasobu).
        internal static void EnsureResearch()
        {
            if (_part == null)
            {
                return;
            }
            foreach (ResearchSettingsList list in Resources.FindObjectsOfTypeAll<ResearchSettingsList>())
            {
                if (list?.list == null || list.list.Any(r => r != null && r.id == ResearchId))
                {
                    continue;
                }
                ResearchSettings required = list.list.FirstOrDefault(r => r != null && r.id == RequiredResearchId);
                if (_research == null)
                {
                    _research = ScriptableObject.CreateInstance<ResearchSettings>();
                    _research.name = "MiningResearch";
                    _research.hideFlags = HideFlags.DontUnloadUnusedAsset;
                    _research.id = ResearchId;
                    _research.techName = "Mining";
                    _research.unlockedParts = new[] { _part };
                    _research.unlockedFeatures = new ResearchUnlockableFeature[0];
                    _research.unlockedGuideEntriesIds = new uint[0];
                    _research.resourceCost = new[]
                    {
                        CreateItemInstance(ItemsConstants.IronItemId, 20),
                        CreateItemInstance(112, 10),
                    };
                    _research.positionInTechTree = new Vector2Int(4, 4);
                    _research.availableInDemo = true;
                }
                _research.requirements = required != null ? new[] { required } : new ResearchSettings[0];
                list.list.Add(_research);
                Melon<MiningMod>.Logger.Msg($"Dodano badanie 'Mining' (wymaga: {(required != null ? required.techName : "nic")})");
            }
        }
    }
}
