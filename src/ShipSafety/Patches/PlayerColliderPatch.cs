using HarmonyLib;
using Ships.Physics;
using UnityEngine;

namespace ShipSafety.Patches
{
    [HarmonyPatch(typeof(ShipServerPhysics), nameof(ShipServerPhysics.CreatePlayerCollider))]
    internal static class PlayerColliderPatch
    {
        private static void Postfix(GameObject __result)
        {
            if (!ShipSafetyMod.Enabled.Value || __result == null)
            {
                return;
            }
            int ship = LayerMask.GetMask("Ship");
            foreach (Collider collider in __result.GetComponentsInChildren<Collider>(true))
            {
                collider.excludeLayers |= ship;
            }
        }
    }
}
