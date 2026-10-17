using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Items.Model;
using MelonLoader;
using Odyssey.Content;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.State;
using Ships.Interface.Model.Parts.StateTypes;
using Ships.Interface.Settings;
using UI.Inventory;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Odyssey.Patches
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

    internal class BigChestScroll : MonoBehaviour
    {
        internal RectTransform Content;
        internal float Step, MaxOffset;
        private float _offset;

        private void Update()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || Content == null || MaxOffset <= 0f)
            {
                return;
            }
            float wheel = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(wheel) < 0.01f)
            {
                return;
            }
            Canvas canvas = GetComponentInParent<Canvas>();
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (!RectTransformUtility.RectangleContainsScreenPoint((RectTransform)transform, mouse.position.ReadValue(), camera))
            {
                return;
            }
            _offset = Mathf.Clamp(_offset - Mathf.Sign(wheel) * Step, 0f, MaxOffset);
            Content.anchoredPosition = new Vector2(0f, _offset);
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
                Melon<BuildingMod>.Logger.Error($"Chest menu: {e}");
            }
        }

        internal const string ViewportName = "BigChestViewport";
        private const float CellScale = 0.8f, MaxFrameWidth = 3600f, MaxExtraHeight = 120f;
        private const int MaxVisibleRows = 3;

        internal static void Expand(GameObject menu, int slots, int columns)
        {
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
            if (layout != null && gridRect.parent.name != ViewportName)
            {
                float stepX = layout.cellSize.x + layout.spacing.x, stepY = layout.cellSize.y + layout.spacing.y;
                float margin = -gridRect.sizeDelta.x;
                int oldColumns = Mathf.Max(1, Mathf.FloorToInt((gridRect.rect.width - layout.padding.horizontal + layout.spacing.x) / stepX));
                int oldRows = Mathf.CeilToInt(existing.Length / (float)oldColumns);
                float baseHeight = oldRows * layout.cellSize.y + (oldRows - 1) * layout.spacing.y + layout.padding.vertical;
                float gridTop = gridRect.anchoredPosition.y + (1f - gridRect.pivot.y) * gridRect.rect.height;

                Canvas canvas = gridRect.GetComponentInParent<Canvas>();
                float screenWidth = canvas != null ? ((RectTransform)canvas.rootCanvas.transform).rect.width : MaxFrameWidth;
                float maxGridWidth = (Mathf.Min(MaxFrameWidth, screenWidth - 240f) - margin) / CellScale;
                columns = Mathf.Clamp(Mathf.FloorToInt((maxGridWidth - layout.padding.horizontal + layout.spacing.x) / stepX), 1, slots);
                int rows = Mathf.CeilToInt(slots / (float)columns);
                int shownRows = Mathf.Min(rows, MaxVisibleRows);
                layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                layout.constraintCount = columns;
                float width = columns * layout.cellSize.x + (columns - 1) * layout.spacing.x + layout.padding.horizontal;
                float height = rows * layout.cellSize.y + (rows - 1) * layout.spacing.y + layout.padding.vertical;
                float shownHeight = (shownRows * layout.cellSize.y + (shownRows - 1) * layout.spacing.y + layout.padding.vertical) * CellScale;
                float extra = Mathf.Clamp(shownHeight - baseHeight, 0f, MaxExtraHeight);
                float visibleHeight = baseHeight + extra;

                RectTransform frame = gridRect.parent as RectTransform;
                while (frame != null && frame.anchorMin != frame.anchorMax)
                {
                    frame = frame.parent as RectTransform;
                }
                if (frame != null)
                {
                    frame.sizeDelta = new Vector2(Mathf.Max(frame.sizeDelta.x, width * CellScale + margin), frame.sizeDelta.y + extra);
                }

                var parent = (RectTransform)gridRect.parent;
                var viewport = new GameObject(ViewportName, typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
                viewport.gameObject.layer = gridRect.gameObject.layer;
                viewport.SetParent(parent, false);
                viewport.SetSiblingIndex(gridRect.GetSiblingIndex());
                viewport.anchorMin = gridRect.anchorMin;
                viewport.anchorMax = gridRect.anchorMax;
                viewport.pivot = new Vector2(0.5f, 1f);
                viewport.anchoredPosition = new Vector2(gridRect.anchoredPosition.x, gridTop + extra / 2f);
                Vector2 stretch = Vector2.Scale(parent.rect.size, gridRect.anchorMax - gridRect.anchorMin);
                viewport.sizeDelta = new Vector2(width * CellScale, visibleHeight) - stretch;

                gridRect.SetParent(viewport, false);
                gridRect.anchorMin = gridRect.anchorMax = gridRect.pivot = new Vector2(0.5f, 1f);
                gridRect.anchoredPosition = Vector2.zero;
                gridRect.sizeDelta = new Vector2(width, height);
                gridRect.localScale = Vector3.one * CellScale;

                var scroll = viewport.gameObject.AddComponent<BigChestScroll>();
                scroll.Content = gridRect;
                scroll.Step = stepY * CellScale;
                scroll.MaxOffset = Mathf.Max(0f, height * CellScale - visibleHeight);
            }
            LoadSlots.Invoke(grid, null);
            UpdateInventory.Invoke(grid, null);
        }

    }
}
