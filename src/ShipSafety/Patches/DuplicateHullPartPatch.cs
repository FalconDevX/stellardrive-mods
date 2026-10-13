using System.Collections.Generic;
using HarmonyLib;
using MelonLoader;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Placement;
using Ships.Visuals;

namespace ShipSafety.Patches
{
    [HarmonyPatch(typeof(ShipHullVisualChunk), nameof(ShipHullVisualChunk.RegisterHullSegment))]
    internal static class DuplicateHullPartPatch
    {
        private static readonly AccessTools.FieldRef<ShipHullVisualChunk, Dictionary<HullPartPosition, HullPart>> PlacedRef =
            AccessTools.FieldRefAccess<ShipHullVisualChunk, Dictionary<HullPartPosition, HullPart>>("_placedParts");

        private static readonly HashSet<HullPartPosition> Logged = new HashSet<HullPartPosition>();

        private static bool Prefix(ShipHullVisualChunk __instance, HullPart part)
        {
            Dictionary<HullPartPosition, HullPart> placed = PlacedRef(__instance);
            HullPartPosition position = part.HullPlacement.Position;
            if (placed == null || !placed.TryGetValue(position, out HullPart existing))
            {
                return true;
            }
            if (Logged.Add(position))
            {
                Melon<ShipSafetyMod>.Logger.Warning(
                    $"Two hull parts in the same place ({position}): kept {existing.Settings?.name}, skipped the visuals of {part.Settings?.name}");
            }
            return false;
        }
    }
}
