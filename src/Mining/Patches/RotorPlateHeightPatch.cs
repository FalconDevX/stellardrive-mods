using HarmonyLib;
using Ships.Interface.Model.Joints;
using Ships.Interface.Model.Placement;

namespace Mining.Patches
{
    // Talerz rotora siedział w połowie wysokości kratki rotora, więc wszystko zbudowane na nim było pół kratki
    // przesunięte względem siatki statku. Przesuwamy punkt zawieszenia talerza o pół kratki dalej - na górną ścianę
    // kratki (wygląd podstawy rozciąga Content/RotorUnlock). Gra buduje to połączenie przy tworzeniu rotora i przy
    // każdym odtwarzaniu połączeń, więc dotyczy też rotorów już postawionych (ich talerz podniesie się o pół kratki).
    [HarmonyPatch(typeof(RotorJointConnection), nameof(RotorJointConnection.BuildFromRotorPart))]
    internal static class RotorPlateHeightPatch
    {
        private static void Postfix(RotorJointConnection __result, IPartPlacement __1)
        {
            if (__result != null && __1 != null)
            {
                __result.PosA += __1.UpOrientation.GetVector3() * 0.5f;
            }
        }
    }
}
