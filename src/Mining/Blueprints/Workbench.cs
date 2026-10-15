using System;
using System.Collections.Generic;
using System.Linq;
using Items.Model;
using MelonLoader;
using Mining.Content;
using Players;
using Ships.Interface.Settings;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mining.Blueprints
{
    internal static class Workbench
    {
        internal const ushort PartId = 233;
        internal const ushort ResearchId = 214;
        internal const ushort ResearchTablePartId = 22;
        private static PartSettings _part;
        private static bool _failed;

        internal static PartSettings Part => _part;

        internal static readonly (uint item, (uint item, int count)[] cost)[] Recipes =
        {
            (0, new[] { (ItemsConstants.IronItemId, 15), (112u, 5) }),
            (1, new[] { (ItemsConstants.IronItemId, 15), (112u, 8) }),
            (2, new[] { (ItemsConstants.IronItemId, 20), (112u, 10), (ItemsConstants.GlassItemId, 5) }),
            (BlueprintToolItem.PaintToolItemId, new[] { (ItemsConstants.IronItemId, 10), (112u, 5), (ItemsConstants.GlassItemId, 5) }),
            (BlueprintToolItem.ItemId, new[] { (ItemsConstants.IronItemId, 20), (112u, 15), (ItemsConstants.GlassItemId, 10) }),
        };

        internal static PartSettings GetOrCreatePart(IEnumerable<PartSettings> gameParts)
        {
            if (_part != null || _failed)
            {
                return _part;
            }
            PartSettings table = PartFactory.Find(gameParts, ResearchTablePartId);
            if (table?.part == null)
            {
                return null;
            }
            try
            {
                GameObject root = Object.Instantiate(table.part, PartFactory.Holder);
                root.name = "ToolWorkbench";
                foreach (MeshRenderer r in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    r.sharedMaterials = r.sharedMaterials.Select(Tint).ToArray();
                }
                PartSettings settings = Object.Instantiate(table);
                settings.name = "MiningToolWorkbench";
                settings.hideFlags = HideFlags.DontUnloadUnusedAsset;
                settings.id = PartId;
                settings.fullLabel = "Tool Workbench";
                settings.description = "Crafts tools from your inventory: Build Tool, Recycler, Mining Laser, Paint Tool and Blueprint Tool. Press F to open it.";
                settings.localizedDescription = new UnityEngine.Localization.LocalizedString();
                settings.part = root;
                PartFactory.SetCost(settings, (ItemsConstants.IronItemId, 20), (112, 10), (ItemsConstants.GlassItemId, 5));
                _part = settings;
                Melon<MiningMod>.Logger.Msg($"Added part '{settings.fullLabel}' (id {PartId})");
            }
            catch (Exception e)
            {
                _failed = true;
                Melon<MiningMod>.Logger.Error($"Could not build the workbench: {e}");
            }
            return _part;
        }

        private static Material Tint(Material source)
        {
            if (source == null) return null;
            var m = new Material(source) { name = source.name + "_Workbench" };
            if (m.HasProperty("_Color")) m.SetColor("_Color", new Color(1f, 0.82f, 0.55f));
            return m;
        }

        internal static bool Craft(uint itemId, out string message)
        {
            var recipe = Recipes.FirstOrDefault(r => r.item == itemId);
            if (recipe.cost == null)
            {
                message = "Unknown recipe";
                return false;
            }
            TrackedPlayerServer player = ServerInventory.LocalPlayer();
            if (player == null)
            {
                message = "Only the host can craft at the workbench";
                return false;
            }
            var cost = recipe.cost.GroupBy(c => c.item).ToDictionary(g => g.Key, g => g.Sum(c => c.count));
            var missing = ServerInventory.Missing(player, cost);
            if (missing.Count > 0)
            {
                message = "Not enough materials: " + ServerInventory.Describe(missing);
                return false;
            }
            int before = ServerInventory.Count(player, itemId);
            player.AddItemToInventory(new ItemDrop { ItemId = itemId, Quantity = 1 });
            if (ServerInventory.Count(player, itemId) <= before)
            {
                message = "No free inventory slot";
                return false;
            }
            ServerInventory.Remove(player, cost);
            message = $"Crafted {ServerInventory.ItemName(itemId)}";
            return true;
        }
    }
}
