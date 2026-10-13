using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using UnityEngine;
using WorldTracking.Interface.Values;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Mining.Content
{
    internal sealed class LaserFx : MonoBehaviour
    {
        internal const float Speed = 320f;
        internal const float Range = 450f;

        private static readonly Color BoltGlow = new Color(3.2f, 0.25f, 0.2f, 0.85f);
        private static readonly Color BoltCore = new Color(4f, 2.6f, 2.4f, 1f);
        private static readonly Color Flash = new Color(3f, 0.5f, 0.35f, 0.9f);
        private static readonly Color Spark = new Color(3.5f, 1.2f, 0.4f, 1f);

        private struct Impact
        {
            public float Time;
            public Vector3 Point, Normal;
        }

        private static LaserFx _instance;
        private static bool _failed;
        private ParticleSystem _bolts, _glow, _sparks;
        private Light _light;
        private float _lightLevel;
        private readonly List<Impact> _impacts = new List<Impact>();

        internal static void Fire(Vector3 origin, Vector3 direction, Vector3 carrier)
        {
            LaserFx fx = Get();
            if (fx == null)
            {
                return;
            }
            float distance = Range;
            if (ClientScene.PhysicsScene.Raycast(origin, direction, out RaycastHit hit, Range, ~0, QueryTriggerInteraction.Ignore))
            {
                distance = hit.distance;
                fx._impacts.Add(new Impact { Time = Time.time + distance / Speed, Point = hit.point, Normal = hit.normal });
            }
            float life = Mathf.Max(0.02f, distance / Speed);
            Vector3 velocity = direction * Speed + carrier;
            Emit(fx._bolts, origin, velocity, 0.42f, life, BoltGlow);
            Emit(fx._bolts, origin, velocity, 0.13f, life, BoltCore);
            for (int i = 0; i < 3; i++)
            {
                Emit(fx._glow, origin + direction * 0.05f, carrier + direction * Random.Range(1f, 4f) + Random.insideUnitSphere * 0.8f, Random.Range(0.35f, 0.7f), Random.Range(0.06f, 0.12f), Flash);
            }
            fx._light.transform.position = origin + direction * 0.3f;
            fx._lightLevel = 1f;
            Audio.LaserSound.PlayShot(origin);
        }

        internal static void Vent(Vector3 position, Vector3 up, Vector3 carrier, float heat)
        {
            LaserFx fx = Get();
            if (fx == null)
            {
                return;
            }
            Vector3 velocity = carrier + up * Random.Range(0.8f, 1.8f) + Random.insideUnitSphere * 0.25f;
            var steam = new Color(0.32f, 0.3f, 0.3f, 0.12f + 0.2f * heat);
            Emit(fx._glow, position, velocity, Random.Range(0.18f, 0.32f), Random.Range(0.7f, 1.2f), steam);
        }

        private static LaserFx Get()
        {
            if (_instance != null)
            {
                return _instance;
            }
            if (_failed)
            {
                return null;
            }
            ParticleSystem template = Resources.FindObjectsOfTypeAll<ParticleSystem>().FirstOrDefault(p => p.name == "DustPoof");
            if (template == null)
            {
                return null;
            }
            try
            {
                var go = new GameObject("LaserFx");
                Object.DontDestroyOnLoad(go);
                _instance = go.AddComponent<LaserFx>();
                _instance._bolts = Create(go.transform, template, "LaserBolts", 600, ParticleSystemRenderMode.Stretch, 1f);
                _instance._glow = Create(go.transform, template, "LaserGlow", 400, ParticleSystemRenderMode.Billboard, 1.8f);
                _instance._sparks = Create(go.transform, template, "LaserSparks", 1500, ParticleSystemRenderMode.Stretch, 0.6f);
                var bolts = _instance._bolts.GetComponent<ParticleSystemRenderer>();
                bolts.velocityScale = 0.016f;
                bolts.lengthScale = 2f;
                var sparks = _instance._sparks.GetComponent<ParticleSystemRenderer>();
                sparks.velocityScale = 0.03f;
                sparks.lengthScale = 1f;
                var light = new GameObject("LaserLight").AddComponent<Light>();
                light.transform.SetParent(go.transform, false);
                light.type = LightType.Point;
                light.color = new Color(1f, 0.3f, 0.25f);
                light.range = 8f;
                light.intensity = 0f;
                light.shadows = LightShadows.None;
                _instance._light = light;
                Melon<MiningMod>.Logger.Msg($"Laser effects ready (shader {_instance._bolts.GetComponent<ParticleSystemRenderer>().sharedMaterial?.shader?.name})");
            }
            catch (System.Exception e)
            {
                _failed = true;
                Melon<MiningMod>.Logger.Error($"Laser effects failed: {e}");
            }
            return _instance;
        }

        private static ParticleSystem Create(Transform parent, ParticleSystem template, string name, int max, ParticleSystemRenderMode mode, float grow)
        {
            GameObject go = Object.Instantiate(template.gameObject, parent);
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
            var force = system.forceOverLifetime;
            force.enabled = false;
            var color = system.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(new Gradient
            {
                colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) },
            });
            var size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, grow));
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = mode;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (renderer.sharedMaterial != null)
            {
                var material = new Material(renderer.sharedMaterial) { name = name };
                foreach (string property in new[] { "_BaseColor", "_Color", "_TintColor" })
                {
                    if (material.HasProperty(property)) material.SetColor(property, Color.white);
                }
                if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 2f);
                if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
                if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                renderer.sharedMaterial = material;
            }
            system.Play(true);
            return system;
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

        private void Update()
        {
            float now = Time.time;
            for (int i = _impacts.Count - 1; i >= 0; i--)
            {
                Impact impact = _impacts[i];
                if (impact.Time > now)
                {
                    continue;
                }
                _impacts.RemoveAt(i);
                Vector3 n = impact.Normal;
                for (int k = 0; k < 14; k++)
                {
                    Vector3 dir = (n + Random.insideUnitSphere * 0.9f).normalized;
                    Emit(_sparks, impact.Point + n * 0.03f, dir * Random.Range(3f, 11f), Random.Range(0.04f, 0.09f), Random.Range(0.15f, 0.45f), Spark);
                }
                Emit(_glow, impact.Point + n * 0.1f, n * 0.5f, Random.Range(0.8f, 1.3f), 0.15f, Flash);
                Emit(_glow, impact.Point + n * 0.15f, n * 0.3f + Random.insideUnitSphere * 0.2f, Random.Range(0.5f, 0.9f), Random.Range(0.4f, 0.7f), new Color(0.4f, 0.35f, 0.33f, 0.35f));
                Audio.LaserSound.PlayHit(impact.Point);
            }
            _lightLevel = Mathf.MoveTowards(_lightLevel, 0f, Time.deltaTime * 12f);
            if (_light != null)
            {
                _light.intensity = _lightLevel * 6f;
            }
        }
    }
}
