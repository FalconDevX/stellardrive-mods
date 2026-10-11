using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Mining.Content;
using Ships.Interface.Services;
using Ships.Interface.Settings;
using UI.BuildMenu;
using UI.Research;

namespace Mining.Patches
{
    // Gra szuka części przez Resources.FindObjectsOfTypeAll i zapamiętuje je w słowniku (każdy PartSettingsProvider osobno).
    // Po zbudowaniu słownika dokładamy do niego koparkę - wtedy znają ją zapis, sieć, budowanie i miniaturki.
    [HarmonyPatch(typeof(PartSettingsProvider), "EnsureDictIsCached")]
    internal static class PartSettingsProviderPatch
    {
        private static readonly AccessTools.FieldRef<PartSettingsProvider, Dictionary<ushort, PartSettings>> DictRef =
            AccessTools.FieldRefAccess<PartSettingsProvider, Dictionary<ushort, PartSettings>>("_partsDict");

        private static void Postfix(PartSettingsProvider __instance)
        {
            Dictionary<ushort, PartSettings> dict = DictRef(__instance);
            if (dict == null || dict.ContainsKey(Excavator.PartId))
            {
                return;
            }
            PartSettings part = Excavator.GetOrCreatePart(dict.Values);
            if (part != null)
            {
                dict[part.id] = part;
                Excavator.EnsureResearch();
            }
        }
    }

    // Menu budowania: koparka w tym samym wierszu co skrzynia.
    [HarmonyPatch(typeof(BuildMenu), "LoadTabPanel")]
    internal static class BuildMenuPatch
    {
        private static void Prefix(BuildMenuTabConfig[] ___tabsConfig)
        {
            PartSettings excavator = Excavator.Part;
            if (excavator == null || ___tabsConfig == null)
            {
                return;
            }
            var rows = ___tabsConfig.Where(t => t?.rows != null).SelectMany(t => t.rows).Where(r => r?.parts != null).ToList();
            if (rows.Any(r => r.parts.Contains(excavator)))
            {
                return;
            }
            BuildMenuTabConfig.BuildMenuRow row = rows.FirstOrDefault(r => r.parts.Any(p => p != null && p.id == 30)) ?? rows.LastOrDefault();
            if (row != null)
            {
                row.parts = row.parts.Concat(new[] { excavator }).ToArray();
            }
        }
    }

    [HarmonyPatch(typeof(ResearchCanvas), "GenerateButtons")]
    internal static class ResearchCanvasPatch
    {
        private static void Prefix()
        {
            new PartSettingsProvider().GetAllPartSettings();
            Excavator.EnsureResearch();
        }
    }
}
