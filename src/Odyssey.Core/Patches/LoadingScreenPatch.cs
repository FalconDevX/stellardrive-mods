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
        private static int _picture = -1;

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
            BuildContent(root, early: false);
            EarlyLoadingScreenPatch.Hide();
            Melon<CoreMod>.Logger.Msg($"Loading screen: {root.name} ({string.Join(", ", root.Cast<Transform>().Select(t => t.name))})");
        }

        internal static void BuildContent(RectTransform root, bool early)
        {
            _textures = _textures ?? Images.Select(n => ModAssets.LoadTexture(n)).ToArray();
            if (_picture < 0)
            {
                _picture = UnityEngine.Random.Range(0, _textures.Length);
            }
            var layer = Rect("OdysseyLoading", root);
            Stretch(layer, Vector2.zero, Vector2.one);
            Transform background = root.Find(early ? "Background" : "Image");
            layer.SetSiblingIndex(background != null ? background.GetSiblingIndex() + 1 : 0);
            Transform spinner = root.Find("Loading");
            if (spinner != null) spinner.gameObject.SetActive(false);

            var picture = layer.gameObject.AddComponent<RawImage>();
            picture.texture = _textures[_picture % _textures.Length];
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
            Label(hud, "Title", font, "ODYSSEY", 46f, new Vector2(0.06f, 0.155f), TextAlignmentOptions.BottomLeft, Accent, 14f);
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

            TMP_Text tip = Label(hud, "Tip", font, "", 24f, new Vector2(0.94f, 0.155f), TextAlignmentOptions.BottomRight, new Color(1f, 1f, 1f, 0.85f), 1f);
            Label(hud, "World", font, WorldName(), 26f, new Vector2(0.94f, 0.9f), TextAlignmentOptions.BottomRight, new Color(1f, 1f, 1f, 0.9f), 4f);
            AddLogo(hud);
            hud.gameObject.AddComponent<LoadingProgress>().Init(fill.rectTransform, stage, percent, tip, early);
        }

        internal static void ResetPicture() => _picture = -1;

        private static string WorldName()
        {
            string file = Managers.Interface.MainGameSceneLoadData.SaveFileFullName;
            string folder = string.IsNullOrEmpty(file) ? null : System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(file));
            return string.IsNullOrEmpty(folder) ? "JOINING A WORLD" : "WORLD  <color=#ED5C52>" + folder.ToUpperInvariant() + "</color>";
        }

        private static void AddLogo(RectTransform hud)
        {
            Texture2D logo = Resources.FindObjectsOfTypeAll<Texture2D>().FirstOrDefault(t => t.name == "sd_white_small");
            var size = new Vector2(630f, 280f) * 0.62f;
            var area = new Vector2(size.x * 1.44f, size.y * 1.72f);
            var orbits = Rect("Orbits", hud).gameObject.AddComponent<RawImage>();
            orbits.rectTransform.anchorMin = orbits.rectTransform.anchorMax = new Vector2(0.04f, 0.95f);
            orbits.rectTransform.pivot = new Vector2(0f, 1f);
            orbits.rectTransform.sizeDelta = area;
            orbits.texture = TitleScreenPatch.DrawOrbits(area, Accent, out Vector2 starAt);
            orbits.raycastTarget = false;
            if (logo != null)
            {
                var image = Rect("Logo", orbits.rectTransform).gameObject.AddComponent<RawImage>();
                image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(0.5f, 0.5f - 0.07f * 0.6f);
                image.rectTransform.sizeDelta = size * 0.82f;
                image.texture = logo;
                image.raycastTarget = false;
            }
            var star = Rect("Star", orbits.rectTransform).gameObject.AddComponent<RawImage>();
            star.rectTransform.anchorMin = star.rectTransform.anchorMax = starAt;
            star.rectTransform.sizeDelta = Vector2.one * size.y * 0.42f;
            star.texture = TitleScreenPatch.DrawStar(128);
            star.color = Accent;
            star.raycastTarget = false;
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

        private static readonly string[] Tips =
        {
            "Press N to find the nearest ore deposit, U for uranium and K for glass.",
            "An excavator mines on its own - give it power and a signal of 1.",
            "Hydrogen thrusters are 1.8x stronger than chemical ones. The electrolyzer makes their fuel from water.",
            "A nuclear reactor needs uranium, water and a signal of 1. The large reactor gives five times more power.",
            "Inflate pontoons with oxygen and your ship floats on the lakes of Otrera b.",
            "Press J to toggle the jetpack and L to hold your altitude. It burns wood from the last backpack slot.",
            "Scan a part of your ship with the Blueprint Tool and press B to open the blueprint catalog.",
            "Press V in a pilot seat for the chase camera. Hold the right mouse button to look around the ship.",
            "The Power Meter shows how loaded your power network is, in percent.",
            "Info panels show any signal as text. Press F on a panel to edit what it says.",
            "Floor hatches turn with R, in 90 degree steps.",
            "Large doors and gates go back to what their signal says 30 seconds after you use them by hand.",
        };

        internal static AsyncOperation Operation;

        private static float _start = -1f;
        private static float _shown;
        private static int _tipIndex = -1;
        private static float _nextTip;

        private RectTransform _fill;
        private TMP_Text _stage, _percent, _tip;
        private bool _early;

        internal static void Begin()
        {
            _start = Time.realtimeSinceStartup;
            _shown = 0f;
            _tipIndex = UnityEngine.Random.Range(0, Tips.Length);
            _nextTip = _start + 6f;
            Operation = null;
        }

        internal void Init(RectTransform fill, TMP_Text stage, TMP_Text percent, TMP_Text tip, bool early)
        {
            if (_start < 0f)
            {
                Begin();
            }
            _early = early;
            _tip = tip;
            _tip.text = Tips[_tipIndex];
            _fill = fill;
            _stage = stage;
            _percent = percent;
            Refresh();
        }

        private void Update()
        {
            float expected = Mathf.Max(3f, CoreMod.LastLoadSeconds.Value);
            float elapsed = Time.realtimeSinceStartup - _start;
            float target = 0.95f * (1f - Mathf.Exp(-2.6f * elapsed / expected));
            if (Operation != null && !Operation.isDone)
            {
                target = Mathf.Max(target, Mathf.Clamp01(Operation.progress / 0.9f) * 0.6f);
            }
            _shown = Mathf.MoveTowards(_shown, target, Time.unscaledDeltaTime * 0.5f);
            if (Time.realtimeSinceStartup >= _nextTip)
            {
                _nextTip = Time.realtimeSinceStartup + 6f;
                _tipIndex = (_tipIndex + 1) % Tips.Length;
            }
            Refresh();
        }

        private void Refresh()
        {
            _fill.anchorMax = new Vector2(_shown, 1f);
            _percent.text = $"{Mathf.FloorToInt(_shown * 100f)}%";
            _stage.text = Stages.Last(s => _shown >= s.at).text;
            _tip.text = Tips[_tipIndex];
        }

        private void OnDisable()
        {
            if (_early || _start < 0f)
            {
                return;
            }
            float elapsed = Time.realtimeSinceStartup - _start;
            if (elapsed > 1f && elapsed < 600f)
            {
                CoreMod.LastLoadSeconds.Value = elapsed;
                MelonPreferences.Save();
            }
            _start = -1f;
            Operation = null;
            LoadingScreenPatch.ResetPicture();
        }
    }
}
