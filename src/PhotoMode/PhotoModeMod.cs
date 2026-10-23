using System;
using System.Collections.Generic;
using System.IO;
using FishNet;
using FishNet.Managing.Timing;
using HarmonyLib;
using MelonLoader;
using MelonLoader.Utils;
using PhotoMode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[assembly: MelonInfo(typeof(PhotoModeMod), "Photo Mode", "0.1.0", "mateu")]
[assembly: MelonGame("CuriousOwlGames", "StellarDrive")]

namespace PhotoMode
{
    public class PhotoModeMod : MelonMod
    {
        internal static MelonPreferences_Entry<Key> ToggleKey;
        internal static MelonPreferences_Entry<bool> FreezeTime;
        internal static MelonPreferences_Entry<int> SuperSize;
        internal static MelonPreferences_Entry<float> MouseSensitivity;
        internal static bool Frozen;

        private static readonly List<Canvas> Hidden = new List<Canvas>();
        private static readonly List<(DepthOfField effect, bool active)> Focus = new List<(DepthOfField, bool)>();
        private bool _active;
        private Camera _camera;
        private Vector3 _pivot;
        private Quaternion _basis = Quaternion.identity;
        private bool _capturing;
        private MelonPreferences_Entry<bool> _fpsCounter;
        private bool _fpsShown;
        private float _yaw, _pitch, _roll, _distance, _fov, _gameFov;
        private float _timeScale = 1f;
        private bool _help = true;
        private string _message;
        private float _messageUntil;
        private GUIStyle _style, _shadow;

        public override void OnInitializeMelon()
        {
            var category = MelonPreferences.CreateCategory("PhotoMode", "Photo Mode");
            ToggleKey = category.CreateEntry("ToggleKey", Key.P, description: "Key that opens and closes the photo mode");
            FreezeTime = category.CreateEntry("FreezeTime", true, description: "Stop time in the photo mode (only when no other player is connected, so the world never stops for others)");
            SuperSize = category.CreateEntry("SuperSize", 2, description: "Photos are saved at this many times the screen resolution (1-4)");
            MouseSensitivity = category.CreateEntry("MouseSensitivity", 0.15f, description: "How fast the mouse turns the camera around the scene");
            category.SaveToFile(false);
            RenderPipelineManager.beginCameraRendering += BeforeRender;
        }

        public override void OnUpdate()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null)
            {
                return;
            }
            if (kb[ToggleKey.Value].wasPressedThisFrame || (_active && kb.escapeKey.wasPressedThisFrame))
            {
                if (_active) Exit();
                else Enter();
                return;
            }
            if (!_active)
            {
                return;
            }
            if (_camera == null)
            {
                Exit();
                return;
            }
            HideHud();
            Steer(kb, Mouse.current);
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.f12Key.wasPressedThisFrame)
            {
                MelonCoroutines.Start(Capture());
            }
            if (kb.hKey.wasPressedThisFrame)
            {
                _help = !_help;
            }
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            if (_active) Exit();
        }

        private void Enter()
        {
            _camera = Camera.main;
            if (_camera == null)
            {
                return;
            }
            Transform t = _camera.transform;
            _distance = Physics.Raycast(t.position, t.forward, out RaycastHit hit, 300f, ~0, QueryTriggerInteraction.Ignore) ? Mathf.Max(3f, hit.distance) : 15f;
            _pivot = t.position + t.forward * _distance;
            Vector3 up = t.up;
            Vector3 flat = Vector3.ProjectOnPlane(t.forward, up);
            _basis = Quaternion.LookRotation(flat.sqrMagnitude > 0.0001f ? flat.normalized : t.forward, up);
            _yaw = 0f;
            _pitch = Mathf.Clamp(Vector3.SignedAngle(flat, t.forward, Vector3.Cross(up, flat)), -89f, 89f);
            _roll = 0f;
            Focus.Clear();
            foreach (Volume volume in UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsSortMode.None))
            {
                if (volume.sharedProfile != null && volume.sharedProfile.TryGet(out DepthOfField dof))
                {
                    Focus.Add((dof, dof.active));
                    dof.active = false;
                }
            }
            _gameFov = _camera.fieldOfView;
            _fov = _gameFov;
            _active = true;
            bool alone = !InstanceFinder.IsClientStarted || (InstanceFinder.IsServerStarted && InstanceFinder.ServerManager.Clients.Count <= 1);
            Frozen = FreezeTime.Value && alone;
            if (Frozen)
            {
                _timeScale = Time.timeScale;
                Time.timeScale = 0f;
            }
            Show(Frozen ? "Photo mode - time stopped" : FreezeTime.Value ? "Photo mode - time keeps running because other players are connected" : "Photo mode");
            LoggerInstance.Msg($"Photo mode on (time {(Frozen ? "stopped" : "running")})");
        }

        private void Exit()
        {
            _active = false;
            if (Frozen)
            {
                Time.timeScale = _timeScale;
                Frozen = false;
            }
            if (_camera != null)
            {
                _camera.fieldOfView = _gameFov;
            }
            foreach (Canvas c in Hidden)
            {
                if (c != null) c.enabled = true;
            }
            Hidden.Clear();
            foreach (var (effect, active) in Focus)
            {
                if (effect != null) effect.active = active;
            }
            Focus.Clear();
            LoggerInstance.Msg("Photo mode off");
        }

        private static void HideHud()
        {
            foreach (Canvas c in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (c.enabled && c.renderMode != RenderMode.WorldSpace)
                {
                    c.enabled = false;
                    Hidden.Add(c);
                }
            }
        }

        private void Steer(Keyboard kb, Mouse mouse)
        {
            float dt = Time.unscaledDeltaTime;
            if (mouse != null)
            {
                Vector2 delta = mouse.delta.ReadValue() * MouseSensitivity.Value;
                _yaw += delta.x;
                _pitch = Mathf.Clamp(_pitch - delta.y, -89f, 89f);
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    _distance = Mathf.Clamp(_distance * Mathf.Pow(0.9f, Mathf.Sign(scroll)), 0.5f, 5000f);
                }
            }
            Quaternion look = _basis * Quaternion.Euler(_pitch, _yaw, 0f);
            float speed = Mathf.Max(2f, _distance * 0.6f) * (kb.leftShiftKey.isPressed ? 4f : 1f) * dt;
            Vector3 move = Vector3.zero;
            if (kb.wKey.isPressed) move += look * Vector3.forward;
            if (kb.sKey.isPressed) move -= look * Vector3.forward;
            if (kb.dKey.isPressed) move += look * Vector3.right;
            if (kb.aKey.isPressed) move -= look * Vector3.right;
            if (kb.eKey.isPressed) move += _basis * Vector3.up;
            if (kb.qKey.isPressed) move -= _basis * Vector3.up;
            _pivot += move * speed;
            if (kb.zKey.isPressed) _fov = Mathf.Clamp(_fov - 25f * dt, 5f, 120f);
            if (kb.xKey.isPressed) _fov = Mathf.Clamp(_fov + 25f * dt, 5f, 120f);
            if (kb.cKey.isPressed) _roll += 30f * dt;
            if (kb.vKey.isPressed) _roll -= 30f * dt;
            if (kb.rKey.wasPressedThisFrame) { _roll = 0f; _fov = _gameFov; }
        }

        private void BeforeRender(ScriptableRenderContext context, Camera camera)
        {
            if (!_active || camera == null || camera != _camera)
            {
                return;
            }
            Quaternion look = _basis * Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 position = _pivot - look * Vector3.forward * _distance;
            camera.transform.SetPositionAndRotation(position, look * Quaternion.Euler(0f, 0f, _roll));
            camera.fieldOfView = _fov;
        }

        private System.Collections.IEnumerator Capture()
        {
            _capturing = true;
            _fpsCounter = _fpsCounter ?? MelonPreferences.GetEntry<bool>("FpsCounter", "ShowFps");
            _fpsShown = _fpsCounter != null && _fpsCounter.Value;
            if (_fpsCounter != null) _fpsCounter.Value = false;
            yield return null;
            yield return null;
            string folder = Path.Combine(MelonEnvironment.UserDataDirectory, "Photos");
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, $"Odyssey_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png");
            ScreenCapture.CaptureScreenshot(path, Mathf.Clamp(SuperSize.Value, 1, 4));
            yield return null;
            yield return null;
            yield return null;
            _capturing = false;
            if (_fpsCounter != null) _fpsCounter.Value = _fpsShown;
            Show($"Photo saved: UserData/Photos/{Path.GetFileName(path)}");
            LoggerInstance.Msg($"Photo saved: {path}");
        }

        private void Show(string text)
        {
            _message = text;
            _messageUntil = Time.unscaledTime + 4f;
        }

        public override void OnGUI()
        {
            if (_capturing)
            {
                return;
            }
            if (!_active && Time.unscaledTime > _messageUntil)
            {
                return;
            }
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = 18, richText = true };
                _style.normal.textColor = Color.white;
                _shadow = new GUIStyle(_style);
                _shadow.normal.textColor = new Color(0f, 0f, 0f, 0.8f);
            }
            float y = 20f;
            if (Time.unscaledTime <= _messageUntil && !string.IsNullOrEmpty(_message))
            {
                Label(new Rect(20f, y, 1400f, 28f), _message);
                y += 34f;
            }
            if (_active && _help)
            {
                string help = $"{ToggleKey.Value} / Esc - close    Mouse - turn around    Wheel - closer / farther    WASD, Q / E - move (Shift faster)\n" +
                              $"Z / X - zoom ({_fov:0} degrees)    C / V - tilt    R - reset    Enter / F12 - take a photo    H - hide this help";
                Label(new Rect(20f, y, 1600f, 60f), help);
            }
        }

        private void Label(Rect rect, string text)
        {
            GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), text, _shadow);
            GUI.Label(rect, text, _style);
        }
    }

    [HarmonyPatch(typeof(TimeManager), "TickUpdate")]
    internal static class FreezeTicksPatch
    {
        private static bool Prefix() => !PhotoModeMod.Frozen;
    }
}
