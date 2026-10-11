using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using MelonLoader;
using MelonLoader.Utils;
using Tools.Common;
using UnityEngine;
using WorldTracking.Interface.Values;
using Object = UnityEngine.Object;

namespace Mining.Content
{
    // Ogień i kurz na ziemi pod lecącym jetpackiem (kopie mgiełki gry DustPoof, jak pył koparki) oraz własny
    // dźwięk ognia, jeśli jest plik. Jeden zestaw na postać - podpięty pod jej efekty, więc znika razem z nią.
    internal sealed class JetpackFx : MonoBehaviour
    {
        private const float MaxGroundDistance = 6f;
        private const float MaxHearDistance = 40f;

        private ParticleSystem _fire;
        private ParticleSystem _dust;
        private bool _firePlaying;
        private bool _dustPlaying;
        private FMOD.Channel _channel;
        private bool _hasChannel;
        private float _intensity;

        internal bool Flying;
        internal Vector3 Up = Vector3.up;

        internal static JetpackFx For(Component effects)
        {
            var fx = effects.GetComponent<JetpackFx>();
            return fx != null ? fx : effects.gameObject.AddComponent<JetpackFx>();
        }

        private void Awake()
        {
            _fire = CreateEffect("JetpackGroundFire", lifetime: (0.25f, 0.55f), size: (0.25f, 0.6f), speed: (2.5f, 4.5f), rate: 45f, angle: 80f, radius: 0.2f, grow: 1.6f);
            if (_fire != null)
            {
                var color = _fire.colorOverLifetime;
                color.enabled = true;
                color.color = new ParticleSystem.MinMaxGradient(new Gradient
                {
                    colorKeys = new[] { new GradientColorKey(new Color(1f, 0.95f, 0.6f), 0f), new GradientColorKey(new Color(1f, 0.55f, 0.1f), 0.35f), new GradientColorKey(new Color(0.6f, 0.15f, 0.05f), 1f) },
                    alphaKeys = new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.7f, 0.5f), new GradientAlphaKey(0f, 1f) },
                });
                var main = _fire.main;
                main.startColor = Color.white;
            }
            _dust = CreateEffect("JetpackGroundDust", lifetime: (1.2f, 2.2f), size: (0.6f, 1.3f), speed: (1.2f, 2.4f), rate: 14f, angle: 85f, radius: 0.4f, grow: 2.2f);
            if (_dust != null)
            {
                var main = _dust.main;
                main.startColor = new Color(0.55f, 0.5f, 0.45f, 0.35f);
            }
        }

        private ParticleSystem CreateEffect(string name, (float, float) lifetime, (float, float) size, (float, float) speed, float rate, float angle, float radius, float grow)
        {
            ParticleSystem template = Resources.FindObjectsOfTypeAll<ParticleSystem>().FirstOrDefault(p => p.name == "DustPoof");
            if (template == null)
            {
                return null;
            }
            ParticleSystem effect = Instantiate(template.gameObject, transform).GetComponent<ParticleSystem>();
            effect.name = name;
            effect.transform.localScale = Vector3.one;
            effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = effect.main;
            main.loop = true;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.Item1, lifetime.Item2);
            main.startSize = new ParticleSystem.MinMaxCurve(size.Item1, size.Item2);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.Item1, speed.Item2);
            main.maxParticles = 80;
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
            var sizeOverLifetime = effect.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, grow));
            return effect;
        }

        private void LateUpdate()
        {
            bool onGround = false;
            if (Flying && ClientScene.PhysicsScene.Raycast(transform.position + Up * 0.5f, -Up, out RaycastHit hit, MaxGroundDistance + 0.5f, RaycastMasks.BuildDetectionLayer))
            {
                onGround = true;
                // ogień "rozlewa się" po ziemi: stożek z punktu trafienia, skierowany wzdłuż normalnej podłoża
                Quaternion rotation = Quaternion.LookRotation(hit.normal, Vector3.ProjectOnPlane(transform.forward, hit.normal) + hit.normal * 0.001f);
                float closeness = 1f - Mathf.Clamp01((hit.distance - 0.5f) / MaxGroundDistance);
                if (_fire != null)
                {
                    _fire.transform.SetPositionAndRotation(hit.point + hit.normal * 0.05f, rotation);
                    var emission = _fire.emission;
                    emission.rateOverTime = 45f * closeness;
                }
                if (_dust != null)
                {
                    _dust.transform.SetPositionAndRotation(hit.point + hit.normal * 0.1f, rotation);
                    var emission = _dust.emission;
                    emission.rateOverTime = 14f * Mathf.Sqrt(closeness);
                }
            }
            SetPlaying(_fire, ref _firePlaying, onGround);
            SetPlaying(_dust, ref _dustPlaying, onGround);

            _intensity = Mathf.MoveTowards(_intensity, Flying ? 1f : 0f, Time.deltaTime * (Flying ? 6f : 4f));
            UpdateSound();
        }

        private static void SetPlaying(ParticleSystem effect, ref bool playing, bool shouldPlay)
        {
            if (effect == null || playing == shouldPlay)
            {
                return;
            }
            if (shouldPlay) effect.Play(true);
            else effect.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            playing = shouldPlay;
        }

        // Własny dźwięk ognia gra tylko wtedy, gdy jest plik (inaczej zostaje dźwięk dysz z gry).
        internal static bool HasCustomSound => JetpackSound.TryGet(out _);

        private void UpdateSound()
        {
            Camera cam = Camera.main;
            float distance = cam != null ? Vector3.Distance(cam.transform.position, transform.position) : float.MaxValue;
            float attenuation = Mathf.Clamp01(1f - distance / MaxHearDistance);
            float volume = _intensity * attenuation * attenuation * MiningMod.JetpackVolume.Value;
            if (volume <= 0.001f || !JetpackSound.TryGet(out FMOD.Sound sound))
            {
                StopSound();
                return;
            }
            if (!_hasChannel)
            {
                _channel = Audio.DrillSound.Play(sound);
                _hasChannel = true;
            }
            _channel.setVolume(volume);
            _channel.setPitch(0.85f + 0.15f * _intensity);
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

    // Dźwięk ognia jetpacka (pętla): UserData/Mining/jetpack.(wav|ogg|mp3) albo plik w modzie Assets/jetpack_loop.*
    internal static class JetpackSound
    {
        private static FMOD.Sound _sound;
        private static bool _created;
        private static bool _missing;

        internal static bool TryGet(out FMOD.Sound sound)
        {
            sound = _sound;
            if (_created || _missing)
            {
                return _created;
            }
            try
            {
                if (!FMODUnity.RuntimeManager.IsInitialized)
                {
                    return false;
                }
                string source = null;
                byte[] data = null;
                foreach (string ext in new[] { "wav", "ogg", "mp3" })
                {
                    string path = Path.Combine(MelonEnvironment.UserDataDirectory, "Mining", "jetpack." + ext);
                    if (File.Exists(path)) { data = File.ReadAllBytes(path); source = path; break; }
                    data = ModAssets.Read("jetpack_loop." + ext);
                    if (data != null) { source = "jetpack_loop." + ext; break; }
                }
                if (data == null)
                {
                    _missing = true;
                    return false;
                }
                var info = new FMOD.CREATESOUNDEXINFO { cbsize = Marshal.SizeOf(typeof(FMOD.CREATESOUNDEXINFO)), length = (uint)data.Length };
                FMOD.MODE mode = FMOD.MODE.LOOP_NORMAL | FMOD.MODE._2D | FMOD.MODE.CREATESAMPLE | FMOD.MODE.OPENMEMORY;
                FMOD.RESULT result = FMODUnity.RuntimeManager.CoreSystem.createSound(data, mode, ref info, out _sound);
                if (result != FMOD.RESULT.OK)
                {
                    throw new Exception($"createSound: {result}");
                }
                _created = true;
                sound = _sound;
                Melon<MiningMod>.Logger.Msg($"Dźwięk jetpacka: {source}");
            }
            catch (Exception e)
            {
                _missing = true;
                Melon<MiningMod>.Logger.Warning($"Nie udało się przygotować dźwięku jetpacka: {e.Message}");
            }
            return _created;
        }
    }
}
