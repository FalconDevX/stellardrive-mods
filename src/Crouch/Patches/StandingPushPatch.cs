using HarmonyLib;
using Players;
using Players.Physics.Model;
using UnityEngine;

namespace Crouch.Patches
{
    [HarmonyPatch(typeof(TrackedPlayerServer), "ReactToCollision")]
    internal static class StandingPushPatch
    {
        private static bool Prefix(TrackedPlayerServer __instance, PlayerPhysicsCollision collision)
        {
            if (collision.Collider == null || collision.Collider.GetComponentInParent<Rigidbody>() == null || collision.Impact.sqrMagnitude < 1e-6f)
            {
                return true;
            }
            Vector3 down = -(__instance.PhysicsState.Rotation * Vector3.up);
            return Vector3.Dot(collision.Impact.normalized, down) < 0.6f;
        }
    }
}
