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

        private static void Postfix(MainMenuTitle __instance, GameObject ___demoText)
        {
            if (___demoText != null && ___demoText.transform.parent.Find("OdysseyTitle") == null)
            {
                MelonCoroutines.Start(ApplyLater(__instance, ___demoText));
            }
        }

        private static System.Collections.IEnumerator ApplyLater(MainMenuTitle title, GameObject demo)
        {
            yield return null;
            yield return null;
            if (title == null || demo == null)
            {
                yield break;
            }
            try
            {
                Canvas.ForceUpdateCanvases();
                Apply(title, demo);
            }
            catch (Exception e)
            {
                Melon<CoreMod>.Logger.Error($"Title screen: {e}");
            }
        }

        private static void Apply(MainMenuTitle titleScreen, GameObject demo)
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
            text.characterSpacing = 30f;
            text.fontFeatures.Clear();
            text.raycastTarget = false;
            text.ForceMeshUpdate();
            float half = text.GetPreferredValues(Title).x * 0.5f;

            for (int side = -1; side <= 1; side += 2)
            {
                float inner = side * (half + LineGap);
                Shape(root, "Line", new Vector2(inner + side * LineLength * 0.5f, 0f), new Vector2(LineLength, LineThickness), 0f, color);
            }
            AddOrbits((RectTransform)titleScreen.transform, demoRect, root, color);
            Melon<CoreMod>.Logger.Msg($"Title screen: '{Title}' (was {(original != null ? "text '" + original.text + "'" : "an image")}, {demoRect.rect.size})");
        }

        private static void AddOrbits(RectTransform titleScreen, RectTransform demo, RectTransform title, Color red)
        {
            Graphic logoGraphic = FindLogo(titleScreen, demo, title);
            var parent = (RectTransform)(logoGraphic != null ? logoGraphic.rectTransform.parent : demo.parent);
            Rect logo = logoGraphic != null ? RectIn(parent, logoGraphic.rectTransform) : FallbackLogo(parent, demo);
            var area = new Rect(logo.center.x - logo.width * 0.72f, logo.yMin - logo.height * 0.42f, logo.width * 1.44f, logo.height * 1.72f);
            var orbits = new GameObject("OdysseyOrbits", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            RectTransform rect = orbits.rectTransform;
            rect.SetParent(parent, false);
            if (logoGraphic != null && logoGraphic.transform.parent == parent)
            {
                rect.SetSiblingIndex(logoGraphic.transform.GetSiblingIndex() + 1);
            }
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
            Melon<CoreMod>.Logger.Msg($"Title screen: logo {(logoGraphic != null ? logoGraphic.name : "not found")} {logo} under {parent.name}, orbits {area}");
        }

        private static Graphic FindLogo(RectTransform titleScreen, RectTransform demo, RectTransform title)
        {
            foreach (Graphic g in UnityEngine.Object.FindObjectsByType<Graphic>(FindObjectsSortMode.None))
            {
                string texture = g is Image image && image.sprite != null ? image.sprite.name : g.mainTexture != null ? g.mainTexture.name : "";
                Melon<CoreMod>.Logger.Msg($"Title screen scene: {g.GetType().Name} {Path(g.transform, null)} texture {texture} {g.rectTransform.rect.size}");
            }
            Graphic best = null;
            float bestArea = 0f;
            foreach (Graphic g in titleScreen.GetComponentsInChildren<Graphic>(true))
            {
                if (g.transform.IsChildOf(demo) || g.transform.IsChildOf(title) || !g.gameObject.activeInHierarchy || g is TMP_Text)
                {
                    continue;
                }
                Rect r = RectIn(titleScreen, g.rectTransform);
                Melon<CoreMod>.Logger.Msg($"Title screen: {g.GetType().Name} {Path(g.transform, titleScreen)} {r}");
                float area = r.width * r.height;
                if (r.width > Screen.width * 0.9f || area <= bestArea)
                {
                    continue;
                }
                best = g;
                bestArea = area;
            }
            return best;
        }

        private static string Path(Transform t, Transform root) => t == root || t.parent == null ? t.name : Path(t.parent, root) + "/" + t.name;

        private static Rect RectIn(RectTransform space, RectTransform target)
        {
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            Vector2 min = space.InverseTransformPoint(corners[0]), max = space.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y), Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
        }

        private static Rect FallbackLogo(RectTransform parent, RectTransform demo)
        {
            Rect d = RectIn(parent, demo);
            float h = Mathf.Max(d.height, 40f);
            return new Rect(d.center.x - h * 4.5f, d.center.y + h * 0.6f, h * 9f, h * 4f);
        }

        private static Texture2D DrawOrbits(Vector2 size, Color red, out Vector2 star)
        {
            int w = ArcTextureWidth, h = Mathf.Max(64, Mathf.RoundToInt(ArcTextureWidth * size.y / Mathf.Max(size.x, 1f)));
            var pixels = new Color32[w * h];
            float scale = w / Mathf.Max(size.x, 1f);
            DrawArc(pixels, w, h, new Vector2(0.5f, 0.47f), new Vector2(0.47f, 0.47f), 26f, 170f, 3.2f * scale, Color.white, 0.95f);
            DrawArc(pixels, w, h, new Vector2(0.47f, 0.5f), new Vector2(0.48f, 0.44f), -64f, 30f, 3.6f * scale, red, 1f);
            float a = 30f * Mathf.Deg2Rad;
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
