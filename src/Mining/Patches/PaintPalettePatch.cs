using System;
using System.Collections.Generic;
using System.Linq;
using Core.Services;
using HarmonyLib;
using MelonLoader;
using Players.Interface.Services;
using UI.PaintMenu;
using UnityEngine;
using UnityEngine.UI;

namespace Mining.Patches
{
    [HarmonyPatch(typeof(PaintMenu), "OnEnable")]
    internal static class PaintPalettePatch
    {
        private const string PaletteName = "MiningPalette";
        private static readonly float[] Hues = { 0f, 0.06f, 0.12f, 0.2f, 0.32f, 0.44f, 0.5f, 0.56f, 0.64f, 0.74f, 0.84f };
        private static readonly (float s, float v)[] Shades = { (0.15f, 1f), (0.3f, 1f), (0.55f, 1f), (0.85f, 0.95f), (0.9f, 0.7f), (0.9f, 0.5f), (0.9f, 0.3f) };
        private static readonly float[] Greys = { 1f, 0.85f, 0.7f, 0.55f, 0.4f, 0.25f, 0.1f };

        private static void Postfix(PaintMenu __instance, PaintComponentPick ___hue, PaintComponentPick ___saturation, PaintComponentPick ___value)
        {
            try
            {
                if (___hue == null || __instance.transform.Find(PaletteName) != null || __instance.GetComponentsInChildren<Transform>(true).Any(t => t.name == PaletteName))
                {
                    return;
                }
                if (__instance.GetComponent<PaletteBuilder>() == null)
                {
                    __instance.gameObject.AddComponent<PaletteBuilder>().Init(___hue, ___saturation, ___value);
                }
            }
            catch (Exception e)
            {
                Melon<MiningMod>.Logger.Error($"Paint palette: {e}");
            }
        }

        internal static void Build(PaintComponentPick hue, PaintComponentPick saturation, PaintComponentPick value)
        {
            var picks = new[] { hue, saturation, value }.Where(p => p != null).ToArray();
            var parent = (RectTransform)hue.transform.parent;
            var corners = new Vector3[4];
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            foreach (PaintComponentPick pick in picks)
            {
                ((RectTransform)pick.transform).GetWorldCorners(corners);
                foreach (Vector3 c in corners)
                {
                    Vector2 p = parent.InverseTransformPoint(c);
                    min = Vector2.Min(min, p);
                    max = Vector2.Max(max, p);
                }
            }
            int columns = Hues.Length + 1, rows = Shades.Length;
            float width = max.x - min.x;
            if (width < 50f)
            {
                width = parent.rect.width * 0.9f;
                min = new Vector2(-width / 2f, parent.rect.yMax - parent.rect.height * 0.08f);
                max = new Vector2(width / 2f, min.y);
            }
            float cell = width / (columns + (columns - 1) * 0.15f);
            float gap = cell * 0.15f;
            float height = rows * cell + (rows - 1) * gap;

            var palette = new GameObject(PaletteName, typeof(RectTransform)).GetComponent<RectTransform>();
            palette.SetParent(parent, false);
            palette.SetSiblingIndex(hue.transform.GetSiblingIndex());
            var layoutGroup = parent.GetComponent<LayoutGroup>();
            Melon<MiningMod>.Logger.Msg($"Paint palette: {columns}x{rows}, width {width:0}, cell {cell:0}, parent {parent.name} {parent.rect.size}, layout {(layoutGroup != null ? layoutGroup.GetType().Name : "none")}");
            if (layoutGroup != null)
            {
                var element = palette.gameObject.AddComponent<LayoutElement>();
                element.preferredHeight = element.minHeight = height;
                element.preferredWidth = element.minWidth = width;
            }
            else
            {
                palette.anchorMin = palette.anchorMax = parent.pivot;
                palette.pivot = new Vector2(0f, 1f);
                palette.anchoredPosition = new Vector2(min.x, max.y) + Vector2.Scale(parent.rect.size, parent.pivot) - Vector2.Scale(parent.rect.size, parent.pivot);
                palette.localPosition = new Vector3(min.x, max.y, 0f);
            }
            palette.sizeDelta = new Vector2(width, height);
            var grid = palette.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(cell, cell);
            grid.spacing = new Vector2(gap, gap);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
            grid.childAlignment = TextAnchor.UpperLeft;

            var swatches = new List<(Image frame, Color color)>();
            var tracker = ServiceCache.Get<IPlayerPaintToolSelectionTracker>();
            for (int row = 0; row < rows; row++)
            {
                swatches.Add(AddSwatch(palette, Color.HSVToRGB(0f, 0f, Greys[row]), Select));
                foreach (float h in Hues)
                {
                    swatches.Add(AddSwatch(palette, Color.HSVToRGB(h, Shades[row].s, Shades[row].v), Select));
                }
            }
            foreach (PaintComponentPick pick in picks) pick.gameObject.SetActive(false);
            Highlight();

            void Select(Color color)
            {
                Color.RGBToHSV(color, out float h, out float s, out float v);
                hue.CurrentValue = h;
                saturation.CurrentValue = s;
                value.CurrentValue = v;
                if (tracker != null) tracker.SelectedPaintColor = color;
                Highlight();
            }

            void Highlight()
            {
                Color current = tracker != null ? tracker.SelectedPaintColor : Color.clear;
                foreach (var (frame, color) in swatches)
                {
                    bool on = Mathf.Abs(color.r - current.r) + Mathf.Abs(color.g - current.g) + Mathf.Abs(color.b - current.b) < 0.03f;
                    frame.gameObject.SetActive(on);
                }
            }
        }

        private static (Image frame, Color color) AddSwatch(RectTransform parent, Color color, Action<Color> select)
        {
            var go = new GameObject("Swatch", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            var button = go.GetComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            button.colors = colors;
            button.onClick.AddListener(() => select(color));

            var frameGo = new GameObject("Selected", typeof(RectTransform), typeof(Image));
            var frameRect = frameGo.GetComponent<RectTransform>();
            frameRect.SetParent(go.transform, false);
            frameRect.anchorMin = new Vector2(0.27f, 0.27f);
            frameRect.anchorMax = new Vector2(0.73f, 0.73f);
            frameRect.offsetMin = frameRect.offsetMax = Vector2.zero;
            var frame = frameGo.GetComponent<Image>();
            frame.color = new Color(0.05f, 0.05f, 0.05f, 1f);
            frame.raycastTarget = false;
            var dotGo = new GameObject("Dot", typeof(RectTransform), typeof(Image));
            var dotRect = dotGo.GetComponent<RectTransform>();
            dotRect.SetParent(frameRect, false);
            dotRect.anchorMin = new Vector2(0.2f, 0.2f);
            dotRect.anchorMax = new Vector2(0.8f, 0.8f);
            dotRect.offsetMin = dotRect.offsetMax = Vector2.zero;
            var dot = dotGo.GetComponent<Image>();
            dot.color = Color.white;
            dot.raycastTarget = false;
            frameGo.SetActive(false);
            return (frame, color);
        }
    }

    internal sealed class PaletteBuilder : MonoBehaviour
    {
        private PaintComponentPick _hue, _saturation, _value;
        private int _frames;

        internal void Init(PaintComponentPick hue, PaintComponentPick saturation, PaintComponentPick value)
        {
            _hue = hue;
            _saturation = saturation;
            _value = value;
        }

        private void LateUpdate()
        {
            if (++_frames < 2)
            {
                return;
            }
            try
            {
                Canvas.ForceUpdateCanvases();
                PaintPalettePatch.Build(_hue, _saturation, _value);
            }
            catch (Exception e)
            {
                Melon<MiningMod>.Logger.Error($"Paint palette: {e}");
            }
            Destroy(this);
        }
    }
}
