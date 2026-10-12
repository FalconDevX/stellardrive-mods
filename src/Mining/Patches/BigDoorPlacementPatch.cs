using HarmonyLib;
using Ships.Interface.Model.Placement;
using Tools.Build.Model;
using Tools.Build.SnapDetectors.Types;
using UnityEngine;

namespace Mining.Patches
{
    // Gra stawia część "ścienną" od pola, w które celujesz, w prawo i w górę (dla drzwi 2x2 to nie przeszkadza).
    // Duże drzwi (3-10 pól szerokości) przesuwamy tak, żeby celowane pole było na środku ich szerokości.
    [HarmonyPatch]
    internal static class BigDoorPlacementPatch
    {
        private static System.Collections.Generic.IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(WallSquaredOnFloorSnapDetector), nameof(WallSquaredOnFloorSnapDetector.DetectSnapPlacement));
            yield return AccessTools.Method(typeof(WallSquaredOnWallSnapDetector), nameof(WallSquaredOnWallSnapDetector.DetectSnapPlacement));
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
}
