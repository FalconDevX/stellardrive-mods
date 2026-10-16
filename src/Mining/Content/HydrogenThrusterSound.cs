using HarmonyLib;
using Ships.Interface.Model.Parts;
using Ships.Parts.Thruster;
using UnityEngine;

namespace Mining.Content
{
    internal class HydrogenThrusterSound : MonoBehaviour
    {
        private Audio.LoopEmitter _sound;
        private Audio.OneShotChannel _start, _stop;
        private float _target, _level, _peak, _pitch = 1f, _hear = 90f, _lastStop = -10f, _lastStart = -10f;

        internal void SetIntensity(float intensity)
        {
            float next = Mathf.Clamp01(intensity);
            if (next <= 0.01f && _target > 0.01f)
            {
                Shutdown();
            }
            else if (next > 0.01f && _target <= 0.01f && _level < 0.3f)
            {
                Ignite();
            }
            _target = next;
        }

        private void Awake()
        {
            var bounds = GetComponentInParent<ShipPartBounds>();
            float size = bounds != null ? bounds.bounds.magnitude : 1.5f;
            _pitch = Mathf.Clamp(1.12f - 0.055f * size, 0.68f, 1.08f);
            _hear = 70f + 18f * size;
            _sound = new Audio.LoopEmitter("hydrogen", _hear, MiningMod.HydrogenEngineVolume);
        }

        private void Update()
        {
            _level = Mathf.MoveTowards(_level, _target, Time.deltaTime * (_target > _level ? 3f : 0.9f));
            _peak = _target > 0.01f ? Mathf.Max(_peak * Mathf.Exp(-Time.deltaTime * 0.5f), _level) : _peak;
            float volume = Mathf.Pow(_level, 0.7f);
            _sound.Update(transform.position, volume, _pitch * (0.72f + 0.36f * _level));
            _start.Follow(transform.position);
            _stop.Follow(transform.position);
        }

        private void Ignite()
        {
            if (Time.time - _lastStart < 0.8f)
            {
                return;
            }
            _lastStart = Time.time;
            _start = Audio.OneShot.Play("hydrogen_start", transform.position, _hear, MiningMod.HydrogenEngineVolume.Value * 0.45f, _pitch);
        }

        private void Shutdown()
        {
            float strength = Mathf.Max(_level, _peak);
            _peak = 0f;
            if (strength < 0.15f || Time.time - _lastStop < 0.6f)
            {
                return;
            }
            _lastStop = Time.time;
            _stop = Audio.OneShot.Play("hydrogen_stop", transform.position, _hear, MiningMod.HydrogenEngineVolume.Value * (0.25f + 0.35f * strength), _pitch);
        }

        private void OnDisable()
        {
            if (_target > 0.01f || _level > 0.15f)
            {
                Shutdown();
            }
            _target = 0f;
            _level = 0f;
            _sound?.Stop();
        }

        private void OnDestroy() => OnDisable();
    }

    [HarmonyPatch(typeof(ThrusterSFX), nameof(ThrusterSFX.SetIntensity))]
    internal static class HydrogenThrusterSoundPatch
    {
        private static bool Prefix(ThrusterSFX __instance, float intensity)
        {
            var sound = __instance.GetComponent<HydrogenThrusterSound>();
            if (sound == null)
            {
                return true;
            }
            sound.SetIntensity(intensity);
            return false;
        }
    }
}
