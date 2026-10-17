using System;
using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using Planets.Terrain;
using Planets.Terrain.Evaluation.Config;
using Planets.Terrain.Model;
using Planets.Terrain.Utils;
using UnityEngine;

namespace Water.Terrain
{
    internal static class OceanTerrain
    {
        private const int FirstStepId = 9100;
        private const float Frequency = 3f;
        private const float LandSlope = 6000f;
        private const float LandDrop = 800f;
        private const float DeepSlope = 900f;
        private const float SpawnMargin = 0.12f;
        private const float SpawnGuardRadius = 0.06f;
        private const int Samples = 6000;
        private static readonly Vector3 Spawn = new Vector3(0.584f, 0.068f, -0.809f).normalized;

        internal static void Apply(PlanetTerrain terrain, ClampStep lakeClamp)
        {
            float coverage = Mathf.Clamp(WaterMod.OceanCoverage.Value, 0f, 0.6f);
            if (coverage <= 0f)
            {
                return;
            }
            TerrainConfig config = terrain.terrainConfig;
            List<TerrainGenerationStep> steps = LakeTerrainPatch.AllSteps(config);
            if (steps.Any(s => s.outputId >= FirstStepId && s.outputId < FirstStepId + 20))
            {
                return;
            }
            if (!OnMainChain(steps, lakeClamp))
            {
                Melon<WaterMod>.Logger.Warning("Oceans skipped: the lake level step is not on the main terrain chain");
                return;
            }

            NoiseConfig noise = new NoiseConfig { frequency = Frequency, octaves = 4, gain = 0.5f, lacunarity = 2f, strength = 1f };
            Vector3[] probe = SampleDirections();
            float[] values = new float[probe.Length];
            float threshold = 0f;
            int seed = Mathf.Max(1, WaterMod.OceanSeed.Value);
            bool safe = false;
            for (int attempt = 0; attempt < 200 && !safe; attempt++, seed++)
            {
                noise.center = Center(seed);
                for (int i = 0; i < probe.Length; i++) values[i] = NoiseCpuCalculator.EvaluateNoise(noise, probe[i]);
                Array.Sort(values);
                threshold = values[Mathf.Clamp((int)(coverage * values.Length), 0, values.Length - 1)];
                safe = SpawnIsLand(noise, threshold);
            }
            seed--;
            if (!safe)
            {
                Melon<WaterMod>.Logger.Warning("Oceans skipped: no layout keeps the landing area dry");
                return;
            }

            float depth = Mathf.Clamp(WaterMod.OceanDepth.Value, 0f, 200f);
            int id = FirstStepId;
            var noises = new List<NoiseStep>();
            var clamps = new List<ClampStep>();
            var adds = new List<AddStep>();

            int landMask = Mask(noise, threshold, LandSlope, LandDrop, 40f, ref id, noises, clamps);
            var landAdd = new AddStep { inputId = lakeClamp.inputId, inputId2 = landMask, outputId = id++ };
            lakeClamp.inputId = landAdd.outputId;
            adds.Add(landAdd);

            if (depth > 0f)
            {
                int deepMask = Mask(noise, threshold, DeepSlope, depth, 8f, ref id, noises, clamps);
                int clampOut = id++;
                var deepAdd = new AddStep { inputId = clampOut, inputId2 = deepMask, outputId = lakeClamp.outputId };
                lakeClamp.outputId = clampOut;
                adds.Add(deepAdd);
            }

            config.noiseSteps = (config.noiseSteps ?? new NoiseStep[0]).Concat(noises).ToArray();
            config.clampSteps = (config.clampSteps ?? new ClampStep[0]).Concat(clamps).ToArray();
            config.addSteps = (config.addSteps ?? new AddStep[0]).Concat(adds).ToArray();
            Melon<WaterMod>.Logger.Msg($"Oceans: about {coverage * 100f:0}% of the planet, layout {seed}, up to {depth:0} m deeper than lakes, landing area stays dry");
        }

        private static int Mask(NoiseConfig noise, float threshold, float slope, float drop, float smoothness, ref int id, List<NoiseStep> noises, List<ClampStep> clamps)
        {
            var constant = new ClampStep { inputId = -1, outputId = id++, min = -threshold * slope, max = -threshold * slope, smoothness = 0f };
            NoiseConfig scaled = noise;
            scaled.strength = slope;
            var wave = new NoiseStep { inputId = constant.outputId, outputId = id++, noiseConfig = scaled };
            var limit = new ClampStep { inputId = wave.outputId, outputId = id++, min = -drop, max = 0f, smoothness = smoothness };
            clamps.Add(constant);
            noises.Add(wave);
            clamps.Add(limit);
            return limit.outputId;
        }

        private static bool OnMainChain(List<TerrainGenerationStep> steps, TerrainGenerationStep target)
        {
            TerrainGenerationStep step = steps.FirstOrDefault(s => s.outputId == 100);
            for (int guard = 0; step != null && guard < 256; guard++)
            {
                if (step == target) return true;
                int input = step.inputId;
                step = steps.FirstOrDefault(s => s.outputId == input);
            }
            return false;
        }

        private static bool SpawnIsLand(NoiseConfig noise, float threshold)
        {
            Vector3 a = Vector3.Cross(Spawn, Vector3.up).normalized;
            Vector3 b = Vector3.Cross(Spawn, a);
            if (NoiseCpuCalculator.EvaluateNoise(noise, Spawn) < threshold + SpawnMargin) return false;
            for (int ring = 1; ring <= 3; ring++)
            {
                float r = SpawnGuardRadius * ring / 3f;
                for (int k = 0; k < 12; k++)
                {
                    float angle = k * Mathf.PI / 6f;
                    Vector3 dir = (Spawn + (a * Mathf.Cos(angle) + b * Mathf.Sin(angle)) * r).normalized;
                    if (NoiseCpuCalculator.EvaluateNoise(noise, dir) < threshold + SpawnMargin * 0.5f) return false;
                }
            }
            return true;
        }

        private static Vector3[] SampleDirections()
        {
            var dirs = new Vector3[Samples];
            float golden = Mathf.PI * (3f - Mathf.Sqrt(5f));
            for (int i = 0; i < Samples; i++)
            {
                float y = 1f - 2f * (i + 0.5f) / Samples;
                float r = Mathf.Sqrt(1f - y * y);
                dirs[i] = new Vector3(Mathf.Cos(golden * i) * r, y, Mathf.Sin(golden * i) * r);
            }
            return dirs;
        }

        private static Vector3 Center(int seed)
        {
            var random = new System.Random(seed * 7919 + 17);
            return new Vector3((float)(random.NextDouble() * 200.0 - 100.0), (float)(random.NextDouble() * 200.0 - 100.0), (float)(random.NextDouble() * 200.0 - 100.0));
        }
    }
}
