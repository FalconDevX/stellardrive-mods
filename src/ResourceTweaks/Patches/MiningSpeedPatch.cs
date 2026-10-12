using HarmonyLib;
using Items.Model;
using Planets.Objects;
using UnityEngine;

namespace ResourceTweaks.Patches
{
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
