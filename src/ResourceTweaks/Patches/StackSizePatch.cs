using System;
using HarmonyLib;
using Items.Model;
using MelonLoader;

namespace ResourceTweaks.Patches
{
    // ItemSettingsList buduje słownik przedmiotów przy pierwszym użyciu - wtedy podnosimy limity.
    // ItemSettings to ScriptableObject, więc zmiana działa na cały czas działania gry.
    [HarmonyPatch(typeof(ItemSettingsList), "ResetDict")]
    internal static class StackSizePatch
    {
        private static void Postfix(ItemSettings[] ___items)
        {
            if (___items == null)
            {
                return;
            }

            // InventorySlot.Quantity to byte, więc więcej niż 255 się nie zmieści.
            byte target = (byte)Math.Max(1, Math.Min(255, ResourceTweaksMod.MaxStackSize.Value));
            int changed = 0;
            foreach (ItemSettings item in ___items)
            {
                // Stack 1 mają narzędzia i przedmioty unikalne - nie ruszamy ich.
                if (item != null && item.maxStackSize > 1 && item.maxStackSize < target)
                {
                    item.maxStackSize = target;
                    changed++;
                }
            }

            Melon<ResourceTweaksMod>.Logger.Msg($"Stack {target} ustawiony dla {changed} przedmiotów");
        }
    }
}
