using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Core.Services;
using MelonLoader;
using MelonLoader.Utils;
using Planets;
using Planets.EnvironmentGeneration;
using Planets.EnvironmentGeneration.Configs;
using Planets.Network;
using Planets.Services;
using Planets.Terrain;
using Planets.Terrain.Evaluation.Services;
using UnityEngine;
using Random = System.Random;

namespace Flora.Diagnostics
{
    // Sprawdza, czy na planetach mogą pojawić się złoża, bez latania tam. Gra próbuje postawić każde złoże
    // na każdym fragmencie terenu danego poziomu (dla poziomu 2 to ok. 100-200 m) i odrzuca miejsca, które nie
    // spełniają warunków (wysokość terenu, gęstość roślinności). Tu losujemy punkty na planecie, liczymy
    // prawdziwą wysokość terenu tym samym kalkulatorem co gra i sprawdzamy te same warunki.
    internal static class PlanetResourceScan
    {
        private const int Samples = 3000;
        private const int TriesPerChunk = 100; // gra sprawdza do 20 startów x 5 kroków na fragment

        internal static IEnumerator ScanAfterDelay(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            try
            {
                Scan();
            }
            catch (Exception e)
            {
                Melon<FloraMod>.Logger.Warning($"Skan złóż nie powiódł się: {e}");
            }
        }

        private static void Scan()
        {
            var calculator = ServiceLocator.GetService<PlanetTerrainPointCpuCalculator>();
            Dictionary<ulong, TrackedPlanet> tracked = GetTrackedPlanets();
            if (calculator == null || tracked.Count == 0)
            {
                Melon<FloraMod>.Logger.Warning("Skan złóż: brak planet albo kalkulatora terenu");
                return;
            }

            var sb = new StringBuilder();
            foreach (TrackedPlanet trackedPlanet in tracked.Values.OrderBy(p => p.Planet.name))
            {
                Planet planet = trackedPlanet.Planet;
                var hotspots = planet.GetComponent<PlanetHotspotsEnvironment>();
                var dense = planet.GetComponent<PlanetDenseEnvironment>();
                var terrain = planet.GetComponent<PlanetTerrain>();
                int highestLod = terrain != null ? terrain.HighestLOD : 12;

                var random = new Random((int)planet.Id);
                var points = new List<(Vector3 dir, float height)>(Samples);
                for (int i = 0; i < Samples; i++)
                {
                    Vector3 dir = RandomOnSphere(random);
                    float elevation = calculator.GetElevation(trackedPlanet.TerrainOperationStack, dir);
                    points.Add((dir, elevation - planet.Radius));
                }

                float min = points.Min(p => p.height), max = points.Max(p => p.height);
                sb.AppendLine($"# {planet.name} (id {planet.Id}): promień {planet.Radius / 1000f:0} km, teren od {min:0} do {max:0} m, " +
                              $"roślinność: {(dense != null ? dense.configs.Length + " warstw" : "brak")}");
                string summary = $"{planet.name}: teren {min:0}..{max:0} m";

                if (hotspots?.hotspotsPlacement == null || hotspots.hotspotsPlacement.Count == 0)
                {
                    sb.AppendLine("  brak złóż w konfiguracji");
                    summary += ", brak złóż";
                }
                else
                {
                    foreach (PlanetHotspotsPlacementConfig config in hotspots.hotspotsPlacement)
                    {
                        string[] objects = (config.distributionSteps ?? new HotspotObjectsDistributionStep[0])
                            .Where(s => s?.objectPrefab != null).Select(s => s.objectPrefab.name).ToArray();
                        if (objects.Length == 0)
                        {
                            continue;
                        }
                        int ok = points.Count(p => Allowed(config, p.dir, p.height, dense));
                        double fraction = (double)ok / Samples;
                        double chunks = 6.0 * Math.Pow(4, Math.Max(0, highestLod - config.placementLevel));
                        double chance = 1 - Math.Pow(1 - fraction, TriesPerChunk);
                        double estimate = chunks * config.spawnProbability * chance;
                        double chunkSize = 2 * Math.PI * planet.Radius / 4 / Math.Pow(2, Math.Max(0, highestLod - config.placementLevel));
                        sb.AppendLine($"  [{string.Join(", ", objects)}]: dozwolone na {fraction * 100:0.#}% powierzchni, " +
                                      $"ok. {estimate:N0} miejsc na planecie (fragment ~{chunkSize:0} m)");
                        summary += $", {string.Join("+", objects.Distinct())} {fraction * 100:0.#}%";
                    }
                }
                Melon<FloraMod>.Logger.Msg($"Złoża: {summary}");
            }

            string dir2 = Path.Combine(MelonEnvironment.UserDataDirectory, "Flora");
            Directory.CreateDirectory(dir2);
            string path = Path.Combine(dir2, "planet_resources.txt");
            File.WriteAllText(path, sb.ToString());
            Melon<FloraMod>.Logger.Msg($"Skan złóż zapisany: {path}");
        }

        // Te same warunki co w PlanetHotspotsEnvironmentGeneration.EvaluateSpot (bez odległości od innych złóż).
        private static bool Allowed(PlanetHotspotsPlacementConfig config, Vector3 dir, float height, PlanetDenseEnvironment dense)
        {
            if ((config.avoidDenseEnvConstraint || config.seekDenseEnvConstraint) && dense != null &&
                config.denseEnvConfigId < dense.configs.Length)
            {
                float noise = dense.configs[config.denseEnvConfigId].EvaluateNoiseAtPosition(dir);
                if (config.avoidDenseEnvConstraint && noise >= config.denseEnvThreshold)
                {
                    return false;
                }
                if (!config.avoidDenseEnvConstraint && config.seekDenseEnvConstraint && noise <= config.denseEnvThreshold)
                {
                    return false;
                }
            }
            if (config.minElevationConstraint && height < config.minElevation)
            {
                return false;
            }
            if (config.maxElevationConstraint && height > config.maxElevation)
            {
                return false;
            }
            return true;
        }

        private static Dictionary<ulong, TrackedPlanet> GetTrackedPlanets()
        {
            var result = new Dictionary<ulong, TrackedPlanet>();
            foreach (MonoBehaviour tracker in new MonoBehaviour[]
                     {
                         UnityEngine.Object.FindObjectOfType<PlanetsServerTracker>(),
                         UnityEngine.Object.FindObjectOfType<PlanetsClientTracker>()
                     })
            {
                if (tracker == null)
                {
                    continue;
                }
                if (HarmonyLib.Traverse.Create(tracker).Field("_planets").GetValue() is IDictionary planets)
                {
                    foreach (DictionaryEntry entry in planets)
                    {
                        if (entry.Value is TrackedPlanet planet && !result.ContainsKey((ulong)entry.Key))
                        {
                            result[(ulong)entry.Key] = planet;
                        }
                    }
                }
                if (result.Count > 0)
                {
                    break;
                }
            }
            return result;
        }

        private static Vector3 RandomOnSphere(Random random)
        {
            double z = random.NextDouble() * 2 - 1;
            double angle = random.NextDouble() * 2 * Math.PI;
            double r = Math.Sqrt(1 - z * z);
            return new Vector3((float)(r * Math.Cos(angle)), (float)(r * Math.Sin(angle)), (float)z);
        }
    }
}
