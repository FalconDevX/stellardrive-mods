using System.Linq;
using Items.Model;
using Mining.Audio;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.State;
using Ships.Interface.Model.Parts.StateTypes;
using Ships.Parts.Common;
using UnityEngine;

namespace Mining.Content
{
    // Wygląd koparki u każdego gracza: wiertło rozkręca się, wysuwa w ziemię i kręci, a silnik warczy, gdy koparka pracuje.
    // "Pracuje" = znacznik ustawiany przez serwer w ukrytym slocie (Excavator.WorkingMarkerSlot) - zmienia się od razu po odcięciu prądu lub sygnału.
    internal class ExcavatorVisuals : DefaultShipPartVisuals
    {
        private const float MaxRpm = 240f;
        private const float RestLift = 1.23f;  // w spoczynku wał schowany w korpusie i wieżyczce, czubek tuż nad ziemią
        private const float Extend = 0.9f;     // przy pracy wysuwa się tyle w dół (w ziemię)
        private const float MaxHearDistance = 35f;

        private Transform _drill;
        private Vector3 _drillRest;
        private float _speed;
        private float _extend;
        private FMOD.Channel _channel;
        private bool _hasChannel;

        internal bool ForceWorking;

        // Pył spod wiertła: kopia mgiełki, którą gra pokazuje przy ścinaniu drzew (DustPoof).
        private static ParticleSystem _dustTemplate;
        private ParticleSystem _dust;
        private bool _dustPlaying;

        private void Awake()
        {
            _drill = transform.Find("DrillPivot");
            if (_drill != null)
            {
                _drillRest = _drill.localPosition;
            }
            CreateDust();
        }

        private void CreateDust()
        {
            if (_dustTemplate == null)
            {
                _dustTemplate = Resources.FindObjectsOfTypeAll<ParticleSystem>().FirstOrDefault(p => p.name == "DustPoof");
            }
            if (_dustTemplate == null || _drill == null)
            {
                return;
            }
            _dust = Instantiate(_dustTemplate.gameObject, transform).GetComponent<ParticleSystem>();
            _dust.name = "DrillDust";
            // Przy ziemi pod wiertłem, cząsteczki lecą w górę.
            _dust.transform.localPosition = new Vector3(_drillRest.x, 0.05f, _drillRest.z);
            _dust.transform.localRotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);
            _dust.transform.localScale = Vector3.one;
            _dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            // W grze to jednorazowy duży obłok (8 m, 12 s) - tu ciągła, niska chmurka kurzu przy wiertle.
            var main = _dust.main;
            main.loop = true;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.7f);
            main.maxParticles = 40;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = _dust.emission;
            emission.enabled = true;
            emission.rateOverTime = 7f;
            emission.SetBursts(new ParticleSystem.Burst[0]);
            var shape = _dust.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 55f;
            shape.radius = 0.25f;
        }

        private bool _serverWorking;

        public override void SetNetworkState(IStatefulPartState state)
        {
            base.SetNetworkState(state);
            _serverWorking = state is StorageState s && Excavator.HasWorkingMarker(s);
        }

        public override void ReactToNetworkStateChange(StatefulPartStateChangeEvent change)
        {
            base.ReactToNetworkStateChange(change);
            _serverWorking = change.NewState is StorageState s && Excavator.HasWorkingMarker(s);
        }

        private void Update()
        {
            bool working = ForceWorking || _serverWorking;
            _speed = Mathf.MoveTowards(_speed, working ? 1f : 0f, Time.deltaTime * (working ? 0.8f : 2.5f)); // zatrzymuje się szybko

            if (_drill != null)
            {
                // Najpierw się rozkręca, potem powoli wysuwa w dół; po zatrzymaniu chowa się z powrotem.
                _extend = Mathf.MoveTowards(_extend, _speed > 0.6f ? 1f : 0f, Time.deltaTime * (_speed > 0.6f ? 0.25f : 0.4f));
                _drill.localPosition = _drillRest + Vector3.up * RestLift - Vector3.up * (Extend * Mathf.SmoothStep(0f, 1f, _extend));
                _drill.Rotate(Vector3.up, _speed * MaxRpm * 6f * Time.deltaTime, Space.Self);
            }
            UpdateDust();
            UpdateSound();
        }

        private void UpdateDust()
        {
            if (_dust == null)
            {
                return;
            }
            bool digging = _extend > 0.25f && _speed > 0.5f;
            if (digging && !_dustPlaying)
            {
                _dust.Play(true);
                _dustPlaying = true;
            }
            else if (!digging && _dustPlaying)
            {
                _dust.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                _dustPlaying = false;
            }
        }

        private void UpdateSound()
        {
            Camera cam = Camera.main;
            float distance = cam != null ? Vector3.Distance(cam.transform.position, transform.position) : float.MaxValue;
            float attenuation = Mathf.Clamp01(1f - distance / MaxHearDistance);
            float volume = _speed * attenuation * attenuation * MiningMod.DrillVolume.Value;

            if (volume <= 0.001f)
            {
                StopSound();
                return;
            }
            if (!_hasChannel)
            {
                if (!DrillSound.TryGet(out FMOD.Sound sound))
                {
                    return;
                }
                _channel = DrillSound.Play(sound);
                _hasChannel = true;
            }
            _channel.setVolume(volume);
            _channel.setPitch(0.55f + 0.45f * _speed);
            if (cam != null)
            {
                Vector3 local = cam.transform.InverseTransformPoint(transform.position);
                _channel.setPan(Mathf.Clamp(local.x / Mathf.Max(1f, local.magnitude), -1f, 1f) * 0.8f);
            }
        }

        private void StopSound()
        {
            if (_hasChannel)
            {
                _channel.stop();
                _hasChannel = false;
            }
        }

        private void OnDisable() => StopSound();

        private void OnDestroy() => StopSound();
    }
}
