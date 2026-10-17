using Core.Services;
using HarmonyLib;
using Odyssey.Content;
using Players.Interface.Services;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.State;
using Ships.Interface.Services;
using Items.Model;
using UI.Inventory;
using UnityEngine;

namespace Odyssey.Patches
{
    [HarmonyPatch(typeof(StorageMenuActivator), nameof(StorageMenuActivator.ShowMenu))]
    internal static class ExcavatorMenuPatch
    {
        private static void Postfix(GameObject ____currentMenu)
        {
            if (____currentMenu == null || !IsLocalPlayerUsingExcavator())
            {
                return;
            }
            foreach (InventorySlotUI slot in ____currentMenu.GetComponentsInChildren<InventorySlotUI>(true))
            {
                if (slot.origin == InventoryType.Chest && slot.id >= Excavator.SlotCount)
                {
                    slot.gameObject.SetActive(false);
                }
            }
        }

        internal static bool IsLocalPlayerUsingExcavator() => StorageMenuPatch.IsLocalPlayerUsing(Excavator.IsExcavator);

    }
}

