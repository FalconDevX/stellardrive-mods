using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Core.Values;
using MelonLoader;
using Odyssey.Server;
using Planets.Interface.Services;
using Ships;
using Ships.Grid.Model;
using Ships.Interface.Model.State;
using Ships.Interface.Services;
using Ships.Services;
using UnityEngine;
using WorldTracking.Interface.Model.Spaces;
using WorldTracking.Interface.Services;
using Object = UnityEngine.Object;

namespace Odyssey.Diagnostics
{
    internal static class ReentryShow
    {
        private sealed class Scene
        {
            public ShipsServerTracker Ships;
            public ISpacesServerProvider Spaces;
            public IShipsClientProvider ShipsClient;
            public TrackedShipServer Ship;
            public Camera Main, Shot;
            public float Radius, Height, Orbital;
            public Type Outline;
            public readonly List<Canvas> Hidden = new List<Canvas>();
        }

        internal static IEnumerator Run()
        {
            var log = Melon<DevMod>.Logger;
            var scene = new Scene
            {
                Main = Camera.main,
                Ships = ServiceCache.Get<ShipsServerTracker>(),
                Spaces = ServiceCache.Get<ISpacesServerProvider>(),
                ShipsClient = ServiceCache.Get<IShipsClientProvider>(),
                Outline = HarmonyLib.AccessTools.TypeByName("Outline"),
            };
            var planets = ServiceCache.Get<IPlanetsServerProvider>();
            if (scene.Main == null || scene.Ships == null || scene.Spaces == null || planets == null || scene.ShipsClient == null)
            {
                log.Msg("[reentry] services missing");
                yield break;
            }
            scene.Ship = scene.Ships.Ships.OrderByDescending(s => s.PartCount).FirstOrDefault();
            if (scene.Ship == null || !(scene.Spaces.GetSpace(scene.Ship.PhysicsState.SpaceId) is PlanetSurfaceSpace space))
            {
                log.Msg("[reentry] no ship on a planet");
                yield break;
            }
            var props = planets.GetPlanetProps(space.PlanetId);
            scene.Radius = (float)props.Radius;
            scene.Height = props.PhysicalProperties.atmosphereProperties.atmosphereHeight;
            scene.Orbital = Mathf.Sqrt(props.PhysicalProperties.gravityInG * 9.81f * scene.Radius);
            log.Msg($"[reentry] ship {scene.Ship.Id} ({scene.Ship.PartCount} parts): planet radius {scene.Radius:0} m, atmosphere {scene.Height:0} m, orbital speed {scene.Orbital:0} m/s");

            MelonPreferences_Entry<bool> fps = MelonPreferences.GetEntry<bool>("FpsCounter", "ShowFps");
            bool fpsShown = fps != null && fps.Value;
            if (fps != null) fps.Value = false;
            scene.Shot = new GameObject("ShowCam").AddComponent<Camera>();
            scene.Shot.CopyFrom(scene.Main);
            scene.Shot.fieldOfView = 50f;
            scene.Shot.farClipPlane = Mathf.Max(scene.Main.farClipPlane, 200000f);
            var data = scene.Shot.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            var mainData = scene.Main.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (mainData != null) data.renderPostProcessing = mainData.renderPostProcessing;
            Showcase.Observer = scene.Shot.transform;
            Material sky = RenderSettings.skybox;
            log.Msg($"[reentry] camera clear {scene.Main.clearFlags}, background {scene.Main.backgroundColor}, skybox {(sky != null ? sky.name + " / " + sky.shader?.name : "none")}, ambient {RenderSettings.ambientMode} {RenderSettings.ambientSkyColor}");
            scene.Shot.clearFlags = CameraClearFlags.SolidColor;
            scene.Shot.backgroundColor = new Color(0.01f, 0.012f, 0.02f, 1f);

            yield return Reentry(scene, log);
            yield return Space(scene, log);

            HydrogenThrusterServer.ForcedStrength = -1f;
            foreach (Canvas c in scene.Hidden) if (c != null) c.enabled = true;
            if (fps != null) fps.Value = fpsShown;
            Showcase.Observer = null;
            scene.Main.enabled = true;
            Object.Destroy(scene.Shot.gameObject);
        }

        private static IEnumerator Space(Scene scene, MelonLogger.Instance log)
        {
            Place(scene, scene.Height * 8f, 0f, 150f);
            HydrogenThrusterServer.ForcedStrength = 1f;
            log.Msg("[space] ship above the atmosphere, all hydrogen engines at full thrust");
            float[] times = { 3f, 4.5f, 6f, 7.5f, 9f, 10.5f };
            yield return Shoot(scene, log, "space", times, 11f, (visual, bounds, view, flow) =>
            {
                float s = bounds.HighestExtent;
                Vector3 c = bounds.Center;
                Vector3 engines = new Vector3(c.x, c.y, bounds.UpperCoord.z);
                switch (view % 6)
                {
                    case 0: return (visual.TransformPoint(c + new Vector3(-1.1f * s, 0.45f * s, 1.5f * s)), visual.TransformPoint(engines - new Vector3(0f, 0f, 0.4f * s)));
                    case 1: return (visual.TransformPoint(engines + new Vector3(-0.35f * s, 0.12f * s, 0.6f * s)), visual.TransformPoint(engines));
                    case 2: return (visual.TransformPoint(c + new Vector3(1.9f * s, 0.15f * s, 0.3f * s)), visual.TransformPoint(c - new Vector3(0f, 0.6f * s, 0f)));
                    case 3: return (visual.TransformPoint(c + new Vector3(0.6f * s, -0.55f * s, -1.5f * s)), visual.TransformPoint(engines));
                    case 4: return (visual.TransformPoint(engines + new Vector3(0.9f * s, -0.3f * s, 1.1f * s)), visual.TransformPoint(c));
                    default: return (visual.TransformPoint(c + new Vector3(-0.4f * s, 1.3f * s, 0.9f * s)), visual.TransformPoint(engines - new Vector3(0f, 0f, 0.6f * s)));
                }
            });
            HydrogenThrusterServer.ForcedStrength = -1f;
        }

        private static IEnumerator Reentry(Scene scene, MelonLogger.Instance log)
        {
            MelonPreferences_Entry<bool> debug = MelonPreferences.GetEntry<bool>("Reentry", "Debug");
            if (debug != null) debug.Value = true;
            Place(scene, scene.Height * 0.95f, 12f, scene.Orbital * 0.95f);
            log.Msg("[reentry] ship nose first into the top of the atmosphere");
            float[] times = { 1.5f, 2.5f, 3.5f, 4.5f, 5.5f, 6.5f };
            yield return Shoot(scene, log, "reentry", times, 7f, (visual, bounds, view, flow) =>
            {
                float s = bounds.HighestExtent;
                Vector3 c = visual.TransformPoint(bounds.Center);
                Vector3 side = Vector3.Cross(flow, visual.up).normalized;
                Vector3 offset = (view % 3) == 0 ? -flow * 1.5f + visual.up * 0.8f + side * 0.6f
                    : (view % 3) == 1 ? side * 1.6f + visual.up * 0.25f + flow * 0.35f
                    : flow * 1.7f + visual.up * 0.45f - side * 0.9f;
                Vector3 target = (view % 3) == 2 ? c : c + flow * s * 0.25f;
                return (c + offset * s, target);
            });
            if (debug != null) debug.Value = false;
        }

        private static void Place(Scene scene, float altitude, float descentDegrees, float speed)
        {
            ShipPhysicsState state = scene.Ship.PhysicsState;
            if (!(scene.Spaces.GetSpace(state.SpaceId) is PlanetSurfaceSpace space))
            {
                return;
            }
            Vector3Double planetPos = space.Center + (Vector3Double)state.Position;
            Vector3 up = (Vector3)planetPos.Normalized;
            Vector3 start = (Vector3)(planetPos.Normalized * (scene.Radius + altitude) - space.Center);
            Vector3 nose = Vector3.ProjectOnPlane(state.Rotation * Vector3.back, up).normalized;
            Quaternion level = Quaternion.LookRotation(-nose, up);
            float angle = descentDegrees * Mathf.Deg2Rad;
            Vector3 velocity = (nose * Mathf.Cos(angle) - up * Mathf.Sin(angle)) * speed;
            scene.Ship.SetPhysicsState(new ShipPhysicsState(state.SpaceId, start, level, velocity));
        }

        private static IEnumerator Shoot(Scene scene, MelonLogger.Instance log, string name, float[] times, float duration,
            Func<Transform, CoordBounds, int, Vector3, (Vector3 position, Vector3 target)> view)
        {
            float begin = Time.time, nextLog = 0f;
            int shot = 0;
            Vector3 last = Vector3.zero, flow = Vector3.forward;
            bool hasLast = false;
            while (Time.time - begin < duration)
            {
                float t = Time.time - begin;
                ShipPhysicsState now = scene.Ship.PhysicsState;
                if (t >= nextLog)
                {
                    nextLog = t + 1f;
                    float altitude = scene.Spaces.GetSpace(now.SpaceId) is PlanetSurfaceSpace s ? (float)((s.Center + (Vector3Double)now.Position).Magnitude - scene.Radius) : float.NaN;
                    log.Msg($"[{name}] t {t:0.0} s: altitude {altitude:0} m, speed {now.Velocity.magnitude:0} m/s, frame {Time.unscaledDeltaTime * 1000f:0.0} ms");
                }
                HideOverlays(scene);
                if (scene.ShipsClient.TryGetShipVisualTransform(scene.Ship.Id, out Transform visual))
                {
                    if (hasLast && (visual.position - last).sqrMagnitude > 0.01f && (visual.position - last).magnitude < 500f)
                    {
                        flow = Vector3.Lerp(flow, (visual.position - last).normalized, 0.2f).normalized;
                    }
                    last = visual.position;
                    hasLast = true;
                    var (position, target) = view(visual, scene.Ship.GetHullCoordBounds(), shot, flow);
                    scene.Shot.transform.position = position;
                    scene.Shot.transform.rotation = Quaternion.LookRotation(target - position, visual.up);
                    scene.Main.enabled = false;
                }
                if (shot < times.Length && t >= times[shot])
                {
                    string path = Path.Combine(DevTools.OutDir, $"{name}{shot}.png");
                    ScreenCapture.CaptureScreenshot(path);
                    log.Msg($"[{name}] shot {shot} at {t:0.0} s: {path}");
                    shot++;
                }
                yield return null;
            }
        }

        private static void HideOverlays(Scene scene)
        {
            foreach (Canvas c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (c.enabled)
                {
                    c.enabled = false;
                    scene.Hidden.Add(c);
                }
            }
            if (scene.Outline != null)
            {
                foreach (Object o in Object.FindObjectsByType(scene.Outline, FindObjectsSortMode.None))
                {
                    Object.Destroy(o);
                }
            }
        }
    }
}
