using System.Collections.Generic;
using HarmonyLib;
using Ships.Interface.Model.Parts.State;
using Ships.Interface.Model.Parts.StateTypes;
using Ships.Parts.GasExhaust;
using UnityEngine;
using UnityEngine.VFX;

namespace Mining.Content
{
    internal class VentExtras : MonoBehaviour
    {
        internal const string FanName = "Fan";
        internal const float AirBoost = 1.6f;
        private const float FanSpeed = 900f;

        private static readonly AccessTools.FieldRef<GasExhaustVisuals, VisualEffect> Main =
            AccessTools.FieldRefAccess<GasExhaustVisuals, VisualEffect>("gasVfx");

        private readonly List<VisualEffect> _extra = new List<VisualEffect>();
        private Transform _fan;
        private float _size = 1f, _level, _target, _rate;
        private bool _playing;
        private Audio.LoopEmitter _sound;

        internal static void Apply(GasExhaustVisuals visuals, float outputRate)
        {
            if (visuals == null) return;
            VentExtras extras = visuals.GetComponent<VentExtras>() ?? visuals.gameObject.AddComponent<VentExtras>();
            extras.Init(Main(visuals));
            VisualEffect main = Main(visuals);
            if (main != null && outputRate > 0f) main.SetFloat("OutputRate", outputRate / 0.5f * AirBoost);
            extras.SetRate(outputRate);
        }

        private bool _ready;

        private void Init(VisualEffect main)
        {
            if (_ready) return;
            _ready = true;
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name == FanName)
                {
                    _fan = t;
                    break;
                }
            }
            foreach (VisualEffect fx in GetComponentsInChildren<VisualEffect>(true))
            {
                if (fx != main) _extra.Add(fx);
            }
            _size = _fan != null ? Mathf.Max(1f, _fan.lossyScale.x / Mathf.Max(0.001f, transform.lossyScale.x)) : 1f;
            _sound = new Audio.LoopEmitter("fan", 8f + 4f * _size, MiningMod.VentVolume);
        }

        private void SetRate(float rate)
        {
            _rate = rate;
            _target = rate > 0f ? 1f : 0f;
            bool play = rate > 0f;
            foreach (VisualEffect fx in _extra)
            {
                if (fx == null) continue;
                if (play)
                {
                    fx.SetFloat("OutputRate", rate / 0.5f * AirBoost / Mathf.Max(1, _extra.Count) * 2f);
                    if (!_playing) fx.Play();
                }
                else if (_playing)
                {
                    fx.Stop();
                }
            }
            _playing = play;
        }

        private void Update()
        {
            _level = Mathf.MoveTowards(_level, _target, Time.deltaTime / (_target > _level ? 1.5f : 2.5f));
            if (_fan != null && _level > 0f)
            {
                _fan.Rotate(Vector3.up, FanSpeed / Mathf.Sqrt(_size) * _level * Time.deltaTime, Space.Self);
            }
            _sound?.Update(transform.position, _level * 0.45f, (0.7f + 0.3f * _level) / Mathf.Pow(_size, 0.15f));
        }

        private void OnDisable() => _sound?.Stop();

        private void OnDestroy() => OnDisable();
    }

    [HarmonyPatch(typeof(GasExhaustVisuals), "UpdateState")]
    internal static class VentExtrasPatch
    {
        private static void Postfix(GasExhaustVisuals __instance, GasExhaustState state) => VentExtras.Apply(__instance, state.OutputRate);
    }
}
