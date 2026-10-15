using System;
using System.Collections.Generic;
using HarmonyLib;
using Items.Model;
using Items.Services;
using MelonLoader;
using Rendering.Services;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Mining.Patches
{
    [HarmonyPatch(typeof(ItemThumbnailTextureCache), nameof(ItemThumbnailTextureCache.GetThumbnail))]
    internal static class ToolIconPatch
    {
        private static readonly HashSet<uint> Angled = new HashSet<uint> { Blueprints.BlueprintToolItem.PaintToolItemId, Blueprints.BlueprintToolItem.ItemId };

        private static bool Prefix(ItemThumbnailTextureCache __instance, uint itemId, Dictionary<uint, Texture2D> ____thumbnails, ObjectThumbnailRenderer ____thumbnailRenderer, ItemSettingsList ___items, ref Texture2D __result)
        {
            if (!Angled.Contains(itemId) || ____thumbnails == null || ____thumbnailRenderer == null)
            {
                return true;
            }
            if (____thumbnails.TryGetValue(itemId, out Texture2D cached))
            {
                __result = cached;
                return false;
            }
            ItemSettings item = ___items != null ? ___items.GetItemSettingsById(itemId) : null;
            if (item?.toolSettings?.prefab == null)
            {
                return true;
            }
            try
            {
                var resolution = Traverse.Create(__instance).Field("_thumbnailsResolutionProvider").Method("GetItemSmallThumbnailResolution").GetValue<int>();
                Texture2D texture = Traverse.Create(typeof(ItemThumbnailTextureCache)).Method("SetupThumbnailTexture", resolution).GetValue<Texture2D>();
                ____thumbnails[itemId] = texture;
                GameObject view = Object.Instantiate(item.toolSettings.prefab);
                view.layer = LayerMask.NameToLayer("PartThumbnailRender");
                Quaternion rotation = Quaternion.AngleAxis(32f, Vector3.forward) * Quaternion.AngleAxis(-28f, Vector3.up) * Quaternion.LookRotation(Vector3.right, Vector3.up);
                ____thumbnailRenderer.Render(view, rotation, texture);
                __result = texture;
                return false;
            }
            catch (Exception e)
            {
                Melon<MiningMod>.Logger.Warning($"Tool icon {itemId}: {e.Message}");
                ____thumbnails.Remove(itemId);
                return true;
            }
        }
    }
}
