using HarmonyLib;
using Ships.Physics;
using UnityEngine;

namespace Odyssey.Patches
{
    [HarmonyPatch(typeof(ShipRigidbody), "EnsureRigidBodyExists")]
    internal static class ShipCollisionModePatch
    {
        private static readonly AccessTools.FieldRef<ShipRigidbody, Rigidbody> Body = AccessTools.FieldRefAccess<ShipRigidbody, Rigidbody>("_rigidbody");

        private static void Postfix(ShipRigidbody __instance)
        {
            Rigidbody body = Body(__instance);
            if (body != null && TweaksMod.FastShipCollisions.Value && body.collisionDetectionMode == CollisionDetectionMode.ContinuousDynamic)
            {
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            }
        }
    }
}
