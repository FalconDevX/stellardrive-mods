using Odyssey.Content;

namespace Odyssey.Patches
{
    internal static class Registration
    {
        internal static void Register()
        {
            PartFactory.ItemFactories[Uranium.ItemId] = Uranium.GetOrCreate;
            ModParts.Add(Excavator.PartId, Excavator.GetOrCreatePart, () => Excavator.Part, 30);
            ModParts.Add(Excavator.EnsureResearch);
            Server.ShipPartIndex.Track(new[] { Excavator.PartId });
        }
    }
}
