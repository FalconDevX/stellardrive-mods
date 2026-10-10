using HarmonyLib;
using MelonLoader;
using Ships.Physics;
using UnityEngine;

namespace ShipSafety.Patches
{
    // Po kroku fizyki, zanim gra zapisze stan statku: jeśli rigidbody ma absurdalną prędkość (np. po kolizji
    // albo błędzie fizyki), logujemy to i obcinamy prędkość do limitu (kierunek zostaje).
    [HarmonyPatch(typeof(ShipServerPhysics), nameof(ShipServerPhysics.WritePhysicsState))]
    internal static class VelocityGuardPatch
    {
        private static readonly AccessTools.FieldRef<ShipServerPhysics, ShipRigidbody> ShipRigidbodyRef =
            AccessTools.FieldRefAccess<ShipServerPhysics, ShipRigidbody>("_shipRigidbody");

        private static readonly AccessTools.FieldRef<ShipRigidbody, Rigidbody> RigidbodyRef =
            AccessTools.FieldRefAccess<ShipRigidbody, Rigidbody>("_rigidbody");

        private static float _nextLogTime;

        private static void Prefix(ShipServerPhysics __instance)
        {
            ShipRigidbody shipRigidbody = ShipRigidbodyRef(__instance);
            Rigidbody rigidbody = shipRigidbody != null ? RigidbodyRef(shipRigidbody) : null;
            if (rigidbody == null || rigidbody.isKinematic)
            {
                return;
            }

            Vector3 velocity = rigidbody.linearVelocity;
            float speed = velocity.magnitude;
            float max = ShipSafetyMod.MaxSpeed.Value;
            bool broken = float.IsNaN(speed) || float.IsInfinity(speed);
            if (!broken && speed <= max)
            {
                return;
            }

            if (Time.realtimeSinceStartup >= _nextLogTime)
            {
                _nextLogTime = Time.realtimeSinceStartup + 1f;
                Melon<ShipSafetyMod>.Logger.Warning(
                    $"Statek ma prędkość {speed:0.#} m/s (przed krokiem fizyki {shipRigidbody.VelocityBeforeSim.magnitude:0.#} m/s, masa rigidbody {rigidbody.mass:0})");
            }
            if (ShipSafetyMod.Enabled.Value)
            {
                rigidbody.linearVelocity = broken ? Vector3.zero : velocity * (max / speed);
            }
        }
    }
}
