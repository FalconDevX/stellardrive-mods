using System;
using System.Collections;
using System.IO;
using System.Text;
using MelonLoader;
using MelonLoader.Utils;
using Planets.EnvironmentGeneration;
using Planets.EnvironmentGeneration.Configs;
using UnityEngine;

namespace Flora.Diagnostics
{
    internal static class WorldEnvironmentDump
    {
        internal static IEnumerator DumpAfterDelay(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            try
            {
                Dump();
            }
            catch (Exception e)
            {
                Melon<FloraMod>.Logger.Error("Deposit dump error: " + e);
            }
        }

        private static void Dump()
        {
            var sb = new StringBuilder();
            foreach (PlanetHotspotsEnvironment env in UnityEngine.Object.FindObjectsByType<PlanetHotspotsEnvironment>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                sb.AppendLine($"# Hotspoty: {Path(env.transform)} (configs: {env.hotspotsPlacement?.Count ?? 0})");
                for (int i = 0; i < (env.hotspotsPlacement?.Count ?? 0); i++)
                {
                    PlanetHotspotsPlacementConfig c = env.hotspotsPlacement[i];
                    sb.AppendLine($"## hotspot {i}: level={c.placementLevel} prob={c.spawnProbability} load={c.loadDistance} " +
                                  $"avoidDense={c.avoidDenseEnvConstraint} seekDense={c.seekDenseEnvConstraint}({c.denseEnvConfigId}>{c.denseEnvThreshold}) " +
                                  $"elev={c.minElevationConstraint}:{c.minElevation}-{c.maxElevationConstraint}:{c.maxElevation} minOther={c.minOtherHotspotDistance}");
                    foreach (HotspotObjectsDistributionStep step in c.distributionSteps ?? Array.Empty<HotspotObjectsDistributionStep>())
                    {
                        sb.AppendLine($"- '{step.objectPrefab?.name}' count={step.countMin}-{step.countMax} dist={step.centerDistanceMin}-{step.centerDistanceMax} " +
                                      $"spacing={step.minDistanceBetweenObjects} rot={step.rotationType}");
                        if (step.objectPrefab != null)
                        {
                            DumpVisuals(sb, step.objectPrefab.transform, "    ");
                        }
                    }
                }
            }

            foreach (PlanetStaticEnvironment env in UnityEngine.Object.FindObjectsByType<PlanetStaticEnvironment>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                sb.AppendLine($"# Static: {Path(env.transform)} objects={env.objects?.Count ?? 0}");
                foreach (PlanetStaticObjectPlacementConfig o in env.objects ?? new System.Collections.Generic.List<PlanetStaticObjectPlacementConfig>())
                {
                    sb.AppendLine($"- '{o.prefab?.name}' pos={o.relativePosition} elev={o.elevation} sp={o.onlyInSinglePlayer}");
                }
            }

            string dir = System.IO.Path.Combine(MelonEnvironment.UserDataDirectory, "Flora");
            Directory.CreateDirectory(dir);
            string file = System.IO.Path.Combine(dir, "world_hotspots.txt");
            File.WriteAllText(file, sb.ToString());
            Melon<FloraMod>.Logger.Msg($"Deposit and object dump: {file}");
        }

        private static void DumpVisuals(StringBuilder sb, Transform t, string indent)
        {
            if (t.GetComponent<ParticleSystem>() != null)
            {
                return;
            }
            var mf = t.GetComponent<MeshFilter>();
            var mr = t.GetComponent<MeshRenderer>();
            if (mf != null && mf.sharedMesh != null)
            {
                sb.Append($"{indent}{t.name}: mesh '{mf.sharedMesh.name}' verts={mf.sharedMesh.vertexCount} size={mf.sharedMesh.bounds.size}");
                if (mr != null)
                {
                    foreach (Material mat in mr.sharedMaterials)
                    {
                        if (mat == null) continue;
                        string color = mat.HasProperty("_AlbedoColor") ? mat.GetColor("_AlbedoColor").ToString() : mat.HasProperty("_Color") ? mat.GetColor("_Color").ToString() : "";
                        sb.Append($" | mat '{mat.name}' ({mat.shader.name}) {color}");
                    }
                }
                sb.AppendLine();
            }
            foreach (Transform child in t)
            {
                DumpVisuals(sb, child, indent);
            }
        }

        private static string Path(Transform t)
        {
            return t.parent == null ? t.name : Path(t.parent) + "/" + t.name;
        }
    }
}
