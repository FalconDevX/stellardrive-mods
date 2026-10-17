using System.Linq;
using Items.Model;
using Odyssey.Audio;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.State;
using Ships.Interface.Model.Parts.StateTypes;
using Ships.Parts.Common;
using UnityEngine;

namespace Odyssey.Content
{
    internal class ExcavatorVisuals : DefaultShipPartVisuals
    {
        private const float MaxRpm = 240f;
        private const float RestLift = 1.23f;
        private const float Extend = 0.9f;
        private const float MaxHearDistance = 25f;

        private Transform _drill;
        private Vector3 _drillRest;
        private float _speed;
        private float _extend;
        private FMOD.Channel _channel;
        private bool _hasChannel;

        internal bool ForceWorking;

        private static readonly Vector3 ExhaustTop = new Vector3(-0.44f, 1.99f, 0.34f);
        private ParticleSystem _dust;
        private ParticleSystem _smoke;
        private bool _dustPlaying;
        private bool _smokePlaying;

        private void Awake()
        {
            _drill = transform.Find("DrillPivot");
            if (_drill != null)
            {
                _drillRest = _drill.localPosition;
                _dust = CreateEffect("DustPoof", "DrillDust", new Vector3(_drillRest.x, 0.05f, _drillRest.z),
                    lifetime: (1.8f, 3f), size: (0.6f, 1.4f), speed: (0.2f, 0.7f), rate: 7f, angle: 55f, radius: 0.25f, grow: 1f);
            }
            _smoke = CreateEffect("SmokePoof", "ExhaustSmoke", ExhaustTop,
                lifetime: (2.5f, 3.5f), size: (0.15f, 0.3f), speed: (0.9f, 1.3f), rate: 6f, angle: 8f, radius: 0.03f, grow: 5f);
            if (_smoke != null)
            {
                var main = _smoke.main;
                main.startColor = new Color(0.55f, 0.55f, 0.56f, 0.32f);
                var color = _smoke.colorOverLifetime;
                color.enabled = true;
                var gradient = new Gradient
                {
                    colorKeys = new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.85f, 0.85f, 0.85f), 1f) },
                    alphaKeys = new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.6f, 0.6f), new GradientAlphaKey(0f, 1f) },
                };
                color.color = new ParticleSystem.MinMaxGradient(gradient);
            }
        }

        private ParticleSystem CreateEffect(string templateName, string name, Vector3 localPosition, (float, float) lifetime, (float, float) size,
            (float, float) speed, float rate, float angle, float radius, float grow)
        {
            ParticleSystem template = ParticleTemplates.Get(templateName);
            if (template == null)
            {
                return null;
            }
            ParticleSystem effect = Instantiate(template.gameObject, transform).GetComponent<ParticleSystem>();
            effect.name = name;
            effect.transform.localPosition = localPosition;
            effect.transform.localRotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);
            effect.transform.localScale = Vector3.one;
            effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = effect.main;
            main.loop = true;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.Item1, lifetime.Item2);
            main.startSize = new ParticleSystem.MinMaxCurve(size.Item1, size.Item2);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.Item1, speed.Item2);
            main.maxParticles = 40;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = effect.emission;
            emission.enabled = true;
            emission.rateOverTime = rate;
            emission.SetBursts(new ParticleSystem.Burst[0]);
            var shape = effect.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = angle;
            shape.radius = radius;
            if (grow > 1f)
            {
                var sizeOverLifetime = effect.sizeOverLifetime;
                sizeOverLifetime.enabled = true;
                sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, grow));
            }
            return effect;
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
            _speed = Mathf.MoveTowards(_speed, working ? 1f : 0f, Time.deltaTime * (working ? 0.8f : 2.5f));

            if (_drill != null)
            {
                _extend = Mathf.MoveTowards(_extend, _speed > 0.6f ? 1f : 0f, Time.deltaTime * (_speed > 0.6f ? 0.25f : 0.4f));
                _drill.localPosition = _drillRest + Vector3.up * RestLift - Vector3.up * (Extend * Mathf.SmoothStep(0f, 1f, _extend));
                _drill.Rotate(Vector3.up, _speed * MaxRpm * 6f * Time.deltaTime, Space.Self);
            }
            UpdateDust();
            UpdateSound();
        }

        private void UpdateDust()
        {
            SetPlaying(_dust, ref _dustPlaying, _extend > 0.25f && _speed > 0.5f);
            SetPlaying(_smoke, ref _smokePlaying, _speed > 0.3f);
        }

        private static void SetPlaying(ParticleSystem effect, ref bool playing, bool shouldPlay)
        {
            if (effect == null || playing == shouldPlay)
            {
                return;
            }
            if (shouldPlay)
            {
                effect.Play(true);
            }
            else
            {
                effect.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            playing = shouldPlay;
        }

        private void UpdateSound()
        {
            Camera cam = Camera.main;
            float distance = cam != null ? Vector3.Distance(cam.transform.position, transform.position) : float.MaxValue;
            float attenuation = Mathf.Clamp01(1f - distance / MaxHearDistance);
            float volume = _speed * attenuation * attenuation * MiningMod.DrillVolume.Value * 0.15f;

            if (volume <= 0.001f)
            {
                StopSound();
                return;
            }
            if (_hasChannel && !DrillSound.IsAlive(_channel))
            {
                _hasChannel = false;
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
            _channel.setPitch(0.4f + 0.3f * _speed);
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
