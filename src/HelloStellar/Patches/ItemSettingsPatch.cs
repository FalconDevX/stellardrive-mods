using HarmonyLib;
using Items.Model;
using MelonLoader;

namespace HelloStellar.Patches
{
    [HarmonyPatch(typeof(ItemSettingsList), "ResetDict")]
    internal static class ItemSettingsPatch
    {
        private static void Postfix(ItemSettings[] ___items)
        {
            Melon<HelloStellarMod>.Logger.Msg($"Loaded {___items?.Length ?? 0} item definitions");
        }
    }
}
