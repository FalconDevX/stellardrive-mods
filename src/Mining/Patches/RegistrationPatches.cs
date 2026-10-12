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
    internal static class ModParts
    {
        internal static readonly (ushort id, Func<IEnumerable<PartSettings>, PartSettings> create, Func<PartSettings> get, ushort menuNextTo)[] All =
        {
            (Excavator.PartId, Excavator.GetOrCreatePart, () => Excavator.Part, 30),
            (HydrogenThruster.PartId, HydrogenThruster.GetOrCreatePart, () => HydrogenThruster.Part, 19),
            (ElectrolyzerPart.PartId, ElectrolyzerPart.GetOrCreatePart, () => ElectrolyzerPart.Part, 80),
            (Reactor.PartId, Reactor.GetOrCreatePart, () => Reactor.Part, 60),
            (WaterPump.PartId, WaterPump.GetOrCreatePart, () => WaterPump.Part, 81),
            (BigFluidTank.PartId, BigFluidTank.GetOrCreatePart, () => BigFluidTank.Part, BigFluidTank.TankPartId),
            (BigDoors.Variants[0].id, p => BigDoors.GetOrCreate(BigDoors.Variants[0].id, p), () => BigDoors.Get(BigDoors.Variants[0].id), BigDoors.DoorPartId),
            (BigDoors.Variants[1].id, p => BigDoors.GetOrCreate(BigDoors.Variants[1].id, p), () => BigDoors.Get(BigDoors.Variants[1].id), BigDoors.DoorPartId),
            (BigDoors.Variants[2].id, p => BigDoors.GetOrCreate(BigDoors.Variants[2].id, p), () => BigDoors.Get(BigDoors.Variants[2].id), BigDoors.DoorPartId),
            (BigDoors.Variants[3].id, p => BigDoors.GetOrCreate(BigDoors.Variants[3].id, p), () => BigDoors.Get(BigDoors.Variants[3].id), BigDoors.DoorPartId),
            (BigDoors.Variants[4].id, p => BigDoors.GetOrCreate(BigDoors.Variants[4].id, p), () => BigDoors.Get(BigDoors.Variants[4].id), BigDoors.DoorPartId),
            (BigDoors.Variants[5].id, p => BigDoors.GetOrCreate(BigDoors.Variants[5].id, p), () => BigDoors.Get(BigDoors.Variants[5].id), BigDoors.DoorPartId),
        };

        internal static readonly ModResearch LargeDoorsResearch = new ModResearch(BigDoors.ResearchId, "Large Doors", new Vector2Int(3, 6), 16,
            () => BigDoors.All,
            (Items.Model.ItemsConstants.IronItemId, 30), (112, 10));

        internal static readonly ModResearch LargeTanksResearch = new ModResearch(BigFluidTank.ResearchId, "Large Tanks", new Vector2Int(3, 3), 8,
            () => new[] { BigFluidTank.Part },
            (Items.Model.ItemsConstants.IronItemId, 40), (Items.Model.ItemsConstants.GlassItemId, 15));

        internal static readonly ModResearch HydrogenResearch = new ModResearch(201, "Hydrogen Propulsion", new Vector2Int(5, 4), Excavator.ResearchId,
            () => new[] { HydrogenThruster.Part, ElectrolyzerPart.Part, WaterPump.Part },
            (Items.Model.ItemsConstants.IronItemId, 30), (112, 15), (Items.Model.ItemsConstants.GlassItemId, 10));

        internal static readonly ModResearch MechanicsResearch = new ModResearch(RotorUnlock.ResearchId, "Mechanics", new Vector2Int(4, 5), 16,
            () => new[] { RotorUnlock.Rotor },
            (Items.Model.ItemsConstants.IronItemId, 20), (112, 10));

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
                yield return (mod.get(), mod.menuNextTo);
            }
            yield return (RotorUnlock.Rotor, RotorUnlock.MenuNextTo);
        }

        internal static void EnsureResearch()
        {
            Excavator.EnsureResearch();
            HydrogenResearch.Ensure();
            MechanicsResearch.Ensure();
            NuclearResearch.Ensure();
            JetpackResearch.Ensure();
            LargeDoorsResearch.Ensure();
            LargeTanksResearch.Ensure();
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

    [HarmonyPatch(typeof(BuildMenu), "LoadTabPanel")]
    internal static class BuildMenuPatch
    {
        private const int MaxPerRow = 8;

        private static void Prefix(BuildMenuTabConfig[] ___tabsConfig)
        {
            if (___tabsConfig == null)
            {
                return;
            }
            var rows = ___tabsConfig.Where(t => t?.rows != null).SelectMany(t => t.rows).Where(r => r?.parts != null).ToList();
            foreach (var (part, menuNextTo) in ModParts.MenuParts())
            {
                if (part == null || rows.Any(r => r.parts.Contains(part)))
                {
                    continue;
                }
                BuildMenuTabConfig.BuildMenuRow row = rows.FirstOrDefault(r => r.parts.Any(p => p != null && p.id == menuNextTo)) ?? rows.LastOrDefault();
                if (row != null && row.parts.Length >= MaxPerRow)
                {
                    var tab = ___tabsConfig.FirstOrDefault(t => t?.rows != null && t.rows.Contains(row));
                    row = tab?.rows.Where(r => r?.parts != null && r.parts.Length < MaxPerRow).OrderBy(r => r.parts.Length).FirstOrDefault() ?? row;
                }
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
