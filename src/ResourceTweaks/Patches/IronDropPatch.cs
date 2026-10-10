using System;
using HarmonyLib;
using Items.Model;
using Planets.Objects;

namespace ResourceTweaks.Patches
{
    // Patchujemy tylko źródła "z natury": wydobycie (MinablePlanetObject) i recykling obiektów
    // na planecie (RecyclablePlanetObject). Gruz po rozbiciu obiektu też bierze drop stąd.
    // ItemInstance.ItemDrop zostaje nietknięte - z niego liczone są koszty budowy i badań,
    // a przedmioty wyrzucone przez gracza i gruz statków mają osobną ścieżkę (brak duplikacji).
    internal static class IronDrop
    {
        internal static void Apply(ref ItemDrop drop)
        {
            if (drop.ItemId != ItemsConstants.IronItemId || drop.Quantity == 0)
            {
                return;
            }

            float multiplier = ResourceTweaksMod.IronDropMultiplier.Value;
            int amount = (int)Math.Round(drop.Quantity * multiplier);
            drop.Quantity = (byte)Math.Max(1, Math.Min(255, amount));
        }
    }

    [HarmonyPatch(typeof(MinablePlanetObject), nameof(MinablePlanetObject.GetItemDrop))]
    internal static class MinableIronDropPatch
    {
        private static void Postfix(ref ItemDrop __result) => IronDrop.Apply(ref __result);
    }

    [HarmonyPatch(typeof(RecyclablePlanetObject), nameof(RecyclablePlanetObject.ItemDrop), MethodType.Getter)]
    internal static class RecyclableIronDropPatch
    {
        private static void Postfix(ref ItemDrop __result) => IronDrop.Apply(ref __result);
    }
}
