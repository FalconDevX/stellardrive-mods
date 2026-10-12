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
    internal static class PlanetResourceScan
    {
        private const int Samples = 3000;
        private const int TriesPerChunk = 100;

        internal static IEnumerator ScanAfterDelay(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            try
            {
                Scan();
            }
            catch (Exception e)
            {
                Melon<FloraMod>.Logger.Warning($"Deposit scan failed: {e}");
            }
        }

        private static void Scan()
        {
            var calculator = ServiceLocator.GetService<PlanetTerrainPointCpuCalculator>();
            Dictionary<ulong, TrackedPlanet> tracked = GetTrackedPlanets();
            if (calculator == null || tracked.Count == 0)
            {
                Melon<FloraMod>.Logger.Warning("Deposit scan: no planets or terrain calculator");
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
                sb.AppendLine($"# {planet.name} (id {planet.Id}): radius {planet.Radius / 1000f:0} km, terrain from {min:0} to {max:0} m, " +
                              $"vegetation: {(dense != null ? dense.configs.Length + " layers" : "none")}");
                string summary = $"{planet.name}: terrain {min:0}..{max:0} m";

                if (hotspots?.hotspotsPlacement == null || hotspots.hotspotsPlacement.Count == 0)
                {
                    sb.AppendLine("  no deposits in the configuration");
                    summary += ", no deposits";
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
                        sb.AppendLine($"  [{string.Join(", ", objects)}]: allowed on {fraction * 100:0.#}% of the surface, " +
                                      $"about {estimate:N0} spots on the planet (chunk ~{chunkSize:0} m)");
                        summary += $", {string.Join("+", objects.Distinct())} {fraction * 100:0.#}%";
                    }
                }
                Melon<FloraMod>.Logger.Msg($"Deposits: {summary}");
            }

            string dir2 = Path.Combine(MelonEnvironment.UserDataDirectory, "Flora");
            Directory.CreateDirectory(dir2);
            string path = Path.Combine(dir2, "planet_resources.txt");
            File.WriteAllText(path, sb.ToString());
            Melon<FloraMod>.Logger.Msg($"Deposit scan saved: {path}");
        }

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
