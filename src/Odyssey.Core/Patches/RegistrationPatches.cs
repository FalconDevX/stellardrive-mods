using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MelonLoader;
using Odyssey.Content;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Services;
using Ships.Interface.Settings;
using UI.BuildMenu;
using UI.Research;
using UnityEngine;

namespace Odyssey.Patches
{
    internal static class ModParts
    {
        internal static readonly List<(ushort id, Func<IEnumerable<PartSettings>, PartSettings> create, Func<PartSettings> get, ushort menuNextTo)> All =
            new List<(ushort, Func<IEnumerable<PartSettings>, PartSettings>, Func<PartSettings>, ushort)>();

        private static readonly List<Action> Research = new List<Action>();
        internal static readonly List<Action<IEnumerable<PartSettings>>> PartsLoaded = new List<Action<IEnumerable<PartSettings>>>();
        internal static readonly List<Func<PartSettings, ushort>> MenuNextTo = new List<Func<PartSettings, ushort>>();
        internal static readonly List<Func<(PartSettings part, ushort menuNextTo)>> ExtraMenuParts = new List<Func<(PartSettings, ushort)>>();
        internal static readonly List<Func<IEnumerable<(ushort id, ushort baseId)>>> MenuVariants = new List<Func<IEnumerable<(ushort, ushort)>>>();

        internal static void Add(ushort id, Func<IEnumerable<PartSettings>, PartSettings> create, Func<PartSettings> get, ushort menuNextTo = 0) =>
            All.Add((id, create, get, menuNextTo));

        internal static void Add(ModResearch research) => Research.Add(research.Ensure);

        internal static void Add(Action ensureResearch) => Research.Add(ensureResearch);

        internal static IEnumerable<(ushort id, ushort baseId)> Variants() => MenuVariants.SelectMany(v => v());

        internal static IEnumerable<(PartSettings part, ushort menuNextTo)> MenuParts()
        {
            foreach (var mod in All)
            {
                PartSettings part = mod.get();
                ushort next = mod.menuNextTo;
                foreach (Func<PartSettings, ushort> rule in MenuNextTo)
                {
                    ushort other = rule(part);
                    if (other != 0) next = other;
                }
                yield return (part, next);
            }
            foreach (Func<(PartSettings, ushort)> extra in ExtraMenuParts)
            {
                yield return extra();
            }
        }

        internal static void EnsureResearch()
        {
            for (int pass = 0; pass < 2; pass++)
            {
                foreach (Action ensure in Research)
                {
                    try
                    {
                        ensure();
                    }
                    catch (Exception e)
                    {
                        Melon<CoreMod>.Logger.Error($"Research registration: {e}");
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(PartSettingsProvider), "EnsureDictIsCached")]
    internal static class PartSettingsProviderPatch
    {
        private static readonly AccessTools.FieldRef<PartSettingsProvider, Dictionary<ushort, PartSettings>> DictRef =
            AccessTools.FieldRefAccess<PartSettingsProvider, Dictionary<ushort, PartSettings>>("_partsDict");

        private static void Postfix(PartSettingsProvider __instance)
        {
            Dictionary<ushort, PartSettings> dict = DictRef(__instance);
            if (dict != null)
            {
                foreach (Action<IEnumerable<PartSettings>> loaded in ModParts.PartsLoaded) loaded(dict.Values);
            }
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
                    CheckSockets(part);
                }
            }
            ModParts.EnsureResearch();
        }

        private static void CheckSockets(PartSettings part)
        {
            if (part.part == null)
            {
                return;
            }
            List<byte> ids = part.part.GetComponentsInChildren<SocketObject>(true).Select(s => s.Id).OrderBy(id => id).ToList();
            for (int i = 0; i < ids.Count; i++)
            {
                if (ids[i] != i)
                {
                    Melon<CoreMod>.Logger.Error($"Part '{part.fullLabel}' (id {part.id}) has socket ids {string.Join(", ", ids)} - they must be 0..{ids.Count - 1}, the game cannot build it");
                    return;
                }
            }
        }
    }

    [HarmonyPatch(typeof(BuildMenu), "Start")]
    internal static class BuildMenuPatch
    {
        private const int MaxPerRow = 11;
        private const ushort DoorPartId = 14;
        private const float MenuWidth = 3000f, MenuCenter = -380f, InfoCenter = 1510f;

        private static readonly (string name, ushort[] ids)[] Tabs =
        {
            ("Control", new ushort[] { 3, 29, 17, 46, 18, 50, 54, 45, 48, 82, 56, 49, 68, 260, 39, 247, 225, 226, 227, 243 }),
            ("Propulsion", new ushort[] { 69, 19, 201, 228, 229, 230, 53, 248, 16, 58, 222, 251, 252 }),
            ("Power", new ushort[] { 61, 65, 60, 203, 231 }),
            ("Fluids", new ushort[] { 24, 81, 57, 23, 218, 215, 79, 66, 67, 202, 214, 80 }),
            ("Doors", new ushort[] { DoorPartId, 42, 234, 235, 236, 244, 217, 210, 211, 212, 213, 216, 223, 224, 253, 254, 255, 256, 257, 258, 259 }),
            ("Utility", new ushort[] { 22, 233, 15, 38, 30, 219, 220, 232, 43, 41, 200, 221, 55 }),
        };

        private static void Prefix(ref BuildMenuTabConfig[] ___tabsConfig)
        {
            if (___tabsConfig == null || ___tabsConfig.Any(t => t?.name == Tabs[0].name))
            {
                return;
            }
            try
            {
                ___tabsConfig = Sort(___tabsConfig);
            }
            catch (Exception e)
            {
                Melon<CoreMod>.Logger.Error($"Build menu tabs: {e}");
            }
        }

        private static void Postfix(BuildMenu __instance)
        {
            try
            {
                Widen(__instance);
            }
            catch (Exception e)
            {
                Melon<CoreMod>.Logger.Error($"Build menu size: {e}");
            }
        }

        private static void Widen(BuildMenu menu)
        {
            var panel = menu.transform.Find("Menu") as RectTransform;
            if (panel == null)
            {
                return;
            }
            panel.sizeDelta = new Vector2(MenuWidth, panel.sizeDelta.y);
            panel.anchoredPosition = new Vector2(MenuCenter, panel.anchoredPosition.y);
            if (menu.transform.Find("PartInfo") is RectTransform info)
            {
                info.anchoredPosition = new Vector2(InfoCenter, info.anchoredPosition.y);
            }
            TabButton[] tabs = Traverse.Create(menu).Field<TabButton[]>("_tabs").Value ?? new TabButton[0];
            var bar = panel.Find("TabsSelect/Tabs") as RectTransform;
            if (tabs.Length == 0 || bar == null)
            {
                return;
            }
            var group = bar.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            float spacing = group != null ? group.spacing : 0f;
            float padding = group != null ? group.padding.horizontal : 0f;
            float available = MenuWidth - 164f - padding - 130f - spacing * (tabs.Length + 1);
            float natural = ((RectTransform)tabs[0].transform).rect.width;
            float width = Mathf.Min(natural, available / tabs.Length);
            Melon<CoreMod>.Logger.Msg($"Build menu: {tabs.Length} tabs, {width:0} wide (spacing {spacing:0}, padding {padding:0})");
            foreach (TabButton tab in tabs)
            {
                var rt = (RectTransform)tab.transform;
                rt.sizeDelta = new Vector2(width, rt.sizeDelta.y);
                UnityEngine.UI.LayoutElement element = tab.GetComponent<UnityEngine.UI.LayoutElement>() ?? tab.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
                element.minWidth = width;
                element.preferredWidth = width;
                element.flexibleWidth = 0f;
                foreach (TMPro.TMP_Text text in tab.GetComponentsInChildren<TMPro.TMP_Text>(true))
                {
                    text.enableAutoSizing = false;
                    text.fontSize *= Mathf.Min(1f, width / natural * 0.95f);
                    text.overflowMode = TMPro.TextOverflowModes.Ellipsis;
                }
            }
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(bar);
        }

        private static BuildMenuTabConfig[] Sort(BuildMenuTabConfig[] tabs)
        {
            new PartSettingsProvider().GetAllPartSettings();
            List<PartSettings> all = tabs.Where(t => t?.rows != null).SelectMany(t => t.rows).Where(r => r?.parts != null).SelectMany(r => r.parts).Where(p => p != null).ToList();
            foreach (var (part, _) in ModParts.MenuParts())
            {
                if (part != null && !all.Contains(part)) all.Add(part);
            }
            Dictionary<ushort, PartSettings> byId = all.GroupBy(p => p.id).ToDictionary(g => g.Key, g => g.First());
            var provider = new PartSettingsProvider();
            foreach (ushort id in Tabs.SelectMany(t => t.ids))
            {
                if (byId.ContainsKey(id)) continue;
                PartSettings missing = provider.GetPartSettingById(id);
                if (missing != null)
                {
                    byId[id] = missing;
                    Melon<CoreMod>.Logger.Msg($"Build menu: added '{missing.fullLabel}' (id {id}) that no game tab listed");
                }
            }
            List<(ushort id, ushort baseId)> variants = ModParts.Variants().ToList();
            var sorted = new HashSet<ushort>(Tabs.SelectMany(t => t.ids).Concat(variants.Select(v => v.id)));
            BuildMenuTabConfig parts = tabs.FirstOrDefault(t => t?.name == "Parts") ?? tabs.Last(t => t != null);
            Texture doorIcon = tabs.FirstOrDefault(t => t?.rows != null && t.rows.Any(r => r?.parts != null && r.parts.Any(p => p != null && p.id == DoorPartId)))?.icon ?? parts.icon;

            var result = new List<BuildMenuTabConfig>();
            foreach (BuildMenuTabConfig tab in tabs)
            {
                if (tab == parts || tab?.rows == null) continue;
                tab.rows = tab.rows.Where(r => r?.parts != null)
                    .Select(r => new BuildMenuTabConfig.BuildMenuRow { parts = r.parts.Where(p => p != null && !sorted.Contains(p.id)).ToArray() })
                    .Where(r => r.parts.Length > 0).ToArray();
                if (tab.rows.Length > 0) result.Add(tab);
            }
            var placed = new HashSet<PartSettings>(result.SelectMany(t => t.rows).SelectMany(r => r.parts));
            List<PartSettings> leftovers = all.Where(p => !sorted.Contains(p.id) && !placed.Contains(p)).Distinct().ToList();

            foreach (var (name, ids) in Tabs)
            {
                List<PartSettings> list = ids.Where(byId.ContainsKey).Select(id => byId[id]).ToList();
                if (name == Tabs[Tabs.Length - 1].name) list.AddRange(leftovers);
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    PartSettings[] bigger = variants.Where(v => v.baseId == list[i].id && byId.ContainsKey(v.id)).Select(v => byId[v.id]).ToArray();
                    list.InsertRange(i + 1, bigger);
                }
                if (list.Count == 0) continue;
                if (list.Count > MaxPerRow * 2)
                {
                    Melon<CoreMod>.Logger.Warning($"Build menu tab {name} has {list.Count} parts - more than two rows, the last ones may not show");
                }
                var rows = new List<BuildMenuTabConfig.BuildMenuRow>();
                for (int i = 0; i < list.Count; i += MaxPerRow)
                {
                    rows.Add(new BuildMenuTabConfig.BuildMenuRow { parts = list.Skip(i).Take(MaxPerRow).ToArray() });
                }
                result.Add(new BuildMenuTabConfig { name = name, icon = name == "Doors" ? doorIcon : parts.icon, rows = rows.ToArray() });
            }
            Melon<CoreMod>.Logger.Msg($"Build menu: {result.Count} tabs ({string.Join(", ", result.Select(t => $"{t.name} {t.rows.Sum(r => r.parts.Length)}"))})");
            BuildMenuTabConfig fluids = result.FirstOrDefault(t => t.name == "Fluids");
            if (fluids != null)
            {
                Melon<CoreMod>.Logger.Msg("Build menu Fluids: " + string.Join(", ", fluids.rows.SelectMany(r => r.parts).Select(p => $"{p.fullLabel} ({p.id})")));
            }
            return result.ToArray();
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
