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
        private const float LineLength = 150f, LineGap = 28f, LineThickness = 3f, Diamond = 11f;

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
                Shape(root, "Diamond", new Vector2(inner + side * (LineLength + Diamond * 0.9f), 0f), new Vector2(Diamond, Diamond), 45f, color);
                Shape(root, "Tick", new Vector2(inner - side * LineGap * 0.45f, 0f), new Vector2(Diamond * 0.45f, Diamond * 0.45f), 45f, color);
            }
            Melon<CoreMod>.Logger.Msg($"Title screen: '{Title}' (was {(original != null ? "text '" + original.text + "'" : "an image")}, {demoRect.rect.size})");
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
