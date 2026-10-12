using System;
using Core.Services;
using DropItems;
using HarmonyLib;
using Items.Model;
using MelonLoader;
using Players.Interface.Services;
using Players.Inventory;
using UI.Inventory;
using UnityEngine;
using UnityEngine.InputSystem;

[assembly: MelonInfo(typeof(DropItemsMod), "Drop Items", "0.1.0", "mateu")]
[assembly: MelonGame("CuriousOwlGames", "StellarDrive")]

namespace DropItems
{
    public class DropItemsMod : MelonMod
    {
        internal static MelonPreferences_Entry<Key> ToolbarKey;
        internal static MelonPreferences_Entry<Key> InventoryKey;
        internal static InventorySlotUI Hovered;

        public override void OnInitializeMelon()
        {
            var category = MelonPreferences.CreateCategory("DropItems");
            ToolbarKey = category.CreateEntry("ToolbarKey", Key.Q, description: "Drops the item from the selected toolbar slot (with Shift: half of the stack)");
            InventoryKey = category.CreateEntry("InventoryKey", Key.X, description: "Drops the item from the inventory slot under the cursor (with Shift: half of the stack)");
            category.SaveToFile(false);
        }

        public override void OnUpdate()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null || IsTyping())
            {
                return;
            }
            try
            {
                bool half = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
                if (kb[InventoryKey.Value].wasPressedThisFrame && Hovered != null && Hovered.isActiveAndEnabled)
                {
                    Drop(new ItemSlotId { Type = Hovered.origin, SlotId = (byte)Hovered.id }, half);
                }
                else if (kb[ToolbarKey.Value].wasPressedThisFrame && Cursor.lockState == CursorLockMode.Locked && TryGetSelectedToolbarSlot(out byte slot))
                {
                    Drop(new ItemSlotId { Type = InventoryType.ToolBar, SlotId = slot }, half);
                }
            }
            catch (Exception e)
            {
                LoggerInstance.Warning($"Could not drop the item: {e.Message}");
            }
        }

        private static void Drop(ItemSlotId slot, bool half)
        {
            var operations = ServiceLocator.GetService<IPlayerInventoryOperationService>();
            if (operations == null)
            {
                return;
            }
            if (half)
            {
                operations.MoveSplitItemInWorld(slot);
            }
            else
            {
                operations.MoveItemInWorld(slot);
            }
        }

        private static bool TryGetSelectedToolbarSlot(out byte slot)
        {
            slot = 0;
            ToolBelt belt = UnityEngine.Object.FindObjectOfType<ToolBelt>();
            if (belt == null)
            {
                return false;
            }
            int selected = Traverse.Create(belt).Field("currentSelectedId").Method("Get").GetValue<int>();
            if (selected < 1)
            {
                return false;
            }
            slot = (byte)(selected - 1);
            return true;
        }

        private static bool IsTyping()
        {
            var selected = UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
            return selected != null && (selected.GetComponent<TMPro.TMP_InputField>() != null || selected.GetComponent<UnityEngine.UI.InputField>() != null);
        }
    }

    [HarmonyPatch(typeof(InventorySlotUI))]
    internal static class SlotHoverPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch(nameof(InventorySlotUI.OnCursorEnter))]
        private static void Enter(InventorySlotUI __instance) => DropItemsMod.Hovered = __instance;

        [HarmonyPostfix]
        [HarmonyPatch(nameof(InventorySlotUI.OnCursorLeave))]
        private static void Leave(InventorySlotUI __instance)
        {
            if (DropItemsMod.Hovered == __instance)
            {
                DropItemsMod.Hovered = null;
            }
        }
    }
}
