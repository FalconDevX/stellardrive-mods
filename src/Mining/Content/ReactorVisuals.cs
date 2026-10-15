using System.Linq;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.State;
using Ships.Interface.Model.Parts.StateTypes;
using Ships.Parts.Common;
using UnityEngine;

namespace Mining.Content
{
    internal class ReactorVisuals : DefaultShipPartVisuals
    {
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        private static readonly Color Unset = new Color(-1f, -1f, -1f, -1f);
        private Color _bodyColor = Unset;

        private static void SetEmission(Material material, ref Color last, Color value)
        {
            if (material == null || last == value)
            {
                return;
            }
            last = value;
            material.SetColor(EmissionId, value);
        }

        private static readonly Color Cyan = new Color(0.3f, 0.9f, 1f);
        private static readonly Color Amber = new Color(1f, 0.55f, 0.12f);
        private const float MaxSpin = 260f, SpinAccel = 45f;
        private float _spin, _flicker, _nextFlicker;
        private ParticleSystem _steam;
        private bool _steamTried;
        private const float Glow = 4f;
        private Material _body;
        private Transform _rotor;
        private Light _light;
        private float _scale = 1f;
        private float _target, _level;
        private float _phase;
        private readonly Audio.LoopEmitter _sound = new Audio.LoopEmitter("reactor", 30f, MiningMod.ReactorVolume);

        private void Awake()
        {
            Transform bodyTransform = transform.Find("Body");
            var body = bodyTransform != null ? bodyTransform.GetComponent<MeshRenderer>() : null;
            if (body != null)
            {
                _scale = bodyTransform.localScale.x;
            }
            _rotor = transform.Find("Rotor");
            var glow = transform.Find("Glow")?.GetComponent<MeshRenderer>();
            if (glow != null)
            {
                _body = glow.material;
            }
            _phase = Random.value * 10f;
            Transform existing = transform.Find("Glow");
            _light = existing != null ? existing.GetComponent<Light>() : new GameObject("Glow").AddComponent<Light>();
            _light.transform.SetParent(transform, false);
            _light.transform.localPosition = new Vector3(0f, 0.75f * _scale, 0f);
            _light.type = LightType.Point;
            _light.color = Cyan;
            _light.range = 4.5f * _scale;
            _light.shadows = LightShadows.None;
            _light.intensity = 0f;
            _light.enabled = false;
        }

        public override void SetNetworkState(IStatefulPartState state)
        {
            Read(state);
            _level = _target;
        }

        public override void ReactToNetworkStateChange(StatefulPartStateChangeEvent change) => Read(change.NewState);

        private void Read(IStatefulPartState state)
        {
            _target = state is WoodPowerGeneratorState generator ? Mathf.Clamp01(generator.NormalizedProductionLevel) : 0f;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            bool rising = _target > _level + 0.02f;
            _level = Mathf.MoveTowards(_level, _target, dt / (rising ? 3f : 2f));
            float t = Time.time + _phase;
            float pulse = 1f + 0.25f * Mathf.Sin(t * 2.2f) + 0.08f * Mathf.Sin(t * 13f);
            if (rising && _level < 0.6f)
            {
                if (Time.time >= _nextFlicker)
                {
                    _nextFlicker = Time.time + Random.Range(0.04f, 0.18f);
                    _flicker = Random.value < 0.35f ? Random.Range(0.1f, 0.5f) : 1f;
                }
            }
            else
            {
                _flicker = 1f;
            }
            float warm = Mathf.Clamp01((_level - 0.15f) / 0.5f);
            Color tint = Color.Lerp(Amber, Cyan, warm);
            float glow = (0.15f + _level * Glow * pulse) * _flicker;
            SetEmission(_body, ref _bodyColor, tint * glow);
            if (_light != null)
            {
                _light.enabled = _level > 0.01f;
                _light.color = tint;
                _light.intensity = _level * 1.6f * _scale * pulse * _flicker;
            }
            _spin = Mathf.MoveTowards(_spin, MaxSpin * _level, SpinAccel * dt);
            if (_spin > 0.01f && _rotor != null)
            {
                _rotor.Rotate(Vector3.up, -_spin * dt, Space.Self);
            }
            UpdateSteam();
            _sound.Update(transform.position + transform.up * 0.8f * _scale, Mathf.SmoothStep(0f, 1f, _level), 0.75f + 0.3f * _level);
        }

        private void UpdateSteam()
        {
            if (_level > 0.2f && !_steamTried)
            {
                _steamTried = true;
                _steam = CreateSteam();
            }
            if (_steam == null)
            {
                return;
            }
            var emission = _steam.emission;
            emission.rateOverTime = _level > 0.2f ? 2f + 7f * _level : 0f;
            if (_level > 0.2f && !_steam.isPlaying)
            {
                _steam.Play();
            }
        }

        private ParticleSystem CreateSteam()
        {
            ParticleSystem template = Resources.FindObjectsOfTypeAll<ParticleSystem>().FirstOrDefault(p => p.name == "DustPoof");
            if (template == null)
            {
                return null;
            }
            ParticleSystem steam = Instantiate(template.gameObject, transform).GetComponent<ParticleSystem>();
            steam.name = "Steam";
            steam.transform.localPosition = new Vector3(0f, 1.72f * _scale, 0f);
            steam.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            steam.transform.localScale = Vector3.one;
            steam.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = steam.main;
            main.loop = true;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.18f * _scale, 0.32f * _scale);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            main.maxParticles = 40;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = Color.white;
            var emission = steam.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new ParticleSystem.Burst[0]);
            var shape = steam.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = 0.12f * _scale;
            var sizeOverLifetime = steam.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 3.2f));
            var color = steam.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(new Gradient
            {
                colorKeys = new[] { new GradientColorKey(new Color(0.9f, 0.93f, 0.95f), 0f), new GradientColorKey(new Color(0.8f, 0.82f, 0.85f), 1f) },
                alphaKeys = new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.35f, 0.15f), new GradientAlphaKey(0f, 1f) },
            });
            return steam;
        }

        private void OnDisable() => _sound.Stop();

        private void OnDestroy()
        {
            _sound.Stop();
            if (_body != null) Destroy(_body);
        }
    }
}
