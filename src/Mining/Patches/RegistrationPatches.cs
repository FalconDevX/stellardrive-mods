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
            (WaterPump.PartId, WaterPump.GetOrCreatePart, () => WaterPump.Part, 81),
            (LaserCannon.PartId, LaserCannon.GetOrCreatePart, () => LaserCannon.Part, 19),
            (Pontoon.PartId, Pontoon.GetOrCreatePart, () => Pontoon.Part, 19),
            (InfoPanels.Variants[0].Id, p => InfoPanels.GetOrCreate(InfoPanels.Variants[0].Id, p), () => InfoPanels.Get(InfoPanels.Variants[0].Id), 0),
            (InfoPanels.Variants[1].Id, p => InfoPanels.GetOrCreate(InfoPanels.Variants[1].Id, p), () => InfoPanels.Get(InfoPanels.Variants[1].Id), 0),
            (InfoPanels.Variants[2].Id, p => InfoPanels.GetOrCreate(InfoPanels.Variants[2].Id, p), () => InfoPanels.Get(InfoPanels.Variants[2].Id), 0),
            (BigFluidTank.Variants[0].id, p => BigFluidTank.GetOrCreate(BigFluidTank.Variants[0].id, p), () => BigFluidTank.Get(BigFluidTank.Variants[0].id), BigFluidTank.TankPartId),
            (BigFluidTank.Variants[1].id, p => BigFluidTank.GetOrCreate(BigFluidTank.Variants[1].id, p), () => BigFluidTank.Get(BigFluidTank.Variants[1].id), BigFluidTank.TankPartId),
            (BigChest.Variants[0].id, p => BigChest.GetOrCreate(BigChest.Variants[0].id, p), () => BigChest.Get(BigChest.Variants[0].id), BigChest.ChestPartId),
            (BigChest.Variants[1].id, p => BigChest.GetOrCreate(BigChest.Variants[1].id, p), () => BigChest.Get(BigChest.Variants[1].id), BigChest.ChestPartId),
            (BigDoors.Variants[0].id, p => BigDoors.GetOrCreate(BigDoors.Variants[0].id, p), () => BigDoors.Get(BigDoors.Variants[0].id), BigDoors.DoorPartId),
            (BigDoors.Variants[1].id, p => BigDoors.GetOrCreate(BigDoors.Variants[1].id, p), () => BigDoors.Get(BigDoors.Variants[1].id), BigDoors.DoorPartId),
            (BigDoors.Variants[2].id, p => BigDoors.GetOrCreate(BigDoors.Variants[2].id, p), () => BigDoors.Get(BigDoors.Variants[2].id), BigDoors.DoorPartId),
            (BigDoors.Variants[3].id, p => BigDoors.GetOrCreate(BigDoors.Variants[3].id, p), () => BigDoors.Get(BigDoors.Variants[3].id), BigDoors.DoorPartId),
            (BigDoors.Variants[4].id, p => BigDoors.GetOrCreate(BigDoors.Variants[4].id, p), () => BigDoors.Get(BigDoors.Variants[4].id), BigDoors.DoorPartId),
            (BigDoors.Variants[5].id, p => BigDoors.GetOrCreate(BigDoors.Variants[5].id, p), () => BigDoors.Get(BigDoors.Variants[5].id), BigDoors.DoorPartId),
            (BigDoors.Variants[6].id, p => BigDoors.GetOrCreate(BigDoors.Variants[6].id, p), () => BigDoors.Get(BigDoors.Variants[6].id), BigDoors.DoorPartId),
            (BigDoors.Variants[7].id, p => BigDoors.GetOrCreate(BigDoors.Variants[7].id, p), () => BigDoors.Get(BigDoors.Variants[7].id), BigDoors.DoorPartId),
        };

        internal static readonly ModResearch LargeDoorsResearch = new ModResearch(BigDoors.ResearchId, "Large Doors", new Vector2Int(3, 6), 16,
            () => BigDoors.All,
            (Items.Model.ItemsConstants.IronItemId, 30), (112, 10));

        internal static readonly ModResearch LargeStorageResearch = new ModResearch(BigChest.ResearchId, "Large Storage", new Vector2Int(2, 0), 17,
            () => BigChest.All,
            (Items.Model.ItemsConstants.IronItemId, 30), (112, 5));

        internal static readonly ModResearch LargeTanksResearch = new ModResearch(BigFluidTank.ResearchId, "Large Tanks", new Vector2Int(3, 3), 8,
            () => BigFluidTank.All,
            (Items.Model.ItemsConstants.IronItemId, 40), (Items.Model.ItemsConstants.GlassItemId, 15));

        internal static readonly ModResearch HydrogenResearch = new ModResearch(201, "Hydrogen Propulsion", new Vector2Int(5, 4), Excavator.ResearchId,
            () => new[] { HydrogenThruster.Part, ElectrolyzerPart.Part, WaterPump.Part },
            (Items.Model.ItemsConstants.IronItemId, 30), (112, 15), (Items.Model.ItemsConstants.GlassItemId, 10));

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
            () => InfoPanels.All.ToArray(),
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
            MechanicsResearch.Ensure();
            NuclearResearch.Ensure();
            LaserResearch.Ensure();
            PontoonResearch.Ensure();
            InfoPanelsResearch.Ensure();
            JetpackResearch.Ensure();
            LargeDoorsResearch.Ensure();
            LargeTanksResearch.Ensure();
            LargeStorageResearch.Ensure();
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
