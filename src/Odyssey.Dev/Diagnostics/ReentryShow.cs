using System.Collections;
using System.IO;
using System.Linq;
using Core.Values;
using MelonLoader;
using Planets.Interface.Services;
using Ships;
using Ships.Interface.Model.State;
using Ships.Interface.Services;
using Ships.Services;
using UnityEngine;
using WorldTracking.Interface.Model.Spaces;
using WorldTracking.Interface.Services;

namespace Odyssey.Diagnostics
{
    internal static class ReentryShow
    {
        private const float StartHeight = 0.85f, SpeedOfOrbit = 0.95f, DescentDegrees = 25f, Duration = 32f;
        private static readonly float[] ShotTimes = { 1.5f, 2.5f, 3.5f, 4.5f, 5.5f, 7f, 9f };

        internal static IEnumerator Run()
        {
            var log = Melon<DevMod>.Logger;
            Camera cam = Camera.main;
            var ships = ServiceCache.Get<ShipsServerTracker>();
            var spaces = ServiceCache.Get<ISpacesServerProvider>();
            var planets = ServiceCache.Get<IPlanetsServerProvider>();
            var shipsClient = ServiceCache.Get<IShipsClientProvider>();
            if (cam == null || ships == null || spaces == null || planets == null || shipsClient == null)
            {
                log.Msg("[reentry] services missing");
                yield break;
            }
            TrackedShipServer ship = ships.Ships.OrderByDescending(s => s.PartCount).FirstOrDefault();
            ShipPhysicsState state = ship != null ? ship.PhysicsState : default;
            if (ship == null || !(spaces.GetSpace(state.SpaceId) is PlanetSurfaceSpace space))
            {
                log.Msg("[reentry] no ship on a planet");
                yield break;
            }
            var props = planets.GetPlanetProps(space.PlanetId);
            float radius = (float)props.Radius;
            float height = props.PhysicalProperties.atmosphereProperties.atmosphereHeight;
            float gravity = props.PhysicalProperties.gravityInG * 9.81f;
            float orbital = Mathf.Sqrt(gravity * radius);
            Vector3Double planetPos = space.Center + (Vector3Double)state.Position;
            Vector3 up = (Vector3)planetPos.Normalized;
            Vector3 start = (Vector3)(planetPos.Normalized * (radius + height * StartHeight) - space.Center);
            Vector3 forward = Vector3.ProjectOnPlane(state.Rotation * Vector3.back, up).normalized;
            float angle = DescentDegrees * Mathf.Deg2Rad;
            Vector3 velocity = (forward * Mathf.Cos(angle) - up * Mathf.Sin(angle)) * orbital * SpeedOfOrbit;
            log.Msg($"[reentry] ship {ship.Id} ({ship.PartCount} parts): planet radius {radius:0} m, atmosphere {height:0} m, orbital speed {orbital:0} m/s; start {height * StartHeight:0} m up at {velocity.magnitude:0} m/s, {DescentDegrees:0} degrees down");
            ship.SetPhysicsState(new ShipPhysicsState(state.SpaceId, start, state.Rotation, velocity));

            MelonPreferences_Entry<bool> debug = MelonPreferences.GetEntry<bool>("Reentry", "Debug");
            if (debug != null) debug.Value = true;
            MelonPreferences_Entry<bool> fps = MelonPreferences.GetEntry<bool>("FpsCounter", "ShowFps");
            bool fpsShown = fps != null && fps.Value;
            if (fps != null) fps.Value = false;

            var shotCam = new GameObject("ReentryCam").AddComponent<Camera>();
            shotCam.CopyFrom(cam);
            shotCam.fieldOfView = 55f;
            shotCam.farClipPlane = Mathf.Max(cam.farClipPlane, 60000f);
            var data = shotCam.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            var mainData = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (mainData != null) data.renderPostProcessing = mainData.renderPostProcessing;
            var hidden = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.enabled).ToList();
            Showcase.Observer = shotCam.transform;

            float begin = Time.time, nextLog = 0f;
            bool profiled = false;
            int shot = 0;
            Vector3 lastVisual = Vector3.zero, flow = Vector3.forward;
            bool hasLast = false;
            while (Time.time - begin < Duration)
            {
                float t = Time.time - begin;
                ShipPhysicsState now = ship.PhysicsState;
                if (t >= nextLog)
                {
                    nextLog = t + 1f;
                    float altitude = spaces.GetSpace(now.SpaceId) is PlanetSurfaceSpace s ? (float)((s.Center + (Vector3Double)now.Position).Magnitude - radius) : float.NaN;
                    log.Msg($"[reentry] t {t:0.0} s: altitude {altitude:0} m, speed {now.Velocity.magnitude:0} m/s, space {now.SpaceId}, frame {Time.unscaledDeltaTime * 1000f:0.0} ms");
                }
                if (shipsClient.TryGetShipVisualTransform(ship.Id, out Transform visual))
                {
                    if (hasLast && (visual.position - lastVisual).sqrMagnitude > 0.01f && (visual.position - lastVisual).magnitude < 500f)
                    {
                        flow = Vector3.Lerp(flow, (visual.position - lastVisual).normalized, 0.2f).normalized;
                    }
                    lastVisual = visual.position;
                    hasLast = true;
                    var bounds = ship.GetHullCoordBounds();
                    Vector3 center = visual.TransformPoint(bounds.Center);
                    float size = bounds.HighestExtent;
                    Vector3 side = Vector3.Cross(flow, visual.up).normalized;
                    if (side.sqrMagnitude < 0.5f) side = visual.right;
                    int view = shot % 3;
                    Vector3 offset = view == 0 ? -flow * 1.5f + visual.up * 0.8f + side * 0.6f
                        : view == 1 ? side * 1.6f + visual.up * 0.25f + flow * 0.35f
                        : flow * 1.7f + visual.up * 0.45f - side * 0.9f;
                    Vector3 target = view == 2 ? center : center + flow * size * 0.25f;
                    shotCam.transform.position = center + offset * size;
                    shotCam.transform.rotation = Quaternion.LookRotation(target - shotCam.transform.position, visual.up);
                    cam.enabled = false;
                    foreach (Canvas c in hidden) if (c != null) c.enabled = false;
                }
                if (!profiled && t >= 10f)
                {
                    profiled = true;
                    FrameProfiler.Start();
                    log.Msg("[reentry] FPS report started");
                }
                if (shot < ShotTimes.Length && t >= ShotTimes[shot])
                {
                    string path = Path.Combine(DevTools.OutDir, $"reentry{shot}.png");
                    ScreenCapture.CaptureScreenshot(path);
                    log.Msg($"[reentry] shot {shot} at {t:0.0} s: {path}");
                    shot++;
                }
                yield return null;
            }
            foreach (Canvas c in hidden) if (c != null) c.enabled = true;
            if (debug != null) debug.Value = false;
            if (fps != null) fps.Value = fpsShown;
            Showcase.Observer = null;
            cam.enabled = true;
            Object.Destroy(shotCam.gameObject);
        }
    }
}
