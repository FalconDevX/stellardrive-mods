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
    internal static class Excavator
    {
        internal const ushort PartId = 200;
        internal const ushort ResearchId = 200;
        internal const string Label = "Excavator";
        internal const int SlotCount = 4;
        internal const byte WorkingMarkerSlot = 9;

        internal static bool HasWorkingMarker(Ships.Interface.Model.Parts.State.StorageState state)
        {
            InventorySlot slot = state.GetSlot(WorkingMarkerSlot);
            return slot.HasItem && slot.Quantity == 0;
        }

        internal static InventorySlot WorkingMarker(bool working) =>
            working ? new InventorySlot { HasItem = true, ItemId = ItemsConstants.IronItemId, Quantity = 0 } : default(InventorySlot);
        internal static readonly Vector3 DrillPivot = new Vector3(0.25f, 0.72f, 0.1f);
        internal const float DrillLength = 1.9f;

        private const ushort ChestPartId = 30;
        private const ushort GeneratorPartId = 60;
        private const ushort RequiredResearchId = 20;

        private static PartSettings _part;
        private static ResearchSettings _research;
        private static GameObject _holder;
        private static bool _failed;

        internal static PartSettings Part => _part;

        internal static bool IsExcavator(PartSettings settings) => settings != null && settings.id == PartId;

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
                    Melon<MiningMod>.Logger.Error($"Part id {PartId} is already taken by '{p.fullLabel}' - excavator disabled");
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
                Melon<MiningMod>.Logger.Msg($"Added part '{Label}' (id {PartId})");
            }
            catch (Exception e)
            {
                _failed = true;
                Melon<MiningMod>.Logger.Error($"Could not build the excavator: {e}");
            }
            return _part;
        }

        private static PartSettings Build(PartSettings chest, PartSettings generator)
        {
            _holder = new GameObject("MiningPrefabs");
            _holder.SetActive(false);
            _holder.hideFlags = HideFlags.HideAndDontSave;
            Object.DontDestroyOnLoad(_holder);

            GameObject root = Object.Instantiate(chest.part, _holder.transform);
            root.name = "Excavator";

            Mesh bodyMesh = ModAssets.LoadMesh("excavator_body.bin");
            Mesh drillMesh = ModAssets.LoadMesh("excavator_drill.bin");
            Texture2D albedo = ModAssets.LoadTexture("excavator_albedo.png");

            Transform visual = root.transform.Find("Visual");
            MeshRenderer chestRenderer = visual.GetComponentInChildren<MeshRenderer>(true);
            var material = new Material(chestRenderer.sharedMaterial) { name = "Excavator" };
            material.SetTexture("_Albedo", albedo);
            var look = (chestRenderer.gameObject.layer, chestRenderer.shadowCastingMode, chestRenderer.renderingLayerMask);

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

            var box = root.transform.Find("Interactions").GetComponentInChildren<BoxCollider>(true);
            box.center = new Vector3(-0.05f, 1.05f, -0.6f);
            box.size = new Vector3(1.2f, 0.6f, 0.56f);
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
            settings.internalStateClass = chest.internalStateClass;
            settings.snappingStyle = SnappingStyle.PreciseOnFloor;
            settings.description = $"Mines the ore beneath it when standing on an ore deposit (a patch of ore rocks) and powered. Can be placed directly on the ground. " +
                                   $"Works only with signal 1 (socket with the yellow cap, e.g. a lever). Holds up to {SlotCount} stacks of 100 - open it like a chest to take the ore.";
            settings.localizedDescription = new UnityEngine.Localization.LocalizedString();
            settings.part = root;
            settings.thumbnailRotation = Quaternion.Euler(0f, 150f, 0f);
            settings.mass = 300f;
            settings.size = new Vector3(2f, 2f, 2f);
            Traverse.Create(settings).Field("buildingCost").SetValue(new List<ItemInstance>
            {
                CreateItemInstance(ItemsConstants.IronItemId, 30),
                CreateItemInstance(112, 10),
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

        private static readonly Vector3[] SocketPositions = { new Vector3(0.95f, 0.42f, -0.62f), new Vector3(-0.95f, 0.42f, 0.62f) };
        private static readonly float[] SocketYaw = { 270f, 90f };
        internal const byte SignalSocketId = 2;
        private static readonly Vector3 SignalSocketPosition = new Vector3(-0.95f, 0.42f, -0.62f);

        private static void AddPowerSockets(GameObject root, GameObject generator, Bounds body)
        {
            SocketObject[] source = generator.GetComponentsInChildren<SocketObject>(true).Where(s => s.Type == CableType.Power).OrderBy(s => s.Id).ToArray();
            if (source.Length < 2)
            {
                throw new InvalidOperationException("The generator does not have two power sockets");
            }
            var copies = new SocketObject[2];
            for (int i = 0; i < 2; i++)
            {
                SocketObject copy = Object.Instantiate(source[i].gameObject, root.transform).GetComponent<SocketObject>();
                copy.name = source[i].name;
                copy.transform.localPosition = SocketPositions[i];
                copy.transform.localRotation = Quaternion.Euler(0f, SocketYaw[i], 0f);
                copies[i] = copy;
            }
            var relayed = Traverse.Create(copies[0]).Field("relayedSockets");
            relayed.SetValue(new[] { copies[1] });
            Traverse.Create(copies[1]).Field("relayedSockets").SetValue(new[] { copies[0] });

            SocketObject signalSource = Resources.FindObjectsOfTypeAll<SocketObject>().FirstOrDefault(s => s.Type == CableType.Signal && s.visualObject != null && s.interactive != null);
            if (signalSource == null)
            {
                Melon<MiningMod>.Logger.Warning("No signal socket found to copy - excavator without a signal input");
                return;
            }
            SocketObject signal = Object.Instantiate(signalSource.gameObject, root.transform).GetComponent<SocketObject>();
            signal.name = "SignalSocket";
            signal.transform.localPosition = SignalSocketPosition;
            signal.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            Traverse.Create(signal).Field("id").SetValue(SignalSocketId);
            Traverse.Create(signal).Field("relayedSockets").SetValue(new SocketObject[0]);
        }

        internal static ItemInstance CreateItemInstance(uint itemId, int quantity)
        {
            ItemSettings item = Resources.FindObjectsOfTypeAll<ItemSettings>().FirstOrDefault(i => i.id == itemId);
            var instance = new ItemInstance();
            Traverse.Create(instance).Field("itemSettings").SetValue(item);
            Traverse.Create(instance).Field("quantity").SetValue(quantity);
            return instance;
        }

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
                Melon<MiningMod>.Logger.Msg($"Added research 'Mining' (requires: {(required != null ? required.techName : "nothing")})");
            }
        }
    }
}
