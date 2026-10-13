using System;
using System.Collections.Generic;
using System.Linq;
using Core.Services;
using Core.Values;
using MelonLoader;
using Ships;
using Ships.Interface.Model.Parts;
using Ships.Services;
using UnityEngine;
using Water.Buoyancy;
using WorldTracking.Interface.Model.Spaces;
using WorldTracking.Interface.Services;
using WorldTracking.Interface.Values;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Water.Effects
{
    internal static class Splashes
    {
        private sealed class Tracked
        {
            public readonly LakeCache Lake = new LakeCache();
            public readonly PontoonSet Pontoons = new PontoonSet();
            public Vector3Double Last;
            public bool HasLast;
            public Vector3 Velocity;
            public bool Wet;
            public float LastSplash = -100f;
            public float WakeCarry;
            public float BubbleCarry;
            public int Seen;
            public bool[] PointWet = new bool[0];
            public float[] PointCarry = new float[0];
            public float[] PointSplash = new float[0];
            public float Flood;
            public float InflowCarry, PourCarry;
            public GameObject FloodPlane;
            public readonly List<Vector3> Cockpits = new List<Vector3>();
            public int CockpitPartCount = -1;
        }

        private struct Delayed
        {
            public float Time;
            public Vector3Double Point;
            public float Power, Radius;
            public int Kind;
        }

        private const float MaxDistance = 700f;
        private const float FloodRise = 0.06f, FloodDrain = 0.14f;
        private static readonly Color DropColor = new Color(0.86f, 0.95f, 1f, 0.9f);
        private static readonly Color SprayColor = new Color(0.93f, 0.97f, 1f, 0.75f);
        private static readonly Color MistColor = new Color(0.92f, 0.96f, 1f, 0.22f);
        private static readonly Color FoamColor = new Color(0.97f, 0.99f, 1f, 0.6f);
        private static readonly Color InsideColor = new Color(0.16f, 0.42f, 0.5f, 0.62f);

        private static readonly Dictionary<uint, Tracked> Entries = new Dictionary<uint, Tracked>();
        private static readonly List<Delayed> Pending = new List<Delayed>();
        private static readonly Queue<(Vector3Double point, float speed)> PlayerSplashes = new Queue<(Vector3Double, float)>();
        private static GameObject _root;
        private static ParticleSystem _drops, _spray, _mist;
        private static Material _insideMaterial;
        private static Mesh _quad;
        private static bool _failed;
        private static int _frame;
        private static float _wakeLevel, _wakePan;

        internal static void Clear()
        {
            foreach (Tracked t in Entries.Values)
            {
                if (t.FloodPlane != null) Object.Destroy(t.FloodPlane);
            }
            Entries.Clear();
            Pending.Clear();
            PlayerSplashes.Clear();
            if (_root != null) Object.Destroy(_root);
            _root = null;
            SplashSound.StopWake();
        }

        internal static void QueuePlayerSplash(Vector3Double planetPoint, float speed)
        {
            if (PlayerSplashes.Count < 8)
            {
                PlayerSplashes.Enqueue((planetPoint, speed));
            }
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
                SplashSound.StopWake();
                Melon<WaterMod>.Logger.Error($"Splashes disabled: {e}");
            }
        }

        private static void Run()
        {
            float dt = Time.deltaTime;
            Camera cam = Surface.WaterSurface.OverrideCamera != null ? Surface.WaterSurface.OverrideCamera : Camera.main;
            if (!(ClientOrigin.LocalVisualSpace is PlanetSurfaceSpace space) || space.PlanetId != WaterLevel.PlanetId || cam == null || dt <= 0f)
            {
                FadeWake(0f, 0f, dt);
                return;
            }
            var transpose = ServiceLocator.GetService<IWorldTransposeClient>();
            var tracker = ServiceLocator.GetService<ShipsClientTracker>();
            if (transpose == null || tracker == null || !WaterLevel.Ready())
            {
                return;
            }
            var planetSpace = new PlanetSurfaceSpace { PlanetId = space.PlanetId, Center = Vector3Double.Zero };
            Quaternion toVisual = transpose.Rotation(planetSpace, ClientOrigin.LocalVisualSpace, Quaternion.identity);
            Vector3 ToVisual(Vector3Double p) => (Vector3)transpose.Position(planetSpace, ClientOrigin.LocalVisualSpace, p);
            Vector3 camPos = cam.transform.position;
            _frame++;
            float wake = 0f, wakePan = 0f;
            float sea = WaterLevel.SeaRadius;

            RunDelayed(ToVisual, toVisual, camPos);
            while (PlayerSplashes.Count > 0)
            {
                var (point, speed) = PlayerSplashes.Dequeue();
                Vector3Double surface = point.Normalized * sea;
                Splash(ToVisual(surface), toVisual * (Vector3)point.Normalized, Vector3.zero, speed, 0.6f, 0.45f, camPos);
            }

            foreach (TrackedShipClient ship in tracker.VisuallyLoadedShip)
            {
                if (ship == null || ship.PartCount <= 0)
                {
                    continue;
                }
                if (!Entries.TryGetValue(ship.Id, out Tracked entry))
                {
                    Entries[ship.Id] = entry = new Tracked();
                }
                entry.Seen = _frame;
                Vector3 visualPos = ship.GetVisualPos();
                Vector3Double pos = transpose.Position(ClientOrigin.LocalVisualSpace, planetSpace, (Vector3Double)visualPos);
                Quaternion rot = transpose.Rotation(ClientOrigin.LocalVisualSpace, planetSpace, ship.VisualRotation);
                if (entry.HasLast)
                {
                    Vector3 raw = (Vector3)(pos - entry.Last) / dt;
                    if (raw.magnitude < 400f)
                    {
                        entry.Velocity = Vector3.Lerp(entry.Velocity, raw, 1f - Mathf.Exp(-dt * 15f));
                    }
                }
                entry.Last = pos;
                entry.HasLast = true;
                bool near = (visualPos - camPos).magnitude <= MaxDistance;
                bool lake = near && WaterLevel.IsLake(pos, entry.Lake);

                var bounds = ship.GetHullCoordBounds();
                Immersion water = WaterLevel.Measure(pos, rot, bounds.Center, bounds.Extents);
                float f = lake ? water.Fraction : 0f;
                Vector3 velocity = entry.Velocity;
                float vUp = Vector3.Dot(velocity, water.Up);
                Vector3 horizontal = velocity - water.Up * vUp;
                Vector3 up = toVisual * water.Up;
                Vector3 visualHorizontal = toVisual * horizontal;

                if (near)
                {
                    Flooding(ship, entry, bounds.Center, bounds.Extents, Mathf.Clamp01((f - 0.1f) / 0.9f), up, toVisual * velocity, dt);
                }
                if (!lake)
                {
                    entry.Wet = false;
                    continue;
                }

                Vector3 hit = ToVisual(water.SurfacePoint);
                PontoonEffects(ship, entry, pos, rot, horizontal, vUp, toVisual, ToVisual, camPos, dt, ref wake, ref wakePan, cam);

                if (f > 0f && !entry.Wet && -vUp > 2.5f && Time.time - entry.LastSplash > 0.35f)
                {
                    entry.LastSplash = Time.time;
                    Splash(hit, up, visualHorizontal, -vUp, water.Radius, 1f, camPos);
                    QueueAnimation(water.SurfacePoint, -vUp, water.Radius);
                }
                else if (f <= 0f && entry.Wet && vUp > 3f && Time.time - entry.LastSplash > 0.35f)
                {
                    entry.LastSplash = Time.time;
                    Splash(hit, up, visualHorizontal, vUp, water.Radius, 0.45f, camPos);
                }
                entry.Wet = f > 0f;
                if (f <= 0f)
                {
                    continue;
                }

                float speed = horizontal.magnitude;
                if (f < 0.98f && speed > 1.5f)
                {
                    float level = Mathf.Clamp01(speed / 25f) * Mathf.Clamp01(water.Radius / 4f + 0.3f);
                    Wake(ref entry.WakeCarry, hit, up, visualHorizontal, speed, water.Radius, dt);
                    Hear(level, hit, camPos, cam, ref wake, ref wakePan);
                }
                float topDepth = -(float)(water.HullCenter.Magnitude - sea) - water.Height * 0.5f;
                if (f > 0.5f && vUp < -0.3f && topDepth < 12f)
                {
                    Bubbles(entry, hit, up, water.Radius, Mathf.Clamp01(-vUp / 3f) * Mathf.Clamp01(1f - topDepth / 12f), dt);
                }
            }

            FadeWake(wake, wakePan, dt);
            if (_frame % 300 == 0)
            {
                foreach (uint id in Entries.Where(p => _frame - p.Value.Seen > 300).Select(p => p.Key).ToList())
                {
                    if (Entries[id].FloodPlane != null) Object.Destroy(Entries[id].FloodPlane);
                    Entries.Remove(id);
                }
            }
        }

        private static void Hear(float level, Vector3 point, Vector3 camPos, Camera cam, ref float wake, ref float wakePan)
        {
            float heard = level / (1f + (point - camPos).magnitude / 20f);
            if (heard > wake)
            {
                wake = heard;
                Vector3 local = cam.transform.InverseTransformPoint(point);
                wakePan = Mathf.Clamp(local.x / Mathf.Max(1f, local.magnitude), -1f, 1f) * 0.8f;
            }
        }

        private static void PontoonEffects(TrackedShipClient ship, Tracked entry, Vector3Double pos, Quaternion rot, Vector3 horizontal, float vUp,
            Quaternion toVisual, Func<Vector3Double, Vector3> toVisualPoint, Vector3 camPos, float dt, ref float wake, ref float wakePan, Camera cam)
        {
            Pontoons.Refresh(ship, entry.Pontoons);
            int n = entry.Pontoons.Points.Count;
            if (n == 0)
            {
                return;
            }
            Pontoons.UpdateInflation(ship, entry.Pontoons, dt);
            if (entry.PointWet.Length != n)
            {
                entry.PointWet = new bool[n];
                entry.PointCarry = new float[n];
                entry.PointSplash = new float[n];
            }
            float sea = WaterLevel.SeaRadius;
            float speed = horizontal.magnitude;
            Vector3 visualHorizontal = toVisual * horizontal;
            for (int i = 0; i < n; i++)
            {
                var (id, local) = entry.Pontoons.Points[i];
                float inflation = Pontoons.InflationOf(entry.Pontoons, id);
                float radius = Pontoons.Radius * Mathf.Lerp(0.3f, 1f, inflation);
                Vector3Double planet = pos + (Vector3Double)(rot * local);
                float altitude = (float)(planet.Magnitude - sea);
                bool wet = altitude < radius && altitude > -radius * 4f;
                Vector3 up = toVisual * (Vector3)planet.Normalized;
                Vector3 hit = toVisualPoint(planet.Normalized * sea);
                if (wet && !entry.PointWet[i] && -vUp > 1.2f && Time.time - entry.PointSplash[i] > 0.4f)
                {
                    entry.PointSplash[i] = Time.time;
                    Splash(hit, up, visualHorizontal, -vUp, 0.7f, 0.55f, camPos);
                    if (-vUp > 4f)
                    {
                        QueueAnimation(planet.Normalized * sea, -vUp * 0.6f, 0.9f);
                    }
                }
                entry.PointWet[i] = wet;
                if (wet && speed > 1f)
                {
                    Wake(ref entry.PointCarry[i], hit, up, visualHorizontal, speed, 0.55f, dt);
                    Hear(Mathf.Clamp01(speed / 18f) * 0.6f, hit, camPos, cam, ref wake, ref wakePan);
                }
            }
        }

        private static void Flooding(TrackedShipClient ship, Tracked entry, Vector3 center, Vector3 extents, float outside, Vector3 up, Vector3 velocity, float dt)
        {
            float previous = entry.Flood;
            entry.Flood = Mathf.MoveTowards(entry.Flood, outside, (outside > entry.Flood ? FloodRise : FloodDrain) * dt);
            Transform shipTransform = ship.VisualObject != null ? ship.VisualObject.transform : null;
            if (shipTransform == null || !Build())
            {
                return;
            }
            Vector3 min = center - extents, size = extents * 2f;
            Vector3 shipUp = shipTransform.InverseTransformDirection(up);

            if (entry.Flood > 0.005f)
            {
                if (entry.FloodPlane == null)
                {
                    entry.FloodPlane = CreatePlane(shipTransform);
                }
                entry.FloodPlane.SetActive(true);
                Transform plane = entry.FloodPlane.transform;
                float y = min.y + size.y * entry.Flood;
                plane.localPosition = new Vector3(center.x, y, center.z);
                plane.localRotation = Quaternion.identity;
                plane.localScale = new Vector3(Mathf.Max(0.5f, size.x - 0.3f), 1f, Mathf.Max(0.5f, size.z - 0.3f));
            }
            else if (entry.FloodPlane != null)
            {
                entry.FloodPlane.SetActive(false);
            }

            if (outside > entry.Flood + 0.02f)
            {
                SetGravity(up);
                float strength = Mathf.Clamp01((outside - entry.Flood) * 4f);
                entry.InflowCarry += dt * strength * Mathf.Min(400f, (size.x + size.z) * 18f);
                int count = Mathf.Min(60, (int)entry.InflowCarry);
                entry.InflowCarry -= count;
                float waterY = min.y + size.y * outside;
                for (int i = 0; i < count; i++)
                {
                    Vector3 edge = RandomEdge(center, extents, 0.92f);
                    edge.y = Mathf.Lerp(min.y + size.y * entry.Flood, waterY, Random.value);
                    Vector3 inward = new Vector3(center.x - edge.x, 0f, center.z - edge.z).normalized;
                    Vector3 p = shipTransform.TransformPoint(edge);
                    Vector3 v = shipTransform.TransformDirection(inward * Random.Range(1.5f, 5f) + shipUp * Random.Range(0f, 1.5f)) + velocity;
                    Emit(Random.value < 0.6f ? _drops : _spray, p, v, Random.Range(0.08f, 0.35f), Random.Range(0.5f, 1.2f), Random.value < 0.6f ? DropColor : FoamColor);
                }
                if (entry.Flood > 0.02f && Random.value < dt * 20f * strength)
                {
                    Vector3 local = new Vector3(Random.Range(min.x, min.x + size.x) * 0.9f, min.y + size.y * entry.Flood, Random.Range(min.z, min.z + size.z) * 0.9f);
                    Emit(_spray, shipTransform.TransformPoint(local), up * Random.Range(0.3f, 1f) + velocity, Random.Range(0.3f, 0.8f), Random.Range(0.6f, 1.2f), FoamColor);
                }
            }
            else if (entry.Flood > 0.01f && previous > outside + 0.005f && entry.Flood > outside)
            {
                Pour(ship, entry, shipTransform, center, extents, up, velocity, Mathf.Clamp01((entry.Flood - outside) * 3f), dt);
            }
        }

        private static void Pour(TrackedShipClient ship, Tracked entry, Transform shipTransform, Vector3 center, Vector3 extents, Vector3 up, Vector3 velocity, float strength, float dt)
        {
            SetGravity(up);
            RefreshCockpits(ship, entry);
            Vector3 min = center - extents, size = extents * 2f;
            float waterY = min.y + size.y * entry.Flood;
            float upward = Vector3.Dot(velocity, up);
            entry.PourCarry += dt * strength * Mathf.Min(500f, 120f + (size.x + size.z) * 10f);
            int count = Mathf.Min(90, (int)entry.PourCarry);
            entry.PourCarry -= count;
            for (int i = 0; i < count; i++)
            {
                Vector3 p, v;
                bool cockpit = entry.Cockpits.Count > 0 && Random.value < 0.6f;
                if (cockpit)
                {
                    Vector3 c = entry.Cockpits[Random.Range(0, entry.Cockpits.Count)];
                    Vector3 local = c + new Vector3(Random.Range(-0.6f, 0.6f), Mathf.Min(waterY - c.y, 1.2f) + 0.4f, Random.Range(-0.6f, 0.6f));
                    p = shipTransform.TransformPoint(local);
                    Vector3 spread = shipTransform.TransformDirection(new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f))) * Random.Range(0.5f, 2.5f);
                    v = velocity * 0.85f + up * Random.Range(1f, 4f + Mathf.Max(0f, upward) * 0.3f) + spread;
                }
                else
                {
                    Vector3 edge = RandomEdge(center, extents, 1.02f);
                    edge.y = Mathf.Min(waterY, min.y + size.y * Random.Range(0.1f, 1f));
                    Vector3 outward = new Vector3(edge.x - center.x, 0f, edge.z - center.z).normalized;
                    p = shipTransform.TransformPoint(edge);
                    v = velocity * 0.9f + shipTransform.TransformDirection(outward) * Random.Range(0.5f, 2.2f) - up * Random.Range(0f, 1f);
                }
                if (Random.value < 0.75f)
                {
                    Emit(_drops, p, v, Random.Range(0.08f, 0.24f), Random.Range(1.2f, 2.6f), DropColor);
                }
                else
                {
                    Emit(_spray, p, v * 0.8f, Random.Range(0.4f, 1.1f), Random.Range(0.8f, 1.8f), SprayColor);
                }
            }
        }

        private static void RefreshCockpits(TrackedShipClient ship, Tracked entry)
        {
            if (entry.CockpitPartCount == ship.PartCount)
            {
                return;
            }
            entry.CockpitPartCount = ship.PartCount;
            entry.Cockpits.Clear();
            foreach (StatefulPart part in ship.StatefulParts)
            {
                string label = part?.Settings?.fullLabel ?? part?.Settings?.name ?? "";
                if (label.IndexOf("seat", StringComparison.OrdinalIgnoreCase) >= 0 || label.IndexOf("cockpit", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    label.IndexOf("pilot", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    entry.Cockpits.Add(part.LocalPosition);
                }
            }
        }

        private static Vector3 RandomEdge(Vector3 center, Vector3 extents, float inset)
        {
            float a = Random.value * 2f - 1f;
            switch (Random.Range(0, 4))
            {
                case 0: return center + new Vector3(extents.x * inset, 0f, extents.z * a * inset);
                case 1: return center + new Vector3(-extents.x * inset, 0f, extents.z * a * inset);
                case 2: return center + new Vector3(extents.x * a * inset, 0f, extents.z * inset);
                default: return center + new Vector3(extents.x * a * inset, 0f, -extents.z * inset);
            }
        }

        private static GameObject CreatePlane(Transform parent)
        {
            if (_quad == null)
            {
                _quad = new Mesh { name = "FloodPlane" };
                _quad.vertices = new[] { new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, 0.5f), new Vector3(-0.5f, 0f, 0.5f) };
                _quad.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
                _quad.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
                _quad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
                _quad.RecalculateBounds();
            }
            if (_insideMaterial == null)
            {
                _insideMaterial = Surface.WaterMaterial.CreateSimple(InsideColor);
            }
            var go = new GameObject("FloodWater");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = _quad;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _insideMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        private static void FadeWake(float target, float pan, float dt)
        {
            _wakeLevel = Mathf.MoveTowards(_wakeLevel, target, Mathf.Max(dt, 0.01f) * 1.5f);
            _wakePan = Mathf.Lerp(_wakePan, pan, 0.1f);
            SplashSound.SetWake(_wakeLevel, _wakePan);
        }

        private static void QueueAnimation(Vector3Double surfacePoint, float speed, float radius)
        {
            float power = Mathf.Clamp(speed / 20f, 0.12f, 2.5f);
            float now = Time.time;
            Pending.Add(new Delayed { Time = now + 0.12f, Point = surfacePoint, Power = power, Radius = radius * 1.15f, Kind = 0 });
            Pending.Add(new Delayed { Time = now + 0.35f, Point = surfacePoint, Power = power * 0.8f, Radius = radius * 1.6f, Kind = 0 });
            Pending.Add(new Delayed { Time = now + 0.7f, Point = surfacePoint, Power = power * 0.55f, Radius = radius * 2.2f, Kind = 0 });
            if (power > 0.25f)
            {
                Pending.Add(new Delayed { Time = now + 0.45f, Point = surfacePoint, Power = power, Radius = radius, Kind = 1 });
            }
        }

        private static void RunDelayed(Func<Vector3Double, Vector3> toVisualPoint, Quaternion toVisual, Vector3 camPos)
        {
            if (Pending.Count == 0 || !Build())
            {
                return;
            }
            float now = Time.time;
            for (int i = Pending.Count - 1; i >= 0; i--)
            {
                Delayed d = Pending[i];
                if (d.Time > now)
                {
                    continue;
                }
                Pending.RemoveAt(i);
                Vector3 up = toVisual * (Vector3)d.Point.Normalized;
                Vector3 hit = toVisualPoint(d.Point);
                Basis(up, out Vector3 t1, out Vector3 t2);
                SetGravity(up);
                if (d.Kind == 0)
                {
                    int ring = Mathf.Min(300, (int)(30 + 90 * d.Power * Mathf.Sqrt(d.Radius)));
                    for (int k = 0; k < ring; k++)
                    {
                        Vector3 outward = Ring(t1, t2, Random.value * Mathf.PI * 2f);
                        Vector3 p = hit + outward * d.Radius * Random.Range(0.92f, 1.08f) + up * 0.05f;
                        Vector3 v = outward * Random.Range(1f, 3f) * Mathf.Sqrt(d.Power) + up * Random.Range(0.5f, 2.5f) * Mathf.Sqrt(d.Power);
                        Emit(_spray, p, v, Random.Range(0.35f, 0.9f) * (0.6f + 0.4f * d.Power), Random.Range(0.8f, 1.6f), FoamColor);
                    }
                }
                else
                {
                    float lift = Mathf.Sqrt(d.Power);
                    int column = Mathf.Min(400, (int)(60 + 160 * d.Power));
                    for (int k = 0; k < column; k++)
                    {
                        Vector3 outward = Ring(t1, t2, Random.value * Mathf.PI * 2f);
                        Vector3 p = hit + outward * d.Radius * 0.25f * Random.value;
                        Vector3 v = up * Random.Range(8f, 20f) * lift + outward * Random.Range(0f, 1.5f);
                        Emit(_drops, p, v, Random.Range(0.08f, 0.25f), Random.Range(1.2f, 2.8f), DropColor);
                    }
                    for (int k = 0; k < 12; k++)
                    {
                        Emit(_spray, hit + up * Random.Range(0f, 3f) * lift, up * Random.Range(4f, 10f) * lift + Ring(t1, t2, Random.value * 6.28f),
                            Random.Range(0.6f, 1.4f), Random.Range(1f, 1.8f), SprayColor);
                    }
                    SplashSound.PlayImpact(hit, d.Power * 0.5f, camPos);
                }
            }
        }

        private static void Splash(Vector3 hit, Vector3 up, Vector3 horizontal, float speed, float radius, float scale, Vector3 camPos)
        {
            if (!Build())
            {
                return;
            }
            float size = Mathf.Lerp(0.75f, 1.5f, Mathf.Clamp01(radius / 8f));
            float power = Mathf.Clamp(speed / 20f, 0.12f, 2.5f) * size * scale;
            float lift = Mathf.Sqrt(power);
            Basis(up, out Vector3 t1, out Vector3 t2);
            SetGravity(up);

            int crown = Mathf.Min(450, (int)(40 + 130 * power * Mathf.Sqrt(radius)));
            for (int i = 0; i < crown; i++)
            {
                Vector3 outward = Ring(t1, t2, Random.value * Mathf.PI * 2f);
                Vector3 p = hit + outward * radius * Random.Range(0.6f, 1.05f);
                Vector3 v = outward * Random.Range(2f, 8f) * lift + up * Random.Range(4f, 15f) * lift + horizontal * 0.25f;
                Emit(_spray, p, v, Random.Range(0.4f, 1.4f) * (0.7f + 0.3f * power), Random.Range(0.9f, 2.1f), SprayColor);
            }
            int drops = Mathf.Min(1000, (int)(100 + 380 * power * Mathf.Sqrt(radius * 0.5f)));
            for (int i = 0; i < drops; i++)
            {
                Vector3 outward = Ring(t1, t2, Random.value * Mathf.PI * 2f);
                Vector3 p = hit + outward * radius * Mathf.Sqrt(Random.value) * 0.9f;
                Vector3 v = up * Random.Range(3f, 22f) * lift + outward * Random.Range(1f, 9f) * lift + horizontal * 0.3f;
                Emit(_drops, p, v, Random.Range(0.06f, 0.22f), Random.Range(1f, 3.2f), DropColor);
            }
            int mist = Mathf.Min(70, (int)(6 + 20 * power));
            for (int i = 0; i < mist; i++)
            {
                Vector3 outward = Ring(t1, t2, Random.value * Mathf.PI * 2f);
                Vector3 p = hit + outward * radius * Random.value + up * Random.Range(0f, 2f * lift);
                Vector3 v = up * Random.Range(0.5f, 3.5f) * lift + outward * Random.Range(0.5f, 3f) + horizontal * 0.15f;
                Emit(_mist, p, v, Random.Range(2f, 6f) * Mathf.Sqrt(radius / 3f + 0.5f), Random.Range(2.5f, 5.5f), MistColor);
            }
            SplashSound.PlayImpact(hit, power, camPos);
        }

        private static void Wake(ref float carry, Vector3 hit, Vector3 up, Vector3 horizontal, float speed, float radius, float dt)
        {
            if (!Build())
            {
                return;
            }
            Basis(up, out Vector3 t1, out Vector3 t2);
            SetGravity(up);
            Vector3 forward = horizontal / Mathf.Max(0.01f, speed);
            Vector3 side = Vector3.Cross(up, forward);
            carry += dt * Mathf.Min(600f, speed * Mathf.Max(radius, 1f) * 4f);
            int count = Mathf.Min(80, (int)carry);
            carry -= count;
            float lift = Mathf.Sqrt(Mathf.Clamp(speed / 15f, 0.1f, 3f));
            for (int i = 0; i < count; i++)
            {
                float sideSign = Random.value < 0.5f ? -1f : 1f;
                Vector3 p = hit + forward * radius * Random.Range(0.2f, 0.9f) + side * sideSign * radius * Random.Range(0.6f, 1.05f);
                Vector3 v = side * sideSign * Random.Range(1.5f, 6f) * lift + up * Random.Range(1.5f, 7f) * lift + horizontal * Random.Range(0.3f, 0.7f);
                if (Random.value < 0.7f)
                {
                    Emit(_drops, p, v, Random.Range(0.06f, 0.18f), Random.Range(0.7f, 1.8f), DropColor);
                }
                else
                {
                    Emit(_spray, p, v * 0.6f, Random.Range(0.3f, 1f), Random.Range(0.6f, 1.4f), SprayColor);
                }
            }
            if (speed > 12f && Random.value < dt * speed * 0.15f)
            {
                Emit(_mist, hit + Ring(t1, t2, Random.value * Mathf.PI * 2f) * radius, up * Random.Range(0.5f, 2f) + horizontal * 0.5f,
                    Random.Range(2f, 5f), Random.Range(2f, 4f), MistColor);
            }
        }

        private static void Bubbles(Tracked entry, Vector3 hit, Vector3 up, float radius, float strength, float dt)
        {
            if (!Build())
            {
                return;
            }
            Basis(up, out Vector3 t1, out Vector3 t2);
            SetGravity(up);
            entry.BubbleCarry += dt * strength * radius * 12f;
            int count = Mathf.Min(30, (int)entry.BubbleCarry);
            entry.BubbleCarry -= count;
            for (int i = 0; i < count; i++)
            {
                Vector3 p = hit + Ring(t1, t2, Random.value * Mathf.PI * 2f) * radius * Mathf.Sqrt(Random.value) + up * 0.05f;
                Emit(_spray, p, up * Random.Range(0.3f, 1.6f), Random.Range(0.3f, 0.9f), Random.Range(0.8f, 1.8f), FoamColor);
            }
        }

        internal static void Bubbling(Vector3 point, Vector3 up, Vector3 toward, float strength, float dt, ref float carry)
        {
            if (!Build())
            {
                return;
            }
            SetGravity(up);
            carry += dt * strength * 40f;
            int count = Mathf.Min(40, (int)carry);
            carry -= count;
            for (int i = 0; i < count; i++)
            {
                Vector3 offset = Random.insideUnitSphere * 0.9f;
                Vector3 p = point + offset;
                Vector3 v = toward - offset * Random.Range(1f, 2.5f) + up * Random.Range(0.2f, 1.2f);
                Emit(_spray, p, v, Random.Range(0.08f, 0.25f), Random.Range(0.4f, 0.8f), FoamColor);
            }
        }

        internal static void SurfaceFoam(Vector3 point, Vector3 up, float strength, float dt, ref float carry)
        {
            if (!Build())
            {
                return;
            }
            Basis(up, out Vector3 t1, out Vector3 t2);
            SetGravity(up);
            carry += dt * strength * 25f;
            int count = Mathf.Min(30, (int)carry);
            carry -= count;
            for (int i = 0; i < count; i++)
            {
                Vector3 outward = Ring(t1, t2, Random.value * Mathf.PI * 2f);
                Emit(_spray, point + outward * Random.Range(0f, 0.6f), outward * Random.Range(0.2f, 0.8f) + up * Random.Range(0.4f, 1.6f),
                    Random.Range(0.2f, 0.5f), Random.Range(0.5f, 1.1f), FoamColor);
            }
        }

        private static Vector3 Ring(Vector3 t1, Vector3 t2, float angle) => t1 * Mathf.Cos(angle) + t2 * Mathf.Sin(angle);

        private static void Basis(Vector3 up, out Vector3 t1, out Vector3 t2)
        {
            t1 = Vector3.Cross(up, Mathf.Abs(up.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            t2 = Vector3.Cross(up, t1);
        }

        private static void Emit(ParticleSystem system, Vector3 position, Vector3 velocity, float size, float lifetime, Color color)
        {
            var p = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = velocity,
                startSize = size,
                startLifetime = lifetime,
                startColor = color,
                rotation = Random.value * 360f,
                applyShapeToPosition = false,
            };
            system.Emit(p, 1);
        }

        private static void SetGravity(Vector3 up)
        {
            SetForce(_drops, -up * 9.8f);
            SetForce(_spray, -up * 7f);
            SetForce(_mist, up * 0.3f);
        }

        private static void SetForce(ParticleSystem system, Vector3 force)
        {
            var module = system.forceOverLifetime;
            module.enabled = true;
            module.space = ParticleSystemSimulationSpace.World;
            module.x = force.x;
            module.y = force.y;
            module.z = force.z;
        }

        private static bool Build()
        {
            if (_root != null && _drops != null && _spray != null && _mist != null)
            {
                return true;
            }
            ParticleSystem template = Resources.FindObjectsOfTypeAll<ParticleSystem>().FirstOrDefault(p => p.name == "DustPoof");
            if (template == null)
            {
                return false;
            }
            if (_root != null) Object.Destroy(_root);
            _root = new GameObject("WaterSplashes");
            Object.DontDestroyOnLoad(_root);
            _drops = Create(template, "SplashDrops", 6000, ParticleSystemRenderMode.Stretch, grow: 0.7f, drag: 0.15f);
            _spray = Create(template, "SplashSpray", 4000, ParticleSystemRenderMode.Billboard, grow: 2.4f, drag: 0.8f);
            _mist = Create(template, "SplashMist", 400, ParticleSystemRenderMode.Billboard, grow: 2.2f, drag: 1.2f);
            Melon<WaterMod>.Logger.Msg("Water: splash particles ready");
            return true;
        }

        private static ParticleSystem Create(ParticleSystem template, string name, int max, ParticleSystemRenderMode mode, float grow, float drag)
        {
            GameObject go = Object.Instantiate(template.gameObject, _root.transform);
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
            main.simulationSpace = ParticleSystemSimulationSpace.World;
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
            var color = system.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(new Gradient
            {
                colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 0.55f), new GradientAlphaKey(0f, 1f) },
            });
            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, grow));
            var limit = system.limitVelocityOverLifetime;
            limit.enabled = true;
            limit.limit = 1000f;
            limit.drag = drag;
            limit.multiplyDragByParticleSize = false;
            limit.multiplyDragByParticleVelocity = mode != ParticleSystemRenderMode.Stretch;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = mode;
            if (mode == ParticleSystemRenderMode.Stretch)
            {
                renderer.velocityScale = 0.04f;
                renderer.lengthScale = 1.6f;
            }
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (renderer.sharedMaterial != null)
            {
                var material = new Material(renderer.sharedMaterial) { name = name };
                foreach (string property in new[] { "_BaseColor", "_Color", "_TintColor" })
                {
                    if (material.HasProperty(property)) material.SetColor(property, Color.white);
                }
                renderer.sharedMaterial = material;
            }
            system.Play(true);
            return system;
        }
    }
}
