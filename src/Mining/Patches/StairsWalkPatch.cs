using HarmonyLib;
using Players.Network.Model;
using Players.Physics;
using UnityEngine;

namespace Mining.Patches
{
    [HarmonyPatch(typeof(PlayerWalkingPhysics), "GroundCheck")]
    internal static class StairsWalkPatch
    {
        private const float StickDistance = 0.6f;
        private const float StairsRange = 1.6f;
        private static readonly RaycastHit[] Hits = new RaycastHit[32];
        private static readonly Collider[] Nearby = new Collider[64];

        private static void Postfix(ref PlayerPhysicsState __0, Vector3 ____currentUp, Vector3 ____currentGravity, PhysicsScene ____physicsScene,
            CapsuleCollider ____playerColliderPrefab, int ____layerMask, ref Vector3 ____groundContactPoint, ref Vector3 ____groundContactNormal)
        {
            if (__0.IsOnGround || !__0.IsPreviouslyGrounded || __0.IsJumping || ____currentGravity == Vector3.zero || ____playerColliderPrefab == null)
            {
                return;
            }
            Vector3 down = -____currentUp;
            float radius = ____playerColliderPrefab.radius * 0.999f;
            float max = ____playerColliderPrefab.height / 2f - ____playerColliderPrefab.radius + 0.1f + StickDistance;
            int count = ____physicsScene.SphereCast(__0.Position, radius, down, Hits, max, ____layerMask, QueryTriggerInteraction.Ignore);
            RaycastHit best = default;
            float nearest = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = Hits[i];
                if (hit.collider == null || hit.collider.CompareTag("Player") || hit.distance <= 0f || hit.distance >= nearest) continue;
                if (Vector3.Dot(hit.normal, ____currentUp) < 0.6f) continue;
                nearest = hit.distance;
                best = hit;
            }
            if (best.collider == null || !NearStairs(____physicsScene, __0.Position, best.collider, ____layerMask))
            {
                return;
            }
            __0.IsOnGround = true;
            ____groundContactPoint = best.point;
            ____groundContactNormal = best.normal;
        }

        private static bool NearStairs(PhysicsScene scene, Vector3 position, Collider ground, int layerMask)
        {
            if (ground.GetComponent<StairsCollider>() != null)
            {
                return true;
            }
            int count = scene.OverlapSphere(position, StairsRange, Nearby, layerMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (Nearby[i] != null && Nearby[i].GetComponent<StairsCollider>() != null) return true;
            }
            return false;
        }
    }
}
