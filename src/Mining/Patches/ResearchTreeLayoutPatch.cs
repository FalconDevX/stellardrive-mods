using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MelonLoader;
using TMPro;
using UI.Research;
using UnityEngine;
using UnityEngine.UI;

namespace Mining.Patches
{
    [HarmonyPatch(typeof(ResearchCanvas), "GenerateButtons")]
    internal static class ResearchTreeLayoutPatch
    {
        private const float ColumnWidth = 500f, RowHeight = 225f, Left = 330f, Top = 765f;

        private static readonly (string name, Color color)[] Categories =
        {
            ("SIGNALS", new Color(0.35f, 0.7f, 1f)),
            ("SHIP & TRAVEL", new Color(0.6f, 0.5f, 1f)),
            ("POWER & PROPULSION", new Color(1f, 0.6f, 0.25f)),
            ("FLUIDS & MINING", new Color(0.3f, 0.85f, 0.75f)),
            ("BUILDING", new Color(0.75f, 0.75f, 0.75f)),
            ("MATERIALS", new Color(0.55f, 0.85f, 0.95f)),
            ("STORAGE", new Color(0.95f, 0.85f, 0.35f)),
            ("GEAR", new Color(0.55f, 0.9f, 0.45f)),
        };

        private static readonly Dictionary<ushort, (int column, int row)> Layout = new Dictionary<ushort, (int, int)>
        {
            { 1, (0, 0) }, { 15, (3, 0) }, { 4, (4, 0) }, { 5, (5, 0) }, { 211, (6, 0) },
            { 12, (2, 1) }, { 11, (3, 1) }, { 21, (4, 1) }, { 22, (5, 1) }, { 210, (6, 1) },
            { 2, (1, 2) }, { 20, (3, 2) }, { 201, (5, 2) }, { 204, (6, 2) }, { 212, (7, 2) }, { 213, (8, 2) }, { 10, (2, 2) },
            { 8, (2, 3) }, { 207, (3, 3) }, { 200, (4, 3) },
            { 6, (1, 4) }, { 18, (2, 4) }, { 16, (3, 4) }, { 206, (4, 4) }, { 203, (5, 4) },
            { 7, (2, 5) },
            { 17, (1, 6) }, { 208, (2, 6) },
            { 14, (2, 7) }, { 205, (3, 7) }, { 209, (4, 7) },
        };

        private static void Postfix(RectTransform ___buttonsContainer, List<ResearchSelectButton> ____buttons)
        {
            try
            {
                Apply(___buttonsContainer, ____buttons);
            }
            catch (Exception e)
            {
                Melon<MiningMod>.Logger.Error($"Research tree layout: {e}");
            }
        }

        private static void Apply(RectTransform container, List<ResearchSelectButton> buttons)
        {
            if (container == null || buttons == null || buttons.Count == 0)
            {
                return;
            }
            int maxColumn = 0;
            var used = new HashSet<(int, int)>();
            foreach (ResearchSelectButton button in buttons)
            {
                if (button.research != null && Layout.TryGetValue(button.research.id, out var cell))
                {
                    used.Add(cell);
                }
            }
            foreach (ResearchSelectButton button in buttons)
            {
                if (button.research == null) continue;
                if (!Layout.TryGetValue(button.research.id, out var cell))
                {
                    cell = (Mathf.Max(0, button.research.positionInTechTree.x), Categories.Length - 1);
                    while (used.Contains(cell)) cell.column++;
                    used.Add(cell);
                }
                button.transform.localPosition = new Vector3(Left + ColumnWidth * cell.column, RowY(cell.row));
                maxColumn = Mathf.Max(maxColumn, cell.column);
            }
            container.sizeDelta = new Vector2(Left + 440f + maxColumn * ColumnWidth, container.sizeDelta.y);
            DrawBands(container, buttons[0], maxColumn);
        }

        private static float RowY(int row) => Top - RowHeight * row;

        private static void DrawBands(RectTransform container, ResearchSelectButton sample, int maxColumn)
        {
            Transform old = container.Find("MiningTreeBands");
            if (old != null) UnityEngine.Object.Destroy(old.gameObject);
            var root = new GameObject("MiningTreeBands", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(container, false);
            root.SetAsFirstSibling();
            root.anchorMin = root.anchorMax = ((RectTransform)sample.transform).anchorMin;
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = Vector2.zero;
            root.localPosition = Vector3.zero;
            root.localScale = Vector3.one;

            var buttonRect = (RectTransform)sample.transform;
            float centerOffset = buttonRect.rect.center.y;
            float left = Left + buttonRect.rect.xMin - 60f;
            TMP_Text font = sample.GetComponentInChildren<TMP_Text>(true);
            float width = maxColumn * ColumnWidth + buttonRect.rect.width + 120f;
            for (int row = 0; row < Categories.Length; row++)
            {
                var (name, color) = Categories[row];
                float y = RowY(row) + centerOffset;
                var band = new GameObject("Band " + name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                band.SetParent(root, false);
                band.anchorMin = band.anchorMax = new Vector2(0f, 0.5f);
                band.pivot = new Vector2(0f, 0.5f);
                band.localPosition = new Vector3(left, y, 0f);
                band.sizeDelta = new Vector2(width, RowHeight - 14f);
                var image = band.GetComponent<Image>();
                image.color = new Color(color.r, color.g, color.b, row % 2 == 0 ? 0.10f : 0.06f);
                image.raycastTarget = false;

                var strip = new GameObject("Strip", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                strip.SetParent(band, false);
                strip.anchorMin = new Vector2(0f, 0f);
                strip.anchorMax = new Vector2(0f, 1f);
                strip.pivot = new Vector2(0f, 0.5f);
                strip.sizeDelta = new Vector2(10f, 0f);
                strip.anchoredPosition = Vector2.zero;
                var stripImage = strip.GetComponent<Image>();
                stripImage.color = new Color(color.r, color.g, color.b, 0.85f);
                stripImage.raycastTarget = false;

                if (font != null)
                {
                    var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<RectTransform>();
                    label.SetParent(band, false);
                    label.anchorMin = label.anchorMax = new Vector2(0f, 1f);
                    label.pivot = new Vector2(0f, 1f);
                    label.anchoredPosition = new Vector2(22f, -6f);
                    label.sizeDelta = new Vector2(700f, 40f);
                    var text = label.GetComponent<TextMeshProUGUI>();
                    text.font = font.font;
                    text.fontSize = 26f;
                    text.fontStyle = FontStyles.Bold;
                    text.color = new Color(color.r, color.g, color.b, 0.95f);
                    text.text = name;
                    text.raycastTarget = false;
                }
            }
        }
    }
}
