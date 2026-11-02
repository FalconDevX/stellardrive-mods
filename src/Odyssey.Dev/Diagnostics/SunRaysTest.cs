using System.Collections;
using System.IO;
using System.Linq;
using MelonLoader;
using Players.Interface.Model;
using Players.Interface.Values;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Odyssey.Diagnostics
{
    internal static class SunRaysTest
    {
        private sealed class ShotCamera : ICamera
        {
            private readonly Camera _camera;

            public ShotCamera(Camera camera) => _camera = camera;

            public Vector3 Position => _camera.transform.position;

            public Vector3 Direction => _camera.transform.forward;

            public Quaternion Rotation => _camera.transform.rotation;

            public Matrix4x4 ProjectionMatrix => _camera.projectionMatrix;

            public Vector3 WorldToViewport(Vector3 point) => _camera.WorldToViewportPoint(point);
        }

        internal static IEnumerator Run()
        {
            var log = Melon<DevMod>.Logger;
            Camera main = Camera.main;
            Light sun = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l => l.type == LightType.Directional && l.isActiveAndEnabled).OrderByDescending(l => l.intensity).FirstOrDefault();
            MelonPreferences_Entry<bool> force = MelonPreferences.GetEntry<bool>("SunRays", "Force");
            MelonPreferences_Entry<bool> debug = MelonPreferences.GetEntry<bool>("SunRays", "Debug");
            MelonPreferences_Entry<float> frequency = MelonPreferences.GetEntry<float>("SunRays", "Frequency");
            MelonPreferences_Entry<bool> enabled = MelonPreferences.GetEntry<bool>("SunRays", "Enabled");
            if (main == null || sun == null || force == null || debug == null || frequency == null || enabled == null)
            {
                log.Msg($"[sunrays] missing: camera {main != null}, sun {sun != null}, prefs {force != null}");
                yield break;
            }
            debug.Value = true;
            frequency.Value = 1f;
            Vector3 center = Shader.GetGlobalVector("_PlanetPositionWS");
            Vector3 up = (main.transform.position - center).normalized;
            Vector3 toSun = -sun.transform.forward;
            log.Msg($"[sunrays] sun height {Vector3.Dot(up, toSun):0.00}, camera {main.transform.position}, planet {center}");
            foreach (Canvas c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) c.enabled = false;

            Camera shot = new GameObject("SunRaysCam").AddComponent<Camera>();
            shot.CopyFrom(main);
            shot.fieldOfView = 70f;
            var data = shot.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            var mainData = main.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (mainData != null) data.renderPostProcessing = mainData.renderPostProcessing;
            Showcase.Observer = shot.transform;
            Showcase.SetInstancerCamera(shot);
            shot.transform.position = main.transform.position + up * 40f;
            main.enabled = false;
            CameraValue value = Resources.FindObjectsOfTypeAll<CameraValue>().FirstOrDefault();
            ICamera previous = value?.Get();
            value?.Set(new ShotCamera(shot));

            Vector3 flat = Vector3.ProjectOnPlane(toSun, up).normalized;
            if (flat.sqrMagnitude < 0.5f) flat = Vector3.Cross(up, Vector3.right).normalized;
            Vector3 towardSun = (flat + up * 0.25f).normalized;
            Vector3 side = (Vector3.Cross(up, flat) + up * 0.2f).normalized;

            MelonPreferences_Entry<float> intensity = MelonPreferences.GetEntry<float>("SunRays", "Intensity");
            enabled.Value = false;
            yield return new WaitForSecondsRealtime(8f);
            yield return Shoot(shot, towardSun, up, "sunrays_base_sun", log);
            yield return Shoot(shot, side, up, "sunrays_base_side", log);
            enabled.Value = true;
            force.Value = true;
            intensity.Value = 0.1f;
            yield return Shoot(shot, towardSun, up, "sunrays_min_sun", log);
            intensity.Value = 3f;
            yield return Shoot(shot, towardSun, up, "sunrays_max_sun", log);
            yield return Shoot(shot, side, up, "sunrays_max_side", log);
            intensity.Value = 1f;
            yield return Shoot(shot, towardSun, up, "sunrays_on_sun", log);
            enabled.Value = false;
            yield return Shoot(shot, towardSun, up, "sunrays_off_sun", log);
            enabled.Value = true;
            force.Value = false;
            debug.Value = false;
            frequency.Value = 0.6f;
            value?.Set(previous);
            Showcase.Observer = null;
            main.enabled = true;
            Showcase.SetInstancerCamera(main);
            Object.Destroy(shot.gameObject);
        }

        private static IEnumerator Shoot(Camera shot, Vector3 direction, Vector3 up, string name, MelonLogger.Instance log)
        {
            shot.transform.rotation = Quaternion.LookRotation(direction, up);
            yield return new WaitForSecondsRealtime(4f);
            string path = Path.Combine(DevTools.OutDir, name + ".png");
            ScreenCapture.CaptureScreenshot(path);
            log.Msg($"[sunrays] {path}, frame {Time.unscaledDeltaTime * 1000f:0.0} ms");
            yield return new WaitForSecondsRealtime(1f);
        }
    }
}
