using System;
using System.Collections.Generic;
using System.Linq;
using Core.Values;
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
        private sealed class Tracked
        {
            public float Heat, Flux;
            public Vector3 LastVisual;
            public bool HasLast;
            public int Seen;
            public float PlasmaCarry, TrailCarry, SmokeCarry, SparkCarry;
            public Light Light;
            public float NextLog;
        }

        private const float EarthOrbitalSpeed = 7800f;
        private const float GlowStartSpeed = 1300f, PlasmaSpeed = 4500f;
        private const float FluxStart = 0.004f, FluxFull = 0.02f;
        private const float HeatUpSeconds = 1.5f, CoolDownSeconds = 5f;

        private static readonly Dictionary<uint, Tracked> Entries = new Dictionary<uint, Tracked>();
        private static GameObject _root;
        private static ParticleSystem _plasma, _trail, _smoke, _sparks;
        private static float _nextBuild;
        private static int _frame;
        private static bool _failed;

        internal static void Clear()
        {
            foreach (Tracked t in Entries.Values)
            {
                if (t.Light != null) Object.Destroy(t.Light.gameObject);
            }
            Entries.Clear();
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

        private static void Run()
        {
            float dt = Time.deltaTime;
            Camera cam = Camera.main;
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
                bool jumped = entry.HasLast && visualVelocity.magnitude > 20000f;
                entry.LastVisual = visualPos;
                entry.HasLast = true;
                if (jumped)
                {
                    visualVelocity = Vector3.zero;
                }

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
                    if (ReentryMod.Debug.Value && flux > 0.01f && Time.unscaledTime >= entry.NextLog)
                    {
                        entry.NextLog = Time.unscaledTime + 2f;
                        Melon<ReentryMod>.Logger.Msg($"Ship {ship.Id}: {airVelocity.magnitude:0} m/s (orbital {orbital:0}), air {density / surface:0.#####} of the surface, heating {flux:0.00}, hull heat {entry.Heat:0.00}");
                    }
                }
                entry.Flux = flux;
                entry.Heat = Mathf.MoveTowards(entry.Heat, flux, dt / (flux > entry.Heat ? HeatUpSeconds : CoolDownSeconds));

                float glow = Mathf.Max(flux, entry.Heat * 0.6f) * brightness;
                bool near = (visualPos - camPos).magnitude <= ReentryMod.MaxDistance.Value;
                UpdateLight(entry, shipTransform, ship, flux, glow, near);
                if (!near || glow < 0.02f || airVelocity.sqrMagnitude < 1f)
                {
                    continue;
                }

                Quaternion toVisual = transpose.Rotation(physics.SpaceId, ClientOrigin.LocalVisualSpace, Quaternion.identity);
                Vector3 flow = (toVisual * airVelocity).normalized;
                float speed = airVelocity.magnitude;
                var bounds = ship.GetHullCoordBounds();
                Emit(entry, shipTransform, bounds.Center, bounds.Extents, flow, speed, visualVelocity, flux, entry.Heat, brightness, dt);
            }

            if (_frame % 120 == 0)
            {
                foreach (uint id in Entries.Where(p => _frame - p.Value.Seen > 120).Select(p => p.Key).ToList())
                {
                    if (Entries[id].Light != null) Object.Destroy(Entries[id].Light.gameObject);
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

        private static void Emit(Tracked entry, Transform ship, Vector3 center, Vector3 extents, Vector3 flow, float speed, Vector3 shipVelocity,
            float flux, float heat, float brightness, float dt)
        {
            Vector3 localFlow = ship.InverseTransformDirection(flow);
            Vector3 areas = new Vector3(extents.y * extents.z * Mathf.Abs(localFlow.x), extents.x * extents.z * Mathf.Abs(localFlow.y), extents.x * extents.y * Mathf.Abs(localFlow.z));
            float frontal = areas.x + areas.y + areas.z;
            if (frontal <= 0.0001f)
            {
                return;
            }
            float size = Mathf.Sqrt(frontal);
            float level = Mathf.Clamp01(flux);
            float stream = Mathf.Clamp(speed * 0.12f, 25f, 260f);
            Color hot = Temperature(level, brightness);
            Color cool = Temperature(Mathf.Clamp01(Mathf.Max(level, heat) * 0.6f), brightness);

            float plasmaRate = flux > 0.02f ? Mathf.Lerp(60f, 900f, level) * Mathf.Clamp(size / 4f, 0.5f, 6f) : 0f;
            entry.PlasmaCarry += plasmaRate * dt;
            while (entry.PlasmaCarry >= 1f)
            {
                entry.PlasmaCarry -= 1f;
                Vector3 point = ship.TransformPoint(FacePoint(center, extents, localFlow, areas, frontal, 1.08f));
                Vector3 spread = Random.insideUnitSphere * stream * 0.25f;
                Vector3 velocity = shipVelocity - flow * stream + spread - Vector3.ProjectOnPlane(spread, flow) * 0.5f;
                float s = Mathf.Lerp(0.6f, 2.2f, level) * Random.Range(0.7f, 1.3f) * Mathf.Clamp(size / 6f, 0.6f, 3f);
                Particle(_plasma, point, velocity, s, Random.Range(0.06f, 0.18f) * Mathf.Lerp(1f, 1.8f, level), Color.Lerp(hot, new Color(1f, 0.97f, 0.9f, hot.a), level * level * 0.5f));
            }

            float trailScale = Mathf.Clamp(ReentryMod.TrailLength.Value, 0f, 3f);
            if (trailScale > 0f && flux > 0.08f)
            {
                float trailRate = Mathf.Lerp(30f, 260f, level) * Mathf.Clamp(size / 5f, 0.5f, 4f);
                entry.TrailCarry += trailRate * dt;
                Vector3 travel = shipVelocity * dt;
                while (entry.TrailCarry >= 1f)
                {
                    entry.TrailCarry -= 1f;
                    float back = Random.value;
                    Vector3 point = ship.TransformPoint(FacePoint(center, extents, -localFlow, areas, frontal, 0.9f)) - travel * back;
                    Vector3 velocity = shipVelocity - flow * speed * Random.Range(0.04f, 0.14f) + Random.insideUnitSphere * stream * 0.12f;
                    float s = Mathf.Clamp(size, 2f, 40f) * Random.Range(0.5f, 0.9f) * Mathf.Lerp(0.7f, 1.2f, level);
                    Particle(_trail, point, velocity, s, Random.Range(0.4f, 1.1f) * trailScale * Mathf.Lerp(0.6f, 1.2f, level), cool);
                }
            }

            if (level > 0.45f)
            {
                entry.SparkCarry += Mathf.Lerp(0f, 140f, (level - 0.45f) / 0.55f) * dt;
                while (entry.SparkCarry >= 1f)
                {
                    entry.SparkCarry -= 1f;
                    Vector3 point = ship.TransformPoint(FacePoint(center, extents, localFlow, areas, frontal, 1.02f));
                    Vector3 velocity = shipVelocity - flow * stream * Random.Range(1.2f, 2.5f) + Random.insideUnitSphere * stream * 0.5f;
                    Particle(_sparks, point, velocity, Random.Range(0.15f, 0.45f), Random.Range(0.3f, 0.9f), new Color(1f, 0.75f, 0.35f, Mathf.Clamp01(brightness)));
                }
            }

            float smoke = Mathf.Clamp01(heat * 1.5f) * (1f - level * 0.6f);
            if (smoke > 0.15f && trailScale > 0f)
            {
                entry.SmokeCarry += smoke * 25f * Mathf.Clamp(size / 6f, 0.5f, 3f) * dt;
                while (entry.SmokeCarry >= 1f)
                {
                    entry.SmokeCarry -= 1f;
                    Vector3 point = ship.TransformPoint(FacePoint(center, extents, -localFlow, areas, frontal, 1f));
                    Vector3 velocity = shipVelocity - flow * speed * Random.Range(0.15f, 0.35f) + Random.insideUnitSphere * 3f;
                    float gray = Random.Range(0.18f, 0.32f);
                    Particle(_smoke, point, velocity, Mathf.Clamp(size, 2f, 30f) * Random.Range(0.8f, 1.4f), Random.Range(2.5f, 5f) * trailScale, new Color(gray, gray, gray, 0.35f * smoke));
                }
            }
        }

        private static Vector3 FacePoint(Vector3 center, Vector3 extents, Vector3 localFlow, Vector3 areas, float total, float push)
        {
            float pick = Random.value * total;
            int axis = pick < areas.x ? 0 : pick < areas.x + areas.y ? 1 : 2;
            Vector3 p = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), Random.Range(-1f, 1f));
            p[axis] = Mathf.Sign(localFlow[axis]) * push;
            return center + Vector3.Scale(p, extents);
        }

        private static Color Temperature(float level, float brightness)
        {
            Color c;
            if (level < 0.3f) c = Color.Lerp(new Color(0.55f, 0.06f, 0.01f), new Color(1f, 0.35f, 0.05f), level / 0.3f);
            else if (level < 0.7f) c = Color.Lerp(new Color(1f, 0.35f, 0.05f), new Color(1f, 0.78f, 0.45f), (level - 0.3f) / 0.4f);
            else c = Color.Lerp(new Color(1f, 0.78f, 0.45f), new Color(1f, 0.62f, 0.95f), (level - 0.7f) / 0.3f);
            c.a = Mathf.Clamp01(Mathf.Lerp(0.25f, 1f, level) * brightness);
            return c;
        }

        private static void UpdateLight(Tracked entry, Transform ship, TrackedShipClient tracked, float flux, float glow, bool near)
        {
            bool on = near && ReentryMod.Lights.Value && glow > 0.03f;
            if (!on)
            {
                if (entry.Light != null) entry.Light.enabled = false;
                return;
            }
            if (entry.Light == null)
            {
                var go = new GameObject("ReentryGlow");
                go.transform.SetParent(ship, false);
                entry.Light = go.AddComponent<Light>();
                entry.Light.type = LightType.Point;
                entry.Light.shadows = LightShadows.None;
            }
            var bounds = tracked.GetHullCoordBounds();
            float size = bounds.HighestExtent;
            Vector3 front = tracked.State.PhysicsState.Velocity.sqrMagnitude > 1f
                ? ship.InverseTransformDirection(ServiceCache.Get<IWorldTransposeClient>().Rotation(tracked.State.PhysicsState.SpaceId, ClientOrigin.LocalVisualSpace, Quaternion.identity) * tracked.State.PhysicsState.Velocity.normalized)
                : Vector3.zero;
            entry.Light.transform.localPosition = bounds.Center + Vector3.Scale(front, bounds.Extents) * 1.3f;
            entry.Light.enabled = true;
            Color lightColor = Temperature(Mathf.Clamp01(Mathf.Max(flux, entry.Heat * 0.5f)), 1f);
            lightColor.a = 1f;
            entry.Light.color = lightColor;
            entry.Light.range = Mathf.Clamp(size * 4f, 15f, 250f);
            entry.Light.intensity = glow * (2f + size * 0.15f) * (0.85f + 0.15f * Mathf.PerlinNoise(Time.time * 12f, entry.Seen * 0.01f));
        }

        private static void Particle(ParticleSystem system, Vector3 position, Vector3 velocity, float size, float lifetime, Color color)
        {
            var p = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = velocity,
                startSize = size,
                startLifetime = lifetime,
                startColor = color,
                rotation = Random.Range(0f, 360f),
                applyShapeToPosition = false,
            };
            system.Emit(p, 1);
        }

        private static bool Build()
        {
            if (_root != null && _plasma != null && _trail != null && _smoke != null && _sparks != null)
            {
                return true;
            }
            if (Time.unscaledTime < _nextBuild)
            {
                return false;
            }
            _nextBuild = Time.unscaledTime + 5f;
            ParticleSystem template = Resources.FindObjectsOfTypeAll<ParticleSystem>().FirstOrDefault(p => p.name == "DustPoof");
            if (template == null)
            {
                return false;
            }
            if (_root != null) Object.Destroy(_root);
            _root = new GameObject("ReentryEffects");
            Object.DontDestroyOnLoad(_root);
            Material additive = Additive(template);
            _plasma = Create(template, "ReentryPlasma", 6000, ParticleSystemRenderMode.Stretch, additive, grow: 1.6f, fadeStart: 0.4f, velocityScale: 0.012f, length: 1.4f);
            _trail = Create(template, "ReentryTrail", 5000, ParticleSystemRenderMode.Billboard, additive, grow: 2.8f, fadeStart: 0.15f, velocityScale: 0f, length: 1f);
            _sparks = Create(template, "ReentrySparks", 1500, ParticleSystemRenderMode.Stretch, additive, grow: 0.4f, fadeStart: 0.6f, velocityScale: 0.02f, length: 2f);
            _smoke = Create(template, "ReentrySmoke", 2000, ParticleSystemRenderMode.Billboard, null, grow: 3.5f, fadeStart: 0.3f, velocityScale: 0f, length: 1f);
            Melon<ReentryMod>.Logger.Msg($"Reentry: particles ready ({(additive != null ? "additive plasma" : "dust material")})");
            return true;
        }

        private static Material Additive(ParticleSystem template)
        {
            Material source = template.GetComponent<ParticleSystemRenderer>()?.sharedMaterial;
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (source == null || shader == null)
            {
                return null;
            }
            var m = new Material(shader) { name = "ReentryPlasma" };
            Texture texture = source.HasProperty("_BaseMap") ? source.GetTexture("_BaseMap") : source.HasProperty("_MainTex") ? source.GetTexture("_MainTex") : null;
            if (texture != null)
            {
                m.SetTexture("_BaseMap", texture);
            }
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 2f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = 3100;
            return m;
        }

        private static ParticleSystem Create(ParticleSystem template, string name, int max, ParticleSystemRenderMode mode, Material material,
            float grow, float fadeStart, float velocityScale, float length)
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
            var limit = system.limitVelocityOverLifetime;
            limit.enabled = false;
            var color = system.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(new Gradient
            {
                colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.55f, 0.35f), 1f) },
                alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.85f, fadeStart), new GradientAlphaKey(0f, 1f) },
            });
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
            else if (renderer.sharedMaterial != null)
            {
                var copy = new Material(renderer.sharedMaterial) { name = name };
                foreach (string property in new[] { "_BaseColor", "_Color", "_TintColor" })
                {
                    if (copy.HasProperty(property)) copy.SetColor(property, Color.white);
                }
                renderer.sharedMaterial = copy;
            }
            system.Play(true);
            return system;
        }
    }
}
