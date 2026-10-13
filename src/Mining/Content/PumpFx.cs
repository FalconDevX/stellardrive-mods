using System.Linq;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
using UnityEngine.VFX;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Mining.Content
{
    internal static class PumpFx
    {
        private static readonly Color Bubble = new Color(0.9f, 0.97f, 1f, 0.55f);
        private static readonly Color Foam = new Color(0.97f, 0.99f, 1f, 0.6f);
        private static readonly Color Murk = new Color(0.55f, 0.7f, 0.72f, 0.25f);

        private static ParticleSystem _particles;
        private static VisualEffect _gasTemplate;
        private static bool _failed, _searchedGas;

        internal static VisualEffect CreateGas(Transform parent)
        {
            if (!_searchedGas)
            {
                _searchedGas = true;
                var exhaust = Resources.FindObjectsOfTypeAll<Ships.Parts.GasExhaust.GasExhaustVisuals>().FirstOrDefault();
                _gasTemplate = exhaust != null ? Traverse.Create(exhaust).Field("gasVfx").GetValue<VisualEffect>() : null;
                Melon<MiningMod>.Logger.Msg($"Pump bubbles: gas exhaust effect {(_gasTemplate != null ? "found" : "not found")}");
            }
            if (_gasTemplate == null)
            {
                return null;
            }
            GameObject go = Object.Instantiate(_gasTemplate.gameObject, parent);
            go.name = "PumpBubbles";
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            var vfx = go.GetComponent<VisualEffect>();
            vfx.Stop();
            return vfx;
        }

        internal static void Emit(Vector3 intake, Vector3 surface, Vector3 up, float level, float dt, ref float carry)
        {
            ParticleSystem system = Get();
            if (system == null)
            {
                return;
            }
            SetForce(system, up * 1.5f);
            carry += dt * level * 70f;
            int count = Mathf.Min(40, (int)carry);
            carry -= count;
            float depth = Mathf.Max(0.1f, Vector3.Dot(surface - intake, up));
            for (int i = 0; i < count; i++)
            {
                float roll = Random.value;
                if (roll < 0.4f)
                {
                    Vector3 offset = Random.onUnitSphere * Random.Range(0.5f, 1.1f);
                    if (Vector3.Dot(offset, up) > depth) offset -= up * (Vector3.Dot(offset, up) - depth * 0.9f);
                    float life = Random.Range(0.35f, 0.6f);
                    Emit(system, intake + offset, -offset / life - up * 1.2f, Random.Range(0.05f, 0.12f), life, Murk);
                }
                else if (roll < 0.8f)
                {
                    float speed = Random.Range(0.8f, 1.8f);
                    Vector3 p = intake + Random.insideUnitSphere * 0.12f;
                    Emit(system, p, up * speed + Random.insideUnitSphere * 0.3f, Random.Range(0.04f, 0.12f), depth / speed * 0.9f, Bubble);
                }
                else
                {
                    Vector3 side = Vector3.ProjectOnPlane(Random.onUnitSphere, up).normalized;
                    Emit(system, surface + side * Random.Range(0f, 0.45f), side * Random.Range(0.2f, 0.8f) + up * Random.Range(0.3f, 1.2f),
                        Random.Range(0.12f, 0.3f), Random.Range(0.4f, 0.8f), Foam);
                }
            }
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

        private static void SetForce(ParticleSystem system, Vector3 force)
        {
            var module = system.forceOverLifetime;
            module.enabled = true;
            module.space = ParticleSystemSimulationSpace.World;
            module.x = force.x;
            module.y = force.y;
            module.z = force.z;
        }

        private static ParticleSystem Get()
        {
            if (_particles != null || _failed)
            {
                return _particles;
            }
            ParticleSystem template = Resources.FindObjectsOfTypeAll<ParticleSystem>().FirstOrDefault(p => p.name == "DustPoof");
            if (template == null)
            {
                return null;
            }
            try
            {
                GameObject go = Object.Instantiate(template.gameObject);
                go.name = "PumpWater";
                Object.DontDestroyOnLoad(go);
                foreach (ParticleSystem child in go.GetComponentsInChildren<ParticleSystem>(true))
                {
                    if (child.gameObject != go) Object.Destroy(child.gameObject);
                }
                var system = go.GetComponent<ParticleSystem>();
                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = system.main;
                main.loop = true;
                main.playOnAwake = false;
                main.maxParticles = 2500;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.gravityModifier = 0f;
                main.startColor = Color.white;
                var emission = system.emission;
                emission.enabled = false;
                var shape = system.shape;
                shape.enabled = false;
                var velocity = system.velocityOverLifetime;
                velocity.enabled = false;
                var noise = system.noise;
                noise.enabled = true;
                noise.strength = 0.4f;
                noise.frequency = 2f;
                var collision = system.collision;
                collision.enabled = false;
                var sub = system.subEmitters;
                sub.enabled = false;
                var color = system.colorOverLifetime;
                color.enabled = true;
                color.color = new ParticleSystem.MinMaxGradient(new Gradient
                {
                    colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    alphaKeys = new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.9f, 0.7f), new GradientAlphaKey(0f, 1f) },
                });
                var size = system.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.8f, 1f, 1.4f));
                var renderer = go.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                if (renderer.sharedMaterial != null)
                {
                    var material = new Material(renderer.sharedMaterial) { name = "PumpWater" };
                    foreach (string property in new[] { "_BaseColor", "_Color", "_TintColor" })
                    {
                        if (material.HasProperty(property)) material.SetColor(property, Color.white);
                    }
                    renderer.sharedMaterial = material;
                }
                system.Play(true);
                _particles = system;
            }
            catch (System.Exception e)
            {
                _failed = true;
                Melon<MiningMod>.Logger.Error($"Pump water effects failed: {e}");
            }
            return _particles;
        }
    }
}
