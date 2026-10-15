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
                _body = body.material;
                _scale = bodyTransform.localScale.x;
            }
            _rotor = transform.Find("Rotor");
            var rotor = _rotor != null ? _rotor.GetComponent<MeshRenderer>() : null;
            if (rotor != null && _body != null) rotor.sharedMaterial = _body;
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
            _level = Mathf.MoveTowards(_level, _target, Time.deltaTime / (_target > _level ? 3f : 2f));
            float t = Time.time + _phase;
            float pulse = 1f + 0.25f * Mathf.Sin(t * 2.2f) + 0.08f * Mathf.Sin(t * 13f);
            SetEmission(_body, ref _bodyColor, Cyan * (0.04f + _level * Glow * pulse));
            if (_light != null)
            {
                _light.enabled = _level > 0.01f;
                _light.intensity = _level * 1.6f * _scale * pulse;
            }
            float spin = _level * Time.deltaTime;
            if (spin > 0f && _rotor != null)
            {
                _rotor.Rotate(Vector3.up, -220f * spin, Space.Self);
            }
            _sound.Update(transform.position + transform.up * 0.8f * _scale, Mathf.SmoothStep(0f, 1f, _level), 0.75f + 0.3f * _level);
        }

        private void OnDisable() => _sound.Stop();

        private void OnDestroy()
        {
            _sound.Stop();
            if (_body != null) Destroy(_body);
        }
    }
}
