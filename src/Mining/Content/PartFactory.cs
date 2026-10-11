using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Items.Model;
using MelonLoader;
using Research.Interface.Model;
using Research.Interface.Values;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Settings;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mining.Content
{
    // Wspólne narzędzia do budowania nowych części z prefabów gry (koparka, silnik wodorowy, elektrolizer).
    internal static class PartFactory
    {
        private static GameObject _holder;

        // Nieaktywny kontener na prefaby - kopie prefabów gry nie mogą się pojawić w świecie.
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

        // Materiał części gry (Custom/PlanetObject) z naszą teksturą palety.
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

        // Kopia gniazda z innej części. Lokalne +Z gniazda wskazuje do wnętrza części (jak w częściach gry).
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

        internal static ItemInstance CreateItemInstance(uint itemId, int quantity)
        {
            ItemSettings item = Resources.FindObjectsOfTypeAll<ItemSettings>().FirstOrDefault(i => i.id == itemId)
                                ?? (itemId == Uranium.ItemId ? Uranium.GetOrCreate() : null);
            if (item == null)
            {
                return null; // przedmiot niedostępny - pomijamy go w koszcie, zamiast psuć okno badań/budowania
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

    // Nowe badanie dokładane do każdej listy badań gry (klient i serwer korzystają z tego samego zasobu).
    internal sealed class ModResearch
    {
        private readonly ushort _id;
        private readonly string _name;
        private readonly Vector2Int _position;
        private readonly ushort _requiredId;
        private readonly Func<PartSettings[]> _parts;
        private readonly (uint item, int quantity)[] _cost;
        private ResearchSettings _research;
        internal bool AllowNoParts; // badanie odblokowujące funkcję (np. jetpack), a nie część

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
                    continue; // wymagane badanie (np. inne z tego moda) jeszcze nie dodane - spróbujemy później
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
                Melon<MiningMod>.Logger.Msg($"Dodano badanie '{_name}' (wymaga: {required.techName})");
            }
        }
    }
}
