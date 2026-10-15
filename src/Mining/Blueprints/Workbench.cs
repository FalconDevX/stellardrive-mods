using System;
using System.Collections.Generic;
using System.Linq;
using Items.Model;
using MelonLoader;
using Mining.Content;
using Players;
using Ships.Interface.Settings;
using Ships.Parts.Common;
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
                Mesh body = ModAssets.LoadMesh("workbench_body.bin");
                Transform visual = root.transform.Find("Visuals") ?? root.transform.Find("Visual") ?? throw new InvalidOperationException("Research table has no visuals object");
                MeshRenderer look = visual.GetComponentInChildren<MeshRenderer>(true);
                Material material = PartFactory.CreateMaterial(look.sharedMaterial, ModAssets.LoadTexture("workbench_albedo.jpg"), "ToolWorkbench");
                var lookCopy = new GameObject("look").AddComponent<MeshRenderer>();
                lookCopy.gameObject.layer = look.gameObject.layer;
                lookCopy.shadowCastingMode = look.shadowCastingMode;
                lookCopy.renderingLayerMask = look.renderingLayerMask;
                foreach (Transform child in visual.Cast<Transform>().ToArray())
                {
                    Object.DestroyImmediate(child.gameObject);
                }
                foreach (MeshRenderer r in visual.GetComponents<MeshRenderer>()) Object.DestroyImmediate(r);
                foreach (MeshFilter f in visual.GetComponents<MeshFilter>()) Object.DestroyImmediate(f);
                DefaultShipPartVisuals oldVisuals = visual.GetComponent<DefaultShipPartVisuals>();
                if (oldVisuals != null)
                {
                    Object.DestroyImmediate(oldVisuals);
                    visual.gameObject.AddComponent<DefaultShipPartVisuals>();
                }
                PartFactory.CreateRenderer("Body", visual, body, material, lookCopy).transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
                Object.DestroyImmediate(lookCopy.gameObject);
                Bounds rotated = new Bounds(Quaternion.Euler(0f, -90f, 0f) * body.bounds.center, new Vector3(body.bounds.size.z, body.bounds.size.y, body.bounds.size.x));
                PartFactory.SetBounds(root, rotated);
                Transform interactions = root.transform.Find("Interactions");
                if (interactions != null)
                {
                    foreach (PartInteraction interaction in interactions.GetComponentsInChildren<PartInteraction>(true))
                    {
                        HarmonyLib.Traverse.Create(interaction).Field("detectionDistance").SetValue(3f);
                    }
                    foreach (BoxCollider box in interactions.GetComponentsInChildren<BoxCollider>(true))
                    {
                        box.center = box.transform.InverseTransformPoint(root.transform.TransformPoint(rotated.center));
                        box.size = rotated.size + Vector3.one * 0.02f;
                    }
                }
                PartSettings settings = Object.Instantiate(table);
                settings.name = "MiningToolWorkbench";
                settings.hideFlags = HideFlags.DontUnloadUnusedAsset;
                settings.id = PartId;
                settings.fullLabel = "Tool Workbench";
                settings.description = "Crafts tools from your inventory: Build Tool, Recycler, Mining Laser, Paint Tool and Blueprint Tool. Press F to open it.";
                settings.localizedDescription = new UnityEngine.Localization.LocalizedString();
                settings.part = root;
                settings.size = new Vector3(2f, 2f, 2f);
                settings.mass = 160f;
                settings.thumbnailRotation = Quaternion.Euler(0f, 150f, 0f);
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
