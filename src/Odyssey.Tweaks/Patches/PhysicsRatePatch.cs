using System.Reflection;
using FishNet;
using FishNet.Managing.Timing;
using HarmonyLib;
using MelonLoader;

namespace Odyssey.Patches
{
    internal static class PhysicsRatePatch
    {
        private static readonly FieldInfo MaxFrameTicks = AccessTools.Field(typeof(TimeManager), "_maximumFrameTicks");
        private static TimeManager _applied;
        private static ushort _rate;
        private static int _maxSteps;
        private static byte _gameMaxSteps;

        internal static void Update()
        {
            TimeManager time = InstanceFinder.TimeManager;
            if (time == null)
            {
                return;
            }
            ushort rate = (ushort)UnityEngine.Mathf.Clamp(TweaksMod.PhysicsRate.Value, 20, 120);
            int maxSteps = TweaksMod.MaxPhysicsStepsPerFrame.Value;
            if (time == _applied && rate == _rate && maxSteps == _maxSteps)
            {
                return;
            }
            if (time.TickRate != rate && ((InstanceFinder.ServerManager != null && InstanceFinder.ServerManager.Started) || (InstanceFinder.ClientManager != null && InstanceFinder.ClientManager.Started)))
            {
                return;
            }
            if (time != _applied && MaxFrameTicks != null)
            {
                _gameMaxSteps = (byte)MaxFrameTicks.GetValue(time);
            }
            ushort before = time.TickRate;
            if (before != rate)
            {
                time.SetTickRate(rate);
            }
            if (MaxFrameTicks != null)
            {
                MaxFrameTicks.SetValue(time, maxSteps > 0 ? (byte)UnityEngine.Mathf.Clamp(maxSteps, 1, 255) : _gameMaxSteps);
            }
            _applied = time;
            _rate = rate;
            _maxSteps = maxSteps;
            Melon<TweaksMod>.Logger.Msg($"[physics] {rate} steps per second ({1000f / rate:0.0} ms per step, the game had {before}), at most {(MaxFrameTicks != null ? MaxFrameTicks.GetValue(time) : "?")} steps per frame");
        }
    }
}
