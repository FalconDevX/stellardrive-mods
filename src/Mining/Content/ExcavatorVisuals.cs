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
    // Klient nie zna stanu zasilania, więc "pracuje" = w ostatnich sekundach przybyło rudy w slotach.
    internal class ExcavatorVisuals : DefaultShipPartVisuals
    {
        private const float MaxRpm = 240f;
        private const float RestLift = 0.55f;  // w spoczynku wiertło schowane w korpusie
        private const float Extend = 0.85f;    // przy pracy wysuwa się tyle w dół (w ziemię)
        private const float MaxHearDistance = 35f;

        private Transform _drill;
        private Vector3 _drillRest;
        private int _lastTotal = -1;
        private float _lastProducedAt = -100f;
        private float _speed;
        private float _extend;
        private FMOD.Channel _channel;
        private bool _hasChannel;

        internal bool ForceWorking;

        private void Awake()
        {
            _drill = transform.Find("DrillPivot");
            if (_drill != null)
            {
                _drillRest = _drill.localPosition;
            }
        }

        public override void SetNetworkState(IStatefulPartState state)
        {
            base.SetNetworkState(state);
            _lastTotal = CountItems(state);
        }

        public override void ReactToNetworkStateChange(StatefulPartStateChangeEvent change)
        {
            base.ReactToNetworkStateChange(change);
            int total = CountItems(change.NewState);
            if (_lastTotal >= 0 && total > _lastTotal)
            {
                _lastProducedAt = Time.time;
            }
            _lastTotal = total;
        }

        private static int CountItems(IStatefulPartState state)
        {
            if (!(state is StorageState storage))
            {
                return 0;
            }
            int total = 0;
            for (byte i = 0; i < Excavator.SlotCount; i++)
            {
                InventorySlot slot = storage.GetSlot(i);
                if (slot.HasItem) total += slot.Quantity;
            }
            return total;
        }

        private void Update()
        {
            float window = MiningMod.SecondsPerItem.Value * 1.5f + 1f;
            bool working = ForceWorking || Time.time - _lastProducedAt < window;
            _speed = Mathf.MoveTowards(_speed, working ? 1f : 0f, Time.deltaTime * (working ? 0.8f : 0.5f));

            if (_drill != null)
            {
                // Najpierw się rozkręca, potem powoli wysuwa w dół; po zatrzymaniu chowa się z powrotem.
                _extend = Mathf.MoveTowards(_extend, _speed > 0.6f ? 1f : 0f, Time.deltaTime * (_speed > 0.6f ? 0.25f : 0.4f));
                _drill.localPosition = _drillRest + Vector3.up * RestLift - Vector3.up * (Extend * Mathf.SmoothStep(0f, 1f, _extend));
                _drill.Rotate(Vector3.up, _speed * MaxRpm * 6f * Time.deltaTime, Space.Self);
            }
            UpdateSound();
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
