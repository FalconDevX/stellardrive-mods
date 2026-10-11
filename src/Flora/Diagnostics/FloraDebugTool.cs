using System;
using System.Collections;
using System.Linq;
using System.Text;
using Core.Services;
using Flora.Models;
using HarmonyLib;
using MelonLoader;
using Planets.Network;
using Planets.Services;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Flora.Diagnostics
{
    // Narzędzie do szukania, czemu nowe rośliny się nie pokazują:
    //  F9  - stawia zwykłe (nie-GPU) kopie krzaka i kwiatów przed kamerą: sprawdza model i materiał
    //  F10 - wypisuje do logu stan każdej warstwy roślinności (czy liczona na GPU i ile obiektów ma)
    internal static class FloraDebugTool
    {
        internal static void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null)
            {
                return;
            }
            try
            {
                if (kb.f9Key.wasPressedThisFrame)
                {
                    SpawnTestObjects();
                }
                if (kb.f10Key.wasPressedThisFrame)
                {
                    Report();
                }
            }
            catch (Exception e)
            {
                Melon<FloraMod>.Logger.Error("Błąd narzędzia Flora: " + e);
            }
        }

        private static void SpawnTestObjects()
        {
            Camera cam = Camera.main;
            if (cam == null || !FloraPrefabs.IsReady)
            {
                Melon<FloraMod>.Logger.Warning($"F9: brak kamery ({cam != null}) albo prefabów ({FloraPrefabs.IsReady})");
                return;
            }
            Transform t = cam.transform;
            Vector3 forward = Vector3.ProjectOnPlane(t.forward, t.up).normalized;
            Vector3 basePos = t.position + forward * 3f - t.up * 1.6f;
            GameObject[] all = FloraPrefabs.CreateFlowers().Take(3).ToArray();
            for (int i = 0; i < all.Length; i++)
            {
                Vector3 pos = basePos + Vector3.Cross(t.up, forward) * ((i - all.Length / 2) * 1.5f);
                GameObject go = UnityEngine.Object.Instantiate(all[i], pos, Quaternion.LookRotation(forward, t.up));
                go.name = "FloraTest_" + all[i].name;
                go.SetActive(true);
                MelonCoroutines.Start(DestroyLater(go, 60f));
            }
            Melon<FloraMod>.Logger.Msg($"F9: postawiono {all.Length} testowych roślin 3 m przed kamerą (znikną po 60 s)");
        }

        private static IEnumerator DestroyLater(GameObject go, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (go != null)
            {
                UnityEngine.Object.Destroy(go);
            }
        }

        private static void Report()
        {
            var tracker = ServiceLocator.GetService<PlanetsClientTracker>();
            if (tracker == null)
            {
                Melon<FloraMod>.Logger.Warning("F10: brak PlanetsClientTracker");
                return;
            }
            var sb = new StringBuilder("F10: stan roślinności\n");
            foreach (TrackedPlanetClient planet in tracker.TrackedPlanets)
            {
                object visuals = Traverse.Create(planet).Field("_denseEnvironmentVisuals").GetValue();
                if (visuals == null)
                {
                    continue;
                }
                var levels = (Array)Traverse.Create(visuals).Field("_visuals").GetValue();
                sb.AppendLine($"  planeta {planet.Planet?.name}: {levels.Length} warstw");
                for (int i = 0; i < levels.Length; i++)
                {
                    Traverse lv = Traverse.Create(levels.GetValue(i));
                    bool allocated = lv.Field("_isAllocated").GetValue<bool>();
                    int chunks = ((ICollection)lv.Field("_currentChunks").GetValue())?.Count ?? -1;
                    var config = lv.Field("_environmentConfig").GetValue<Planets.EnvironmentGeneration.Configs.PlanetDensePlacementConfig>();
                    string first = config?.objectsSpawn?.FirstOrDefault().prefab?.name;
                    sb.Append($"    [{i}] {first}: level={config?.level} view={config?.ViewRangeCache} alloc={allocated} chunks={chunks}");
                    if (allocated)
                    {
                        Traverse gpu = Traverse.Create(lv.Field("_placement").GetValue()).Property("Gpu");
                        var sizes = gpu.Property("FinalBufferSizes").GetValue<int[]>();
                        var matrices = gpu.Property("MatricesBuffersRef").GetValue<ComputeBuffer[]>();
                        sb.Append(" typy:");
                        for (int k = 0; k < sizes.Length; k++)
                        {
                            sb.Append($" {sizes[k]}/{CountPlaced(matrices?[k])}");
                        }
                    }
                    sb.AppendLine();
                }
            }
            Melon<FloraMod>.Logger.Msg(sb.ToString());
        }

        // Liczy niezerowe macierze w buforze GPUI (= obiekty faktycznie postawione do rysowania).
        private static string CountPlaced(ComputeBuffer buffer)
        {
            if (buffer == null || !buffer.IsValid())
            {
                return "?";
            }
            int count = Math.Min(buffer.count, 20000);
            var data = new Matrix4x4[count];
            buffer.GetData(data, 0, 0, count);
            int placed = 0;
            for (int i = 0; i < count; i++)
            {
                if (data[i].m33 != 0f || data[i].m00 != 0f)
                {
                    placed++;
                }
            }
            return placed.ToString();
        }
    }
}
