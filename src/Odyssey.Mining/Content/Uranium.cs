using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Items.Model;
using MelonLoader;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Odyssey.Content
{
    internal static class Uranium
    {
        internal const uint ItemId = ModItems.Uranium;
        internal static readonly Color Green = new Color(0.35f, 1f, 0.45f);
        private static ItemSettings _item;
        private static readonly Dictionary<Material, Material> GlowCache = new Dictionary<Material, Material>();

        internal static ItemSettings Item => _item;

        internal static ItemSettings[] EnsureIn(ItemSettings[] items)
        {
            if (items == null || items.Any(i => i != null && i.id == ItemId))
            {
                return items;
            }
            ItemSettings iron = items.FirstOrDefault(i => i != null && i.id == ItemsConstants.IronItemId);
            if (iron == null)
            {
                return items;
            }
            Create(iron);
            return items.Concat(new[] { _item }).ToArray();
        }

        internal static ItemSettings GetOrCreate()
        {
            if (_item == null)
            {
                ItemSettings iron = Resources.FindObjectsOfTypeAll<ItemSettings>().FirstOrDefault(i => i != null && i.id == ItemsConstants.IronItemId);
                if (iron != null)
                {
                    Create(iron);
                }
            }
            return _item;
        }

        private static void Create(ItemSettings iron)
        {
            if (_item == null)
            {
                _item = Object.Instantiate(iron);
                _item.name = "MiningUranium";
                _item.hideFlags = HideFlags.DontUnloadUnusedAsset;
                _item.id = ItemId;
                _item.itemName = "Uranium";
                _item.maxStackSize = 100;
                _item.mass = 3f;
                _item.icon = CreateIcon();
                if (iron.itemObject != null)
                {
                    GameObject model = Object.Instantiate(iron.itemObject, PartFactory.Holder);
                    model.name = "UraniumItem";
                    foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
                    {
                        r.sharedMaterials = r.sharedMaterials.Select(Glow).ToArray();
                    }
                    _item.itemObject = model;
                }
                Melon<MiningMod>.Logger.Msg($"Added item 'Uranium' (id {ItemId})");
            }
        }

        internal static Material Glow(Material source)
        {
            if (source == null)
            {
                return null;
            }
            if (GlowCache.TryGetValue(source, out Material cached) && cached != null)
            {
                return cached;
            }
            var m = new Material(source) { name = source.name + "_Uranium" };
            if (m.HasProperty("_Color")) m.SetColor("_Color", Green);
            if (m.HasProperty("_EmissionColor"))
            {
                m.SetColor("_EmissionColor", Green * 0.7f);
                m.EnableKeyword("_EMISSION");
            }
            GlowCache[source] = m;
            return m;
        }

        private static Texture2D CreateIcon()
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "uraniumIcon" };
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(size / 2f, size / 2f)) / (size / 2f);
                    px[y * size + x] = d < 0.75f ? Color.Lerp(Green, new Color(0.1f, 0.45f, 0.15f), d) : new Color(0, 0, 0, 0);
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }
    }

    [HarmonyPatch(typeof(ItemSettingsList), "ResetDict")]
    internal static class UraniumItemPatch
    {
        private static void Prefix(ref ItemSettings[] ___items)
        {
            ___items = Uranium.EnsureIn(___items);
        }
    }
}
