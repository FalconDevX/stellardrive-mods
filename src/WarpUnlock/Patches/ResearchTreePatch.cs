using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MelonLoader;
using Research.Interface.Services;
using Research.Interface.Values;
using Ships.Interface.Settings;
using UI.Research;

namespace WarpUnlock.Patches
{
    // Demo nie blokuje warpa w logice gry - ekran badań tylko pomija badania bez flagi availableInDemo.
    // Przed zbudowaniem przycisków drzewa włączamy tę flagę dla badań z warpem i dla badań wymaganych przed nimi.
    // Sama flaga siedzi w pamięci (ScriptableObject), nic nie jest zapisywane do plików gry.
    [HarmonyPatch(typeof(ResearchCanvas), "GenerateButtons")]
    internal static class ResearchTreePatch
    {
        private static readonly AccessTools.FieldRef<ResearchCanvas, IResearchProvider> ProviderRef =
            AccessTools.FieldRefAccess<ResearchCanvas, IResearchProvider>("_researchProvider");

        private static bool _logged;

        private static void Prefix(ResearchCanvas __instance)
        {
            IResearchProvider provider = ProviderRef(__instance);
            if (provider?.Researches == null)
            {
                return;
            }

            if (!_logged)
            {
                _logged = true;
                foreach (ResearchSettings research in provider.Researches.Where(r => r != null))
                {
                    Melon<WarpUnlockMod>.Logger.Msg(
                        $"  badanie {research.id} '{research.techName}': demo={research.availableInDemo} ukryte={research.hideFromTechTree} " +
                        $"tylkoDemo={research.exclusiveToDemo} części=[{string.Join(", ", (research.unlockedParts ?? new PartSettings[0]).Where(p => p != null).Select(p => p.fullLabel))}] " +
                        $"wymaga=[{string.Join(", ", (research.requirements ?? new ResearchSettings[0]).Where(r => r != null).Select(r => r.techName))}]");
                }
            }

            string[] keywords = WarpUnlockMod.PartKeywords.Value
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(k => k.Trim())
                .Where(k => k.Length > 0)
                .ToArray();

            var toUnlock = new HashSet<ResearchSettings>();
            foreach (ResearchSettings research in provider.Researches.Where(r => r != null))
            {
                if (WarpUnlockMod.UnlockAllHidden.Value || UnlocksMatchingPart(research, keywords))
                {
                    AddWithRequirements(research, toUnlock);
                }
            }

            foreach (ResearchSettings research in toUnlock.Where(r => !r.availableInDemo))
            {
                research.availableInDemo = true;
                Melon<WarpUnlockMod>.Logger.Msg($"Odblokowano w drzewie: '{research.techName}'");
            }
        }

        private static bool UnlocksMatchingPart(ResearchSettings research, string[] keywords)
        {
            return research.unlockedParts != null && research.unlockedParts.Any(part =>
                part != null && keywords.Any(k =>
                    (part.fullLabel ?? "").IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (part.name ?? "").IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0));
        }

        private static void AddWithRequirements(ResearchSettings research, HashSet<ResearchSettings> result)
        {
            if (research == null || !result.Add(research) || research.requirements == null)
            {
                return;
            }
            foreach (ResearchSettings requirement in research.requirements)
            {
                AddWithRequirements(requirement, result);
            }
        }
    }
}
