using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.State;
using Ships.Interface.Model.Parts.StateTypes;
using Ships.Parts.Common;
using UnityEngine;

namespace Mining.Content
{
    // Okno rdzenia reaktora świeci na cyjan proporcjonalnie do produkcji prądu (stan generatora: NormalizedProductionLevel).
    internal class ReactorVisuals : DefaultShipPartVisuals
    {
        private static readonly Color Cyan = new Color(0.3f, 0.9f, 1f);
        private Material _core;
        private float _target;
        private float _glow = -1f;

        private void Awake()
        {
            var renderer = transform.Find("Core")?.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                _core = renderer.material; // własna kopia materiału dla tego reaktora
            }
        }

        public override void SetNetworkState(IStatefulPartState state) => Read(state);

        public override void ReactToNetworkStateChange(StatefulPartStateChangeEvent change) => Read(change.NewState);

        private void Read(IStatefulPartState state)
        {
            _target = state is WoodPowerGeneratorState generator ? Mathf.Clamp01(generator.NormalizedProductionLevel) : 0f;
        }

        private void Update()
        {
            if (_core == null)
            {
                return;
            }
            float glow = Mathf.MoveTowards(_glow < 0f ? _target : _glow, _target, Time.deltaTime * 0.8f);
            if (Mathf.Approximately(glow, _glow))
            {
                return;
            }
            _glow = glow;
            float pulse = 1f + 0.15f * Mathf.Sin(Time.time * 3f) * glow;
            if (_core.HasProperty("_EmissionColor"))
            {
                _core.SetColor("_EmissionColor", Cyan * (0.15f + 2.2f * glow * pulse));
                _core.EnableKeyword("_EMISSION");
            }
        }

        private void OnDestroy()
        {
            if (_core != null) Destroy(_core);
        }
    }
}
