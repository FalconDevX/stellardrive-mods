using HarmonyLib;
using Items.Model;
using MelonLoader;

namespace HelloStellar.Patches
{
    // Przykładowy patch: po wczytaniu listy przedmiotów wypisuje, ile ich jest.
    // ___items to prywatne pole ItemSettingsList (Harmony wstrzykuje je po nazwie).
    [HarmonyPatch(typeof(ItemSettingsList), "ResetDict")]
    internal static class ItemSettingsPatch
    {
        private static void Postfix(ItemSettings[] ___items)
        {
            Melon<HelloStellarMod>.Logger.Msg($"Wczytano {___items?.Length ?? 0} definicji przedmiotów");
        }
    }
}
