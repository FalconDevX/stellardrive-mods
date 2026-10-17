using System;
using System.Linq;
using HarmonyLib;
using MelonLoader;
using Planets.Terrain.Evaluation.Services;
using Planets.Terrain.Model;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Water.Surface;

namespace Water.Terrain
{
    [HarmonyPatch(typeof(PlanetTerrainTextureGpuCalculator), nameof(PlanetTerrainTextureGpuCalculator.ComputeTerrain))]
    internal static class TerrainBlurPatch
    {
        private const int ShadeRadius = 6;
        private const int MaterialRadius = 2;

        private static ComputeShader _shader;
        private static bool _failed;
        private static int _single, _color;
        private static RenderTexture _tempSingle, _tempColor;

        private static void Postfix(Chunk[] chunks, TerrainComputeTextureResults __result, int ____texRes)
        {
            if (_failed || !WaterMod.Enabled.Value || chunks == null || chunks.Length == 0)
            {
                return;
            }
            try
            {
                if (!Ensure())
                {
                    return;
                }
                int res = ____texRes;
                int count = chunks.Length;
                if (__result.AmbientOcclusionMapTextureRef is RenderTexture shade)
                {
                    Allocate(ref _tempSingle, res, count, GraphicsFormat.R32_SFloat);
                    Blur(_single, "SourceSingle", "TargetSingle", shade, _tempSingle, res, count, ShadeRadius);
                }
                if (__result.MaterialMapTextureRef is RenderTexture materials)
                {
                    Allocate(ref _tempColor, res, count, GraphicsFormat.R16G16B16A16_SFloat);
                    Blur(_color, "SourceColor", "TargetColor", materials, _tempColor, res, count, MaterialRadius);
                }
            }
            catch (Exception e)
            {
                _failed = true;
                Melon<WaterMod>.Logger.Warning($"Terrain edge smoothing disabled: {e.Message}");
            }
        }

        private static void Blur(int kernel, string source, string target, RenderTexture map, RenderTexture temp, int res, int count, int radius)
        {
            _shader.SetInt("TexRes", res);
            _shader.SetInt("Radius", radius);
            _shader.SetInts("Direction", 1, 0);
            _shader.SetTexture(kernel, source, map);
            _shader.SetTexture(kernel, target, temp);
            Dispatch(kernel, res, count);
            _shader.SetInts("Direction", 0, 1);
            _shader.SetTexture(kernel, source, temp);
            _shader.SetTexture(kernel, target, map);
            Dispatch(kernel, res, count);
        }

        private static void Dispatch(int kernel, int res, int count)
        {
            int groups = (res + 7) / 8;
            _shader.Dispatch(kernel, groups, groups, count);
        }

        private static void Allocate(ref RenderTexture texture, int res, int count, GraphicsFormat format)
        {
            if (texture != null && texture.width == res && texture.volumeDepth >= count)
            {
                return;
            }
            if (texture != null)
            {
                texture.Release();
                UnityEngine.Object.Destroy(texture);
            }
            var desc = new RenderTextureDescriptor(res, res, format, 0)
            {
                dimension = TextureDimension.Tex2DArray,
                volumeDepth = count,
                enableRandomWrite = true,
                useMipMap = false,
                msaaSamples = 1,
            };
            texture = new RenderTexture(desc) { name = "TerrainBlurTemp" };
            texture.Create();
            UnityEngine.Object.DontDestroyOnLoad(texture);
        }

        private static bool Ensure()
        {
            if (_shader != null)
            {
                return true;
            }
            _shader = WaterMaterial.Bundle()?.LoadAllAssets<ComputeShader>().FirstOrDefault(s => s.name == "TerrainBlur");
            if (_shader == null || !SystemInfo.supportsComputeShaders)
            {
                _failed = true;
                Melon<WaterMod>.Logger.Warning("Terrain edge smoothing: compute shader not available");
                return false;
            }
            _single = _shader.FindKernel("BlurSingle");
            _color = _shader.FindKernel("BlurColor");
            Melon<WaterMod>.Logger.Msg("Water: smoothing forest shade and terrain material edges");
            return true;
        }
    }
}
