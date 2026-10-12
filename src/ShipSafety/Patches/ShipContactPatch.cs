using HarmonyLib;
using MelonLoader;
using Ships.Physics;
using Ships.Physics.Collision;
using UnityEngine;

namespace ShipSafety.Patches
{
    [HarmonyPatch(typeof(ShipCollisionDestruction), "OnCollisionEnter")]
    internal static class ShipContactDestructionPatch
    {
        private static float _nextLogTime;

        private static bool Prefix(ShipCollisionDestruction __instance, Collision collision)
        {
            if (!ShipSafetyMod.Enabled.Value || collision.rigidbody == null || collision.rigidbody.GetComponent<ShipRigidbody>() == null)
            {
                return true;
            }
            float speed = collision.relativeVelocity.magnitude;
            if (speed >= ShipSafetyMod.ShipCrashSpeed.Value)
            {
                return true;
            }
            if (Time.realtimeSinceStartup >= _nextLogTime)
            {
                _nextLogTime = Time.realtimeSinceStartup + 5f;
                Melon<ShipSafetyMod>.Logger.Msg($"Ship contact ({__instance.name} with {collision.rigidbody.name}) at {speed:0.0} m/s - no damage");
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(ShipRigidbody), nameof(ShipRigidbody.SetRootPhysicsStateServer))]
    internal static class ShipSolverPatch
    {
        private static void Postfix(ShipRigidbody __instance)
        {
            if (!ShipSafetyMod.Enabled.Value)
            {
                return;
            }
            Rigidbody rigidbody = ShipPhysicsAccess.Rigidbody(__instance);
            if (rigidbody == null)
            {
                return;
            }
            int iterations = ShipSafetyMod.ShipSolverIterations.Value;
            if (rigidbody.solverIterations < iterations)
            {
                rigidbody.solverIterations = iterations;
                rigidbody.solverVelocityIterations = Mathf.Max(rigidbody.solverVelocityIterations, iterations / 3);
            }
            float depenetration = ShipSafetyMod.ShipDepenetrationSpeed.Value;
            if (depenetration > 0f && !Mathf.Approximately(rigidbody.maxDepenetrationVelocity, depenetration))
            {
                rigidbody.maxDepenetrationVelocity = depenetration;
            }
        }
    }
}
