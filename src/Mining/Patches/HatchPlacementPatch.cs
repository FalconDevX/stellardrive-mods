using HarmonyLib;
using Mining.Content;
using Ships.Interface.Model;
using Ships.Interface.Model.Placement;
using Ships.Interface.Settings;
using Tools.Build;
using Tools.Build.Model;
using Tools.Build.PlacementSnapDetection;
using Tools.Build.SnapDetectors.Wrappers;
using UnityEngine;

namespace Mining.Patches
{
    internal static class HatchPlacement
    {
        internal static PartSettings Current;

        internal static HullPartPlacement Fix(HullPartPlacement placement, PartSettings part)
        {
            int n = Mathf.RoundToInt(part.size.x);
            var shift = new Vector3Int((n - 1) / 2, 0, (n - 1) / 2);
            return new HullPartPlacement(placement.Coord - shift, VoxelPartType.Floor, PartOrientation.Up, PartOrientation.Right);
        }
    }

    [HarmonyPatch(typeof(PartPreviewDetection), nameof(PartPreviewDetection.CalculateSnapDetectionUpdate))]
    internal static class HatchPreviewPartPatch
    {
        private static void Prefix(SnapDetectionState state) => HatchPlacement.Current = state.partSettings;
    }

    [HarmonyPatch(typeof(HorizontalFloor), nameof(HorizontalFloor.DetectSnapPlacement))]
    internal static class HatchHitPlacementPatch
    {
        private static void Postfix(ref IPartPlacement __result)
        {
            PartSettings part = HatchPlacement.Current;
            if (__result is HullPartPlacement placement && placement.Position.Type == VoxelPartType.Floor && BigDoors.IsHatch(part))
            {
                __result = HatchPlacement.Fix(placement, part);
            }
        }
    }

    [HarmonyPatch(typeof(PlacementSnapDetector), "GetHullPlacementFromProps")]
    internal static class HatchPositionPlacementPatch
    {
        private static void Postfix(PlacementSnapDetectionProperties props, ref HullPartPlacement? __result)
        {
            if (__result.HasValue && __result.Value.Position.Type == VoxelPartType.Floor && props != null && BigDoors.IsHatch(props.PartSettings))
            {
                __result = HatchPlacement.Fix(__result.Value, props.PartSettings);
            }
        }
    }
}
