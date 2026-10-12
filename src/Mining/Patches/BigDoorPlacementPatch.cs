using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Mining.Content;
using Ships.Interface.Model.Placement;
using Tools.Build;
using Tools.Build.Model;
using Tools.Build.SnapDetectors.Types;
using UnityEngine;

namespace Mining.Patches
{
    [HarmonyPatch]
    internal static class BigDoorPlacementPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(WallSquaredOnFloorSnapDetector), nameof(WallSquaredOnFloorSnapDetector.DetectSnapPlacement));
            yield return AccessTools.Method(typeof(WallSquaredOnWallSnapDetector), nameof(WallSquaredOnWallSnapDetector.DetectSnapPlacement));
            yield return AccessTools.Method(typeof(WallOnSlopeSnapDetector), nameof(WallOnSlopeSnapDetector.DetectSnapPlacement));
        }

        private static void Postfix(HitSnapDetectionProperties props, ref IPartPlacement __result)
        {
            if (!(__result is HullPartPlacement placement) || props == null)
            {
                return;
            }
            int width = Mathf.RoundToInt(props.Size.x);
            if (width < 3)
            {
                return;
            }
            Vector3Int shift = PartOrientationHelper.RotateByOrientation(placement.ForwardOrientation, new Vector3Int((width - 1) / 2, 0, 0));
            __result = new HullPartPlacement(placement.Coord - shift, placement.Position.Type, placement.ForwardOrientation, placement.UpOrientation);
        }
    }

    [HarmonyPatch(typeof(PartPreviewDetection), "CalculateSurfacePlacement")]
    internal static class BigDoorSurfacePlacementPatch
    {
        private static void Postfix(SnapDetectionState state, ref (Vector3, bool) __result)
        {
            if (state.partSettings == null || !BigDoors.All.Contains(state.partSettings))
            {
                return;
            }
            float half = state.partSettings.Bounds.center.x;
            __result.Item1 -= state.partRotation * new Vector3(half, 0f, 0f);
        }
    }
}
