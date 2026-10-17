using System;
using System.Linq;
using HarmonyLib;
using MelonLoader;
using TMPro;
using UI.MainMenu;
using UnityEngine;
using UnityEngine.UI;

namespace Odyssey.Patches
{
    [HarmonyPatch(typeof(MainMenuTitle), "Start")]
    internal static class TitleScreenPatch
    {
        private const string Title = "ODYSSEY";
        private const float LineLength = 62f, LineGap = 26f, LineThickness = 3f;
        private const int ArcTextureWidth = 1024;

        private static void Postfix(GameObject ___demoText)
        {
            try
            {
                if (___demoText == null || ___demoText.transform.parent.Find("OdysseyTitle") != null)
                {
                    return;
                }
                Apply(___demoText);
            }
            catch (Exception e)
            {
                Melon<CoreMod>.Logger.Error($"Title screen: {e}");
            }
        }

        private static void Apply(GameObject demo)
        {
            var demoRect = (RectTransform)demo.transform;
            TMP_Text original = demo.GetComponentInChildren<TMP_Text>(true);
            Graphic graphic = original != null ? (Graphic)original : demo.GetComponentInChildren<Graphic>(true);
            Color color = graphic != null ? graphic.color : new Color(0.93f, 0.33f, 0.27f);
            float height = Mathf.Max(demoRect.rect.height, 40f);

            var root = new GameObject("OdysseyTitle", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(demoRect.parent, false);
            root.SetSiblingIndex(demoRect.GetSiblingIndex() + 1);
            root.anchorMin = demoRect.anchorMin;
            root.anchorMax = demoRect.anchorMax;
            root.pivot = demoRect.pivot;
            root.anchoredPosition = demoRect.anchoredPosition;
            root.sizeDelta = demoRect.sizeDelta;
            root.localScale = demoRect.localScale;
            root.localRotation = demoRect.localRotation;
            demo.SetActive(false);

            var text = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            text.rectTransform.SetParent(root, false);
            Stretch(text.rectTransform);
            TMP_Text font = original ?? UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(t => t.font != null);
            if (font != null)
            {
                text.font = font.font;
                text.fontSharedMaterial = font.fontSharedMaterial;
            }
            text.text = Title;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.fontSize = original != null ? original.fontSize : height * 0.8f;
            text.characterSpacing = 38f;
            text.raycastTarget = false;
            text.ForceMeshUpdate();
            float half = text.GetPreferredValues(Title).x * 0.5f;

            for (int side = -1; side <= 1; side += 2)
            {
                float inner = side * (half + LineGap);
                Shape(root, "Line", new Vector2(inner + side * LineLength * 0.5f, 0f), new Vector2(LineLength, LineThickness), 0f, color);
            }
            AddOrbits((RectTransform)demoRect.parent, demoRect, root, color);
            Melon<CoreMod>.Logger.Msg($"Title screen: '{Title}' (was {(original != null ? "text '" + original.text + "'" : "an image")}, {demoRect.rect.size})");
        }

        private static void AddOrbits(RectTransform parent, RectTransform demo, RectTransform title, Color red)
        {
            Rect logo = FindLogo(parent, demo, title);
            var area = new Rect(logo.center.x - logo.width * 0.66f, logo.yMin - logo.height * 0.45f, logo.width * 1.32f, logo.height * 1.75f);
            var orbits = new GameObject("OdysseyOrbits", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            RectTransform rect = orbits.rectTransform;
            rect.SetParent(parent, false);
            rect.SetSiblingIndex(title.GetSiblingIndex());
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            var parentCenter = new Vector2((0.5f - parent.pivot.x) * parent.rect.width, (0.5f - parent.pivot.y) * parent.rect.height);
            rect.anchoredPosition = area.center - parentCenter;
            rect.sizeDelta = area.size;
            orbits.texture = DrawOrbits(area.size, red, out Vector2 star);
            orbits.raycastTarget = false;

            float starSize = Mathf.Clamp(logo.height * 0.42f, 28f, 140f);
            var starImage = new GameObject("OdysseyStar", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            RectTransform starRect = starImage.rectTransform;
            starRect.SetParent(rect, false);
            starRect.anchorMin = starRect.anchorMax = star;
            starRect.pivot = new Vector2(0.5f, 0.5f);
            starRect.anchoredPosition = Vector2.zero;
            starRect.sizeDelta = new Vector2(starSize, starSize);
            starImage.texture = DrawStar(128);
            starImage.color = red;
            starImage.raycastTarget = false;
            Melon<CoreMod>.Logger.Msg($"Title screen: logo {logo}, orbits {area}");
        }

        private static Rect FindLogo(RectTransform parent, RectTransform demo, RectTransform title)
        {
            var corners = new Vector3[4];
            Rect best = Rect.zero;
            foreach (Graphic g in parent.GetComponentsInChildren<Graphic>(true))
            {
                if (g.transform.IsChildOf(demo) || g.transform.IsChildOf(title) || !g.gameObject.activeInHierarchy)
                {
                    continue;
                }
                g.rectTransform.GetWorldCorners(corners);
                Vector2 min = parent.InverseTransformPoint(corners[0]), max = parent.InverseTransformPoint(corners[2]);
                Rect r = Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y), Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
                Melon<CoreMod>.Logger.Msg($"Title screen: {g.GetType().Name} {g.name} {r}");
                if (r.width > Screen.width * 0.9f || r.height > Screen.height * 0.9f || r.width * r.height <= best.width * best.height)
                {
                    continue;
                }
                best = r;
            }
            if (best.width > 0f)
            {
                return best;
            }
            demo.GetWorldCorners(corners);
            Vector2 c = (parent.InverseTransformPoint(corners[0]) + parent.InverseTransformPoint(corners[2])) * 0.5f;
            float h = Mathf.Max(demo.rect.height, 40f);
            return new Rect(c.x - h * 4.5f, c.y + h * 0.6f, h * 9f, h * 4f);
        }

        private static Texture2D DrawOrbits(Vector2 size, Color red, out Vector2 star)
        {
            int w = ArcTextureWidth, h = Mathf.Max(64, Mathf.RoundToInt(ArcTextureWidth * size.y / Mathf.Max(size.x, 1f)));
            var pixels = new Color32[w * h];
            float scale = w / Mathf.Max(size.x, 1f);
            DrawArc(pixels, w, h, new Vector2(0.5f, 0.47f), new Vector2(0.47f, 0.47f), 28f, 168f, 2.2f * scale, Color.white, 0.9f);
            DrawArc(pixels, w, h, new Vector2(0.47f, 0.5f), new Vector2(0.48f, 0.44f), -62f, 38f, 3.2f * scale, red, 1f);
            float a = 38f * Mathf.Deg2Rad;
            star = new Vector2(0.47f + 0.48f * Mathf.Cos(a), 0.5f + 0.44f * Mathf.Sin(a));
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "OdysseyOrbits", wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return tex;
        }

        private static void DrawArc(Color32[] pixels, int w, int h, Vector2 center, Vector2 radius, float fromDeg, float toDeg, float thickness, Color color, float alpha)
        {
            float rx = radius.x * w, ry = radius.y * h, cx = center.x * w, cy = center.y * h;
            int steps = Mathf.CeilToInt(Mathf.Max(rx, ry) * (toDeg - fromDeg) * Mathf.Deg2Rad * 1.5f);
            var rgb = (Color32)color;
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                float angle = Mathf.Lerp(fromDeg, toDeg, t) * Mathf.Deg2Rad;
                float taper = Mathf.Sin(t * Mathf.PI);
                float half = Mathf.Max(0.6f, thickness * 0.5f * Mathf.Lerp(0.35f, 1f, taper));
                float px = cx + rx * Mathf.Cos(angle), py = cy + ry * Mathf.Sin(angle);
                float fade = alpha * Mathf.Clamp01(taper * 2.2f);
                int r = Mathf.CeilToInt(half + 1.5f);
                for (int y = Mathf.Max(0, (int)py - r); y <= Mathf.Min(h - 1, (int)py + r); y++)
                {
                    for (int x = Mathf.Max(0, (int)px - r); x <= Mathf.Min(w - 1, (int)px + r); x++)
                    {
                        float d = Mathf.Sqrt((x - px) * (x - px) + (y - py) * (y - py));
                        float cover = Mathf.Clamp01(half + 0.5f - d) * fade;
                        int idx = y * w + x;
                        if (cover * 255f > pixels[idx].a)
                        {
                            pixels[idx] = new Color32(rgb.r, rgb.g, rgb.b, (byte)(cover * 255f));
                        }
                    }
                }
            }
        }

        private static Texture2D DrawStar(int size)
        {
            var pixels = new Color32[size * size];
            float edge = 12f / size;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = Mathf.Abs((x + 0.5f) / size * 2f - 1f), v = Mathf.Abs((y + 0.5f) / size * 2f - 1f);
                    float body = Mathf.Clamp01((1f - Mathf.Sqrt(u) - Mathf.Sqrt(v)) / edge + 0.5f);
                    float glow = Mathf.Clamp01(1f - Mathf.Sqrt(u * u + v * v) * 2.6f) * 0.35f;
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Max(body, glow) * 255f));
                }
            }
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "OdysseyStar", wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return tex;
        }

        private static void Shape(RectTransform parent, string name, Vector2 position, Vector2 size, float angle, Color color)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            RectTransform rect = image.rectTransform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localRotation = Quaternion.Euler(0f, 0f, angle);
            image.color = color;
            image.raycastTarget = false;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(-600f, 0f);
            rect.offsetMax = new Vector2(600f, 0f);
        }
    }
}
