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
    // Rotor gry obraca się do kąta z sygnału podanego w radianach i robi pełny obrót na sekundę.
    // Tu: sygnał w stopniach (np. wpisany w Constant: 90, -45, 180) i płynny obrót (MiningMod.RotorSpeed stopni/s).
    [HarmonyPatch(typeof(RotorsUpdater), nameof(RotorsUpdater.Update))]
    internal static class RotorDegreesPatch
    {
        internal static float? TestTargetDegrees; // tylko test (Diagnostics) - kąt zamiast sygnału z kabla

        private static bool Prefix(IShipStateWrite ____ship, ConnectionsServerTracker ____connections, float deltaTime)
        {
            float maxStep = Mathf.Max(1f, MiningMod.RotorSpeed.Value) * Mathf.Deg2Rad * deltaTime;
            foreach (StatefulPart part in ____ship.GetAllStatefulParts<RotorState>())
            {
                var state = (RotorState)part.State;
                float targetDegrees = TestTargetDegrees ?? (float)____connections.GetSignalValue(new EntityPartSocket(____ship.Id, part.Id, 0));
                float target = Mathf.Repeat(targetDegrees, 360f) * Mathf.Deg2Rad;
                float delta = Mathf.Repeat(target - state.Angle, Mathf.PI * 2f);
                if (delta > Mathf.PI)
                {
                    delta -= Mathf.PI * 2f; // krótszą drogą
                }
                if (Mathf.Abs(delta) <= 0.0001f)
                {
                    continue;
                }
                float angle = Mathf.Repeat(state.Angle + Mathf.Clamp(delta, -maxStep, maxStep), Mathf.PI * 2f);
                ____ship.UpdatePartState(part.Id, state with { Angle = angle });
            }
            return false;
        }
    }
}
