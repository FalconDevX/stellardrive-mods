using System;
using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using Planets.Interface.Services;
using Ships;
using Ships.Interface.Model.State;
using Ships.Services;
using UnityEngine;
using WorldProvision.Interface.Services;
using WorldTracking.Interface.Model.Spaces;
using WorldTracking.Interface.Services;
using WorldTracking.Interface.Values;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Reentry
{
    internal static class ReentryEffects
    {
        private struct Rim
        {
            public Vector3 Start, Outward, Tangent;
            public float Width, Seed;
        }

        private struct Cell
        {
            public Vector3 Front;
        }

        private sealed class ShipFx
        {
            public GameObject Root;
            public Mesh Trails, Wrap, Cushion;
            public ParticleSystem Sparks;
            public Light Light;
            public Vector3 Flow;
            public int HullCount = -1;
            public float NextRebuild;
            public float CellSize, Size;
            public readonly List<Rim> Rims = new List<Rim>();
            public readonly List<Cell> Cells = new List<Cell>();
        }

        private sealed class Tracked
        {
            public float Heat, Flux;
            public Vector3 LastVisual;
            public bool HasLast;
            public int Seen;
            public float SparkCarry, SmokeCarry;
            public ShipFx Fx;
            public float NextLog;
        }

        private const float EarthOrbitalSpeed = 7800f;
        private const float GlowStartSpeed = 1300f, PlasmaSpeed = 4500f;
        private const float FluxStart = 0.004f, FluxFull = 0.02f;
        private const float HeatUpSeconds = 1.5f, CoolDownSeconds = 5f;
        private const int MaxCells = 900;

        private static readonly Color Core = new Color(1f, 0.8f, 0.5f);
        private static readonly Color Ember = new Color(1f, 0.45f, 0.1f);
        private static readonly Color Rose = new Color(1f, 0.35f, 0.55f);
        private static readonly Color Violet = new Color(0.62f, 0.4f, 1f);
        private static readonly Color Deep = new Color(0.8f, 0.12f, 0.03f);

        private static readonly Dictionary<uint, Tracked> Entries = new Dictionary<uint, Tracked>();
        private static readonly List<Vector3> Verts = new List<Vector3>();
        private static readonly List<Color> Colors = new List<Color>();
        private static readonly List<Vector2> Uvs = new List<Vector2>();
        private static readonly List<int> Tris = new List<int>();
        private static GameObject _root;
        private static ParticleSystem _smoke, _template;
        private static Material _trailAdd, _trailBlend, _cushion, _spark, _smokeMaterial;
        private static float _nextBuild;
        private static int _frame;
        private static bool _failed;

        internal static void Clear()
        {
            foreach (Tracked t in Entries.Values)
            {
                Destroy(t);
            }
            Entries.Clear();
        }

        private static void Destroy(Tracked t)
        {
            if (t.Fx != null)
            {
                if (t.Fx.Root != null) Object.Destroy(t.Fx.Root);
                if (t.Fx.Trails != null) Object.Destroy(t.Fx.Trails);
                if (t.Fx.Wrap != null) Object.Destroy(t.Fx.Wrap);
                if (t.Fx.Cushion != null) Object.Destroy(t.Fx.Cushion);
            }
            t.Fx = null;
        }

        internal static void Update()
        {
            if (_failed || !ReentryMod.Enabled.Value)
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
                Melon<ReentryMod>.Logger.Error($"Reentry effects disabled: {e}");
            }
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
            float dt = Time.deltaTime;
            Camera cam = ActiveCamera();
            if (cam == null || dt <= 0f || ClientOrigin.LocalVisualSpace == null || !Build())
            {
                return;
            }
            var tracker = ServiceCache.Get<ShipsClientTracker>();
            var environment = ServiceCache.Get<IWorldEnvironmentClientProvider>();
            var spaces = ServiceCache.Get<ISpacesClientProvider>();
            var planets = ServiceCache.Get<IPlanetsClientProvider>();
            var transpose = ServiceCache.Get<IWorldTransposeClient>();
            if (tracker == null || environment == null || spaces == null || planets == null || transpose == null)
            {
                return;
            }
            _frame++;
            Vector3 camPos = cam.transform.position;
            float brightness = Mathf.Clamp(ReentryMod.Brightness.Value, 0.2f, 3f);
            Vector2 scroll = new Vector2(0f, -Time.time * 1.7f);
            _trailAdd.SetTextureOffset("_BaseMap", scroll);
            _trailBlend.SetTextureOffset("_BaseMap", scroll * 0.6f);

            foreach (TrackedShipClient ship in tracker.VisuallyLoadedShip)
            {
                if (ship == null || ship.PartCount <= 0 || ship.VisualObject == null)
                {
                    continue;
                }
                if (!Entries.TryGetValue(ship.Id, out Tracked entry))
                {
                    Entries[ship.Id] = entry = new Tracked();
                }
                entry.Seen = _frame;
                Transform shipTransform = ship.VisualObject.transform;
                Vector3 visualPos = shipTransform.position;
                Vector3 visualVelocity = entry.HasLast ? (visualPos - entry.LastVisual) / dt : Vector3.zero;
                if (visualVelocity.magnitude > 20000f) visualVelocity = Vector3.zero;
                entry.LastVisual = visualPos;
                entry.HasLast = true;

                ShipPhysicsState physics = ship.State.PhysicsState;
                float flux = 0f;
                Vector3 airVelocity = Vector3.zero;
                if (spaces.GetSpace(physics.SpaceId) is PlanetSurfaceSpace space)
                {
                    var props = planets.GetPlanetProps(space.PlanetId);
                    float density = environment.GetAtmosphereDensityAtPosition(physics.SpaceId, physics.Position);
                    float surface = Mathf.Max(0.0001f, props.PhysicalProperties.atmosphereProperties.atmosphereDensity);
                    float gravity = props.PhysicalProperties.gravityInG * 9.81f;
                    float orbital = Mathf.Max(100f, Mathf.Sqrt(gravity * (float)props.Radius));
                    airVelocity = physics.Velocity;
                    flux = Heating(airVelocity.magnitude, density / surface, orbital);
                    if (ReentryMod.Debug.Value && airVelocity.magnitude > 200f && Time.unscaledTime >= entry.NextLog)
                    {
                        entry.NextLog = Time.unscaledTime + 2f;
                        Melon<ReentryMod>.Logger.Msg($"Ship {ship.Id}: {airVelocity.magnitude:0} m/s (orbital {orbital:0}), air {density / surface:0.#####} of the surface, heating {flux:0.00}, hull heat {entry.Heat:0.00}");
                    }
                }
                entry.Flux = flux;
                entry.Heat = Mathf.MoveTowards(entry.Heat, flux, dt / (flux > entry.Heat ? HeatUpSeconds : CoolDownSeconds));

                float glow = Mathf.Max(flux, entry.Heat * 0.6f) * brightness;
                bool near = (visualPos - camPos).magnitude <= ReentryMod.MaxDistance.Value;
                if (!near || glow < 0.02f || airVelocity.sqrMagnitude < 1f)
                {
                    if (entry.Fx != null)
                    {
                        entry.Fx.Root.SetActive(false);
                        if (entry.Heat <= 0.001f) Destroy(entry);
                    }
                    continue;
                }

                Quaternion toVisual = transpose.Rotation(physics.SpaceId, ClientOrigin.LocalVisualSpace, Quaternion.identity);
                Vector3 flow = (toVisual * airVelocity).normalized;
                Vector3 f = shipTransform.InverseTransformDirection(flow).normalized;
                ShipFx fx = entry.Fx ?? (entry.Fx = CreateFx(shipTransform));
                fx.Root.SetActive(true);
                int hullCount = ship.State.GetHullParts().Count;
                if (hullCount != fx.HullCount || Vector3.Angle(f, fx.Flow) > 3f || Time.unscaledTime >= fx.NextRebuild)
                {
                    Silhouette(fx, ship, f, hullCount);
                }
                float level = Mathf.Clamp01(flux);
                float over = Mathf.Clamp01(flux - 1f);
                float strength = Mathf.Clamp01(glow);
                BuildTrails(fx, fx.Flow, level, over, strength, brightness);
                BuildCushion(fx, fx.Flow, level, strength, brightness);
                Sparks(entry, fx, fx.Flow, airVelocity.magnitude, level, brightness, dt);
                Smoke(entry, fx, shipTransform, flow, airVelocity.magnitude, visualVelocity, level, dt);
                UpdateLight(entry, fx, fx.Flow, flux, glow);
            }

            if (_frame % 120 == 0)
            {
                foreach (uint id in Entries.Where(p => _frame - p.Value.Seen > 120).Select(p => p.Key).ToList())
                {
                    Destroy(Entries[id]);
                    Entries.Remove(id);
                }
            }
        }

        internal static float Heating(float speed, float relativeDensity, float orbitalSpeed)
        {
            if (speed <= 0f || relativeDensity <= 0f)
            {
                return 0f;
            }
            float earthSpeed = speed * EarthOrbitalSpeed / orbitalSpeed;
            float shock = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(GlowStartSpeed, PlasmaSpeed, earthSpeed));
            float ratio = speed / orbitalSpeed;
            float heatFlux = Mathf.Sqrt(relativeDensity) * ratio * ratio * ratio;
            float flux = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(FluxStart, FluxFull, heatFlux));
            float overdrive = Mathf.Clamp01((heatFlux - FluxFull) / (FluxFull * 4f)) * 0.5f;
            return Mathf.Min(shock, flux) * (1f + overdrive);
        }

        private static void Basis(Vector3 f, out Vector3 u, out Vector3 v)
        {
            u = Vector3.Cross(f, Mathf.Abs(f.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            v = Vector3.Cross(f, u).normalized;
        }

        private static void Silhouette(ShipFx fx, TrackedShipClient ship, Vector3 f, int hullCount)
        {
            fx.Flow = f;
            fx.HullCount = hullCount;
            fx.NextRebuild = Time.unscaledTime + 5f;
            Basis(f, out Vector3 u, out Vector3 v);
            var hull = ship.State.GetHullParts().Keys.Select(k => (Vector3)k.VoxelCoord).ToList();
            fx.Rims.Clear();
            fx.Cells.Clear();
            if (hull.Count == 0)
            {
                return;
            }
            float minU = float.MaxValue, maxU = float.MinValue, minV = float.MaxValue, maxV = float.MinValue;
            foreach (Vector3 p in hull)
            {
                float a = Vector3.Dot(p, u), b = Vector3.Dot(p, v);
                minU = Mathf.Min(minU, a); maxU = Mathf.Max(maxU, a);
                minV = Mathf.Min(minV, b); maxV = Mathf.Max(maxV, b);
            }
            float area = (maxU - minU + 1f) * (maxV - minV + 1f);
            float c = Mathf.Max(1f, Mathf.Sqrt(area / MaxCells));
            fx.CellSize = c;
            fx.Size = Mathf.Sqrt(area);
            var front = new Dictionary<Vector2Int, float>();
            foreach (Vector3 p in hull)
            {
                var key = new Vector2Int(Mathf.FloorToInt((Vector3.Dot(p, u) - minU) / c), Mathf.FloorToInt((Vector3.Dot(p, v) - minV) / c));
                float d = Vector3.Dot(p, f);
                front[key] = front.TryGetValue(key, out float old) ? Mathf.Max(old, d) : d;
            }
            Vector2Int[] dirs = { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };
            foreach (var pair in front)
            {
                Vector2 centre2 = new Vector2(minU + (pair.Key.x + 0.5f) * c, minV + (pair.Key.y + 0.5f) * c);
                Vector3 centre = u * centre2.x + v * centre2.y + f * (pair.Value + 0.5f);
                fx.Cells.Add(new Cell { Front = centre });
                Vector2 outward = Vector2.zero;
                foreach (Vector2Int d in dirs)
                {
                    if (!front.ContainsKey(pair.Key + d)) outward += d;
                }
                if (outward == Vector2.zero)
                {
                    continue;
                }
                outward.Normalize();
                Vector3 o = (u * outward.x + v * outward.y).normalized;
                float seed = Mathf.Abs(Mathf.Sin(pair.Key.x * 12.9898f + pair.Key.y * 78.233f) * 43758.5453f) % 1f;
                fx.Rims.Add(new Rim { Start = centre + o * c * 0.5f, Outward = o, Tangent = Vector3.Cross(f, o).normalized, Width = c, Seed = seed });
            }
        }

        private static void BuildTrails(ShipFx fx, Vector3 f, float level, float over, float strength, float brightness)
        {
            float trail = Mathf.Clamp(ReentryMod.TrailLength.Value, 0.05f, 3f);
            float size = fx.Size;
            float baseLength = size * Mathf.Lerp(0.35f, 1.3f, level) * trail;
            float spread = size * Mathf.Lerp(0.04f, 0.12f, level);
            float alpha = Mathf.Lerp(0.3f, 1f, level) * strength;
            Color head = Color.Lerp(Ember, Core, Mathf.Clamp01(level * 1.1f));
            Color middle = Color.Lerp(Deep, Ember, Mathf.Clamp01(level * 1.8f));
            Color tail = Color.Lerp(Deep, Color.Lerp(Ember, Rose, Mathf.Clamp01((level - 0.7f) * 2f + over) * 0.6f), level);
            float t = Time.time;

            Begin();
            foreach (Rim r in fx.Rims)
            {
                float wobble = 0.75f + 0.5f * Mathf.PerlinNoise(r.Seed * 31f, t * 1.3f);
                float length = baseLength * wobble;
                float w = r.Width * 0.7f;
                Strip(r.Start, r.Tangent, r.Outward, -f, length, w, w * 2.2f, w * 4f, spread * 0.35f, spread * 1.4f,
                    WithAlpha(head, alpha * brightness), WithAlpha(middle, alpha * 0.8f * brightness), WithAlpha(tail, 0f), r.Seed);
            }
            End(fx.Trails);

            Begin();
            if (level > 0.35f)
            {
                float wrapAlpha = Mathf.Clamp01((level - 0.35f) / 0.4f) * 0.2f * strength * brightness;
                Color wrapHead = Ember;
                Color wrapMiddle = Color.Lerp(Ember, Rose, 0.55f + 0.3f * Mathf.Clamp01(level - 0.6f + over));
                Color wrapTail = Color.Lerp(Rose, Violet, 0.45f);
                foreach (Rim r in fx.Rims)
                {
                    float wobble = 0.8f + 0.4f * Mathf.PerlinNoise(r.Seed * 17f + 5f, t * 0.9f);
                    float length = baseLength * 1.7f * wobble;
                    float w = r.Width * 1.1f;
                    Vector3 start = r.Start + r.Outward * r.Width * 0.6f - f * baseLength * 0.04f;
                    Strip(start, r.Tangent, r.Outward, -f, length, w, w * 3f, w * 5.5f, spread * 0.8f, spread * 3f,
                        WithAlpha(wrapHead, wrapAlpha * 0.6f), WithAlpha(wrapMiddle, wrapAlpha), WithAlpha(wrapTail, 0f), r.Seed);
                }
            }
            End(fx.Wrap);
        }

        private static void BuildCushion(ShipFx fx, Vector3 f, float level, float strength, float brightness)
        {
            Begin();
            if (level > 0.08f)
            {
                Basis(f, out Vector3 u, out Vector3 v);
                float c = fx.CellSize;
                float gap = c * 0.6f + fx.Size * Mathf.Lerp(0.01f, 0.035f, level);
                float half = c * 2.4f;
                Color col = WithAlpha(Color.Lerp(Ember, Core, level * 0.8f), Mathf.Lerp(0.06f, 0.28f, level) * strength * brightness);
                Color rim = WithAlpha(Color.Lerp(Ember, Rose, level * 0.5f), col.a * 0.6f);
                foreach (Cell cell in fx.Cells)
                {
                    Quad(cell.Front + f * gap, u * half, v * half, col);
                }
                foreach (Rim r in fx.Rims)
                {
                    Quad(r.Start + f * gap * 0.6f + r.Outward * c * 0.4f, r.Tangent * half * 1.3f, (r.Outward * 0.6f - f * 0.8f).normalized * half * 1.3f, rim);
                }
            }
            End(fx.Cushion);
        }

        private static void Quad(Vector3 p, Vector3 a, Vector3 b, Color col)
        {
            int i = Verts.Count;
            Verts.Add(p - a - b); Verts.Add(p + a - b); Verts.Add(p + a + b); Verts.Add(p - a + b);
            Colors.Add(col); Colors.Add(col); Colors.Add(col); Colors.Add(col);
            Uvs.Add(new Vector2(0f, 0f)); Uvs.Add(new Vector2(1f, 0f)); Uvs.Add(new Vector2(1f, 1f)); Uvs.Add(new Vector2(0f, 1f));
            Tris.Add(i); Tris.Add(i + 1); Tris.Add(i + 2); Tris.Add(i); Tris.Add(i + 2); Tris.Add(i + 3);
        }

        private static void Strip(Vector3 start, Vector3 tangent, Vector3 outward, Vector3 back, float length, float w0, float w1, float w2, float out1, float out2,
            Color c0, Color c1, Color c2, float seed)
        {
            Vector3 m = start + back * length * 0.25f + outward * out1;
            Vector3 e = start + back * length + outward * out2;
            int i = Verts.Count;
            Verts.Add(start - tangent * w0); Verts.Add(start + tangent * w0);
            Verts.Add(m - tangent * w1); Verts.Add(m + tangent * w1);
            Verts.Add(e - tangent * w2); Verts.Add(e + tangent * w2);
            Colors.Add(c0); Colors.Add(c0); Colors.Add(c1); Colors.Add(c1); Colors.Add(c2); Colors.Add(c2);
            float u0 = seed, u1 = seed + 0.35f;
            Uvs.Add(new Vector2(u0, 0f)); Uvs.Add(new Vector2(u1, 0f));
            Uvs.Add(new Vector2(u0, 0.35f)); Uvs.Add(new Vector2(u1, 0.35f));
            Uvs.Add(new Vector2(u0, 1f)); Uvs.Add(new Vector2(u1, 1f));
            Tris.Add(i); Tris.Add(i + 2); Tris.Add(i + 1); Tris.Add(i + 1); Tris.Add(i + 2); Tris.Add(i + 3);
            Tris.Add(i + 2); Tris.Add(i + 4); Tris.Add(i + 3); Tris.Add(i + 3); Tris.Add(i + 4); Tris.Add(i + 5);
        }

        private static Color WithAlpha(Color c, float a)
        {
            c.a = Mathf.Clamp01(a);
            return c;
        }

        private static void Begin()
        {
            Verts.Clear();
            Colors.Clear();
            Uvs.Clear();
            Tris.Clear();
        }

        private static void End(Mesh mesh)
        {
            mesh.Clear();
            mesh.indexFormat = Verts.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(Verts);
            mesh.SetColors(Colors);
            mesh.SetUVs(0, Uvs);
            mesh.SetTriangles(Tris, 0, false);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
        }

        private static void Sparks(Tracked entry, ShipFx fx, Vector3 f, float speed, float level, float brightness, float dt)
        {
            if (level <= 0.5f || fx.Rims.Count == 0)
            {
                return;
            }
            float stream = Mathf.Clamp(speed * 0.12f, 25f, 220f);
            float scale = Mathf.Clamp(fx.Size / 10f, 0.5f, 4f);
            entry.SparkCarry += Mathf.Lerp(0f, 180f, (level - 0.5f) / 0.5f) * scale * dt;
            while (entry.SparkCarry >= 1f)
            {
                entry.SparkCarry -= 1f;
                Rim r = fx.Rims[Random.Range(0, fx.Rims.Count)];
                Vector3 p = r.Start + r.Tangent * Random.Range(-0.5f, 0.5f) * r.Width;
                Vector3 v = -f * stream * Random.Range(1.5f, 3.2f) + r.Outward * stream * Random.Range(0.1f, 0.6f) + Random.insideUnitSphere * stream * 0.2f;
                var e = new ParticleSystem.EmitParams
                {
                    position = p, velocity = v, startSize = Random.Range(0.15f, 0.4f) * Mathf.Sqrt(scale),
                    startLifetime = Random.Range(0.4f, 1.1f), startColor = new Color(1f, 0.8f, 0.45f, Mathf.Clamp01(brightness)), applyShapeToPosition = false,
                };
                fx.Sparks.Emit(e, 1);
            }
        }

        private static void Smoke(Tracked entry, ShipFx fx, Transform ship, Vector3 flow, float speed, Vector3 shipVelocity, float level, float dt)
        {
            float trail = Mathf.Clamp(ReentryMod.TrailLength.Value, 0f, 3f);
            float smoke = Mathf.Clamp01(entry.Heat * 1.5f) * (1f - level * 0.7f);
            if (smoke <= 0.15f || trail <= 0f || fx.Rims.Count == 0)
            {
                return;
            }
            entry.SmokeCarry += smoke * 18f * Mathf.Clamp(fx.Size / 10f, 0.5f, 3f) * dt;
            while (entry.SmokeCarry >= 1f)
            {
                entry.SmokeCarry -= 1f;
                Rim r = fx.Rims[Random.Range(0, fx.Rims.Count)];
                Vector3 p = ship.TransformPoint(r.Start - fx.Flow * fx.Size * Random.Range(0.3f, 1.2f));
                Vector3 v = shipVelocity - flow * speed * Random.Range(0.15f, 0.35f) + Random.insideUnitSphere * 3f;
                float gray = Random.Range(0.18f, 0.32f);
                var e = new ParticleSystem.EmitParams { position = p, velocity = v, startSize = fx.Size * Random.Range(0.15f, 0.3f), startLifetime = Random.Range(2.5f, 5f) * trail, startColor = new Color(gray, gray, gray, 0.35f * smoke), rotation = Random.Range(0f, 360f), applyShapeToPosition = false };
                _smoke.Emit(e, 1);
            }
        }

        private static void UpdateLight(Tracked entry, ShipFx fx, Vector3 f, float flux, float glow)
        {
            if (!ReentryMod.Lights.Value || glow < 0.03f || fx.Cells.Count == 0)
            {
                if (fx.Light != null) fx.Light.enabled = false;
                return;
            }
            if (fx.Light == null)
            {
                var go = new GameObject("ReentryGlow");
                go.transform.SetParent(fx.Root.transform, false);
                fx.Light = go.AddComponent<Light>();
                fx.Light.type = LightType.Point;
                fx.Light.shadows = LightShadows.None;
            }
            Vector3 front = Vector3.zero;
            float best = float.MinValue;
            foreach (Cell c in fx.Cells)
            {
                float d = Vector3.Dot(c.Front, f);
                if (d > best) { best = d; front = c.Front; }
            }
            fx.Light.transform.localPosition = front + f * fx.Size * 0.15f;
            fx.Light.enabled = true;
            fx.Light.color = Color.Lerp(Ember, Core, Mathf.Clamp01(Mathf.Max(flux, entry.Heat * 0.5f)));
            fx.Light.range = Mathf.Clamp(fx.Size * 5f, 20f, 300f);
            fx.Light.intensity = glow * (6f + fx.Size * 0.4f) * (0.85f + 0.15f * Mathf.PerlinNoise(Time.time * 12f, entry.Seen * 0.01f));
        }

        private static ShipFx CreateFx(Transform ship)
        {
            var root = new GameObject("ReentryFx");
            root.transform.SetParent(ship, false);
            var fx = new ShipFx { Root = root, Flow = Vector3.zero };
            fx.Trails = MeshObject(root.transform, "Trails", _trailAdd);
            fx.Wrap = MeshObject(root.transform, "Wrap", _trailBlend);
            fx.Cushion = MeshObject(root.transform, "Cushion", _cushion);
            fx.Sparks = Create(root.transform, "Sparks", 2500, ParticleSystemRenderMode.Stretch, _spark, ParticleSystemSimulationSpace.Local, 0.4f,
                Fade(Color.white, new Color(1f, 0.7f, 0.3f), new Color(1f, 0.3f, 0.05f), 0.6f), 0.03f, 1.5f);
            return fx;
        }

        private static Mesh MeshObject(Transform parent, string name, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var mesh = new Mesh { name = "Reentry" + name };
            mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return mesh;
        }

        private static Gradient Fade(Color start, Color middle, Color end, float hold)
        {
            return new Gradient
            {
                colorKeys = new[] { new GradientColorKey(start, 0f), new GradientColorKey(middle, 0.4f), new GradientColorKey(end, 1f) },
                alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, hold), new GradientAlphaKey(0f, 1f) },
            };
        }

        private static bool Build()
        {
            if (_root != null && _smoke != null && _trailAdd != null)
            {
                return true;
            }
            if (Time.unscaledTime < _nextBuild)
            {
                return false;
            }
            _nextBuild = Time.unscaledTime + 5f;
            _template = Resources.FindObjectsOfTypeAll<ParticleSystem>().FirstOrDefault(p => p.name == "DustPoof");
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (_template == null || shader == null)
            {
                return false;
            }
            if (_root != null) Object.Destroy(_root);
            _root = new GameObject("ReentryEffects");
            Object.DontDestroyOnLoad(_root);
            Texture2D glow = GlowTexture();
            Texture2D streaks = StreakTexture();
            _trailAdd = Material(shader, streaks, true, "ReentryTrails", 0f);
            _trailBlend = Material(shader, streaks, false, "ReentryWrap", 0f);
            _cushion = Material(shader, glow, true, "ReentryCushion", 6f);
            _spark = Material(shader, glow, true, "ReentrySparks", 4f);
            _smokeMaterial = Material(shader, glow, false, "ReentrySmoke", 3f);
            _smoke = Create(_root.transform, "ReentrySmoke", 2000, ParticleSystemRenderMode.Billboard, _smokeMaterial, ParticleSystemSimulationSpace.World, 3.5f,
                Fade(Color.white, Color.white, Color.white, 0.3f), 0f, 1f);
            Melon<ReentryMod>.Logger.Msg("Reentry: plasma trails, shock cushion, sparks and smoke ready");
            return true;
        }

        private static Texture2D GlowTexture()
        {
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, true) { name = "ReentryGlow", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(Mathf.Exp(-r * r * 4.5f) - 0.011f) * Mathf.Clamp01((1f - r) * 6f);
                    pixels[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(true, true);
            return tex;
        }

        private static Texture2D StreakTexture()
        {
            const int w = 128, h = 256;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true) { name = "ReentryStreaks", wrapMode = TextureWrapMode.Repeat };
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float along = y / (float)h;
                for (int x = 0; x < w; x++)
                {
                    float across = x / (float)w;
                    float n = 0f, amp = 1f, total = 0f;
                    for (int o = 0; o < 4; o++)
                    {
                        int fu = 4 << o, fv = 1 << o;
                        n += amp * Mathf.PerlinNoise(across * fu + o * 13.1f, along * fv * 3f + o * 7.7f);
                        total += amp;
                        amp *= 0.55f;
                    }
                    n /= total;
                    float streak = Mathf.Pow(Mathf.Clamp01((n - 0.25f) / 0.6f), 1.6f);
                    float edge = Mathf.Sin(Mathf.PI * Mathf.Repeat(across * 2f, 1f));
                    float a = Mathf.Clamp01(streak * (0.55f + 0.45f * edge) * 1.4f);
                    pixels[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(true, true);
            return tex;
        }

        private static Material Material(Shader shader, Texture2D texture, bool additive, string name, float cameraFade)
        {
            var m = new Material(shader) { name = name };
            m.SetTexture("_BaseMap", texture);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", additive ? 2f : 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            if (cameraFade > 0f)
            {
                m.SetFloat("_CameraFadingEnabled", 1f);
                m.SetFloat("_CameraNearFadeDistance", cameraFade * 0.3f);
                m.SetFloat("_CameraFarFadeDistance", cameraFade);
                m.SetVector("_CameraFadeParams", new Vector4(cameraFade * 0.3f, 1f / (cameraFade * 0.7f), 0f, 0f));
                m.EnableKeyword("_FADING_ON");
            }
            m.renderQueue = 3100;
            return m;
        }

        private static ParticleSystem Create(Transform parent, string name, int max, ParticleSystemRenderMode mode, Material material, ParticleSystemSimulationSpace space,
            float grow, Gradient colors, float velocityScale, float length)
        {
            GameObject go = Object.Instantiate(_template.gameObject, parent);
            go.name = name;
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            foreach (ParticleSystem child in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (child.gameObject != go) Object.Destroy(child.gameObject);
            }
            var system = go.GetComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.loop = true;
            main.duration = 1f;
            main.playOnAwake = false;
            main.maxParticles = max;
            main.simulationSpace = space;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.gravityModifier = 0f;
            main.startColor = Color.white;
            var emission = system.emission;
            emission.enabled = false;
            var shape = system.shape;
            shape.enabled = false;
            var velocity = system.velocityOverLifetime;
            velocity.enabled = false;
            var noise = system.noise;
            noise.enabled = false;
            var collision = system.collision;
            collision.enabled = false;
            var sub = system.subEmitters;
            sub.enabled = false;
            var limit = system.limitVelocityOverLifetime;
            limit.enabled = false;
            var color = system.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(colors);
            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, grow));
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = mode;
            if (mode == ParticleSystemRenderMode.Stretch)
            {
                renderer.velocityScale = velocityScale;
                renderer.lengthScale = length;
            }
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (material != null)
            {
                renderer.sharedMaterial = material;
            }
            system.Play(true);
            return system;
        }
    }
}
