using HarmonyLib;
using Rendering.Services;
using UnityEngine;

namespace Mining.Patches
{
    // Miniaturka części (menu budowania, badania): kamera ortograficzna stoi 5 m przed środkiem obiektu i widzi 10 m
    // w głąb, więc długie części obrócone o 45 stopni (brama 16x8) miały ucięte rogi. Odsuwamy kamerę i wydłużamy zasięg
    // - obraz jest ten sam (rzut ortograficzny), tylko nic nie jest przycinane.
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
