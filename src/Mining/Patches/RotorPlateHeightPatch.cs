using HarmonyLib;
using Mining.Content;
using Ships.Interface.Model.Joints;
using Ships.Interface.Model.Placement;

namespace Mining.Patches
{
    // Talerz rotora na wierzchu rozciągniętej podstawy (Content/RotorUnlock.StretchBase liczy przesunięcie z wymiarów
    // modelu). Gra buduje to połączenie przy tworzeniu rotora i przy odtwarzaniu połączeń - dotyczy też starych rotorów.
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
