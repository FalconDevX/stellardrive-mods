using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MelonLoader;
using Planets;
using Planets.Terrain;
using Planets.Terrain.Evaluation.Config;
using Planets.Terrain.Material;
using UnityEngine;

namespace Water.Terrain
{
    [HarmonyPatch(typeof(PlanetTerrain), "Awake")]
    internal static class LakeTerrainPatch
    {
        internal const float SeaLevel = 64f;
        private const int BedStepId = 9001;
        private const int MaxMaterials = 4;
        private static readonly string[] Preferred = { "mud", "silt", "clay", "gravel", "pebble", "moss", "dirt", "soil", "wet", "swamp" };

        private static void Postfix(PlanetTerrain __instance)
        {
            var planet = __instance.GetComponent<Planet>();
            if (planet == null || planet.Id != 4 || !WaterMod.Enabled.Value || __instance.terrainConfig?.clampSteps == null)
            {
                return;
            }
            float depth = Mathf.Clamp(WaterMod.LakeDepth.Value, 0f, 60f);
            ClampStep lakeClamp = null;
            foreach (var clamp in __instance.terrainConfig.clampSteps)
            {
                if (Mathf.Abs(clamp.min - SeaLevel) < 1f)
                {
                    clamp.min = SeaLevel - depth;
                    lakeClamp = lakeClamp ?? clamp;
                }
            }
            if (lakeClamp != null)
            {
                try
                {
                    OceanTerrain.Apply(__instance, lakeClamp);
                }
                catch (Exception e)
                {
                    Melon<WaterMod>.Logger.Warning($"Oceans skipped: {e.Message}");
                }
            }
            Melon<WaterMod>.Logger.Msg($"Lakes on {planet.name}: bottom down to {SeaLevel - depth:0} m (water at {SeaLevel} m)");
            try
            {
                LakeBed(__instance);
            }
            catch (Exception e)
            {
                Melon<WaterMod>.Logger.Warning($"Lake bed texture skipped: {e.Message}");
            }
        }

        private static void LakeBed(PlanetTerrain terrain)
        {
            TerrainMaterialConfig[] materials = terrain.materials ?? new TerrainMaterialConfig[0];
            TerrainConfig config = terrain.terrainConfig;
            var log = Melon<WaterMod>.Logger;
            for (int i = 0; i < materials.Length; i++)
            {
                Texture2D a = materials[i].albedoTexture;
                log.Msg($"  terrain material {i}: {a?.name} {a?.width}x{a?.height} {a?.graphicsFormat}, tile {materials[i].tile}, color {materials[i].albedoColor}");
            }
            foreach (ColorElevationStep step in config.colorElevationSteps ?? new ColorElevationStep[0])
            {
                log.Msg($"  color step {step.inputId}->{step.outputId}: below {step.bellowThreshold} smooth {step.smoothness} -> material {step.materialIndex}");
            }
            string choice = (WaterMod.LakeBedTexture.Value ?? "").Trim();
            if (choice.Equals("off", StringComparison.OrdinalIgnoreCase) || materials.Length == 0)
            {
                return;
            }
            if (materials.Length >= MaxMaterials)
            {
                log.Msg($"Lake bed: planet already uses {materials.Length} terrain materials, keeping its texture");
                return;
            }
            List<TerrainGenerationStep> steps = AllSteps(config);
            if (steps.Any(s => s.outputId == BedStepId || s.inputId == BedStepId))
            {
                return;
            }
            TerrainGenerationStep last = steps.FirstOrDefault(s => s.outputId == 100);
            if (last == null)
            {
                return;
            }

            TerrainMaterialConfig sand = OwnSand(materials, choice);
            bool fromOtherPlanet = false;
            TerrainMaterialConfig source = sand;
            if (sand == null)
            {
                fromOtherPlanet = TryPick(materials, choice, out source);
                if (!fromOtherPlanet)
                {
                    source = materials[0];
                }
            }
            var bed = new TerrainMaterialConfig
            {
                albedoTexture = source.albedoTexture,
                albedoColor = sand != null ? sand.albedoColor : fromOtherPlanet ? new Color(0.82f, 0.86f, 0.78f) : new Color(0.6f, 0.68f, 0.58f),
                normalMap = source.normalMap,
                roughnessTexture = source.roughnessTexture,
                tile = source.tile,
                footstepSoundParam = source.footstepSoundParam,
            };
            int index = materials.Length;
            terrain.materials = materials.Concat(new[] { bed }).ToArray();
            last.outputId = BedStepId;
            var bedStep = new ColorElevationStep { inputId = BedStepId, outputId = 100, bellowThreshold = SeaLevel + 0.8f, smoothness = 1.5f, materialIndex = index };
            config.colorElevationSteps = (config.colorElevationSteps ?? new ColorElevationStep[0]).Concat(new[] { bedStep }).ToArray();
            foreach (ColorElevationStep step in config.colorElevationSteps)
            {
                Texture2D a = step != bedStep && step.materialIndex < materials.Length ? materials[step.materialIndex].albedoTexture : null;
                if (a != null && a.name.IndexOf("ice", StringComparison.OrdinalIgnoreCase) >= 0 && step.bellowThreshold < SeaLevel + 3f)
                {
                    log.Msg($"Lake bed: shore step {step.inputId}->{step.outputId} ({a.name} below {step.bellowThreshold}) now uses the lake bed sand");
                    step.materialIndex = index;
                    step.smoothness = Mathf.Max(step.smoothness, 1f);
                }
            }
            log.Msg($"Lake bed: material {index} = {source.albedoTexture?.name}" + (sand != null ? " (wet sand)" : fromOtherPlanet ? " (from another planet)" : " (tinted copy)"));
        }

        private static TerrainMaterialConfig OwnSand(TerrainMaterialConfig[] materials, string choice)
        {
            string word = string.IsNullOrEmpty(choice) || choice.Equals("auto", StringComparison.OrdinalIgnoreCase) ? "sand" : choice;
            return materials.FirstOrDefault(m => m?.albedoTexture != null && m.albedoTexture.name.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        internal static List<TerrainGenerationStep> AllSteps(TerrainConfig c)
        {
            var list = new List<TerrainGenerationStep>();
            if (c.noiseSteps != null) list.AddRange(c.noiseSteps);
            if (c.clampSteps != null) list.AddRange(c.clampSteps);
            if (c.absSteps != null) list.AddRange(c.absSteps);
            if (c.craterSteps != null) list.AddRange(c.craterSteps);
            if (c.colorElevationSteps != null) list.AddRange(c.colorElevationSteps);
            if (c.addSteps != null) list.AddRange(c.addSteps);
            return list;
        }

        private static bool TryPick(TerrainMaterialConfig[] own, string choice, out TerrainMaterialConfig picked)
        {
            picked = null;
            Texture2D reference = own[0].albedoTexture;
            Texture normal = own.FirstOrDefault(m => m.normalMap != null)?.normalMap;
            Texture2D rough = own.FirstOrDefault(m => m.roughnessTexture != null)?.roughnessTexture;
            if (reference == null)
            {
                return false;
            }
            var used = new HashSet<Texture2D>(own.Select(m => m.albedoTexture));
            var candidates = new List<TerrainMaterialConfig>();
            foreach (PlanetTerrain other in Resources.FindObjectsOfTypeAll<PlanetTerrain>())
            {
                foreach (TerrainMaterialConfig m in other.materials ?? new TerrainMaterialConfig[0])
                {
                    Texture2D a = m?.albedoTexture;
                    if (a == null || used.Contains(a) || candidates.Any(c => c.albedoTexture == a))
                    {
                        continue;
                    }
                    if (a.width != reference.width || a.height != reference.height || a.graphicsFormat != reference.graphicsFormat || a.mipmapCount != reference.mipmapCount)
                    {
                        continue;
                    }
                    if (normal != null && (m.normalMap == null || m.normalMap.graphicsFormat != normal.graphicsFormat || m.normalMap.width != reference.width))
                    {
                        continue;
                    }
                    if (rough != null && (m.roughnessTexture == null || m.roughnessTexture.graphicsFormat != rough.graphicsFormat || m.roughnessTexture.width != reference.width))
                    {
                        continue;
                    }
                    candidates.Add(m);
                }
            }
            Melon<WaterMod>.Logger.Msg($"Lake bed candidates: {string.Join(", ", candidates.Select(c => c.albedoTexture.name))}");
            if (candidates.Count == 0)
            {
                return false;
            }
            if (!string.IsNullOrEmpty(choice) && !choice.Equals("auto", StringComparison.OrdinalIgnoreCase))
            {
                picked = candidates.FirstOrDefault(c => c.albedoTexture.name.IndexOf(choice, StringComparison.OrdinalIgnoreCase) >= 0);
                if (picked != null)
                {
                    return true;
                }
            }
            foreach (string word in Preferred)
            {
                picked = candidates.FirstOrDefault(c => c.albedoTexture.name.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0);
                if (picked != null)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
