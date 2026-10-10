using System;
using Core.Services;
using HarmonyLib;
using Items.Interface.Service;
using Items.Model;
using MelonLoader;
using Planets.Services;

namespace ResourceTweaks.Patches
{
    // Każdy drop z obiektu na planecie (skały, rudy, hotspoty, asteroidy, recykling, gruz po rozbiciu)
    // przechodzi przez PlanetColliders.GetSpawnedObjectItemDrop - tu mnożymy wszystkie surowce.
    // Koszty budowy/badań, wyrzucone przez gracza przedmioty i gruz statków idą inną drogą,
    // więc nie da się w ten sposób duplikować przedmiotów.
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
                Melon<ResourceTweaksMod>.Logger.Msg($"Drop: przedmiot {__result.ItemId} {before} -> {__result.Quantity}");
            }
        }

        // Narzędzia (np. recycler leżący na starcie) i przedmioty ze stackiem 1 zostawiamy w spokoju -
        // inaczej podniesienie jednego narzędzia dawałoby kilka sztuk.
        private static bool IsStackableResource(uint itemId)
        {
            try
            {
                ItemSettings settings = ServiceLocator.GetService<IItemSettingsProvider>()?.GetItemSettingsById(itemId);
                return settings != null && settings.toolSettings == null && settings.maxStackSize > 1;
            }
            catch (Exception e)
            {
                Melon<ResourceTweaksMod>.Logger.Warning($"Nie znaleziono ustawień przedmiotu {itemId}: {e.Message}");
                return false;
            }
        }
    }
}
