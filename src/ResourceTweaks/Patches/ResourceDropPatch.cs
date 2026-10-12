using System;
using Core.Services;
using HarmonyLib;
using Items.Interface.Service;
using Items.Model;
using MelonLoader;
using Planets.Services;

namespace ResourceTweaks.Patches
{
    [HarmonyPatch(typeof(PlanetColliders), nameof(PlanetColliders.GetSpawnedObjectItemDrop))]
    internal static class ResourceDropPatch
    {
        private static void Postfix(ref ItemDrop __result)
        {
            if (__result.Quantity == 0 || !IsStackableResource(__result.ItemId))
            {
                return;
            }

            byte before = __result.Quantity;
            float multiplier = __result.ItemId == ItemsConstants.IronItemId
                ? ResourceTweaksMod.IronDropMultiplier.Value
                : ResourceTweaksMod.DropMultiplier.Value;
            int amount = (int)Math.Round(before * multiplier);
            __result.Quantity = (byte)Math.Max(1, Math.Min(255, amount));

            if (ResourceTweaksMod.LogDrops.Value)
            {
                Melon<ResourceTweaksMod>.Logger.Msg($"Drop: item {__result.ItemId} {before} -> {__result.Quantity}");
            }
        }

        private static bool IsStackableResource(uint itemId)
        {
            try
            {
                ItemSettings settings = ServiceLocator.GetService<IItemSettingsProvider>()?.GetItemSettingsById(itemId);
                return settings != null && settings.toolSettings == null && settings.maxStackSize > 1;
            }
            catch (Exception e)
            {
                Melon<ResourceTweaksMod>.Logger.Warning($"No settings found for item {itemId}: {e.Message}");
                return false;
            }
        }
    }
}
