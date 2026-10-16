using HarmonyLib;
using Ships.Interface.Model.Parts;
using Ships.Parts.Thruster;
using UnityEngine;

namespace Mining.Content
{
    internal class HydrogenThrusterSound : MonoBehaviour
    {
        private Audio.LoopEmitter _sound;
        private float _target, _level, _pitch = 1f;

        internal void SetIntensity(float intensity) => _target = Mathf.Clamp01(intensity);

        private void Awake()
        {
            var bounds = GetComponentInParent<ShipPartBounds>();
            float size = bounds != null ? bounds.bounds.magnitude : 1.5f;
            _pitch = Mathf.Clamp(1.12f - 0.055f * size, 0.68f, 1.08f);
            _sound = new Audio.LoopEmitter("hydrogen", 70f + 18f * size, MiningMod.HydrogenEngineVolume);
        }

        private void Update()
        {
            _level = Mathf.MoveTowards(_level, _target, Time.deltaTime * (_target > _level ? 2.5f : 1.5f));
            float level = _level > 0f ? 0.35f + 0.65f * _level : 0f;
            _sound.Update(transform.position, level, _pitch * (0.88f + 0.2f * _level));
        }

        private void OnDisable()
        {
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
