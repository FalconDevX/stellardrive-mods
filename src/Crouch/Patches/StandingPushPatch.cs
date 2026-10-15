using HarmonyLib;
using Players;
using Players.Physics;
using Players.Physics.Model;
using UnityEngine;

namespace Crouch.Patches
{
    [HarmonyPatch(typeof(TrackedPlayerServer), "ReactToCollision")]
    internal static class StandingPushPatch
    {
        private static readonly AccessTools.FieldRef<TrackedPlayerServer, PlayerPhysics> PhysicsRef =
            AccessTools.FieldRefAccess<TrackedPlayerServer, PlayerPhysics>("_physics");

        private static readonly AccessTools.FieldRef<PlayerPhysics, PlayerWalkingPhysics> WalkingRef =
            AccessTools.FieldRefAccess<PlayerPhysics, PlayerWalkingPhysics>("_walkingPhysics");

        private static bool Prefix(TrackedPlayerServer __instance, PlayerPhysicsCollision collision)
        {
            if (collision.Collider == null || collision.Collider.GetComponentInParent<Rigidbody>() == null || collision.Impact.sqrMagnitude < 1e-6f)
            {
                return true;
            }
            PlayerPhysics wrapper = PhysicsRef(__instance);
            PlayerWalkingPhysics physics = wrapper != null ? WalkingRef(wrapper) : null;
            if (physics != null && CrouchPhysicsPatch.CrouchedFeet.TryGetValue(physics, out float feet))
            {
                float local = (Quaternion.Inverse(__instance.PhysicsState.Rotation) * (collision.Position - __instance.PhysicsState.Position)).y;
                if (local <= -feet + 0.02f)
                {
                    return false;
                }
            }
            Vector3 down = -(__instance.PhysicsState.Rotation * Vector3.up);
            return Vector3.Dot(collision.Impact.normalized, down) < 0.6f;
        }
    }
}
