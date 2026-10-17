using System.Collections.Generic;
using HarmonyLib;
using MelonLoader;
using Ships.Physics;
using Ships.Physics.Model;
using UnityEngine;

namespace ShipSafety.Patches
{
    internal static class ParkedShipSleep
    {
        private const int RestTicks = 50;
        private const float RestSpeed = 0.05f;
        private const float RestSpin = 0.02f;
        private const float ForceChange = 0.01f;
        private const float TorqueChange = 0.5f;
        private const float MoveTolerance = 0.01f;

        private sealed class Rest
        {
            public int SlowTicks;
            public bool Asleep;
            public ShipPhysicsForcesResult Forces;
            public ShipPhysicsForcesResult LastForces;
        }

        private static readonly Dictionary<ShipRigidbody, Rest> States = new Dictionary<ShipRigidbody, Rest>();
        private static readonly List<ShipRigidbody> Gone = new List<ShipRigidbody>();
        private static int _cleanup;

        internal static bool Active => ShipSafetyMod.Enabled.Value && ShipSafetyMod.SleepParkedShips.Value;

        internal static bool SkipState(ShipRigidbody ship, ShipPhysicsCalculationProperties props)
        {
            if (!Active || !States.TryGetValue(ship, out Rest rest) || !rest.Asleep)
            {
                return false;
            }
            Rigidbody body = ShipPhysicsAccess.Rigidbody(ship);
            if (body == null || body.isKinematic || !body.IsSleeping() || Mathf.Abs(body.mass - props.MassIncludingConnectedShips) > 0.001f
                || (body.position - props.PhysicsState.Position).sqrMagnitude > MoveTolerance * MoveTolerance || body.centerOfMass != props.CenterOfMassIncludingConnectedShips)
            {
                Wake(rest);
                return false;
            }
            return true;
        }

        internal static bool SkipForces(ShipRigidbody ship, ShipPhysicsForcesResult forces)
        {
            if (!Active)
            {
                return false;
            }
            Rigidbody body = ShipPhysicsAccess.Rigidbody(ship);
            if (body == null || body.isKinematic)
            {
                return false;
            }
            if (++_cleanup >= 3000)
            {
                _cleanup = 0;
                Cleanup();
            }
            if (!States.TryGetValue(ship, out Rest rest))
            {
                rest = new Rest();
                States[ship] = rest;
            }
            float mass = Mathf.Max(1f, body.mass);
            if (rest.Asleep)
            {
                if (body.IsSleeping() && Same(forces, rest.Forces, mass))
                {
                    return true;
                }
                Wake(rest);
                return false;
            }
            bool still = body.linearVelocity.sqrMagnitude < RestSpeed * RestSpeed && body.angularVelocity.sqrMagnitude < RestSpin * RestSpin;
            rest.SlowTicks = still && Same(forces, rest.LastForces, mass) ? rest.SlowTicks + 1 : 0;
            rest.LastForces = forces;
            if (rest.SlowTicks < RestTicks)
            {
                return false;
            }
            rest.Asleep = true;
            rest.Forces = forces;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.Sleep();
            Melon<ShipSafetyMod>.Logger.Msg($"Ship '{ship.name}' is parked - physics asleep until something moves it");
            return true;
        }

        private static void Wake(Rest rest)
        {
            rest.Asleep = false;
            rest.SlowTicks = 0;
        }

        private static bool Same(ShipPhysicsForcesResult a, ShipPhysicsForcesResult b, float mass)
        {
            return (a.ForceToAdd - b.ForceToAdd).sqrMagnitude < Sq(ForceChange * mass)
                && (a.TorqueToAddInNewtonMeters - b.TorqueToAddInNewtonMeters).sqrMagnitude < Sq(ForceChange * mass)
                && (a.TorqueToAddInDegreesSecond - b.TorqueToAddInDegreesSecond).sqrMagnitude < Sq(TorqueChange);
        }

        private static float Sq(float v) => v * v;

        private static void Cleanup()
        {
            foreach (ShipRigidbody ship in States.Keys)
            {
                if (ship == null || ShipPhysicsAccess.Rigidbody(ship) == null) Gone.Add(ship);
            }
            foreach (ShipRigidbody ship in Gone)
            {
                States.Remove(ship);
            }
            Gone.Clear();
        }
    }

    [HarmonyPatch(typeof(ShipRigidbody), nameof(ShipRigidbody.SetRootPhysicsStateServer))]
    internal static class ParkedShipStatePatch
    {
        private static bool Prefix(ShipRigidbody __instance, ShipPhysicsCalculationProperties props) => !ParkedShipSleep.SkipState(__instance, props);
    }

    [HarmonyPatch(typeof(ShipRigidbody), nameof(ShipRigidbody.ApplyForces))]
    internal static class ParkedShipForcesPatch
    {
        private static bool Prefix(ShipRigidbody __instance, ShipPhysicsForcesResult forces) => !ParkedShipSleep.SkipForces(__instance, forces);
    }
}
