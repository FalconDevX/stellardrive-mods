using System;
using System.Linq;
using Flora.Models;
using HarmonyLib;
using MelonLoader;
using Planets.EnvironmentGeneration;
using Planets.EnvironmentGeneration.Configs;
using UnityEngine;

namespace Flora.Patches
{
    [HarmonyPatch(typeof(PlanetDenseEnvironment), "Awake")]
    internal static class AddFloraLayersPatch
    {
        private static readonly Func<object, object> CloneObject =
            AccessTools.MethodDelegate<Func<object, object>>(AccessTools.Method(typeof(object), "MemberwiseClone"));

        private static void Prefix(PlanetDenseEnvironment __instance)
        {
            try
            {
                AddLayers(__instance);
            }
            catch (Exception e)
            {
                Melon<FloraMod>.Logger.Error($"Could not add plants on {__instance.gameObject.name}: {e}");
            }
        }

        private static void AddLayers(PlanetDenseEnvironment env)
        {
            if (!FloraMod.FlowersEnabled.Value || env.configs == null || env.configs.Length < 2 ||
                env.configs.Any(c => c?.objectsSpawn?.Any(s => s.prefab != null && s.prefab.name.StartsWith("Flora")) == true))
            {
                return;
            }

            PlanetDensePlacementConfig trees = env.configs.FirstOrDefault(c => c.hasPhysics && c.objectsSpawn.Length > 0);
            PlanetDensePlacementConfig grass = env.configs.FirstOrDefault(c => !c.hasPhysics && c.alignWithTerrain && c.objectsSpawn.Length > 0);
            if (trees == null || grass == null)
            {
                Melon<FloraMod>.Logger.Msg($"{env.gameObject.name}: no trees/grass - skipping");
                return;
            }

            if (!FloraPrefabs.IsReady)
            {
                Material bark = FindMaterial(trees, "Bark") ?? FindMaterial(trees, null);
                FloraPrefabs.SetTemplate(bark);
                FloraPrefabs.SetGrassTemplate(FindMaterial(grass, null));
                if (!FloraPrefabs.IsReady)
                {
                    Melon<FloraMod>.Logger.Warning("No plant material to copy - skipping");
                    return;
                }
            }

            var layers = env.configs.ToList();
            layers.Add(CreateLayer(grass, FloraPrefabs.CreateFlowers(), FloraMod.FlowerNoiseOffset.Value, FloraMod.FlowerViewRange.Value,
                alignWithTerrain: true, noiseShift: new Vector3(-21.3f, 7.9f, 3.1f), noiseFrequency: FloraMod.FlowerNoiseFrequency.Value));
            env.configs = layers.ToArray();

            Melon<FloraMod>.Logger.Msg($"{env.gameObject.name}: added flower fields " +
                                       $"(tree noise: {Describe(trees)}, grass: {Describe(grass)})");
        }

        private static PlanetDensePlacementConfig CreateLayer(PlanetDensePlacementConfig source, GameObject[] prefabs, float noiseOffset, float viewRange,
            bool alignWithTerrain, Vector3 noiseShift, float noiseFrequency)
        {
            var layer = (PlanetDensePlacementConfig)CloneObject(source);

            Traverse t = Traverse.Create(layer);
            t.Field("_spawnTable").SetValue(null);
            t.Field("_noiseConfigBuffer").SetValue(null);
            layer.OnCalculatedCacheChanged = null;
            layer.OnConfigUpdated = null;
            t.Field("viewRange").SetValue(viewRange);

            layer.objectsSpawn = prefabs.Select(p => new PlanetDensePlacementConfig.DenseObjectPlacementType
            {
                prefab = p,
                probability = 10,
                densityBiasValue = 0.5f,
                densityBiasPower = 0f,
            }).ToArray();

            var noise = layer.noiseConfig;
            noise.center += noiseShift;
            if (noiseFrequency > 0f) noise.frequency = noiseFrequency;
            layer.noiseConfig = noise;
            layer.noiseOffset = noiseOffset;

            layer.alignWithTerrain = alignWithTerrain;
            layer.hasPhysics = false;
            layer.hasSound = false;
            layer.isVisibleFromSpace = false;
            layer.generatesAmbientOcclusion = false;
            return layer;
        }

        private static Material FindMaterial(PlanetDensePlacementConfig config, string nameContains)
        {
            foreach (var spawn in config.objectsSpawn)
            {
                if (spawn.prefab == null) continue;
                foreach (var r in spawn.prefab.GetComponentsInChildren<MeshRenderer>(true))
                {
                    foreach (var m in r.sharedMaterials)
                    {
                        if (m != null && m.shader != null && m.shader.name == "Custom/PlanetFoliage" && (nameContains == null || m.name.Contains(nameContains)))
                        {
                            return m;
                        }
                    }
                }
            }
            return null;
        }

        private static string Describe(PlanetDensePlacementConfig c)
        {
            var n = c.noiseConfig;
            return $"offset={c.noiseOffset} strength={n.strength} freq={n.frequency} oct={n.octaves} level={c.level}";
        }
    }
}
