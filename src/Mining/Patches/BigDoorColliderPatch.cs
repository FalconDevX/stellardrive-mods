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
            var timer = __instance != null ? __instance.GetComponent<BigDoorColliderTimer>() : null;
            if (timer == null || !(change.NewState is IDoorState state))
            {
                return true;
            }
            timer.Apply(state.IsOpen);
            return false;
        }
    }
}
