using System.Collections;
using System.Linq;
using Core.Services;
using Core.Values;
using HarmonyLib;
using MelonLoader;
using Planets;
using Planets.Network;
using Planets.Services;
using UnityEngine;
using Water.Terrain;
using WorldTracking.Interface.Model.Spaces;
using WorldTracking.Interface.Services;
using WorldTracking.Interface.Values;

namespace Water.Surface
{
    internal static class WaterSurface
    {
        private const ulong PlanetId = 4;
        private const int Segments = 192;
        private const int Rings = 90;
        private const float Rebuild = 300f;
        private const float MaxAltitude = 6000f;

        private static GameObject _object;
        private static Mesh _mesh;
        private static Vector3 _anchor;
        private static bool _hasAnchor;
        private static float _radius;
        private static int _logBuilds = 3;

        internal static Material Material { get; private set; }
        internal static Camera OverrideCamera;

        internal static void Clear()
        {
            if (_object != null) Object.Destroy(_object);
            _object = null;
            _hasAnchor = false;
            _building = false;
            _hasTriangles = false;
            _tracked = null;
        }

        internal static void Update()
        {
            Camera cam = OverrideCamera != null ? OverrideCamera : Camera.main;
            if (!(ClientOrigin.LocalVisualSpace is PlanetSurfaceSpace space) || space.PlanetId != PlanetId || cam == null)
            {
                if (_object != null) _object.SetActive(false);
                return;
            }
            var transpose = ServiceLocator.GetService<IWorldTransposeClient>();
            if (transpose == null)
            {
                return;
            }
            if (_radius <= 0f)
            {
                Planet planet = FindPlanet();
                if (planet == null) return;
                _radius = planet.Radius + LakeTerrainPatch.SeaLevel;
            }
            var planetSpace = new PlanetSurfaceSpace { PlanetId = space.PlanetId, Center = Vector3Double.Zero };
            Vector3 camPlanet = (Vector3)transpose.Position(ClientOrigin.LocalVisualSpace, planetSpace, (Vector3Double)cam.transform.position);
            float altitude = camPlanet.magnitude - _radius;
            EnsureObject();
            RequestDepth(cam);
            _object.SetActive(altitude < MaxAltitude);
            if (altitude >= MaxAltitude)
            {
                return;
            }
            Vector3 under = camPlanet.normalized * _radius;
            float extent = Mathf.Max(500f, WaterMod.SurfaceRadius.Value);
            if (!_hasAnchor)
            {
                if (!_building) StartBuild(under, extent);
                ContinueBuild(Rings);
            }
            else if (!_building && (under - _anchor).sqrMagnitude > Rebuild * Rebuild)
            {
                StartBuild(under, extent);
            }
            if (_building)
            {
                ContinueBuild(RingsPerFrame);
            }
            UpdateExposure(transpose.Rotation(planetSpace, ClientOrigin.LocalVisualSpace, Quaternion.identity) * camPlanet.normalized);
            UpdateUnderwater(altitude < UnderwaterAltitude);
            _object.transform.position = (Vector3)transpose.Position(planetSpace, ClientOrigin.LocalVisualSpace, (Vector3Double)_anchor);
            _object.transform.rotation = transpose.Rotation(planetSpace, ClientOrigin.LocalVisualSpace, Quaternion.identity);
        }

        private static void EnsureObject()
        {
            if (_object != null)
            {
                return;
            }
            _object = new GameObject("WaterSurface");
            Object.DontDestroyOnLoad(_object);
            _mesh = new Mesh { name = "WaterSurface", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            _hasTriangles = false;
            _hasAnchor = false;
            _building = false;
            _object.AddComponent<MeshFilter>().sharedMesh = _mesh;
            var renderer = _object.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Material = WaterMaterial.Create();
            renderer.sharedMaterial = Material;
        }

        private static Light _sun;
        private static float _exposure = -1f;

        internal static float Exposure => _exposure < 0f ? 1f : _exposure;
        private static float _nextSunSearch;

        private static void UpdateExposure(Vector3 upVisual)
        {
            if (Material == null || !Material.HasProperty(ExposureId))
            {
                return;
            }
            if ((_sun == null || !_sun.isActiveAndEnabled) && Time.unscaledTime > _nextSunSearch)
            {
                _nextSunSearch = Time.unscaledTime + 5f;
                _sun = Object.FindObjectsOfType<Light>().Where(l => l.type == LightType.Directional && l.isActiveAndEnabled).OrderByDescending(l => l.intensity).FirstOrDefault();
                Melon<WaterMod>.Logger.Msg($"Water: sun = {(_sun != null ? _sun.name + " " + _sun.intensity : "none")}");
            }
            float target = 1f;
            if (_sun != null)
            {
                float height = Vector3.Dot(upVisual, -_sun.transform.forward);
                target = Mathf.Lerp(0.1f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.12f, 0.3f, height)));
            }
            _exposure = _exposure < 0f ? target : Mathf.MoveTowards(_exposure, target, Time.deltaTime * 0.5f);
            Material.SetFloat(ExposureId, _exposure);
        }

        private const float UnderwaterAltitude = -0.2f;
        private static float _surfaceFoam = -1f;
        private static bool _underwater;

        private static void UpdateUnderwater(bool underwater)
        {
            if (Material == null || !Material.HasProperty(FoamDistanceId))
            {
                return;
            }
            if (_surfaceFoam < 0f)
            {
                _surfaceFoam = Material.GetFloat(FoamDistanceId);
            }
            if (underwater == _underwater)
            {
                return;
            }
            _underwater = underwater;
            Material.SetFloat(FoamDistanceId, underwater ? 0.0001f : _surfaceFoam);
        }

        private static Camera _depthCam;

        private static void RequestDepth(Camera cam)
        {
            if (cam == _depthCam)
            {
                return;
            }
            _depthCam = cam;
            var data = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (data != null)
            {
                data.requiresDepthTexture = true;
            }
            var asset = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
            Melon<WaterMod>.Logger.Msg($"Water: depth buffer in URP settings: {asset?.supportsCameraDepthTexture}, camera: {data?.requiresDepthTexture}");
        }

        private const int RingsPerFrame = 10;
        private static readonly int ExposureId = Shader.PropertyToID("_Exposure");
        private static readonly int FoamDistanceId = Shader.PropertyToID("_FoamDistance");
        private static Vector3[] _verts, _normals;
        private static Vector2[] _uvs, _depth;
        private static bool _hasTriangles;
        private static bool _building;
        private static int _buildRing;
        private static Vector3 _buildAnchor, _buildUp, _buildT1, _buildT2, _axisU, _axisV;
        private static Vector2 _snap;
        private static float _buildExtent, _baseRadius;
        private static TrackedPlanet _tracked;
        private static System.Diagnostics.Stopwatch _buildWatch;

        private static void StartBuild(Vector3 anchor, float extent)
        {
            int count = 1 + Rings * Segments;
            if (_verts == null || _verts.Length != count)
            {
                _verts = new Vector3[count];
                _normals = new Vector3[count];
                _uvs = new Vector2[count];
                _depth = new Vector2[count];
            }
            Vector3 up = anchor.normalized;
            _buildAnchor = anchor;
            _buildExtent = extent;
            _buildUp = up;
            _buildT1 = Vector3.Cross(up, Mathf.Abs(up.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            _buildT2 = Vector3.Cross(up, _buildT1);
            Vector3 ax = new Vector3(Mathf.Abs(up.x), Mathf.Abs(up.y), Mathf.Abs(up.z));
            _axisU = ax.y >= ax.x && ax.y >= ax.z ? Vector3.right : Vector3.up;
            _axisV = ax.z >= ax.x && ax.z >= ax.y ? Vector3.right : Vector3.forward;
            if (ax.x >= ax.y && ax.x >= ax.z) { _axisU = Vector3.up; _axisV = Vector3.forward; }
            _snap = new Vector2(Mathf.Round(Vector3.Dot(anchor, _axisU) / 500f) * 500f, Mathf.Round(Vector3.Dot(anchor, _axisV) / 500f) * 500f);
            if (_tracked == null || _tracked.Planet == null || _tracked.PointsProvider == null)
            {
                _tracked = FindTracked();
            }
            _baseRadius = _radius - LakeTerrainPatch.SeaLevel;
            _buildWatch = System.Diagnostics.Stopwatch.StartNew();
            _verts[0] = Vector3.zero;
            _normals[0] = up;
            _uvs[0] = new Vector2(Vector3.Dot(anchor, _axisU) - _snap.x, Vector3.Dot(anchor, _axisV) - _snap.y);
            float b0 = _tracked != null ? _tracked.PointsProvider.GetElevationAtPoint(up) - _baseRadius : 0f;
            _depth[0] = new Vector2(Mathf.Max(0f, LakeTerrainPatch.SeaLevel - b0), 0f);
            _buildRing = 1;
            _building = true;
        }

        private static void ContinueBuild(int rings)
        {
            int last = Mathf.Min(Rings, _buildRing + rings - 1);
            for (int r = _buildRing; r <= last; r++)
            {
                float k = (float)r / Rings;
                float d = _buildExtent * k * k;
                float theta = d / _radius;
                float cosTheta = Mathf.Cos(theta), sinTheta = Mathf.Sin(theta);
                for (int s = 0; s < Segments; s++)
                {
                    float phi = s * Mathf.PI * 2f / Segments;
                    Vector3 tangent = _buildT1 * Mathf.Cos(phi) + _buildT2 * Mathf.Sin(phi);
                    Vector3 dir = _buildUp * cosTheta + tangent * sinTheta;
                    int i = 1 + (r - 1) * Segments + s;
                    Vector3 world = dir * _radius;
                    _verts[i] = world - _buildAnchor;
                    _normals[i] = dir;
                    _uvs[i] = new Vector2(Vector3.Dot(world, _axisU) - _snap.x, Vector3.Dot(world, _axisV) - _snap.y);
                    float bottom = _tracked != null ? _tracked.PointsProvider.GetElevationAtPoint(dir) - _baseRadius : LakeTerrainPatch.SeaLevel - 5f;
                    _depth[i] = new Vector2(Mathf.Max(0f, LakeTerrainPatch.SeaLevel - bottom), 0f);
                }
            }
            _buildRing = last + 1;
            if (_buildRing > Rings)
            {
                ApplyBuild();
            }
        }

        private static void ApplyBuild()
        {
            _building = false;
            _mesh.vertices = _verts;
            _mesh.normals = _normals;
            _mesh.uv = _uvs;
            _mesh.uv2 = _depth;
            if (!_hasTriangles)
            {
                _mesh.triangles = BuildTriangles();
                _hasTriangles = true;
            }
            _mesh.bounds = new Bounds(Vector3.zero, Vector3.one * _buildExtent * 2.2f);
            _anchor = _buildAnchor;
            _hasAnchor = true;
            if (_logBuilds-- > 0) Melon<WaterMod>.Logger.Msg($"Water: mesh {_verts.Length} verts, built in {_buildWatch.Elapsed.TotalMilliseconds:0.0} ms");
        }

        private static int[] BuildTriangles()
        {
            var tris = new int[(Segments + (Rings - 1) * Segments * 2) * 3];
            int t = 0;
            for (int s = 0; s < Segments; s++)
            {
                int a = 1 + s, b = 1 + (s + 1) % Segments;
                tris[t++] = 0; tris[t++] = b; tris[t++] = a;
            }
            for (int r = 1; r < Rings; r++)
            {
                for (int s = 0; s < Segments; s++)
                {
                    int a = 1 + (r - 1) * Segments + s, b = 1 + (r - 1) * Segments + (s + 1) % Segments;
                    int c = a + Segments, e = b + Segments;
                    tris[t++] = a; tris[t++] = b; tris[t++] = e;
                    tris[t++] = a; tris[t++] = e; tris[t++] = c;
                }
            }
            return tris;
        }

        internal static Planet FindPlanet()
        {
            foreach (MonoBehaviour tracker in new MonoBehaviour[] { Object.FindObjectOfType<PlanetsClientTracker>(), Object.FindObjectOfType<PlanetsServerTracker>() })
            {
                if (tracker != null && Traverse.Create(tracker).Field("_planets").GetValue() is IDictionary planets)
                {
                    foreach (DictionaryEntry entry in planets)
                    {
                        if (entry.Value is TrackedPlanet planet && planet.Id == PlanetId)
                        {
                            return planet.Planet;
                        }
                    }
                }
            }
            return Object.FindObjectsOfType<Planet>().FirstOrDefault(p => p.Id == PlanetId);
        }

        internal static TrackedPlanet FindTracked()
        {
            foreach (MonoBehaviour tracker in new MonoBehaviour[] { Object.FindObjectOfType<PlanetsServerTracker>(), Object.FindObjectOfType<PlanetsClientTracker>() })
            {
                if (tracker != null && Traverse.Create(tracker).Field("_planets").GetValue() is IDictionary planets)
                {
                    foreach (DictionaryEntry entry in planets)
                    {
                        if (entry.Value is TrackedPlanet planet && planet.Id == PlanetId) return planet;
                    }
                }
            }
            return null;
        }
    }
}
