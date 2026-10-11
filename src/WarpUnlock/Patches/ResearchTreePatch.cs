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
                        $"poz={research.positionInTechTree} wymaga=[{string.Join(", ", (research.requirements ?? new ResearchSettings[0]).Where(r => r != null).Select(r => r.techName))}]");
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
                MoveToFreeSpot(research, provider.Researches);
                Melon<WarpUnlockMod>.Logger.Msg($"Odblokowano w drzewie: '{research.techName}' (pozycja {research.positionInTechTree})");
            }
        }

        // W demie w miejscu ukrytego badania stoi zwykle jego zastępstwo (np. "Demo Complete" zamiast "Stellar Travel"),
        // a przyciski na tej samej pozycji się zasłaniają. Przesuwamy odblokowane badanie na wolne miejsce w tej kolumnie.
        private static void MoveToFreeSpot(ResearchSettings research, IEnumerable<ResearchSettings> all)
        {
            var taken = new HashSet<UnityEngine.Vector2Int>(all
                .Where(r => r != null && r != research && r.availableInDemo && !r.hideFromTechTree)
                .Select(r => r.positionInTechTree));
            UnityEngine.Vector2Int position = research.positionInTechTree;
            if (!taken.Contains(position))
            {
                return;
            }
            for (int offset = 1; offset < 10; offset++)
            {
                foreach (int y in new[] { position.y - offset, position.y + offset })
                {
                    var candidate = new UnityEngine.Vector2Int(position.x, y);
                    if (y >= 0 && !taken.Contains(candidate))
                    {
                        research.positionInTechTree = candidate;
                        return;
                    }
                }
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
