using HarmonyLib;
using MelonLoader;
using Ships.Interface.Model;
using Ships.Physics;
using Ships.Physics.Model;
using UnityEngine;

namespace ShipSafety.Patches
{
    internal static class ShipPhysicsAccess
    {
        internal static readonly AccessTools.FieldRef<ShipServerPhysics, ShipRigidbody> ShipRigidbody =
            AccessTools.FieldRefAccess<ShipServerPhysics, ShipRigidbody>("_shipRigidbody");

        internal static readonly AccessTools.FieldRef<ShipServerPhysics, ShipState> NetworkState =
            AccessTools.FieldRefAccess<ShipServerPhysics, ShipState>("_networkState");

        internal static readonly AccessTools.FieldRef<ShipServerPhysics, ShipMassState> MassState =
            AccessTools.FieldRefAccess<ShipServerPhysics, ShipMassState>("_massState");

        internal static readonly AccessTools.FieldRef<ShipRigidbody, Rigidbody> Rigidbody =
            AccessTools.FieldRefAccess<ShipRigidbody, Rigidbody>("_rigidbody");

        internal static Rigidbody GetRigidbody(ShipServerPhysics physics)
        {
            ShipRigidbody shipRigidbody = ShipRigidbody(physics);
            return shipRigidbody != null ? Rigidbody(shipRigidbody) : null;
        }

        internal static string Describe(ShipServerPhysics physics)
        {
            ShipState state = NetworkState(physics);
            ShipMassState mass = MassState(physics);
            return $"ship {state?.Id}" + (mass != null
                ? $" (hull {mass.HullMass:0.#} kg, with parts {mass.HullAndPartsMass:0.#} kg, with players {mass.HullPartsAndPlayersMass:0.#} kg)"
                : "");
        }
    }

    [HarmonyPatch(typeof(ShipServerPhysics), "UpdateRootRigidbody")]
    internal static class MassGuardPatch
    {
        private const float MinMass = 1f;
        private static float _nextLogTime;

        private static void Postfix(ShipServerPhysics __instance, ShipPhysicsCalculationProperties props)
        {
            Rigidbody rigidbody = ShipPhysicsAccess.GetRigidbody(__instance);
            if (rigidbody == null || rigidbody.mass >= MinMass)
            {
                return;
            }

            ShipMassState massState = ShipPhysicsAccess.MassState(__instance);
            float ownMass = massState != null ? massState.HullPartsAndPlayersMass : 0f;
            float fixedMass = Mathf.Max(MinMass, ownMass);
            if (Time.realtimeSinceStartup >= _nextLogTime)
            {
                _nextLogTime = Time.realtimeSinceStartup + 1f;
                Melon<ShipSafetyMod>.Logger.Warning(
                    $"Rigidbody mass too low: {rigidbody.mass:0.###} kg (the game computed {props.MassIncludingConnectedShips:0.###} kg with connected ships), " +
                    $"{ShipPhysicsAccess.Describe(__instance)}" + (ShipSafetyMod.Enabled.Value ? $" -> set to {fixedMass:0.#} kg" : ""));
            }
            if (ShipSafetyMod.Enabled.Value)
            {
                rigidbody.mass = fixedMass;
            }
        }
    }

    [HarmonyPatch(typeof(ShipServerPhysics), nameof(ShipServerPhysics.WritePhysicsState))]
    internal static class VelocityGuardPatch
    {
        private static float _nextLogTime;

        private static void Prefix(ShipServerPhysics __instance)
        {
            ShipRigidbody shipRigidbody = ShipPhysicsAccess.ShipRigidbody(__instance);
            Rigidbody rigidbody = shipRigidbody != null ? ShipPhysicsAccess.Rigidbody(shipRigidbody) : null;
            if (rigidbody == null || rigidbody.isKinematic)
            {
                return;
            }

            Vector3 velocity = rigidbody.linearVelocity;
            Vector3 before = shipRigidbody.VelocityBeforeSim;
            float speed = velocity.magnitude;
            float jump = (velocity - before).magnitude;
            float max = ShipSafetyMod.MaxSpeed.Value;
            bool broken = float.IsNaN(speed) || float.IsInfinity(speed);
            bool jumped = jump > ShipSafetyMod.MaxSpeedJump.Value;
            if (!broken && !jumped && speed <= max)
            {
                return;
            }

            if (Time.realtimeSinceStartup >= _nextLogTime)
            {
                _nextLogTime = Time.realtimeSinceStartup + 1f;
                Melon<ShipSafetyMod>.Logger.Warning(
                    $"Speed {speed:0.#} m/s (before the physics step {before.magnitude:0.#} m/s, jump {jump:0.#} m/s), " +
                    $"rigidbody mass {rigidbody.mass:0.###} kg, {ShipPhysicsAccess.Describe(__instance)}");
            }
            if (!ShipSafetyMod.Enabled.Value)
            {
                return;
            }

            Vector3 safe = broken || jumped ? before : velocity;
            if (float.IsNaN(safe.x) || float.IsNaN(safe.y) || float.IsNaN(safe.z))
            {
                safe = Vector3.zero;
            }
            if (safe.magnitude > max)
            {
                safe = safe.normalized * max;
            }
            rigidbody.linearVelocity = safe;
            if (broken || jumped)
            {
                rigidbody.angularVelocity = AngularVelocityBeforeSimOrZero(shipRigidbody);
            }
        }

        private static Vector3 AngularVelocityBeforeSimOrZero(ShipRigidbody shipRigidbody)
        {
            Vector3 angular = Traverse.Create(shipRigidbody).Field<Vector3>("_angularVelocityBeforeSim").Value;
            return float.IsNaN(angular.x) || angular.magnitude > 10f ? Vector3.zero : angular;
        }
    }
}
