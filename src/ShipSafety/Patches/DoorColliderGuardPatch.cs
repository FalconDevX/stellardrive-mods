using HarmonyLib;
using Ships.Parts.Door;
using UnityEngine;

namespace ShipSafety.Patches
{
    [HarmonyPatch(typeof(DoorDynamicColliders), "UpdateState")]
    internal static class DoorColliderGuardPatch
    {
        private static readonly AccessTools.FieldRef<DoorDynamicColliders, GameObject> DoorRef =
            AccessTools.FieldRefAccess<DoorDynamicColliders, GameObject>("doorCollider");

        private static bool Prefix(DoorDynamicColliders __instance) => __instance != null && DoorRef(__instance) != null;
    }
}
