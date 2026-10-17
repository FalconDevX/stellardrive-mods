using System;
using System.Linq;
using HarmonyLib;
using MelonLoader;
using Odyssey.Content;
using TMPro;
using UI.Loading;
using UnityEngine;
using UnityEngine.UI;

namespace Odyssey.Patches
{
    [HarmonyPatch(typeof(LoadingScreenDisabler), "Awake")]
    internal static class LoadingScreenPatch
    {
        private static readonly string[] Images = { "loading_0.jpg", "loading_1.jpg" };
        private static readonly Color Accent = new Color(0.93f, 0.36f, 0.32f);
        private static Texture2D[] _textures;

        private static void Postfix(LoadingScreenDisabler __instance)
        {
            try
            {
                Build(__instance.gameObject);
            }
            catch (Exception e)
            {
                Melon<CoreMod>.Logger.Error($"Loading screen: {e}");
            }
        }

        private static void Build(GameObject screen)
        {
            var root = (RectTransform)screen.transform;
            if (root.Find("OdysseyLoading") != null)
            {
                return;
            }
            _textures = _textures ?? Images.Select(n => ModAssets.LoadTexture(n)).ToArray();
            var layer = Rect("OdysseyLoading", root);
            Stretch(layer, Vector2.zero, Vector2.one);
            Transform background = root.Find("Image");
            layer.SetSiblingIndex(background != null ? background.GetSiblingIndex() + 1 : 0);
            Transform spinner = root.Find("Loading");
            if (spinner != null) spinner.gameObject.SetActive(false);

            var picture = layer.gameObject.AddComponent<RawImage>();
            picture.texture = _textures[UnityEngine.Random.Range(0, _textures.Length)];
            picture.raycastTarget = false;
            var fitter = layer.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = (float)picture.texture.width / picture.texture.height;

            var shade = Rect("Shade", root).gameObject.AddComponent<RawImage>();
            Stretch(shade.rectTransform, Vector2.zero, new Vector2(1f, 0.45f));
            shade.rectTransform.SetSiblingIndex(layer.GetSiblingIndex() + 1);
            shade.texture = Gradient();
            shade.raycastTarget = false;

            TMP_Text font = UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(t => t.font != null);
            var hud = Rect("OdysseyLoadingHud", root);
            Stretch(hud, Vector2.zero, Vector2.one);
            hud.SetAsLastSibling();
            Label(hud, "Title", font, "STELLAR DRIVE  <color=#ED5C52>ODYSSEY</color>", 46f, new Vector2(0.06f, 0.155f), TextAlignmentOptions.BottomLeft, Color.white, 8f);
            TMP_Text stage = Label(hud, "Stage", font, "", 26f, new Vector2(0.06f, 0.105f), TextAlignmentOptions.BottomLeft, new Color(1f, 1f, 1f, 0.8f), 2f);
            TMP_Text percent = Label(hud, "Percent", font, "", 26f, new Vector2(0.94f, 0.105f), TextAlignmentOptions.BottomRight, Accent, 2f);

            var track = Rect("Track", hud).gameObject.AddComponent<Image>();
            Stretch(track.rectTransform, new Vector2(0.06f, 0.075f), new Vector2(0.94f, 0.075f));
            track.rectTransform.sizeDelta = new Vector2(0f, 8f);
            track.color = new Color(1f, 1f, 1f, 0.18f);
            track.raycastTarget = false;
            var fill = Rect("Fill", track.rectTransform).gameObject.AddComponent<Image>();
            Stretch(fill.rectTransform, Vector2.zero, new Vector2(0f, 1f));
            fill.color = Accent;
            fill.raycastTarget = false;

            hud.gameObject.AddComponent<LoadingProgress>().Init(fill.rectTransform, stage, percent);
            Melon<CoreMod>.Logger.Msg($"Loading screen: {root.name} ({string.Join(", ", root.Cast<Transform>().Select(t => t.name))})");
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static TMP_Text Label(RectTransform parent, string name, TMP_Text font, string text, float size, Vector2 anchor, TextAlignmentOptions alignment, Color color, float spacing)
        {
            var label = Rect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = anchor;
            label.rectTransform.pivot = new Vector2(alignment == TextAlignmentOptions.BottomRight ? 1f : 0f, 0f);
            label.rectTransform.sizeDelta = new Vector2(1400f, 80f);
            if (font != null)
            {
                label.font = font.font;
                label.fontSharedMaterial = font.fontSharedMaterial;
            }
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.characterSpacing = spacing;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            return label;
        }

        private static Texture2D Gradient()
        {
            var tex = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "OdysseyShade" };
            for (int y = 0; y < 64; y++)
            {
                tex.SetPixel(0, y, new Color(0.02f, 0.03f, 0.06f, Mathf.Lerp(0.95f, 0f, Mathf.Pow(y / 63f, 0.7f))));
            }
            tex.Apply(false, true);
            return tex;
        }
    }

    internal class LoadingProgress : MonoBehaviour
    {
        private static readonly (float at, string text)[] Stages =
        {
            (0f, "Loading world"),
            (0.2f, "Generating the planet surface"),
            (0.45f, "Assembling ships"),
            (0.7f, "Connecting systems"),
            (0.88f, "Preparing your arrival"),
        };

        private RectTransform _fill;
        private TMP_Text _stage, _percent;
        private float _start, _shown;

        internal void Init(RectTransform fill, TMP_Text stage, TMP_Text percent)
        {
            _fill = fill;
            _stage = stage;
            _percent = percent;
            _start = Time.realtimeSinceStartup;
        }

        private void Update()
        {
            float expected = Mathf.Max(3f, CoreMod.LastLoadSeconds.Value);
            float elapsed = Time.realtimeSinceStartup - _start;
            float target = 0.95f * (1f - Mathf.Exp(-2.6f * elapsed / expected));
            _shown = Mathf.MoveTowards(_shown, target, Time.unscaledDeltaTime * 0.5f);
            _fill.anchorMax = new Vector2(_shown, 1f);
            _percent.text = $"{Mathf.FloorToInt(_shown * 100f)}%";
            _stage.text = Stages.Last(s => _shown >= s.at).text;
        }

        private void OnDisable()
        {
            float elapsed = Time.realtimeSinceStartup - _start;
            if (elapsed > 1f && elapsed < 600f)
            {
                CoreMod.LastLoadSeconds.Value = elapsed;
                MelonPreferences.Save();
            }
        }
    }
}
