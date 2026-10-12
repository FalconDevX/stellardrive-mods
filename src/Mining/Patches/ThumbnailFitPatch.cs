using HarmonyLib;
using Rendering.Services;
using UnityEngine;

namespace Mining.Patches
{
    [HarmonyPatch(typeof(ObjectThumbnailRenderer), "PlaceObjectForThumbnailRendering")]
    internal static class ThumbnailFitPatch
    {
        private const float GameDistance = 5f, Distance = 60f;

        private static void Postfix(Camera ____renderCamera)
        {
            if (____renderCamera == null || !____renderCamera.orthographic)
            {
                return;
            }
            Transform t = ____renderCamera.transform;
            Vector3 center = t.position + t.forward * GameDistance;
            t.position = center - t.forward * Distance;
            ____renderCamera.farClipPlane = Distance * 2f;
        }
    }
}
