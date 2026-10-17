using Core.Utils;
using HarmonyLib;
using Tools.Build;
using Tools.Build.Model;
using UnityEngine;

namespace Odyssey.Patches
{
    [HarmonyPatch(typeof(PartPreviewDetection), nameof(PartPreviewDetection.CalculateSnapDetectionUpdate))]
    internal static class CenteredPreviewPatch
    {
        internal static readonly System.Collections.Generic.List<System.Func<Ships.Interface.Settings.PartSettings, bool>> Parts =
            new System.Collections.Generic.List<System.Func<Ships.Interface.Settings.PartSettings, bool>>();

        private static void Prefix(SnapDetectionState state, ref CameraPhysicsData cameraPhysics)
        {
            if (state.partSettings == null || !Parts.Exists(p => p(state.partSettings)))
            {
                return;
            }
            Vector3 center = state.partSettings.Bounds.center;
            cameraPhysics.CameraPos -= state.partRotation * new Vector3(center.x, 0f, center.z);
        }
    }
}
