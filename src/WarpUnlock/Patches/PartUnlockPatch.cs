using System;
using System.Linq;
using HarmonyLib;
using Research.Interface.Values;
using Research;
using Ships.Interface.Settings;

namespace WarpUnlock.Patches
{
    // Zapas: gdyby w danych dema żadne badanie nie odblokowywało części warpa, menu budowania by jej nie pokazało.
    // Wtedy traktujemy ją jako odblokowaną od razu.
    [HarmonyPatch(typeof(ResearchClientProxy), nameof(ResearchClientProxy.IsPartUnlocked))]
    internal static class PartUnlockPatch
    {
        private static readonly AccessTools.FieldRef<ResearchClientProxy, ResearchSettingsList> ListRef =
            AccessTools.FieldRefAccess<ResearchClientProxy, ResearchSettingsList>("researchSettingsList");

        private static void Postfix(ResearchClientProxy __instance, PartSettings part, ref bool __result)
        {
            if (__result || part == null || !Matches(part))
            {
                return;
            }
            ResearchSettingsList list = ListRef(__instance);
            bool anyResearchUnlocksIt = list?.list != null && list.list.Any(r => r?.unlockedParts != null && r.unlockedParts.Contains(part));
            if (!anyResearchUnlocksIt)
            {
                __result = true;
            }
        }

        private static bool Matches(PartSettings part)
        {
            return WarpUnlockMod.PartKeywords.Value
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(k => k.Trim())
                .Any(k => k.Length > 0 &&
                          ((part.fullLabel ?? "").IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0 ||
                           (part.name ?? "").IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0));
        }
    }
}
