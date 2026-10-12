using HarmonyLib;
using MelonLoader;
using Ships.Physics;
using Ships.Physics.Forces;
using Ships.Physics.Model;
using UnityEngine;

namespace ShipSafety.Patches
{
    internal static class ForceGuard
    {
        internal static Vector3 Drag, Gravity, Thrust, Magnetism, Lift;
        private static float _nextLogTime;

        internal static void Check(ShipPhysicsCalculationProperties props, ref ShipPhysicsForcesResult result)
        {
            float mass = props.MassIncludingConnectedShips;
            if (mass <= 0f)
            {
                return;
            }
            float limit = ShipSafetyMod.MaxAcceleration.Value;
            float acceleration = result.ForceToAdd.magnitude / mass;
            if (float.IsNaN(acceleration) || acceleration > limit)
            {
                if (Time.realtimeSinceStartup >= _nextLogTime)
                {
                    _nextLogTime = Time.realtimeSinceStartup + 1f;
                    Melon<ShipSafetyMod>.Logger.Warning(
                        $"Ship acceleration too high: {acceleration:0.#} m/s^2 (mass {mass:0}, speed {props.PhysicsState.Velocity.magnitude:0.#} m/s). " +
                        $"Drag {Drag.magnitude / mass:0.#}, gravity {Gravity.magnitude / mass:0.#}, thrust {Thrust.magnitude / mass:0.#}, " +
                        $"magnetism {Magnetism.magnitude / mass:0.#}, lift {Lift.magnitude / mass:0.#} m/s^2");
                }
                if (ShipSafetyMod.Enabled.Value)
                {
                    Vector3 force = float.IsNaN(acceleration) ? Vector3.zero : result.ForceToAdd * (limit / acceleration);
                    result = new ShipPhysicsForcesResult(force, result.TorqueToAddInNewtonMeters, result.TorqueToAddInDegreesSecond);
                }
            }
        }
    }

    [HarmonyPatch(typeof(ShipDrag), nameof(ShipDrag.ComputeForce))]
    internal static class DragForcePatch
    {
        private static void Postfix(Vector3 __result) => ForceGuard.Drag = __result;
    }

    [HarmonyPatch(typeof(ShipGravity), nameof(ShipGravity.ComputeForce))]
    internal static class GravityForcePatch
    {
        private static void Postfix(Vector3 __result) => ForceGuard.Gravity = __result;
    }

    [HarmonyPatch(typeof(ShipThrust), nameof(ShipThrust.ComputeForce))]
    internal static class ThrustForcePatch
    {
        private static void Postfix(Vector3 __result) => ForceGuard.Thrust = __result;
    }

    [HarmonyPatch(typeof(ShipMagnetism), nameof(ShipMagnetism.ComputeForce))]
    internal static class MagnetismForcePatch
    {
        private static void Postfix(ShipMagnetismResult __result) => ForceGuard.Magnetism = __result.Force;
    }

    [HarmonyPatch(typeof(ShipLift), nameof(ShipLift.ComputeForce))]
    internal static class LiftForcePatch
    {
        private static void Postfix(Vector3 __result) => ForceGuard.Lift = __result;
    }

    [HarmonyPatch(typeof(ShipNewtonianPhysics), nameof(ShipNewtonianPhysics.ComputePhysicsForces))]
    internal static class TotalForcePatch
    {
        private static void Postfix(ShipPhysicsCalculationProperties calculationProperties, ref ShipPhysicsForcesResult __result)
        {
            ForceGuard.Check(calculationProperties, ref __result);
        }
    }
}
