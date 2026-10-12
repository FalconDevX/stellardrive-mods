using System;
using System.Collections;
using System.IO;
using System.Linq;
using Core.Services;
using Core.Values;
using MelonLoader;
using MelonLoader.Utils;
using Planets.Network;
using UnityEngine;
using Water.Surface;
using Water.Terrain;
using WorldTracking.Interface.Model.Spaces;
using WorldTracking.Interface.Services;
using WorldTracking.Interface.Values;

namespace Water.Diagnostics
{
    // Tylko dla twórcy moda: parametr startowy --water-dev-test (świat wczytuje Mining: --mining-dev-world=...).
    // Po wczytaniu szuka najbliższego akwenu, robi zdjęcia z kamery testowej do UserData/Water i zamyka grę.
    internal static class WaterDevTest
    {
        private static bool _started;

        private static bool HasArg(string name) => Environment.GetCommandLineArgs().Any(a => a == "--water-dev-" + name);

        internal static void OnSceneLoaded(string sceneName)
        {
            if (sceneName == "MainGame" && HasArg("test") && !_started)
            {
                _started = true;
                MelonCoroutines.Start(Run());
            }
        }

        private static IEnumerator Run()
        {
            var log = Melon<WaterMod>.Logger;
            string outDir = Path.Combine(MelonEnvironment.UserDataDirectory, "Water");
            Directory.CreateDirectory(outDir);
            yield return new WaitForSeconds(30f);
            try
            {
                Camera cam = Camera.main;
                var transpose = ServiceLocator.GetService<IWorldTransposeClient>();
                TrackedPlanet tracked = WaterSurface.FindTracked();
                if (cam == null || transpose == null || tracked == null || !(ClientOrigin.LocalVisualSpace is PlanetSurfaceSpace surface))
                {
                    log.Msg("[test] brak kamery/planety");
                    yield break;
                }
                var planetSpace = new PlanetSurfaceSpace { PlanetId = surface.PlanetId, Center = Vector3Double.Zero };
                Vector3 camPlanet = (Vector3)transpose.Position(surface, planetSpace, (Vector3Double)cam.transform.position);
                float radius = tracked.Planet.Radius;
                Vector3 up = camPlanet.normalized;
                Vector3 t1 = Vector3.Cross(up, Mathf.Abs(up.y) < 0.9f ? Vector3.up : Vector3.right).normalized, t2 = Vector3.Cross(up, t1);
                Vector3 best = Vector3.zero;
                float bestDist = float.MaxValue, bestElev = 0f;
                for (float x = -4000f; x <= 4000f; x += 50f)
                {
                    for (float y = -4000f; y <= 4000f; y += 50f)
                    {
                        Vector3 dir = (camPlanet + t1 * x + t2 * y).normalized;
                        float elev = tracked.PointsProvider.GetElevationAtPoint(dir) - radius;
                        float dist = x * x + y * y;
                        if (elev < LakeTerrainPatch.SeaLevel - 3f && dist < bestDist)
                        {
                            bestDist = dist;
                            best = dir;
                            bestElev = elev;
                        }
                    }
                }
                if (bestDist == float.MaxValue)
                {
                    log.Msg("[test] brak akwenu w promieniu 4 km");
                    yield break;
                }
                log.Msg($"[test] akwen {Mathf.Sqrt(bestDist):0} m od kamery, dno {bestElev:0.0} m, woda {LakeTerrainPatch.SeaLevel} m; materiał {WaterSurface.Material?.shader?.name}");
                MelonCoroutines.Start(Shots(cam, transpose, planetSpace, surface, best, radius, outDir));
            }
            catch (Exception e)
            {
                log.Error($"[test] {e}");
            }
        }

        private static IEnumerator Shots(Camera cam, IWorldTransposeClient transpose, PlanetSurfaceSpace planetSpace, PlanetSurfaceSpace surface, Vector3 dir, float radius,
            string outDir)
        {
            var testCam = new GameObject("WaterTestCam").AddComponent<Camera>();
            testCam.CopyFrom(cam);
            var mainData = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            var camData = testCam.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (mainData != null)
            {
                camData.renderPostProcessing = mainData.renderPostProcessing;
                camData.antialiasing = mainData.antialiasing;
            }
            WaterSurface.OverrideCamera = testCam;
            float[] heights = { 30f, 8f, 120f };
            for (int shot = 0; shot < heights.Length; shot++)
            {
                for (int frame = 0; frame < 40; frame++)
                {
                    Vector3 target = (Vector3)transpose.Position(planetSpace, surface, (Vector3Double)(dir * (radius + LakeTerrainPatch.SeaLevel)));
                    Vector3 upV = transpose.Rotation(planetSpace, surface, Quaternion.identity) * dir;
                    Vector3 side = Vector3.Cross(upV, Vector3.forward).normalized;
                    testCam.transform.position = target + upV * heights[shot] - side * heights[shot] * 1.5f;
                    testCam.transform.rotation = Quaternion.LookRotation(target - testCam.transform.position, upV);
                    cam.enabled = false;
                    yield return null;
                }
                ScreenCapture.CaptureScreenshot(Path.Combine(outDir, $"water{shot}.png"));
                yield return null;
                yield return null;
            }
            cam.enabled = true;
            WaterSurface.OverrideCamera = null;
            UnityEngine.Object.Destroy(testCam.gameObject);
            Melon<WaterMod>.Logger.Msg("[test] zdjęcia gotowe");
            yield return new WaitForSeconds(2f);
            if (HasArg("quit"))
            {
                Application.Quit();
            }
        }
    }
}
