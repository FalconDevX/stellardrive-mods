using HarmonyLib;
using Items.Model;
using Planets.Objects;
using UnityEngine;

namespace ResourceTweaks.Patches
{
    // Szybsze kopanie laserem: laser co klatkę zbija obiektowi "zdrowie" (ReduceHealth). Krótszy czas kopania
    // = większe obrażenia: czas x0.5 to obrażenia x2. Drzewo rozpoznajemy po tym, że daje drewno.
    // Liczy się u gracza, który kopie - więc każdy gracz potrzebuje moda, żeby kopać szybciej.
    [HarmonyPatch(typeof(MinablePlanetObject), nameof(MinablePlanetObject.ReduceHealth))]
    internal static class MiningSpeedPatch
    {
        private static void Prefix(MinablePlanetObject __instance, ref float amount)
        {
            bool isTree = __instance.dropRate.item != null && __instance.dropRate.item.id == ItemsConstants.WoodItemId;
            float timeFactor = isTree ? ResourceTweaksMod.TreeMiningTime.Value : ResourceTweaksMod.OtherMiningTime.Value;
            amount /= Mathf.Clamp(timeFactor, 0.05f, 10f);
        }
    }
}
