using System;
using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using Ships.Interface.Model.Parts;
using Ships.Interface.Settings;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mining.Content
{
    internal static class BigChest
    {
        internal const ushort ResearchId = 208;
        internal const ushort ChestPartId = 30;

        internal static readonly (ushort id, string name, int scale, int slots, int columns, int iron)[] Variants =
        {
            (219, "Medium Chest", 2, 30, 15, 30),
            (220, "Large Chest", 3, 60, 20, 70),
        };

        private static readonly Dictionary<ushort, PartSettings> Parts = new Dictionary<ushort, PartSettings>();
        private static readonly HashSet<ushort> Failed = new HashSet<ushort>();

        internal static PartSettings Get(ushort id) => Parts.TryGetValue(id, out PartSettings p) ? p : null;

        internal static PartSettings[] All => Variants.Select(v => Get(v.id)).Where(p => p != null).ToArray();

        internal static bool TryGetLayout(PartSettings settings, out int slots, out int columns)
        {
            slots = columns = 0;
            if (settings == null) return false;
            foreach (var v in Variants)
            {
                if (v.id == settings.id)
                {
                    slots = v.slots;
                    columns = v.columns;
                    return true;
                }
            }
            return false;
        }

        internal static PartSettings GetOrCreate(ushort id, IEnumerable<PartSettings> gameParts)
        {
            if (Parts.TryGetValue(id, out PartSettings existing) || Failed.Contains(id))
            {
                return existing;
            }
            PartSettings chest = PartFactory.Find(gameParts, ChestPartId);
            if (chest?.part == null)
            {
                return null;
            }
            var variant = Variants.First(v => v.id == id);
            try
            {
                PartSettings part = Build(chest, variant);
                Parts[id] = part;
                Melon<MiningMod>.Logger.Msg($"Added part '{part.fullLabel}' (id {id})");
                return part;
            }
            catch (Exception e)
            {
                Failed.Add(id);
                Melon<MiningMod>.Logger.Error($"Could not build chest {variant.name}: {e}");
                return null;
            }
        }

        private static PartSettings Build(PartSettings chest, (ushort id, string name, int scale, int slots, int columns, int iron) v)
        {
            GameObject root = Object.Instantiate(chest.part, PartFactory.Holder);
            root.name = "MiningChest" + v.scale;
            PartFactory.ScaleUniform(root, chest.size, v.scale, "Chest");

            PartSettings settings = Object.Instantiate(chest);
            settings.name = root.name;
            settings.hideFlags = HideFlags.DontUnloadUnusedAsset;
            settings.id = v.id;
            settings.fullLabel = v.name;
            settings.description = $"A {v.scale} x {v.scale} x {v.scale} storage chest with {v.slots} slots (the normal chest has 10).";
            settings.localizedDescription = new UnityEngine.Localization.LocalizedString();
            settings.size = chest.size * v.scale;
            settings.part = root;
            settings.mass = chest.mass * v.scale * v.scale;
            PartFactory.SetCost(settings, (Items.Model.ItemsConstants.IronItemId, v.iron));
            return settings;
        }
    }
}
