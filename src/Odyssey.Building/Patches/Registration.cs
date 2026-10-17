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
            ModParts.Add(Pontoon.PartId, Pontoon.GetOrCreatePart, () => Pontoon.Part, 19);
            ModParts.Add(Pontoon.MediumPartId, p => Pontoon.GetOrCreate(Pontoon.MediumPartId, p), () => Pontoon.Get(Pontoon.MediumPartId), Pontoon.PartId);
            ModParts.Add(Pontoon.LargePartId, p => Pontoon.GetOrCreate(Pontoon.LargePartId, p), () => Pontoon.Get(Pontoon.LargePartId), Pontoon.MediumPartId);
            foreach (ushort id in BigChest.Variants.Select(v => v.id).ToArray())
            {
                ModParts.Add(id, p => BigChest.GetOrCreate(id, p), () => BigChest.Get(id), BigChest.ChestPartId);
            }
            ModParts.Add(Barrier.PartId, Barrier.GetOrCreatePart, () => Barrier.Part, BigDoors.DoorPartId);
            ModParts.Add(Stairs.PartId, Stairs.GetOrCreatePart, () => Stairs.Part, BigDoors.DoorPartId);
            ModParts.Add(RetractableStairs.PartId, RetractableStairs.GetOrCreatePart, () => RetractableStairs.Part, BigDoors.DoorPartId);
            foreach (ushort id in BigDoors.Variants.Select(v => v.id).ToArray())
            {
                ModParts.Add(id, p => BigDoors.GetOrCreate(id, p), () => BigDoors.Get(id), BigDoors.DoorPartId);
            }
            ModParts.Add(HeavyGyroscope.PartId, HeavyGyroscope.GetOrCreatePart, () => HeavyGyroscope.Part, HeavyGyroscope.GyroscopePartId);
            Server.ShipPartIndex.Track(Pontoon.Ids.Concat(new[] { RetractableStairs.PartId }));
            CenteredPreviewPatch.Parts.Add(p => Pontoon.Scale(p.id) > 1);
            ModParts.ExtraMenuParts.Add(() => (RotorUnlock.Rotor, RotorUnlock.MenuNextTo));

            ModParts.Add(new ModResearch(BigDoors.ResearchId, "Large Doors", new Vector2Int(3, 6), 16,
                () => BigDoors.All.Concat(new[] { Barrier.Part, Stairs.Part, RetractableStairs.Part }).Where(p => p != null).ToArray(),
                (ItemsConstants.IronItemId, 30), (112, 10)));
            ModParts.Add(new ModResearch(BigChest.ResearchId, "Large Storage", new Vector2Int(2, 0), 17,
                () => BigChest.All,
                (ItemsConstants.IronItemId, 30), (112, 5)));
            ModParts.Add(new ModResearch(RotorUnlock.ResearchId, "Mechanics", new Vector2Int(4, 5), 16,
                () => new[] { RotorUnlock.Rotor, HeavyGyroscope.Part }.Where(p => p != null).ToArray(),
                (ItemsConstants.IronItemId, 20), (112, 10)));
            ModParts.Add(new ModResearch(Pontoon.ResearchId, "Pontoons", new Vector2Int(6, 1), 12,
                () => Pontoon.All,
                (ItemsConstants.IronItemId, 20), (ItemsConstants.WoodItemId, 30)));
        }
    }
}
