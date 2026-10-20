using System.Collections.Generic;
using System.Linq;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.State;
using Ships.Interface.Model.Parts.StateTypes;
using Ships.Parts.Common;
using UnityEngine;

namespace Odyssey.Content
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
        private static readonly Color DeepBlue = new Color(0.08f, 0.25f, 1f);
        private const float MaxSpin = 260f, SpinAccel = 45f;
        private float _spin, _flicker, _nextFlicker;
        private const float Glow = 4f;
        private Material _body;
        private Renderer _glow;
        private Transform _rotor;
        private Light _light;
        private float _scale = 1f;
        private float _target, _level;
        private float _phase;
        private static readonly Color BeaconColor = new Color(1f, 0.45f, 0.08f);
        private const float BeaconSpeed = 300f;
        private Transform[] _beacons = new Transform[0];
        private Light[] _beaconLights = new Light[0];
        private Material _beaconMaterial;
        private bool _beaconsOn;
        private Color _beaconColor = Unset;
        private readonly Audio.LoopEmitter _sound = new Audio.LoopEmitter("reactor", 30f, PowerMod.ReactorVolume);

        private void Awake()
        {
            Transform bodyTransform = transform.Find("Body");
            var body = bodyTransform != null ? bodyTransform.GetComponent<MeshRenderer>() : null;
            if (body != null)
            {
                _scale = bodyTransform.localScale.x;
            }
            _rotor = transform.Find("Rotor");
            var glow = (transform.Find("Glow") ?? _rotor)?.GetComponent<MeshRenderer>();
            if (glow != null)
            {
                _glow = glow;
                _body = glow.material;
            }
            _phase = Random.value * 10f;
            Transform existing = transform.Find("ReactorLight");
            _light = existing != null && existing.GetComponent<Light>() != null ? existing.GetComponent<Light>() : new GameObject("ReactorLight").AddComponent<Light>();
            _light.transform.SetParent(transform, false);
            _light.transform.localPosition = Reactor.RotorPosition * _scale;
            _light.type = LightType.Point;
            _light.color = Cyan;
            _light.range = 4.5f * _scale;
            _light.shadows = LightShadows.None;
            _light.intensity = 0f;
            _light.enabled = false;
            SetUpBeacons();
        }

        private void SetUpBeacons()
        {
            var beacons = new List<Transform>();
            for (int i = 0; i < Reactor.BeaconPositions.Length; i++)
            {
                Transform beacon = transform.Find("Beacon" + i);
                if (beacon != null) beacons.Add(beacon);
            }
            _beacons = beacons.ToArray();
            _beaconLights = new Light[_beacons.Length];
            MeshRenderer first = _beacons.Length > 0 ? _beacons[0].GetComponent<MeshRenderer>() : null;
            _beaconMaterial = first != null ? first.material : null;
            for (int i = 0; i < _beacons.Length; i++)
            {
                MeshRenderer renderer = _beacons[i].GetComponent<MeshRenderer>();
                if (renderer != null && _beaconMaterial != null) renderer.sharedMaterial = _beaconMaterial;
                Transform existing = _beacons[i].Find("BeaconLight");
                Light light = existing != null ? existing.GetComponent<Light>() : null;
                if (light == null)
                {
                    light = new GameObject("BeaconLight").AddComponent<Light>();
                    light.transform.SetParent(_beacons[i], false);
                }
                light.transform.localPosition = new Vector3(0f, 0.05f, 0f);
                light.transform.localRotation = Quaternion.LookRotation(new Vector3(1f, -0.18f, 0f));
                light.type = LightType.Spot;
                light.spotAngle = 50f;
                light.range = 8f * _scale;
                light.color = BeaconColor;
                light.shadows = LightShadows.None;
                light.intensity = 0f;
                light.enabled = false;
                _beaconLights[i] = light;
            }
        }

        private void UpdateBeacons(float dt)
        {
            bool on = _level > 0.05f;
            if (on != _beaconsOn)
            {
                _beaconsOn = on;
                MelonLoader.Melon<PowerMod>.Logger.Msg($"Reactor beacons {(on ? "on" : "off")}: level {_level:0.00}, {_beacons.Length} beacons, material {_beaconMaterial?.shader?.name ?? "none"}, emission {(_beaconMaterial != null && _beaconMaterial.HasProperty(EmissionId))}, active {gameObject.activeInHierarchy}");
            }
            SetEmission(_beaconMaterial, ref _beaconColor, on ? Color.white * 6f : Color.black);
            for (int i = 0; i < _beacons.Length; i++)
            {
                if (on)
                {
                    _beacons[i].Rotate(Vector3.up, BeaconSpeed * dt, Space.Self);
                }
                Light light = _beaconLights[i];
                if (light != null)
                {
                    light.enabled = on;
                    light.intensity = on ? 8f * _scale : 0f;
                }
            }
        }

        public override void SetNetworkState(IStatefulPartState state)
        {
            Read(state);
            _level = _target;
            MelonLoader.Melon<PowerMod>.Logger.Msg($"Reactor visuals: level {_target:0.00}, rotor {_rotor != null}, glow {_body != null}, light {_light != null}");
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
            _spin = Mathf.MoveTowards(_spin, MaxSpin * _level, SpinAccel * dt);
            _sound.Update(transform.position + transform.up * 0.8f * _scale, Mathf.SmoothStep(0f, 1f, _level), 0.75f + 0.3f * _level);
            if (_glow != null && _glow.forceRenderingOff)
            {
                return;
            }
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
            Color tint = Color.Lerp(DeepBlue, Cyan, warm);
            float glow = (0.15f + _level * Glow * pulse) * _flicker;
            SetEmission(_body, ref _bodyColor, tint * glow);
            if (_light != null)
            {
                _light.enabled = _level > 0.01f;
                _light.color = tint;
                _light.intensity = _level * 1.6f * _scale * pulse * _flicker;
            }
            if (_spin > 0.01f && _rotor != null)
            {
                _rotor.Rotate(Vector3.up, -_spin * dt, Space.Self);
            }
            UpdateBeacons(dt);
        }

        private void OnDisable() => _sound.Stop();

        private void OnDestroy()
        {
            _sound.Stop();
            if (_body != null) Destroy(_body);
        }
    }
}
