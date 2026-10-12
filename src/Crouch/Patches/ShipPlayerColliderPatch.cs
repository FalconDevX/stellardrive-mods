using HarmonyLib;
using Ships.Physics;
using UnityEngine;

namespace Crouch.Patches
{
    [HarmonyPatch(typeof(ShipServerPhysics), nameof(ShipServerPhysics.CreatePlayerCollider))]
    internal static class ShipPlayerColliderPatch
    {
        private static void Postfix(GameObject __result)
        {
            if (__result == null)
            {
                return;
            }
            float factor = Mathf.Clamp(CrouchMod.HeightFactor.Value, 0.3f, 0.95f);
            foreach (CapsuleCollider capsule in __result.GetComponentsInChildren<CapsuleCollider>(true))
            {
                float lift = capsule.height * (1f - factor) * 0.5f + 0.05f;
                float height = Mathf.Max(capsule.radius * 2f, capsule.height - lift);
                lift = capsule.height - height;
                Vector3 axis = capsule.direction == 0 ? Vector3.right : capsule.direction == 2 ? Vector3.forward : Vector3.up;
                capsule.height = height;
                capsule.center += axis * (lift * 0.5f);
            }
        }
    }
}
