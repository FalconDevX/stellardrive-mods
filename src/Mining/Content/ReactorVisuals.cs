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
        private Color _bodyColor = Unset, _coreColor = Unset;

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
        private Material _body, _core;
        private Transform[] _cores;
        private Transform _rotor;
        private Light _light;
        private const float SmallBodyGlow = 3.5f, LargeBodyGlow = 8f;
        private float _bodyGlow = SmallBodyGlow;
        private float _target, _level;
        private float _phase;
        private readonly Audio.LoopEmitter _sound = new Audio.LoopEmitter("reactor", 30f, MiningMod.ReactorVolume);

        private void Awake()
        {
            var body = transform.Find("Body")?.GetComponent<MeshRenderer>();
            if (body != null) _body = body.material;
            _cores = new Transform[Reactor.CorePositions.Length];
            Material core = null;
            for (int i = 0; i < _cores.Length; i++)
            {
                _cores[i] = transform.Find("Core" + i);
                var r = _cores[i] != null ? _cores[i].GetComponent<MeshRenderer>() : null;
                if (r == null) continue;
                if (core == null) core = r.material;
                r.sharedMaterial = core;
            }
            _core = core;
            _rotor = transform.Find("Rotor");
            _phase = Random.value * 10f;
            if (_rotor == null)
            {
                _bodyGlow = LargeBodyGlow;
                Transform existing = transform.Find("Glow");
                _light = existing != null ? existing.GetComponent<Light>() : new GameObject("Glow").AddComponent<Light>();
                _light.transform.SetParent(transform, false);
                _light.transform.localPosition = new Vector3(0f, 2.2f, 0f);
                _light.type = LightType.Point;
                _light.color = Cyan;
                _light.range = 9f;
                _light.shadows = LightShadows.None;
                _light.intensity = 0f;
                _light.enabled = false;
            }
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
            SetEmission(_body, ref _bodyColor, Cyan * (_level * _bodyGlow * pulse));
            if (_light != null)
            {
                _light.enabled = _level > 0.01f;
                _light.intensity = _level * 2.5f * pulse;
            }
            SetEmission(_core, ref _coreColor, Cyan * (0.05f + _level * 1.3f * pulse));
            float spin = _level * Time.deltaTime;
            if (spin > 0f)
            {
                foreach (Transform c in _cores)
                {
                    if (c != null) c.Rotate(Vector3.up, 420f * spin, Space.Self);
                }
                if (_rotor != null) _rotor.Rotate(Vector3.up, -160f * spin, Space.Self);
            }
            _sound.Update(transform.position + transform.up * 0.8f, Mathf.SmoothStep(0f, 1f, _level), 0.75f + 0.3f * _level);
        }

        private void OnDisable() => _sound.Stop();

        private void OnDestroy()
        {
            _sound.Stop();
            if (_body != null) Destroy(_body);
            if (_core != null) Destroy(_core);
        }
    }
}
