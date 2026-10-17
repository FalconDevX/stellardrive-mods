using System.Linq;
using Items.Model;
using Odyssey.Content;
using UnityEngine;

namespace Odyssey.Patches
{
    internal static class Registration
    {
        internal static void Register()
        {
            foreach (ushort id in InfoPanels.Variants.Select(v => v.Id).ToArray())
            {
                ModParts.Add(id, p => InfoPanels.GetOrCreate(id, p), () => InfoPanels.Get(id));
            }
            ModParts.MenuNextTo.Add(part => InfoPanels.Is(part) ? InfoPanels.DisplayPartId : (ushort)0);
            ModParts.Add(PowerMeter.PartId, PowerMeter.GetOrCreatePart, () => PowerMeter.Part);
            ModParts.Add(GasMeterSignal.PartId, GasMeterSignal.GetOrCreatePart, () => GasMeterSignal.Part, 68);
            Server.ShipPartIndex.Track(InfoPanels.Variants.Select(v => v.Id));
            CustomTooltipPatch.Labels.Add((InfoPanelInteraction.EditLabel, "Edit text"));
            ModParts.PartsLoaded.Add(PilotSeatTriggers.Extend);

            ModParts.Add(new ModResearch(InfoPanels.ResearchId, "Info Panels", new Vector2Int(6, 0), 5,
                () => InfoPanels.All.Concat(new[] { PowerMeter.Part, GasMeterSignal.Part }).Where(p => p != null).ToArray(),
                (ItemsConstants.IronItemId, 15), (112, 10), (ItemsConstants.GlassItemId, 10)));
        }
    }
}
