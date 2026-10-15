using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Items.Model;
using MelonLoader;
using Players.Interface.Model;
using Research.Interface.Model;
using Research.Interface.Values;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Model.Parts.StateTypes;
using Ships.Interface.Settings;
using Ships.Parts.Common;
using Ships.Parts.Common.Gauge;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mining.Content
{
    internal static class PartFactory
    {
        private static GameObject _holder;

        internal static Transform Holder
        {
            get
            {
                if (_holder == null)
                {
                    _holder = new GameObject("MiningPrefabs");
                    _holder.SetActive(false);
                    _holder.hideFlags = HideFlags.HideAndDontSave;
                    Object.DontDestroyOnLoad(_holder);
                }
                return _holder.transform;
            }
        }

        internal static PartSettings Find(IEnumerable<PartSettings> parts, ushort id) => parts.FirstOrDefault(p => p != null && p.id == id);

        internal static Material CreateMaterial(Material template, Texture2D albedo, string name)
        {
            var material = new Material(template) { name = name };
            material.SetTexture("_Albedo", albedo);
            if (material.HasProperty("_Normal")) material.SetTexture("_Normal", null);
            if (material.HasProperty("_RoughnessMap")) material.SetTexture("_RoughnessMap", null);
            return material;
        }

        internal static GameObject CreateRenderer(string name, Transform parent, Mesh mesh, Material material, MeshRenderer look)
        {
            var go = new GameObject(name) { layer = look.gameObject.layer };
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = look.shadowCastingMode;
            r.renderingLayerMask = look.renderingLayerMask;
            return go;
        }

        internal static SocketObject CloneSocket(SocketObject source, Transform root, byte id, Vector3 position, float yaw)
        {
            SocketObject copy = Object.Instantiate(source.gameObject, root).GetComponent<SocketObject>();
            copy.name = $"{source.Type}Socket{id}";
            copy.transform.localPosition = position;
            copy.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Traverse.Create(copy).Field("id").SetValue(id);
            Traverse.Create(copy).Field("relayedSockets").SetValue(new SocketObject[0]);
            return copy;
        }

        internal static SocketObject FindSocket(PartSettings part, CableType type) =>
            part?.part == null ? null : part.part.GetComponentsInChildren<SocketObject>(true).Where(s => s.Type == type).OrderBy(s => s.Id).FirstOrDefault();

        internal static void SetBounds(GameObject root, Bounds b)
        {
            var bounds = root.GetComponent<ShipPartBounds>();
            bounds.center = b.center;
            bounds.bounds = b.size;
        }

        internal static void ScaleUniform(GameObject root, Vector3 size, float scale, string label)
        {
            var bounds = root.GetComponent<ShipPartBounds>();
            Vector3 c0 = bounds.center, b0 = bounds.bounds;
            Vector3 b1 = b0 * scale;
            var cell = new Vector3(0f, size.y * 0.5f, 0f);
            Vector3 block = cell + (size * scale - size) * 0.5f;
            Vector3 c1 = block + (c0 - cell) * scale;
            bounds.center = c1;
            bounds.bounds = b1;

            Vector3 MapScaled(Vector3 p) => c1 + (p - c0) * scale;

            Vector3 MapKeepSize(Vector3 p)
            {
                var r = new Vector3();
                for (int k = 0; k < 3; k++)
                {
                    float d = p[k] - c0[k], h0 = b0[k] * 0.5f, h1 = b1[k] * 0.5f;
                    r[k] = Mathf.Abs(d) > h0 - 0.15f ? c1[k] + Mathf.Sign(d) * (h1 - (h0 - Mathf.Abs(d))) : c1[k] + d * scale;
                }
                return r;
            }

            var s = Vector3.one * scale;
            var originals = root.GetComponentsInChildren<SocketObject>(true).Select(x => x.transform)
                .Select(t => (t, pos: root.transform.InverseTransformPoint(t.position))).ToList();
            foreach (Transform unit in root.transform.Cast<Transform>().ToArray())
            {
                if (unit.GetComponent<SocketObject>() != null)
                {
                    unit.localPosition = MapScaled(unit.localPosition);
                    continue;
                }
                var sockets = unit.GetComponentsInChildren<SocketObject>(true).Select(x => x.transform)
                    .Select(t => (t, pos: root.transform.InverseTransformPoint(t.position))).ToList();
                var gauges = unit.GetComponentsInChildren<Gauge>(true).Select(x => x.transform)
                    .Select(t => (t, pos: root.transform.InverseTransformPoint(t.position))).ToList();

                Vector3 offset = c1 - Vector3.Scale(s, c0) + Vector3.Scale(s - Vector3.one, unit.localPosition);
                var scaled = new GameObject("Scaled").transform;
                scaled.SetParent(unit, false);
                foreach (Transform child in unit.Cast<Transform>().Where(t => t != scaled).ToArray())
                {
                    child.SetParent(scaled, false);
                }
                scaled.localPosition = offset;
                scaled.localScale = s;
                foreach (BoxCollider box in unit.GetComponents<BoxCollider>())
                {
                    box.center = offset + Vector3.Scale(s, box.center);
                    box.size = Vector3.Scale(s, box.size);
                }
                foreach (var (t, pos) in sockets)
                {
                    t.position = root.transform.TransformPoint(MapScaled(pos));
                    t.localScale /= scale;
                }
                foreach (var (t, pos) in gauges)
                {
                    t.position = root.transform.TransformPoint(MapKeepSize(pos));
                    t.localScale /= scale;
                }
                if (unit.GetComponents<Collider>().Any(c => !(c is BoxCollider)))
                {
                    Melon<MiningMod>.Logger.Warning($"{label}: {unit.name} has a collider other than BoxCollider - it keeps the small size");
                }
            }
            foreach (var (t, pos) in originals)
            {
                Vector3 p = root.transform.InverseTransformPoint(t.position);
                Vector3 snapped = SnapToGrid(p, pos, c1, b1, size, scale);
                t.position = root.transform.TransformPoint(snapped);
                Melon<MiningMod>.Logger.Msg($"{label}: socket {t.name} {pos} -> {snapped}");
            }
        }

        private static Vector3 SnapToGrid(Vector3 p, Vector3 original, Vector3 center, Vector3 bounds, Vector3 size, float scale)
        {
            int normal = 0;
            float best = -1f;
            for (int k = 0; k < 3; k++)
            {
                float edge = bounds[k] > 0.001f ? Mathf.Abs(p[k] - center[k]) / (bounds[k] * 0.5f) : 0f;
                if (edge > best)
                {
                    best = edge;
                    normal = k;
                }
            }
            Vector3 r = p;
            for (int k = 0; k < 3; k++)
            {
                if (k == normal)
                {
                    continue;
                }
                if (k == 1)
                {
                    r.y = Mathf.Min(p.y, original.y);
                    continue;
                }
                float low = -(Mathf.Round(size[k]) - 1f) * 0.5f;
                float high = low + Mathf.Round(size[k] * scale) - 1f;
                r[k] = Mathf.Clamp(Mathf.Round(p[k] - low) + low, low, high);
            }
            return r;
        }

        internal static Transform RemoveInteract(GameObject root)
        {
            Transform interactions = root.transform.Find("Interactions");
            foreach (DefaultShipPartInteractions i in interactions.GetComponentsInChildren<DefaultShipPartInteractions>(true))
            {
                GameObject go = i.gameObject;
                Object.DestroyImmediate(i);
                go.AddComponent<DefaultShipPartInteractions>();
            }
            foreach (PartInteraction original in interactions.GetComponentsInChildren<PartInteraction>(true).ToArray())
            {
                GameObject go = original.gameObject;
                var fields = Traverse.Create(original);
                bool active = fields.Field<bool>("isActive").Value;
                float distance = fields.Field<float>("detectionDistance").Value;
                List<PartInteractionConfig> configs = fields.Field<List<PartInteractionConfig>>("interactions").Value;
                Object.DestroyImmediate(original);
                var copy = Traverse.Create(go.AddComponent<NoInteractPartInteraction>());
                copy.Field("isActive").SetValue(active);
                copy.Field("detectionDistance").SetValue(distance);
                copy.Field("interactions").SetValue((configs ?? new List<PartInteractionConfig>()).Where(c => c.type != InteractionType.Interact).ToList());
            }
            return interactions;
        }

        internal static ItemInstance CreateItemInstance(uint itemId, int quantity)
        {
            ItemSettings item = Resources.FindObjectsOfTypeAll<ItemSettings>().FirstOrDefault(i => i.id == itemId)
                                ?? (itemId == Uranium.ItemId ? Uranium.GetOrCreate() : null);
            if (item == null)
            {
                return null;
            }
            var instance = new ItemInstance();
            Traverse.Create(instance).Field("itemSettings").SetValue(item);
            Traverse.Create(instance).Field("quantity").SetValue(quantity);
            return instance;
        }

        internal static void SetCost(PartSettings settings, params (uint item, int quantity)[] cost)
        {
            Traverse.Create(settings).Field("buildingCost").SetValue(cost.Select(c => CreateItemInstance(c.item, c.quantity)).Where(i => i != null).ToList());
        }
    }

    internal class NoInteractPartInteraction : PartInteraction
    {
        public override IReadOnlyCollection<PartInteractionConfig> Interactions => base.Interactions.Where(c => c.type != InteractionType.Interact).ToList();
    }

    internal sealed class ModResearch
    {
        private readonly ushort _id;
        private readonly string _name;
        private readonly Vector2Int _position;
        private readonly ushort _requiredId;
        private readonly Func<PartSettings[]> _parts;
        private readonly (uint item, int quantity)[] _cost;
        private ResearchSettings _research;
        internal bool AllowNoParts;

        internal ModResearch(ushort id, string name, Vector2Int position, ushort requiredId, Func<PartSettings[]> parts, params (uint, int)[] cost)
        {
            _id = id;
            _name = name;
            _position = position;
            _requiredId = requiredId;
            _parts = parts;
            _cost = cost;
        }

        internal void Ensure()
        {
            PartSettings[] parts = _parts().Where(p => p != null).ToArray();
            if (parts.Length == 0 && !AllowNoParts)
            {
                return;
            }
            foreach (ResearchSettingsList list in Resources.FindObjectsOfTypeAll<ResearchSettingsList>())
            {
                if (list?.list == null || list.list.Any(r => r != null && r.id == _id))
                {
                    continue;
                }
                ResearchSettings required = list.list.FirstOrDefault(r => r != null && r.id == _requiredId);
                if (required == null)
                {
                    continue;
                }
                if (_research == null)
                {
                    _research = ScriptableObject.CreateInstance<ResearchSettings>();
                    _research.name = _name.Replace(" ", "") + "Research";
                    _research.hideFlags = HideFlags.DontUnloadUnusedAsset;
                    _research.id = _id;
                    _research.techName = _name;
                    _research.unlockedFeatures = new ResearchUnlockableFeature[0];
                    _research.unlockedGuideEntriesIds = new uint[0];
                    _research.resourceCost = _cost.Select(c => PartFactory.CreateItemInstance(c.item, c.quantity)).Where(i => i != null).ToArray();
                    _research.positionInTechTree = _position;
                    _research.availableInDemo = true;
                }
                _research.unlockedParts = parts;
                _research.requirements = new[] { required };
                list.list.Add(_research);
                Melon<MiningMod>.Logger.Msg($"Added research '{_name}' (requires: {required.techName})");
            }
        }
    }
}
