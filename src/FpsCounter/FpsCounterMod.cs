using FpsCounter;
using MelonLoader;
using UnityEngine;
using UnityEngine.InputSystem;

[assembly: MelonInfo(typeof(FpsCounterMod), "FPS Counter", "0.1.0", "mateu")]
[assembly: MelonGame("CuriousOwlGames", "StellarDrive")]

namespace FpsCounter
{
    public class FpsCounterMod : MelonMod
    {
        private MelonPreferences_Category _category;
        private MelonPreferences_Entry<bool> _show;
        private MelonPreferences_Entry<Key> _toggleKey;

        private int _frames;
        private float _elapsed;
        private float _worstFrame;
        private string _text = "";
        private GUIStyle _style, _shadow;

        public override void OnInitializeMelon()
        {
            _category = MelonPreferences.CreateCategory("FpsCounter");
            _show = _category.CreateEntry("ShowFps", true, description: "Show FPS in the top right corner of the screen");
            _toggleKey = _category.CreateEntry("ToggleKey", Key.F3, description: "Key that toggles the FPS counter");
            _category.SaveToFile(false);
        }

        public override void OnUpdate()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null && kb[_toggleKey.Value].wasPressedThisFrame)
            {
                _show.Value = !_show.Value;
                _category.SaveToFile(false);
            }

            _frames++;
            _elapsed += Time.unscaledDeltaTime;
            _worstFrame = Mathf.Max(_worstFrame, Time.unscaledDeltaTime);
            if (_elapsed >= 0.5f)
            {
                _text = $"{_frames / _elapsed:0} FPS  ({_worstFrame * 1000f:0} ms max)";
                _frames = 0;
                _elapsed = 0f;
                _worstFrame = 0f;
            }
        }

        public override void OnGUI()
        {
            if (!_show.Value || _text.Length == 0)
            {
                return;
            }
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.UpperRight, fontStyle = FontStyle.Bold };
                _style.normal.textColor = Color.white;
                _shadow = new GUIStyle(_style);
                _shadow.normal.textColor = new Color(0f, 0f, 0f, 0.8f);
            }
            var rect = new Rect(Screen.width - 330, 8, 320, 30);
            GUI.Label(new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height), _text, _shadow);
            GUI.Label(rect, _text, _style);
        }
    }
}
