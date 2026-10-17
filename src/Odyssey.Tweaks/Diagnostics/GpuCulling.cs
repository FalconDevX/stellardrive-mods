using System;
using System.Reflection;
using MelonLoader;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Odyssey.Diagnostics
{
    internal static class GpuCulling
    {
        private static bool _applied, _reported;
        private static float _reportAt;
        private static GPUResidentDrawerMode _mode;
        private static bool _occlusion;

        internal static void Update()
        {
            bool wanted = TweaksMod.GpuOcclusionCulling.Value;
            if (wanted == _applied)
            {
                if (_applied && !_reported && Time.unscaledTime >= _reportAt) Report();
                return;
            }
            var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp == null)
            {
                return;
            }
            var log = Melon<TweaksMod>.Logger;
            try
            {
                if (wanted)
                {
                    _mode = urp.gpuResidentDrawerMode;
                    _occlusion = urp.gpuResidentDrawerEnableOcclusionCullingInCameras;
                    log.Msg($"[gpu culling] turning on (was: drawer {_mode}, occlusion {_occlusion}, rendering path {RenderingPath(urp)})");
                    urp.gpuResidentDrawerMode = GPUResidentDrawerMode.InstancedDrawing;
                    urp.gpuResidentDrawerEnableOcclusionCullingInCameras = true;
                    _reported = false;
                    _reportAt = Time.unscaledTime + 3f;
                }
                else
                {
                    urp.gpuResidentDrawerMode = _mode;
                    urp.gpuResidentDrawerEnableOcclusionCullingInCameras = _occlusion;
                    log.Msg("[gpu culling] turned off");
                }
                Reinitialize();
            }
            catch (Exception e)
            {
                log.Warning($"[gpu culling] could not change: {e.Message}");
            }
            _applied = wanted;
        }

        private static void Report()
        {
            _reported = true;
            bool drawer = Call("IsGPUResidentDrawerEnabled");
            bool occlusion = Call("IsInstanceOcclusionCullingEnabled");
            string text = drawer && occlusion ? "GPU culling is on" : drawer ? "GPU drawing is on, but occlusion culling is not supported here" : "GPU culling is not supported by this game build - nothing changed";
            Melon<TweaksMod>.Logger.Msg($"[gpu culling] resident drawer {drawer}, occlusion culling {occlusion}");
            CoreMod.ShowMessage(text, 6f);
        }

        internal static string Status() => $"GPU culling: setting {TweaksMod.GpuOcclusionCulling.Value}, resident drawer {Call("IsGPUResidentDrawerEnabled")}, occlusion {Call("IsInstanceOcclusionCullingEnabled")}";

        private static string RenderingPath(UniversalRenderPipelineAsset urp)
        {
            try
            {
                object renderer = urp.GetRenderer(0);
                object mode = renderer?.GetType().GetProperty("renderingModeActual", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(renderer);
                return mode?.ToString() ?? "unknown";
            }
            catch (Exception)
            {
                return "unknown";
            }
        }

        private static Type Drawer => Type.GetType("UnityEngine.Rendering.GPUResidentDrawer, Unity.RenderPipelines.GPUDriven.Runtime")
            ?? Type.GetType("UnityEngine.Rendering.GPUResidentDrawer, Unity.RenderPipelines.Core.Runtime");

        private static void Reinitialize()
        {
            MethodInfo m = Drawer?.GetMethod("Reinitialize", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            m?.Invoke(null, null);
        }

        private static bool Call(string name)
        {
            try
            {
                MethodInfo m = Drawer?.GetMethod(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                return m != null && m.Invoke(null, null) is bool b && b;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
