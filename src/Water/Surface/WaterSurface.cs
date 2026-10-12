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
        private const int Segments = 96;
        private const int Rings = 56;
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
            if (!_hasAnchor || (under - _anchor).sqrMagnitude > Rebuild * Rebuild)
            {
                _anchor = under;
                _hasAnchor = true;
                BuildMesh(_anchor, Mathf.Max(500f, WaterMod.SurfaceRadius.Value));
            }
            UpdateExposure(transpose.Rotation(planetSpace, ClientOrigin.LocalVisualSpace, Quaternion.identity) * camPlanet.normalized);
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
            _object.AddComponent<MeshFilter>().sharedMesh = _mesh;
            var renderer = _object.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Material = WaterMaterial.Create();
            renderer.sharedMaterial = Material;
        }

        private static Light _sun;
        private static float _exposure = -1f;
        private static float _nextSunSearch;

        private static void UpdateExposure(Vector3 upVisual)
        {
            if (Material == null || !Material.HasProperty("_Exposure"))
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
            Material.SetFloat("_Exposure", _exposure);
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

        private static void BuildMesh(Vector3 anchor, float extent)
        {
            Vector3 up = anchor.normalized;
            Vector3 t1 = Vector3.Cross(up, Mathf.Abs(up.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 t2 = Vector3.Cross(up, t1);
            var verts = new Vector3[1 + Rings * Segments];
            var normals = new Vector3[verts.Length];
            var uvs = new Vector2[verts.Length];
            var depth = new Vector2[verts.Length];
            Vector3 ax = new Vector3(Mathf.Abs(up.x), Mathf.Abs(up.y), Mathf.Abs(up.z));
            Vector3 axisU = ax.y >= ax.x && ax.y >= ax.z ? Vector3.right : Vector3.up;
            Vector3 axisV = ax.z >= ax.x && ax.z >= ax.y ? Vector3.right : Vector3.forward;
            if (ax.x >= ax.y && ax.x >= ax.z) { axisU = Vector3.up; axisV = Vector3.forward; }
            Vector2 snap = new Vector2(Mathf.Round(Vector3.Dot(anchor, axisU) / 500f) * 500f, Mathf.Round(Vector3.Dot(anchor, axisV) / 500f) * 500f);
            Planets.Network.TrackedPlanet tracked = FindTracked();
            float baseRadius = _radius - LakeTerrainPatch.SeaLevel;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            verts[0] = Vector3.zero;
            normals[0] = up;
            for (int r = 1; r <= Rings; r++)
            {
                float k = (float)r / Rings;
                float d = extent * k * k;
                float theta = d / _radius;
                for (int s = 0; s < Segments; s++)
                {
                    float phi = s * Mathf.PI * 2f / Segments;
                    Vector3 tangent = t1 * Mathf.Cos(phi) + t2 * Mathf.Sin(phi);
                    Vector3 dir = up * Mathf.Cos(theta) + tangent * Mathf.Sin(theta);
                    int i = 1 + (r - 1) * Segments + s;
                    verts[i] = dir * _radius - anchor;
                    normals[i] = dir;
                    Vector3 world = dir * _radius;
                    uvs[i] = new Vector2(Vector3.Dot(world, axisU) - snap.x, Vector3.Dot(world, axisV) - snap.y);
                    float bottom = tracked != null ? tracked.PointsProvider.GetElevationAtPoint(dir) - baseRadius : LakeTerrainPatch.SeaLevel - 5f;
                    depth[i] = new Vector2(Mathf.Max(0f, LakeTerrainPatch.SeaLevel - bottom), 0f);
                }
            }
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
            _mesh.Clear();
            _mesh.vertices = verts;
            _mesh.normals = normals;
            _mesh.uv = uvs;
            _mesh.uv2 = depth;
            {
                Vector3 w0 = anchor;
                uvs[0] = new Vector2(Vector3.Dot(w0, axisU) - snap.x, Vector3.Dot(w0, axisV) - snap.y);
                float b0 = tracked != null ? tracked.PointsProvider.GetElevationAtPoint(up) - baseRadius : 0f;
                depth[0] = new Vector2(Mathf.Max(0f, LakeTerrainPatch.SeaLevel - b0), 0f);
                _mesh.uv = uvs;
                _mesh.uv2 = depth;
            }
            if (_logBuilds-- > 0) Melon<WaterMod>.Logger.Msg($"Water: mesh {verts.Length} verts, bottom read {sw.Elapsed.TotalMilliseconds:0.0} ms");
            _mesh.triangles = tris;
            _mesh.bounds = new Bounds(Vector3.zero, Vector3.one * extent * 2.2f);
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
