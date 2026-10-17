using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.State;
using Ships.Interface.Model.Parts.StateTypes;
using Ships.Parts.Common;
using UnityEngine;

namespace Odyssey.Content
{
    internal class ElectrolyzerSoundVisuals : DefaultShipPartVisuals
    {
        private float _target, _level;
        private readonly Audio.LoopEmitter _sound = new Audio.LoopEmitter("electrolyzer", 15f, PowerMod.ElectrolyzerVolume);

        public override void SetNetworkState(IStatefulPartState state)
        {
            Read(state);
            _level = _target;
        }

        public override void ReactToNetworkStateChange(StatefulPartStateChangeEvent change) => Read(change.NewState);

        private void Read(IStatefulPartState state)
        {
            _target = state is ElectrolyzerState electrolyzer ? Mathf.Clamp01(electrolyzer.NormalizedProductionRate) : 0f;
        }

        private void Update()
        {
            _level = Mathf.MoveTowards(_level, _target, Time.deltaTime / 1.5f);
            _sound.Update(transform.position + transform.up * 0.5f, Mathf.SmoothStep(0f, 1f, _level), 0.9f + 0.1f * _level);
        }

        private void OnDisable() => _sound.Stop();

        private void OnDestroy() => _sound.Stop();
    }
}
