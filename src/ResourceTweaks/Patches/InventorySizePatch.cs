using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Items.Model;
using MelonLoader;
using Players.Inventory;
using Players.Network;
using UI.Inventory;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ResourceTweaks.Patches
{
    internal static class InventorySize
    {
        internal const int Original = 15;

        internal static int Slots => Math.Max(Original, Math.Min(250, ResourceTweaksMod.InventorySlots.Value));

        internal static void Apply()
        {
            InventoryConstants.GridSize = Slots;
        }
    }

    [HarmonyPatch(typeof(PlayerNetworkState), nameof(PlayerNetworkState.ServerSetInventory))]
    internal static class InventoryPaddingPatch
    {
        private static void Prefix(ref IEnumerable<InventorySlot> inventory)
        {
            List<InventorySlot> slots = inventory?.ToList() ?? new List<InventorySlot>();
            while (slots.Count < InventoryConstants.GridSize)
            {
                slots.Add(default);
            }
            inventory = slots;
        }
    }

    internal class InventoryScroll : MonoBehaviour
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

    [HarmonyPatch(typeof(InventoryGrid), "Awake")]
    internal static class InventoryGridPatch
    {
        private const string ViewportName = "PlayerInventoryViewport";
        private static readonly MethodInfo LoadSlots = AccessTools.Method(typeof(InventoryGrid), "LoadSlots");
        private static bool _loggedError;

        private static void Postfix(InventoryGrid __instance, InventoryType ___source)
        {
            if (___source != InventoryType.MainInventory)
            {
                return;
            }
            try
            {
                Expand(__instance, InventoryConstants.GridSize);
                Enlarge(__instance);
            }
            catch (Exception e)
            {
                if (!_loggedError)
                {
                    _loggedError = true;
                    Melon<ResourceTweaksMod>.Logger.Error($"Inventory window: {e}");
                }
            }
        }

        private static void Enlarge(InventoryGrid grid)
        {
            float scale = Mathf.Clamp(ResourceTweaksMod.InventoryScale.Value, 1f, 2.5f);
            Transform parent = grid.transform.parent;
            if (scale <= 1.001f || parent == null)
            {
                return;
            }
            foreach (InventoryGrid other in parent.GetComponentsInChildren<InventoryGrid>(true))
            {
                if (other != grid && Traverse.Create(other).Field<InventoryType>("source").Value != InventoryType.MainInventory)
                {
                    return;
                }
            }
            Transform target = grid.transform;
            for (Transform t = grid.transform.parent; t != null; t = t.parent)
            {
                Canvas canvas = t.GetComponent<Canvas>();
                if (canvas != null && canvas.isRootCanvas)
                {
                    break;
                }
                if (t.GetComponentInChildren<DestroySlotUI>(true) != null)
                {
                    if (t.GetComponentsInChildren<InventoryGrid>(true).Length == 1)
                    {
                        target = t;
                    }
                    break;
                }
            }
            target.localScale *= scale;
            if (target == grid.transform)
            {
                DestroySlotUI trash = parent.root.GetComponentsInChildren<DestroySlotUI>(true)
                    .OrderBy(d => Vector3.Distance(d.transform.position, target.position)).FirstOrDefault();
                if (trash != null && !trash.transform.IsChildOf(target))
                {
                    trash.transform.position = target.position + (trash.transform.position - target.position) * scale;
                    trash.transform.localScale *= scale;
                }
            }
            Melon<ResourceTweaksMod>.Logger.Msg($"Inventory window '{target.name}' scaled x{scale:0.##}");
        }

        private static void Expand(InventoryGrid grid, int slots)
        {
            InventorySlotUI[] existing = grid.GetComponentsInChildren<InventorySlotUI>(true).Where(s => s.origin == InventoryType.MainInventory).ToArray();
            if (existing.Length == 0 || existing.Length >= slots)
            {
                return;
            }
            InventorySlotUI template = existing.Last();
            var gridRect = (RectTransform)template.transform.parent;
            if (gridRect.parent != null && gridRect.parent.name == ViewportName)
            {
                return;
            }
            var layout = gridRect.GetComponent<GridLayoutGroup>();
            float visibleWidth = gridRect.rect.width, visibleHeight = gridRect.rect.height;
            for (int i = existing.Length; i < slots; i++)
            {
                InventorySlotUI copy = UnityEngine.Object.Instantiate(template, gridRect);
                copy.name = template.name.Split('(')[0].Trim() + " " + i;
                copy.id = i;
            }
            LoadSlots.Invoke(grid, null);
            if (layout == null)
            {
                return;
            }

            float stepX = layout.cellSize.x + layout.spacing.x, stepY = layout.cellSize.y + layout.spacing.y;
            int columns = Mathf.Max(1, Mathf.FloorToInt((visibleWidth - layout.padding.horizontal + layout.spacing.x) / stepX));
            int rows = Mathf.CeilToInt(slots / (float)columns);
            float height = rows * layout.cellSize.y + (rows - 1) * layout.spacing.y + layout.padding.vertical;
            float gridTop = gridRect.anchoredPosition.y + (1f - gridRect.pivot.y) * visibleHeight;
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = columns;

            var parent = (RectTransform)gridRect.parent;
            var viewport = new GameObject(ViewportName, typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
            viewport.gameObject.layer = gridRect.gameObject.layer;
            viewport.SetParent(parent, false);
            viewport.SetSiblingIndex(gridRect.GetSiblingIndex());
            viewport.anchorMin = gridRect.anchorMin;
            viewport.anchorMax = gridRect.anchorMax;
            viewport.pivot = new Vector2(0.5f, 1f);
            viewport.anchoredPosition = new Vector2(gridRect.anchoredPosition.x + (0.5f - gridRect.pivot.x) * visibleWidth, gridTop);
            Vector2 stretch = Vector2.Scale(parent.rect.size, gridRect.anchorMax - gridRect.anchorMin);
            viewport.sizeDelta = new Vector2(visibleWidth, visibleHeight) - stretch;

            gridRect.SetParent(viewport, false);
            gridRect.anchorMin = gridRect.anchorMax = gridRect.pivot = new Vector2(0.5f, 1f);
            gridRect.anchoredPosition = Vector2.zero;
            gridRect.sizeDelta = new Vector2(visibleWidth, height);

            var scroll = viewport.gameObject.AddComponent<InventoryScroll>();
            scroll.Content = gridRect;
            scroll.Step = stepY;
            scroll.MaxOffset = Mathf.Max(0f, height - visibleHeight);
        }
    }
}
