using HarmonyLib;
using Mining.Content;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.StateTypes;
using Ships.Parts.Door;

namespace Mining.Patches
{
    [HarmonyPatch(typeof(DoorDynamicColliders), nameof(DoorDynamicColliders.ReactToNetworkStateChange))]
    internal static class BigDoorColliderPatch
    {
        private static bool Prefix(DoorDynamicColliders __instance, StatefulPartStateChangeEvent change)
        {
            var leaves = __instance != null ? __instance.GetComponent<BigDoorLeafColliders>() : null;
            if (leaves == null || !(change.NewState is IDoorState state))
            {
                return true;
            }
            leaves.SetOpen(state.IsOpen, false);
            return false;
        }
    }

    [HarmonyPatch(typeof(DoorDynamicColliders), nameof(DoorDynamicColliders.SetNetworkState))]
    internal static class BigDoorColliderLoadPatch
    {
        private static void Postfix(DoorDynamicColliders __instance, IStatefulPartState state)
        {
            var leaves = __instance != null ? __instance.GetComponent<BigDoorLeafColliders>() : null;
            if (leaves != null && state is IDoorState door)
            {
                leaves.SetOpen(door.IsOpen, true);
            }
        }
    }
}
