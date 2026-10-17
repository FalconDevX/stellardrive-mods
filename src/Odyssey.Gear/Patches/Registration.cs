using Items.Model;
using Odyssey.Blueprints;
using Odyssey.Content;
using Ships.Interface.Settings;
using UnityEngine;

namespace Odyssey.Patches
{
    internal static class Registration
    {
        internal static void Register()
        {
            ModParts.Add(LaserCannon.PartId, LaserCannon.GetOrCreatePart, () => LaserCannon.Part, 19);
            ModParts.Add(Workbench.PartId, Workbench.GetOrCreatePart, () => Workbench.Part, Workbench.ResearchTablePartId);

            Server.ShipPartIndex.Track(new[] { LaserCannon.PartId });
            ResearchTreeLayoutPatch.SingleRowCanvases.Add(WorkbenchUi.IsWorkbench);

            ModParts.Add(new ModResearch(Workbench.ResearchId, "Tool Crafting", new Vector2Int(5, 7), 14,
                () => new[] { Workbench.Part },
                (ItemsConstants.IronItemId, 25), (112, 10)));
            ModParts.Add(new ModResearch(LaserCannon.ResearchId, "Laser Weapons", new Vector2Int(6, 5), 201,
                () => new[] { LaserCannon.Part },
                (ItemsConstants.IronItemId, 30), (112, 15), (ItemsConstants.GlassItemId, 20)));
            ModParts.Add(new ModResearch(Jetpack.ResearchId, "Jetpack", new Vector2Int(3, 1), 14,
                () => new PartSettings[0],
                (ItemsConstants.IronItemId, 25), (112, 10), (ItemsConstants.WoodItemId, 30)) { AllowNoParts = true });
        }
    }
}
