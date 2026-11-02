using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MelonLoader;
using Planets;
using Planets.Clouds;
using Planets.Interface.Model;
using Planets.Lighting;
using Planets.Services;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using WorldTracking.Interface.Services;
using WorldTracking.Interface.Values;
using Object = UnityEngine.Object;

namespace SunRays
{
    internal static class SunRaysEffect
    {
        private const float FadeSeconds = 10f;
        private const float ReadbackSeconds = 1.5f;
        private const float CoverRadius = 16000f;
        private const float CoverSmoothing = 0.25f;
        private const float WeatherPeriod = 700f;
        private const float BaseGain = 0.5f;
        private const float MaxRange = 45000f;
        private const int MinTexels = 8, MaxTexels = 96;
        private const int MapSize = 384;
        private const float MapSecondsVisible = 0.1f, MapSecondsIdle = 1.5f;
        private const float CloudSpinFactor = 0.09f;

        private static readonly int RayColorId = Shader.PropertyToID("_RayColor");
        private static readonly int SunDirectionId = Shader.PropertyToID("_SunDirection");
        private static readonly int PlanetCenterId = Shader.PropertyToID("_PlanetCenter");
        private static readonly int CloudRadiusId = Shader.PropertyToID("_CloudRadius");
        private static readonly int GroundRadiusId = Shader.PropertyToID("_GroundRadius");
        private static readonly int RangeId = Shader.PropertyToID("_Range");
        private static readonly int HeightScaleId = Shader.PropertyToID("_HeightScale");
        private static readonly int MapSizeId = Shader.PropertyToID("_MapSize");
        private static readonly int SoftnessId = Shader.PropertyToID("_Softness");
        private static readonly int StepsId = Shader.PropertyToID("_Steps");
        private static readonly int AnisotropyId = Shader.PropertyToID("_Anisotropy");
        private static readonly int ReferenceId = Shader.PropertyToID("_Reference");
        private static readonly int StreakUId = Shader.PropertyToID("_StreakU");
        private static readonly int StreakVId = Shader.PropertyToID("_StreakV");
        private static readonly int StreakSizeId = Shader.PropertyToID("_StreakSize");
        private static readonly int ShadowMapId = Shader.PropertyToID("_CloudsShadowMap");
        private static readonly int InverseRotationId = Shader.PropertyToID("_CloudsShadowMapInverseRotationWs");
        private static readonly int MapPositionId = Shader.PropertyToID("_CloudsShadowMapPositionRelativeToPlanetWs");
        private static readonly int ExtentsId = Shader.PropertyToID("_CloudsShadowMapExtentsWs");

        private static readonly Color LowSun = new Color(1f, 0.56f, 0.26f);
        private static readonly Color HighSun = new Color(1f, 0.9f, 0.74f);

        private static GameObject _object;
        private static Material _material;
        private static Light _sun;
        private static float _nextSunSearch, _nextReadback, _nextLog, _nextMap;
        private static float _strength, _cover = -1f, _cameraDepth;
        private static bool _pending, _failed;

        internal static void Clear()
        {
            if (_object != null) Object.Destroy(_object);
            _object = null;
            _sun = null;
            _strength = 0f;
            _cover = -1f;
        }

        internal static void Update()
        {
            if (_failed)
            {
                return;
            }
            try
            {
                Run();
            }
            catch (Exception e)
            {
                _failed = true;
                Hide();
                Melon<SunRaysMod>.Logger.Error($"Sun rays disabled: {e}");
            }
        }

        private static void Hide()
        {
            if (_object != null && _object.activeSelf) _object.SetActive(false);
        }

        private static Camera ActiveCamera()
        {
            Camera cam = Camera.main;
            if (cam != null)
            {
                return cam;
            }
            foreach (Camera c in Camera.allCameras)
            {
                if (c != null && c.enabled && c.targetTexture == null) return c;
            }
            return null;
        }

        private static void Run()
        {
            Camera cam = ActiveCamera();
            var planets = ServiceCache.Get<PlanetsClientTracker>();
            var transpose = ServiceCache.Get<IWorldTransposeClient>();
            var time = ServiceCache.Get<IWorldTimeProviderClient>();
            var tracked = planets != null && ClientOrigin.LocalVisualSpace != null ? planets.GetNearestTrackedPlanetWithAtmosphere(ClientOrigin.LocalVisualSpace) : null;
            Planet planet = tracked?.Planet;
            FindSun();
            if (!SunRaysMod.Enabled.Value || cam == null || transpose == null || time == null || planet == null || _sun == null
                || !tracked.IsAtmosphereReadyToRender || !planet.PhysicalProperties.HasClouds)
            {
                _strength = 0f;
                Hide();
                return;
            }
            AtmosphereProperties air = planet.PhysicalProperties.atmosphereProperties;
            CloudLayerProperties layer = air.cloudLayerProperties;
            Vector3 center = transpose.Position(planet.FixedWorldSpace, ClientOrigin.LocalVisualSpace, Vector3.zero);
            Vector3 relative = cam.transform.position - center;
            float radius = relative.magnitude;
            Vector3 up = relative / Mathf.Max(radius, 1f);
            Vector3 toSun = -_sun.transform.forward;
            float sunHeight = Vector3.Dot(up, toSun);
            float sunGate = Smooth(-0.03f, 0.06f, sunHeight);
            float ceilingGate = 1f - Smooth(layer.cloudShellInnerRadius - 400f, layer.cloudShellInnerRadius + 100f, radius);
            if (!SunRaysMod.Force.Value && sunGate * ceilingGate <= 0f)
            {
                _strength = Mathf.MoveTowards(_strength, 0f, Time.deltaTime / FadeSeconds);
                if (_strength <= 0.002f)
                {
                    _cover = -1f;
                    Hide();
                    return;
                }
            }

            RenderTexture map = ShadowMap(planet);
            UpdateShadowMap(planet, map, transpose, time);
            RequestCover(map, relative);
            float coverGate = _cover < 0f ? 0f : Smooth(0.08f, 0.3f, _cover) * (1f - Smooth(0.8f, 0.97f, _cover));
            float weatherGate = Weather(time.CurrentWorldTime, planet);
            float target = SunRaysMod.Force.Value ? 1f : sunGate * ceilingGate * coverGate * weatherGate * Mathf.Clamp01(air.atmosphereDensity);
            _strength = SunRaysMod.Force.Value ? 1f : Mathf.MoveTowards(_strength, target, Time.deltaTime / FadeSeconds);

            if (SunRaysMod.Debug.Value && Time.unscaledTime >= _nextLog)
            {
                _nextLog = Time.unscaledTime + 3f;
                Melon<SunRaysMod>.Logger.Msg($"cover {_cover:0.00}, sun {sunHeight:0.00} ({sunGate:0.00}), altitude {radius - planet.Radius:0} m of clouds at {layer.cloudShellInnerRadius - planet.Radius:0} m ({ceilingGate:0.00}), cover gate {coverGate:0.00}, weather {weatherGate:0.00}, strength {_strength:0.00} -> {target:0.00}");
            }
            if (_strength <= 0.002f || !Ensure(cam))
            {
                Hide();
                return;
            }

            float warmth = Smooth(0f, 0.45f, sunHeight);
            float lowBoost = Mathf.Lerp(1.5f, 1f, warmth);
            float gain = BaseGain * Mathf.Clamp(SunRaysMod.Intensity.Value, 0.1f, 3f) * _strength * lowBoost;
            float range = Mathf.Clamp((layer.cloudShellInnerRadius - planet.Radius) * 12f, 8000f, MaxRange);
            _material.SetVector(RayColorId, Color.Lerp(LowSun, HighSun, warmth) * gain);
            _material.SetVector(SunDirectionId, toSun);
            _material.SetVector(PlanetCenterId, center);
            _material.SetFloat(CloudRadiusId, layer.cloudShellInnerRadius);
            _material.SetFloat(GroundRadiusId, planet.Radius);
            _material.SetFloat(RangeId, range);
            _material.SetFloat(HeightScaleId, Mathf.Max(layer.cloudShellInnerRadius - planet.Radius, 500f));
            _material.SetFloat(MapSizeId, map.width);
            _material.SetFloat(SoftnessId, 400f);
            _material.SetFloat(StepsId, Mathf.Clamp(SunRaysMod.Steps.Value, 12, 64));
            _material.SetFloat(ReferenceId, Mathf.Clamp((layer.cloudShellInnerRadius - planet.Radius) * 1.2f, 500f, 4000f));
            _material.SetFloat(AnisotropyId, 0.6f);
            Vector3 streakU = Vector3.Cross(toSun, Mathf.Abs(toSun.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            _material.SetVector(StreakUId, streakU);
            _material.SetVector(StreakVId, Vector3.Cross(toSun, streakU));
            _material.SetFloat(StreakSizeId, Mathf.Clamp((layer.cloudShellInnerRadius - planet.Radius) * 0.2f, 60f, 600f));
            if (!_object.activeSelf) _object.SetActive(true);
        }

        private static RenderTexture ShadowMap(Planet planet)
        {
            PlanetBakedTextures baked = planet.bakedTextures;
            if (baked.cloudShadowMapTexture == null)
            {
                var desc = new RenderTextureDescriptor(MapSize, MapSize, GraphicsFormat.R32G32B32A32_SFloat, 0)
                {
                    enableRandomWrite = true,
                    dimension = TextureDimension.Tex2D,
                    autoGenerateMips = false,
                    msaaSamples = 1,
                };
                baked.cloudShadowMapTexture = new RenderTexture(desc) { name = "SunRaysCloudShadow", filterMode = FilterMode.Point };
            }
            return baked.cloudShadowMapTexture;
        }

        private static void UpdateShadowMap(Planet planet, RenderTexture map, IWorldTransposeClient transpose, IWorldTimeProviderClient time)
        {
            if (Time.unscaledTime < _nextMap)
            {
                return;
            }
            var updater = ServiceCache.Get<PlanetCloudsShadowUpdater>();
            if (updater == null)
            {
                return;
            }
            _nextMap = Time.unscaledTime + (_strength > 0.002f ? MapSecondsVisible : MapSecondsIdle);
            float spin = planet.PhysicalProperties.AngularRotationSpeedInRadiansPerSeconds * CloudSpinFactor;
            Quaternion clouds = Quaternion.AngleAxis((float)-time.CurrentWorldTime * spin * Mathf.Rad2Deg, Vector3.up);
            Quaternion toPlanet = transpose.Rotation(ClientOrigin.LocalVisualSpace, planet.SurfaceSpace, Quaternion.identity);
            var transform = new CloudsRenderingTransform { LocalToPlanetRot = toPlanet, LocalToCloudRot = Quaternion.Inverse(clouds) * toPlanet };
            var config = new AtmosphereRenderingConfig(planet.PhysicalProperties.atmosphereProperties, planet.Radius);
            updater.UpdateCloudsShadow(planet, config, transform);
            Shader.SetGlobalTexture(ShadowMapId, map);
        }

        private static float Smooth(float from, float to, float value) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, value));

        private static float Weather(double worldTime, Planet planet)
        {
            float frequency = Mathf.Clamp01(SunRaysMod.Frequency.Value);
            if (frequency >= 1f) return 1f;
            if (frequency <= 0f) return 0f;
            float t = (float)(worldTime / WeatherPeriod % 4096.0);
            float noise = Mathf.PerlinNoise(t, planet.Radius % 97f);
            float threshold = Mathf.Lerp(0.72f, 0.28f, frequency);
            return Smooth(threshold - 0.03f, threshold + 0.03f, noise);
        }

        private static void FindSun()
        {
            if ((_sun != null && _sun.isActiveAndEnabled) || Time.unscaledTime < _nextSunSearch)
            {
                return;
            }
            _nextSunSearch = Time.unscaledTime + 5f;
            _sun = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l => l.type == LightType.Directional && l.isActiveAndEnabled).OrderByDescending(l => l.intensity).FirstOrDefault();
        }

        private static void RequestCover(RenderTexture map, Vector3 relative)
        {
            if (_pending || Time.unscaledTime < _nextReadback)
            {
                return;
            }
            _nextReadback = Time.unscaledTime + ReadbackSeconds;
            Vector4 q = Shader.GetGlobalVector(InverseRotationId);
            Vector3 start = Shader.GetGlobalVector(MapPositionId);
            Vector3 extents = Shader.GetGlobalVector(ExtentsId);
            if (extents.x <= 0f || extents.y <= 0f)
            {
                return;
            }
            Vector3 local = new Quaternion(q.x, q.y, q.z, q.w) * (relative - start);
            _cameraDepth = local.z / Mathf.Max(extents.z, 1f);
            float u = local.x / (2f * extents.x) + 0.5f, v = local.y / (2f * extents.y) + 0.5f;
            if (u < 0f || u > 1f || v < 0f || v > 1f)
            {
                return;
            }
            int halfX = Mathf.Clamp(Mathf.RoundToInt(CoverRadius / (2f * extents.x) * map.width), MinTexels, MaxTexels);
            int halfY = Mathf.Clamp(Mathf.RoundToInt(CoverRadius / (2f * extents.y) * map.height), MinTexels, MaxTexels);
            int x0 = Mathf.Clamp(Mathf.RoundToInt(u * map.width) - halfX, 0, map.width - 1);
            int y0 = Mathf.Clamp(Mathf.RoundToInt(v * map.height) - halfY, 0, map.height - 1);
            int width = Mathf.Min(halfX * 2, map.width - x0);
            int height = Mathf.Min(halfY * 2, map.height - y0);
            _pending = true;
            AsyncGPUReadback.Request(map, 0, x0, width, y0, height, 0, 1, OnCover);
        }

        private static void OnCover(AsyncGPUReadbackRequest request)
        {
            _pending = false;
            if (request.hasError)
            {
                return;
            }
            NativeArray<float> data = request.GetData<float>();
            int cloudy = 0, total = 0;
            float nearest = float.MaxValue, farthest = float.MinValue;
            for (int i = 0; i < data.Length; i += 4)
            {
                total++;
                if (data[i] <= 1.5f)
                {
                    cloudy++;
                    nearest = Mathf.Min(nearest, data[i]);
                    farthest = Mathf.Max(farthest, data[i]);
                }
            }
            if (SunRaysMod.Debug.Value)
            {
                Melon<SunRaysMod>.Logger.Msg($"map: {cloudy}/{total} cloudy, hit depth {nearest:0.0000}-{farthest:0.0000}, camera depth {_cameraDepth:0.0000}");
            }
            if (total == 0)
            {
                return;
            }
            float cover = cloudy / (float)total;
            _cover = _cover < 0f ? cover : Mathf.Lerp(_cover, cover, CoverSmoothing);
        }

        private static bool Ensure(Camera cam)
        {
            if (_object == null)
            {
                if (_material == null) _material = CreateMaterial();
                if (_material == null)
                {
                    _failed = true;
                    return false;
                }
                var mesh = new Mesh { name = "SunRays" };
                mesh.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f) };
                mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
                mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
                _object = new GameObject("SunRays");
                Object.DontDestroyOnLoad(_object);
                _object.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = _object.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = _material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                _object.SetActive(false);
            }
            if (_object.transform.parent != cam.transform)
            {
                _object.transform.SetParent(cam.transform, false);
                _object.transform.localPosition = Vector3.forward * (cam.nearClipPlane + 0.05f);
                _object.transform.localRotation = Quaternion.identity;
            }
            return true;
        }

        private static Material CreateMaterial()
        {
            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("SunRays.sunrays.bundle"))
            {
                if (s == null)
                {
                    Melon<SunRaysMod>.Logger.Error("The sun rays shader bundle is missing");
                    return null;
                }
                var data = new byte[s.Length];
                s.Read(data, 0, data.Length);
                AssetBundle bundle = AssetBundle.LoadFromMemory(data);
                Shader shader = bundle != null ? bundle.LoadAllAssets<Shader>().FirstOrDefault(x => x.name == "StellarDriveMods/SunRays") : null;
                if (shader == null || !shader.isSupported)
                {
                    Melon<SunRaysMod>.Logger.Error($"Sun rays shader not available: {shader?.name}");
                    return null;
                }
                return new Material(shader) { name = "SunRays" };
            }
        }
    }
}
