using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Items.Model;
using MelonLoader;
using Mining.Content;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.State;
using Ships.Interface.Model.Parts.StateTypes;
using Ships.Interface.Settings;
using UI.Inventory;
using UnityEngine;
using UnityEngine.UI;

namespace Mining.Patches
{
    internal static class StorageSlots
    {
        private static readonly Func<StorageState, InventorySlot[]> GetSlots =
            AccessTools.MethodDelegate<Func<StorageState, InventorySlot[]>>(AccessTools.PropertyGetter(typeof(StorageState), "InventorySlots"));

        internal static int Count(StorageState state)
        {
            InventorySlot[] slots = state != null ? GetSlots(state) : null;
            return slots != null && slots.Length > 0 ? slots.Length : 10;
        }

        internal static InventorySlot[] Slots(StorageState state) => GetSlots(state);
    }

    [HarmonyPatch(typeof(StorageState), nameof(StorageState.SlotCount), MethodType.Getter)]
    internal static class StorageSlotCountPatch
    {
        private static bool Prefix(StorageState __instance, ref int __result)
        {
            __result = StorageSlots.Count(__instance);
            return false;
        }
    }

    [HarmonyPatch(typeof(StorageState), nameof(StorageState.WithUpdatedSlot))]
    internal static class StorageWithUpdatedSlotPatch
    {
        private static bool Prefix(StorageState __instance, byte slotId, InventorySlot slotData, ref IStoragePart __result)
        {
            InventorySlot[] current = StorageSlots.Slots(__instance);
            var copy = new InventorySlot[StorageSlots.Count(__instance)];
            Array.Copy(current, copy, Math.Min(current.Length, copy.Length));
            copy[slotId] = slotData;
            __result = __instance with { InventorySlots = copy };
            return false;
        }
    }

    [HarmonyPatch]
    internal static class StorageSerializationSlotCountPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            Type serialization = AccessTools.TypeByName("Managers.Server.Network.Serialization.StatefulPartSerialization");
            yield return AccessTools.Method(serialization, "WriteState");
            yield return AccessTools.Method(serialization, "ReadNewState");
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo getter = AccessTools.PropertyGetter(typeof(StorageState), nameof(StorageState.SlotCount));
            MethodInfo replacement = AccessTools.Method(typeof(StorageSlots), nameof(StorageSlots.Count));
            foreach (CodeInstruction code in instructions)
            {
                if (code.Calls(getter))
                {
                    code.opcode = OpCodes.Call;
                    code.operand = replacement;
                }
                yield return code;
            }
        }
    }

    [HarmonyPatch(typeof(PartStateFactory), nameof(PartStateFactory.BuildDefaultState))]
    internal static class BigChestDefaultStatePatch
    {
        private static void Postfix(PartSettings partSettings, ref IStatefulPartState __result)
        {
            if (__result is StorageState && BigChest.TryGetLayout(partSettings, out int slots, out _))
            {
                __result = new StorageState { InventorySlots = new InventorySlot[slots] };
            }
        }
    }

    [HarmonyPatch(typeof(StorageMenuActivator), nameof(StorageMenuActivator.ShowMenu))]
    internal static class BigChestMenuPatch
    {
        private static readonly MethodInfo LoadSlots = AccessTools.Method(typeof(InventoryGrid), "LoadSlots");
        private static readonly MethodInfo UpdateInventory = AccessTools.Method(typeof(InventoryGrid), "UpdateInventory");

        private static void Postfix(GameObject ____currentMenu)
        {
            if (____currentMenu == null)
            {
                return;
            }
            PartSettings settings = null;
            if (!StorageMenuPatch.IsLocalPlayerUsing(s => { settings = s; return BigChest.TryGetLayout(s, out _, out _); }))
            {
                return;
            }
            BigChest.TryGetLayout(settings, out int slots, out int columns);
            try
            {
                Expand(____currentMenu, slots, columns);
            }
            catch (Exception e)
            {
                Melon<MiningMod>.Logger.Error($"Chest menu: {e}");
            }
        }

        internal static void Expand(GameObject menu, int slots, int columns)
        {
            const float MaxGridWidth = 3300f;
            InventoryGrid grid = menu.GetComponentsInChildren<InventoryGrid>(true)
                .FirstOrDefault(g => g.GetComponentsInChildren<InventorySlotUI>(true).Any(s => s.origin == InventoryType.Chest));
            if (grid == null)
            {
                return;
            }
            InventorySlotUI[] existing = grid.GetComponentsInChildren<InventorySlotUI>(true);
            InventorySlotUI template = existing.Last();
            var gridRect = (RectTransform)template.transform.parent;
            var layout = gridRect.GetComponent<GridLayoutGroup>();
            for (int i = existing.Length; i < slots; i++)
            {
                InventorySlotUI copy = UnityEngine.Object.Instantiate(template, gridRect);
                copy.name = template.name.Split('(')[0].Trim() + " " + i;
                copy.id = i;
            }
            if (layout != null)
            {
                int rows = Mathf.CeilToInt(slots / (float)columns);
                float cellStep = layout.cellSize.x + layout.spacing.x;
                int oldColumns = Mathf.Max(1, Mathf.FloorToInt((gridRect.rect.width - layout.padding.horizontal + layout.spacing.x) / cellStep));
                int oldRows = Mathf.CeilToInt(existing.Length / (float)oldColumns);
                float oldRowsHeight = oldRows * layout.cellSize.y + (oldRows - 1) * layout.spacing.y + layout.padding.vertical;
                float oldHeight = gridRect.sizeDelta.y;
                layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                layout.constraintCount = columns;
                float width = columns * layout.cellSize.x + (columns - 1) * layout.spacing.x + layout.padding.horizontal;
                float height = rows * layout.cellSize.y + (rows - 1) * layout.spacing.y + layout.padding.vertical;
                float scale = Mathf.Min(1f, MaxGridWidth / width, oldRowsHeight / height);
                float margin = -gridRect.sizeDelta.x;
                gridRect.localScale = Vector3.one * scale;
                RectTransform frame = gridRect.parent as RectTransform;
                while (frame != null && frame.anchorMin != frame.anchorMax)
                {
                    frame = frame.parent as RectTransform;
                }
                if (frame != null)
                {
                    float frameWidth = Mathf.Max(frame.sizeDelta.x, width * scale + margin);
                    frame.sizeDelta = new Vector2(frameWidth, frame.sizeDelta.y);
                    gridRect.sizeDelta = new Vector2(width - frameWidth, oldHeight / scale);
                }
            }
            LoadSlots.Invoke(grid, null);
            UpdateInventory.Invoke(grid, null);
        }

    }
}
