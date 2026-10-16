using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MelonLoader;
using Mining.Content;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Services;
using Ships.Interface.Settings;
using UI.BuildMenu;
using UI.Research;
using UnityEngine;

namespace Mining.Patches
{
    internal static class ModParts
    {
        internal static readonly (ushort id, Func<IEnumerable<PartSettings>, PartSettings> create, Func<PartSettings> get, ushort menuNextTo)[] All =
        {
            (Excavator.PartId, Excavator.GetOrCreatePart, () => Excavator.Part, 30),
            (HydrogenThruster.PartId, HydrogenThruster.GetOrCreatePart, () => HydrogenThruster.Part, 19),
            (ElectrolyzerPart.PartId, ElectrolyzerPart.GetOrCreatePart, () => ElectrolyzerPart.Part, 80),
            (Reactor.PartId, Reactor.GetOrCreatePart, () => Reactor.Part, 60),
            (Reactor.LargePartId, Reactor.GetOrCreateLarge, () => Reactor.Large, Reactor.PartId),
            (WaterPump.PartId, WaterPump.GetOrCreatePart, () => WaterPump.Part, 81),
            (HydrogenEngines.Variants[0].Id, p => HydrogenEngines.GetOrCreate(HydrogenEngines.Variants[0].Id, p), () => HydrogenEngines.Get(HydrogenEngines.Variants[0].Id), HydrogenThruster.PartId),
            (HydrogenEngines.Variants[1].Id, p => HydrogenEngines.GetOrCreate(HydrogenEngines.Variants[1].Id, p), () => HydrogenEngines.Get(HydrogenEngines.Variants[1].Id), HydrogenThruster.PartId),
            (HydrogenEngines.Variants[2].Id, p => HydrogenEngines.GetOrCreate(HydrogenEngines.Variants[2].Id, p), () => HydrogenEngines.Get(HydrogenEngines.Variants[2].Id), HydrogenThruster.PartId),
            (LaserCannon.PartId, LaserCannon.GetOrCreatePart, () => LaserCannon.Part, 19),
            (Pontoon.PartId, Pontoon.GetOrCreatePart, () => Pontoon.Part, 19),
            (InfoPanels.Variants[0].Id, p => InfoPanels.GetOrCreate(InfoPanels.Variants[0].Id, p), () => InfoPanels.Get(InfoPanels.Variants[0].Id), 0),
            (InfoPanels.Variants[1].Id, p => InfoPanels.GetOrCreate(InfoPanels.Variants[1].Id, p), () => InfoPanels.Get(InfoPanels.Variants[1].Id), 0),
            (InfoPanels.Variants[2].Id, p => InfoPanels.GetOrCreate(InfoPanels.Variants[2].Id, p), () => InfoPanels.Get(InfoPanels.Variants[2].Id), 0),
            (InfoPanels.Variants[3].Id, p => InfoPanels.GetOrCreate(InfoPanels.Variants[3].Id, p), () => InfoPanels.Get(InfoPanels.Variants[3].Id), 0),
            (BigFluidTank.Variants[0].id, p => BigFluidTank.GetOrCreate(BigFluidTank.Variants[0].id, p), () => BigFluidTank.Get(BigFluidTank.Variants[0].id), BigFluidTank.TankPartId),
            (BigFluidTank.Variants[1].id, p => BigFluidTank.GetOrCreate(BigFluidTank.Variants[1].id, p), () => BigFluidTank.Get(BigFluidTank.Variants[1].id), BigFluidTank.TankPartId),
            (BigChest.Variants[0].id, p => BigChest.GetOrCreate(BigChest.Variants[0].id, p), () => BigChest.Get(BigChest.Variants[0].id), BigChest.ChestPartId),
            (BigChest.Variants[1].id, p => BigChest.GetOrCreate(BigChest.Variants[1].id, p), () => BigChest.Get(BigChest.Variants[1].id), BigChest.ChestPartId),
            (Barrier.PartId, Barrier.GetOrCreatePart, () => Barrier.Part, BigDoors.DoorPartId),
            (Stairs.PartId, Stairs.GetOrCreatePart, () => Stairs.Part, BigDoors.DoorPartId),
            (RetractableStairs.PartId, RetractableStairs.GetOrCreatePart, () => RetractableStairs.Part, BigDoors.DoorPartId),
            (ScaledVariants.Variants[0].Id, p => ScaledVariants.GetOrCreate(ScaledVariants.Variants[0].Id, p), () => ScaledVariants.Get(ScaledVariants.Variants[0].Id), 0),
            (ScaledVariants.Variants[1].Id, p => ScaledVariants.GetOrCreate(ScaledVariants.Variants[1].Id, p), () => ScaledVariants.Get(ScaledVariants.Variants[1].Id), 0),
            (ScaledVariants.Variants[2].Id, p => ScaledVariants.GetOrCreate(ScaledVariants.Variants[2].Id, p), () => ScaledVariants.Get(ScaledVariants.Variants[2].Id), 0),
            (ScaledVariants.Variants[3].Id, p => ScaledVariants.GetOrCreate(ScaledVariants.Variants[3].Id, p), () => ScaledVariants.Get(ScaledVariants.Variants[3].Id), 0),
            (ScaledVariants.Variants[4].Id, p => ScaledVariants.GetOrCreate(ScaledVariants.Variants[4].Id, p), () => ScaledVariants.Get(ScaledVariants.Variants[4].Id), 0),
            (ScaledVariants.Variants[5].Id, p => ScaledVariants.GetOrCreate(ScaledVariants.Variants[5].Id, p), () => ScaledVariants.Get(ScaledVariants.Variants[5].Id), 0),
            (ScaledVariants.Variants[6].Id, p => ScaledVariants.GetOrCreate(ScaledVariants.Variants[6].Id, p), () => ScaledVariants.Get(ScaledVariants.Variants[6].Id), 0),
            (ScaledVariants.Variants[7].Id, p => ScaledVariants.GetOrCreate(ScaledVariants.Variants[7].Id, p), () => ScaledVariants.Get(ScaledVariants.Variants[7].Id), 0),
            (Blueprints.Workbench.PartId, Blueprints.Workbench.GetOrCreatePart, () => Blueprints.Workbench.Part, Blueprints.Workbench.ResearchTablePartId),
            (BigChest.Variants[2].id, p => BigChest.GetOrCreate(BigChest.Variants[2].id, p), () => BigChest.Get(BigChest.Variants[2].id), BigChest.ChestPartId),
            (BigDoors.Variants[0].id, p => BigDoors.GetOrCreate(BigDoors.Variants[0].id, p), () => BigDoors.Get(BigDoors.Variants[0].id), BigDoors.DoorPartId),
            (BigDoors.Variants[1].id, p => BigDoors.GetOrCreate(BigDoors.Variants[1].id, p), () => BigDoors.Get(BigDoors.Variants[1].id), BigDoors.DoorPartId),
            (BigDoors.Variants[2].id, p => BigDoors.GetOrCreate(BigDoors.Variants[2].id, p), () => BigDoors.Get(BigDoors.Variants[2].id), BigDoors.DoorPartId),
            (BigDoors.Variants[3].id, p => BigDoors.GetOrCreate(BigDoors.Variants[3].id, p), () => BigDoors.Get(BigDoors.Variants[3].id), BigDoors.DoorPartId),
            (BigDoors.Variants[4].id, p => BigDoors.GetOrCreate(BigDoors.Variants[4].id, p), () => BigDoors.Get(BigDoors.Variants[4].id), BigDoors.DoorPartId),
            (BigDoors.Variants[5].id, p => BigDoors.GetOrCreate(BigDoors.Variants[5].id, p), () => BigDoors.Get(BigDoors.Variants[5].id), BigDoors.DoorPartId),
            (BigDoors.Variants[6].id, p => BigDoors.GetOrCreate(BigDoors.Variants[6].id, p), () => BigDoors.Get(BigDoors.Variants[6].id), BigDoors.DoorPartId),
            (BigDoors.Variants[7].id, p => BigDoors.GetOrCreate(BigDoors.Variants[7].id, p), () => BigDoors.Get(BigDoors.Variants[7].id), BigDoors.DoorPartId),
            (BigDoors.Variants[8].id, p => BigDoors.GetOrCreate(BigDoors.Variants[8].id, p), () => BigDoors.Get(BigDoors.Variants[8].id), BigDoors.DoorPartId),
            (PowerMeter.PartId, PowerMeter.GetOrCreatePart, () => PowerMeter.Part, 0),
        };

        internal static readonly ModResearch LargeDoorsResearch = new ModResearch(BigDoors.ResearchId, "Large Doors", new Vector2Int(3, 6), 16,
            () => BigDoors.All.Concat(new[] { Barrier.Part, Stairs.Part, RetractableStairs.Part }).Where(p => p != null).ToArray(),
            (Items.Model.ItemsConstants.IronItemId, 30), (112, 10));

        internal static readonly ModResearch ToolCraftingResearch = new ModResearch(Blueprints.Workbench.ResearchId, "Tool Crafting", new Vector2Int(5, 7), 14,
            () => new[] { Blueprints.Workbench.Part },
            (Items.Model.ItemsConstants.IronItemId, 25), (112, 10));

        internal static readonly ModResearch LargeStorageResearch = new ModResearch(BigChest.ResearchId, "Large Storage", new Vector2Int(2, 0), 17,
            () => BigChest.All,
            (Items.Model.ItemsConstants.IronItemId, 30), (112, 5));

        internal static readonly ModResearch LargeTanksResearch = new ModResearch(BigFluidTank.ResearchId, "Large Tanks", new Vector2Int(3, 3), 8,
            () => BigFluidTank.All.Concat(ScaledVariants.All).ToArray(),
            (Items.Model.ItemsConstants.IronItemId, 40), (Items.Model.ItemsConstants.GlassItemId, 15));

        internal static readonly ModResearch HydrogenResearch = new ModResearch(201, "Hydrogen Propulsion", new Vector2Int(5, 4), Excavator.ResearchId,
            () => new[] { HydrogenThruster.Part, ElectrolyzerPart.Part, WaterPump.Part },
            (Items.Model.ItemsConstants.IronItemId, 30), (112, 15), (Items.Model.ItemsConstants.GlassItemId, 10));

        internal static readonly ModResearch HeavyHydrogenResearch = new ModResearch(HydrogenEngines.ResearchId, "Heavy Hydrogen Engines", new Vector2Int(6, 3), 201,
            () => HydrogenEngines.All,
            (Items.Model.ItemsConstants.IronItemId, 80), (112, 40), (Items.Model.ItemsConstants.GlassItemId, 20));

        internal static readonly ModResearch LargeReactorResearch = new ModResearch(Reactor.LargeResearchId, "Large Reactor", new Vector2Int(7, 4), 204,
            () => new[] { Reactor.Large },
            (Items.Model.ItemsConstants.IronItemId, 150), (112, 80), (Items.Model.ItemsConstants.GlassItemId, 40));

        internal static readonly ModResearch MechanicsResearch = new ModResearch(RotorUnlock.ResearchId, "Mechanics", new Vector2Int(4, 5), 16,
            () => new[] { RotorUnlock.Rotor },
            (Items.Model.ItemsConstants.IronItemId, 20), (112, 10));

        internal static readonly ModResearch LaserResearch = new ModResearch(LaserCannon.ResearchId, "Laser Weapons", new Vector2Int(6, 5), 201,
            () => new[] { LaserCannon.Part },
            (Items.Model.ItemsConstants.IronItemId, 30), (112, 15), (Items.Model.ItemsConstants.GlassItemId, 20));

        internal static readonly ModResearch PontoonResearch = new ModResearch(Pontoon.ResearchId, "Pontoons", new Vector2Int(6, 1), 12,
            () => new[] { Pontoon.Part },
            (Items.Model.ItemsConstants.IronItemId, 20), (Items.Model.ItemsConstants.WoodItemId, 30));

        internal static readonly ModResearch InfoPanelsResearch = new ModResearch(InfoPanels.ResearchId, "Info Panels", new Vector2Int(6, 0), 5,
            () => InfoPanels.All.Concat(new[] { PowerMeter.Part }).Where(p => p != null).ToArray(),
            (Items.Model.ItemsConstants.IronItemId, 15), (112, 10), (Items.Model.ItemsConstants.GlassItemId, 10));

        internal static readonly ModResearch NuclearResearch = new ModResearch(204, "Nuclear Power", new Vector2Int(6, 4), 201,
            () => new[] { Reactor.Part },
            (Items.Model.ItemsConstants.IronItemId, 40), (112, 20), (Uranium.ItemId, 10));

        internal static readonly ModResearch JetpackResearch = new ModResearch(Jetpack.ResearchId, "Jetpack", new Vector2Int(3, 1), 14,
            () => new PartSettings[0],
            (Items.Model.ItemsConstants.IronItemId, 25), (112, 10), (Items.Model.ItemsConstants.WoodItemId, 30)) { AllowNoParts = true };

        internal static IEnumerable<(PartSettings part, ushort menuNextTo)> MenuParts()
        {
            foreach (var mod in All)
            {
                PartSettings part = mod.get();
                yield return (part, InfoPanels.Is(part) && InfoPanels.DisplayPartId != 0 ? InfoPanels.DisplayPartId : mod.menuNextTo);
            }
            yield return (RotorUnlock.Rotor, RotorUnlock.MenuNextTo);
        }

        internal static void EnsureResearch()
        {
            Excavator.EnsureResearch();
            HydrogenResearch.Ensure();
            HeavyHydrogenResearch.Ensure();
            MechanicsResearch.Ensure();
            NuclearResearch.Ensure();
            LargeReactorResearch.Ensure();
            LaserResearch.Ensure();
            PontoonResearch.Ensure();
            InfoPanelsResearch.Ensure();
            JetpackResearch.Ensure();
            LargeDoorsResearch.Ensure();
            LargeTanksResearch.Ensure();
            LargeStorageResearch.Ensure();
            ToolCraftingResearch.Ensure();
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
                PilotSeatTriggers.Extend(dict.Values);
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
                    Melon<MiningMod>.Logger.Error($"Part '{part.fullLabel}' (id {part.id}) has socket ids {string.Join(", ", ids)} - they must be 0..{ids.Count - 1}, the game cannot build it");
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
            ("Control", new ushort[] { 3, 29, 17, 46, 18, 50, 54, 45, 48, 82, 56, 49, 68, 39, 247, 225, 226, 227, PowerMeter.PartId }),
            ("Propulsion", new ushort[] { 69, 19, 201, 228, 229, 230, 53, 16, 58, 222 }),
            ("Power", new ushort[] { 61, 65, 60, 203, Reactor.LargePartId }),
            ("Fluids", new ushort[] { 24, 81, 57, 23, 218, 215, 79, 66, 67, 202, 214, 80 }),
            ("Doors", new ushort[] { DoorPartId, 42, Barrier.PartId, Stairs.PartId, RetractableStairs.PartId, 244, 217, 210, 211, 212, 213, 216, 223, 224 }),
            ("Utility", new ushort[] { 22, Blueprints.Workbench.PartId, 15, 38, 30, 219, 220, 232, 43, 41, 200, 221, 55 }),
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
                Melon<MiningMod>.Logger.Error($"Build menu tabs: {e}");
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
                Melon<MiningMod>.Logger.Error($"Build menu size: {e}");
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
            Melon<MiningMod>.Logger.Msg($"Build menu: {tabs.Length} tabs, {width:0} wide (spacing {spacing:0}, padding {padding:0})");
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
            var sorted = new HashSet<ushort>(Tabs.SelectMany(t => t.ids).Concat(ScaledVariants.Ids));
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
                    PartSettings[] bigger = ScaledVariants.Variants.Where(v => ScaledVariants.BaseId(v.Id) == list[i].id && byId.ContainsKey(v.Id)).Select(v => byId[v.Id]).ToArray();
                    list.InsertRange(i + 1, bigger);
                }
                if (list.Count == 0) continue;
                var rows = new List<BuildMenuTabConfig.BuildMenuRow>();
                for (int i = 0; i < list.Count; i += MaxPerRow)
                {
                    rows.Add(new BuildMenuTabConfig.BuildMenuRow { parts = list.Skip(i).Take(MaxPerRow).ToArray() });
                }
                result.Add(new BuildMenuTabConfig { name = name, icon = name == "Doors" ? doorIcon : parts.icon, rows = rows.ToArray() });
            }
            Melon<MiningMod>.Logger.Msg($"Build menu: {result.Count} tabs ({string.Join(", ", result.Select(t => $"{t.name} {t.rows.Sum(r => r.parts.Length)}"))})");
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
