using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Mining.Content;
using Ships.Interface.Services;
using Ships.Interface.Settings;
using UI.BuildMenu;
using UI.Research;
using UnityEngine;

namespace Mining.Patches
{
    // Wszystkie nowe części moda: jak je zbudować i obok której części gry pokazać w menu budowania.
    internal static class ModParts
    {
        internal static readonly (ushort id, Func<IEnumerable<PartSettings>, PartSettings> create, Func<PartSettings> get, ushort menuNextTo)[] All =
        {
            (Excavator.PartId, Excavator.GetOrCreatePart, () => Excavator.Part, 30),            // obok skrzyni
            (HydrogenThruster.PartId, HydrogenThruster.GetOrCreatePart, () => HydrogenThruster.Part, 19), // obok silnika chemicznego
            (ElectrolyzerPart.PartId, ElectrolyzerPart.GetOrCreatePart, () => ElectrolyzerPart.Part, 80), // obok destylatora
        };

        // Napęd wodorowy za badaniem Mining (w tym samym rzędzie drzewa).
        internal static readonly ModResearch HydrogenResearch = new ModResearch(201, "Hydrogen Propulsion", new Vector2Int(5, 4), Excavator.ResearchId,
            () => new[] { HydrogenThruster.Part, ElectrolyzerPart.Part },
            (Items.Model.ItemsConstants.IronItemId, 30), (112, 15), (Items.Model.ItemsConstants.GlassItemId, 10));

        internal static void EnsureResearch()
        {
            Excavator.EnsureResearch();
            HydrogenResearch.Ensure();
        }
    }

    // Gra szuka części przez Resources.FindObjectsOfTypeAll i zapamiętuje je w słowniku (każdy PartSettingsProvider osobno).
    // Po zbudowaniu słownika dokładamy do niego nasze części - wtedy znają je zapis, sieć, budowanie i miniaturki.
    [HarmonyPatch(typeof(PartSettingsProvider), "EnsureDictIsCached")]
    internal static class PartSettingsProviderPatch
    {
        private static readonly AccessTools.FieldRef<PartSettingsProvider, Dictionary<ushort, PartSettings>> DictRef =
            AccessTools.FieldRefAccess<PartSettingsProvider, Dictionary<ushort, PartSettings>>("_partsDict");

        private static void Postfix(PartSettingsProvider __instance)
        {
            Dictionary<ushort, PartSettings> dict = DictRef(__instance);
            if (dict == null || ModParts.All.All(p => dict.ContainsKey(p.id)))
            {
                return;
            }
            foreach (var mod in ModParts.All)
            {
                if (dict.ContainsKey(mod.id))
                {
                    continue;
                }
                PartSettings part = mod.create(dict.Values.ToList());
                if (part != null)
                {
                    dict[part.id] = part;
                }
            }
            ModParts.EnsureResearch();
        }
    }

    // Menu budowania: każda nowa część w tym samym wierszu co podobna część gry.
    [HarmonyPatch(typeof(BuildMenu), "LoadTabPanel")]
    internal static class BuildMenuPatch
    {
        private static void Prefix(BuildMenuTabConfig[] ___tabsConfig)
        {
            if (___tabsConfig == null)
            {
                return;
            }
            var rows = ___tabsConfig.Where(t => t?.rows != null).SelectMany(t => t.rows).Where(r => r?.parts != null).ToList();
            foreach (var mod in ModParts.All)
            {
                PartSettings part = mod.get();
                if (part == null || rows.Any(r => r.parts.Contains(part)))
                {
                    continue;
                }
                BuildMenuTabConfig.BuildMenuRow row = rows.FirstOrDefault(r => r.parts.Any(p => p != null && p.id == mod.menuNextTo)) ?? rows.LastOrDefault();
                if (row != null)
                {
                    row.parts = row.parts.Concat(new[] { part }).ToArray();
                }
            }
        }
    }

    [HarmonyPatch(typeof(ResearchCanvas), "GenerateButtons")]
    internal static class ResearchCanvasPatch
    {
        private static void Prefix()
        {
            new PartSettingsProvider().GetAllPartSettings();
            ModParts.EnsureResearch();
        }
    }
}
