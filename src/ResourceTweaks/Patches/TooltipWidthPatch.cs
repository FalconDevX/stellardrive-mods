using HarmonyLib;
using MelonLoader;
using TMPro;
using UI.Inventory;
using UnityEngine;
using UnityEngine.UI;

namespace ResourceTweaks.Patches
{
    // Dymek z nazwą przedmiotu (po najechaniu na slot) ma w grze stałą szerokość, więc pasek pod krótką
    // nazwą ("Iron") ciągnie się daleko w prawo. Tu dopasowujemy szerokość paska do długości tekstu.
    [HarmonyPatch(typeof(ItemTooltip), nameof(ItemTooltip.Show))]
    internal static class TooltipWidthPatch
    {
        private static readonly AccessTools.FieldRef<ItemTooltip, TMP_Text> TextRef =
            AccessTools.FieldRefAccess<ItemTooltip, TMP_Text>("_textObject");

        private static float _padding = -1f;

        private static void Postfix(ItemTooltip __instance, string label)
        {
            TMP_Text text = TextRef(__instance);
            var root = __instance.transform as RectTransform;
            if (text == null || root == null)
            {
                return;
            }

            if (_padding < 0f)
            {
                _padding = MeasurePadding(root, text.rectTransform);
                Melon<ResourceTweaksMod>.Logger.Msg($"Dymek przedmiotu: szerokość {root.rect.width:0}, margines {_padding:0}");
            }

            float width = Mathf.Ceil(text.GetPreferredValues(label).x + _padding);

            // Gdyby szerokość narzucał layout, wyłączamy go dla osi X.
            var fitter = root.GetComponent<ContentSizeFitter>();
            if (fitter != null)
            {
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            }
            var element = root.GetComponent<LayoutElement>();
            if (element != null)
            {
                element.minWidth = -1f;
                element.preferredWidth = width;
            }

            root.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);

            // Tekst rozciągnięty na cały dymek sam się dopasuje; tekst o stałej szerokości poszerzamy ręcznie.
            RectTransform textRect = text.rectTransform;
            if (textRect != root && Mathf.Approximately(textRect.anchorMin.x, textRect.anchorMax.x))
            {
                textRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width - _padding);
            }
        }

        // Margines = odstęp tekstu od lewej krawędzi dymka, liczony z obu stron.
        private static float MeasurePadding(RectTransform root, RectTransform textRect)
        {
            if (textRect == root)
            {
                return 12f;
            }
            var rootCorners = new Vector3[4];
            var textCorners = new Vector3[4];
            root.GetWorldCorners(rootCorners);
            textRect.GetWorldCorners(textCorners);
            float scale = root.lossyScale.x != 0f ? root.lossyScale.x : 1f;
            float left = (textCorners[0].x - rootCorners[0].x) / scale;
            return Mathf.Clamp(left * 2f, 8f, 40f);
        }
    }
}
