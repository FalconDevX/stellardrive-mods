using HarmonyLib;
using Odyssey.Content;
using Ships.Interface.Model.Joints;
using Ships.Interface.Model.Placement;

namespace Odyssey.Patches
{
    [HarmonyPatch(typeof(RotorJointConnection), nameof(RotorJointConnection.BuildFromRotorPart))]
    internal static class RotorPlateHeightPatch
    {
        private static void Postfix(RotorJointConnection __result, IPartPlacement __1)
        {
            if (__result == null || __1 == null || RotorUnlock.Rotor == null || RotorUnlock.PlateOffset == 0f)
            {
                return;
            }
            __result.PosA += __1.UpOrientation.GetVector3() * RotorUnlock.PlateOffset;
        }
    }
}
