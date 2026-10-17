using Core.Utils;
using HarmonyLib;
using Mining.Content;
using Tools.Build;
using Tools.Build.Model;
using UnityEngine;

namespace Mining.Patches
{
    [HarmonyPatch(typeof(PartPreviewDetection), nameof(PartPreviewDetection.CalculateSnapDetectionUpdate))]
    internal static class CenteredPreviewPatch
    {
        private static void Prefix(SnapDetectionState state, ref CameraPhysicsData cameraPhysics)
        {
            if (state.partSettings == null || (!BigFluidTank.TryGetCapacity(state.partSettings, out _) && Pontoon.Scale(state.partSettings.id) <= 1))
            {
                return;
            }
            Vector3 center = state.partSettings.Bounds.center;
            cameraPhysics.CameraPos -= state.partRotation * new Vector3(center.x, 0f, center.z);
        }
    }
}
