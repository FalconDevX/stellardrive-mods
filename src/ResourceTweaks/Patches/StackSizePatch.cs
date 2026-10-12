using System;
using HarmonyLib;
using Items.Model;
using MelonLoader;

namespace ResourceTweaks.Patches
{
    [HarmonyPatch(typeof(ItemSettingsList), "ResetDict")]
    internal static class StackSizePatch
    {
        private static void Postfix(ItemSettings[] ___items)
        {
            if (___items == null)
            {
                return;
            }

            byte target = (byte)Math.Max(1, Math.Min(255, ResourceTweaksMod.MaxStackSize.Value));
            int changed = 0;
            foreach (ItemSettings item in ___items)
            {
                if (item == null)
                {
                    continue;
                }
                byte original = item.maxStackSize;
                if (original > 1 && original < target)
                {
                    item.maxStackSize = target;
                    changed++;
                }
                if (ResourceTweaksMod.LogDrops.Value)
                {
                    Melon<ResourceTweaksMod>.Logger.Msg($"  item {item.id} '{item.itemName}': stack {original} -> {item.maxStackSize}");
                }
            }

            Melon<ResourceTweaksMod>.Logger.Msg($"Stack {target} set for {changed} items");
        }
    }
}
