using HarmonyLib;
using TMPro;
using UI.Inventory;

namespace ResourceTweaks.Patches
{
    [HarmonyPatch(typeof(InventorySlotUI), nameof(InventorySlotUI.UpdateSlot))]
    internal static class SlotQuantityTextPatch
    {
        private static readonly AccessTools.FieldRef<InventorySlotUI, TMP_Text> QuantityRef =
            AccessTools.FieldRefAccess<InventorySlotUI, TMP_Text>("quantity");

        private static void Postfix(InventorySlotUI __instance)
        {
            TMP_Text text = QuantityRef(__instance);
            if (text == null || text.textWrappingMode == TextWrappingModes.NoWrap)
            {
                return;
            }
            float size = text.fontSize;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.enableAutoSizing = true;
            text.fontSizeMax = size;
            text.fontSizeMin = size * 0.6f;
        }
    }
}
