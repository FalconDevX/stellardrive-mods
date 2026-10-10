using System;
using HarmonyLib;
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
            if (__result.Quantity == 0)
            {
                return;
            }

            byte before = __result.Quantity;
            float multiplier = ResourceTweaksMod.DropMultiplier.Value;
            int amount = (int)Math.Round(before * multiplier);
            __result.Quantity = (byte)Math.Max(1, Math.Min(255, amount));

            if (ResourceTweaksMod.LogDrops.Value)
            {
                Melon<ResourceTweaksMod>.Logger.Msg($"Drop: przedmiot {__result.ItemId} {before} -> {__result.Quantity}");
            }
        }
    }
}
