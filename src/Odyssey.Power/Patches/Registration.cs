using System.Linq;
using Items.Model;
using Odyssey.Content;
using UnityEngine;

namespace Odyssey.Patches
{
    internal static class Registration
    {
        private const ushort MiningResearchId = 200;

        internal static void Register()
        {
            ModParts.Add(HydrogenThruster.PartId, HydrogenThruster.GetOrCreatePart, () => HydrogenThruster.Part, 19);
            ModParts.Add(ElectrolyzerPart.PartId, ElectrolyzerPart.GetOrCreatePart, () => ElectrolyzerPart.Part, 80);
            ModParts.Add(Reactor.PartId, Reactor.GetOrCreatePart, () => Reactor.Part, 60);
            ModParts.Add(Reactor.LargePartId, Reactor.GetOrCreateLarge, () => Reactor.Large, Reactor.PartId);
            ModParts.Add(WaterPump.PartId, WaterPump.GetOrCreatePart, () => WaterPump.Part, 81);
            foreach (ushort id in HydrogenEngines.Variants.Select(v => v.Id).ToArray())
            {
                ModParts.Add(id, p => HydrogenEngines.GetOrCreate(id, p), () => HydrogenEngines.Get(id), HydrogenThruster.PartId);
            }
            foreach (ushort id in BigFluidTank.Variants.Select(v => v.id).ToArray())
            {
                ModParts.Add(id, p => BigFluidTank.GetOrCreate(id, p), () => BigFluidTank.Get(id), BigFluidTank.TankPartId);
            }
            foreach (ushort id in ScaledVariants.Ids.ToArray())
            {
                ModParts.Add(id, p => ScaledVariants.GetOrCreate(id, p), () => ScaledVariants.Get(id));
            }
            Server.ShipPartIndex.Track(WaterPump.Ids);
            CenteredPreviewPatch.Parts.Add(p => BigFluidTank.TryGetCapacity(p, out _));
            CustomTooltipPatch.Labels.Add((WaterPumpInteraction.LowerLabel, "Lower hose"));
            CustomTooltipPatch.Labels.Add((WaterPumpInteraction.RaiseLabel, "Raise hose"));
            ModParts.MenuVariants.Add(() => ScaledVariants.Ids.Select(id => (id, ScaledVariants.BaseId(id))));

            ModParts.Add(new ModResearch(201, "Hydrogen Propulsion", new Vector2Int(5, 4), MiningResearchId,
                () => new[] { HydrogenThruster.Part, ElectrolyzerPart.Part, WaterPump.Part },
                (ItemsConstants.IronItemId, 30), (112, 15), (ItemsConstants.GlassItemId, 10)));
            ModParts.Add(new ModResearch(HydrogenEngines.ResearchId, "Heavy Hydrogen Engines", new Vector2Int(6, 3), 201,
                () => HydrogenEngines.All,
                (ItemsConstants.IronItemId, 80), (112, 40), (ItemsConstants.GlassItemId, 20)));
            ModParts.Add(new ModResearch(204, "Nuclear Power", new Vector2Int(6, 4), 201,
                () => new[] { Reactor.Part },
                (ItemsConstants.IronItemId, 40), (112, 20), (ModItems.Uranium, 10)));
            ModParts.Add(new ModResearch(Reactor.LargeResearchId, "Large Reactor", new Vector2Int(7, 4), 204,
                () => new[] { Reactor.Large },
                (ItemsConstants.IronItemId, 150), (112, 80), (ItemsConstants.GlassItemId, 40)));
            ModParts.Add(new ModResearch(BigFluidTank.ResearchId, "Large Tanks", new Vector2Int(3, 3), 8,
                () => BigFluidTank.All.Concat(ScaledVariants.All).ToArray(),
                (ItemsConstants.IronItemId, 40), (ItemsConstants.GlassItemId, 15)));
        }
    }
}
