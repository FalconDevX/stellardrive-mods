using HarmonyLib;
using Ships.Cables;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Model.Parts.State;
using Ships.Interface.Model.State;
using Ships.Parts.Rotor;
using UnityEngine;

namespace Mining.Server
{
    [HarmonyPatch(typeof(RotorsUpdater), nameof(RotorsUpdater.Update))]
    internal static class RotorDegreesPatch
    {
        internal static float? TestTargetDegrees;
        private static bool _loggedError;

        private static bool Prefix(IShipStateWrite ____ship, ConnectionsServerTracker ____connections, float deltaTime)
        {
            float maxStep = Mathf.Max(1f, MiningMod.RotorSpeed.Value) * Mathf.Deg2Rad * deltaTime;
            foreach (StatefulPart part in ____ship.GetAllStatefulParts<RotorState>())
            {
                var state = (RotorState)part.State;
                float targetDegrees = TestTargetDegrees ?? ToDegrees((float)____connections.GetSignalValue(new EntityPartSocket(____ship.Id, part.Id, 0)));
                float target = Mathf.Repeat(targetDegrees, 360f) * Mathf.Deg2Rad;
                float delta = Mathf.Repeat(target - state.Angle, Mathf.PI * 2f);
                if (delta > Mathf.PI)
                {
                    delta -= Mathf.PI * 2f;
                }
                if (Mathf.Abs(delta) <= 0.0001f)
                {
                    continue;
                }
                float angle = Mathf.Repeat(state.Angle + Mathf.Clamp(delta, -maxStep, maxStep), Mathf.PI * 2f);
                try
                {
                    var shipServer = ____ship as Ships.TrackedShipServer;
                    if (RotorObstacles.IsBlocked(shipServer, part, state, angle))
                    {
                        continue;
                    }
                }
                catch (System.Exception e)
                {
                    if (!_loggedError)
                    {
                        _loggedError = true;
                        MelonLoader.Melon<MiningMod>.Logger.Warning($"Rotor - obstacle check: {e}");
                    }
                }
                ____ship.UpdatePartState(part.Id, state with { Angle = angle });
            }
            return false;
        }

        private static float ToDegrees(float signal) => Mathf.Abs(signal) <= 1f ? signal * MiningMod.RotorLeverAngle.Value : signal;
    }
}
