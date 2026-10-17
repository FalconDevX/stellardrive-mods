using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Odyssey.Patches
{
    [HarmonyPatch]
    internal static class EarlyLoadingScreenPatch
    {
        private static readonly MethodInfo LoadScene =
            AccessTools.Method(typeof(SceneManager), nameof(SceneManager.LoadScene), new[] { typeof(string), typeof(LoadSceneMode) });

        private static readonly MethodInfo Replacement = AccessTools.Method(typeof(EarlyLoadingScreenPatch), nameof(Load));

        private static GameObject _overlay;

        private static IEnumerable<MethodBase> TargetMethods()
        {
            return new[]
            {
                "UI.MainMenu.StartGameDetails:StartHost",
                "UI.MainMenu.StartGameDetails:StartSinglePlayer",
                "UI.MainMenu.MenuJoinGame:JoinGame",
                "Managers.Client.Services.SteamJoinGameCallbackListener:Update",
                "Managers.Client.Services.SteamMainMenuQuickJoinGame:Start",
            }.Select(name => (MethodBase)AccessTools.Method(name)).Where(m => m != null);
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.Calls(LoadScene))
                {
                    instruction.operand = Replacement;
                }
                yield return instruction;
            }
        }

        internal static void Load(string scene, LoadSceneMode mode)
        {
            if (scene != "MainGame")
            {
                SceneManager.LoadScene(scene, mode);
                return;
            }
            if (_overlay != null)
            {
                return;
            }
            try
            {
                Show();
            }
            catch (Exception e)
            {
                Melon<CoreMod>.Logger.Error($"Early loading screen: {e}");
                Hide();
                SceneManager.LoadScene(scene, mode);
                return;
            }
            MelonCoroutines.Start(LoadAfterFrames(scene, mode));
        }

        private static IEnumerator LoadAfterFrames(string scene, LoadSceneMode mode)
        {
            yield return null;
            yield return null;
            AsyncOperation operation = SceneManager.LoadSceneAsync(scene, mode);
            if (operation == null)
            {
                Hide();
                SceneManager.LoadScene(scene, mode);
                yield break;
            }
            LoadingProgress.Operation = operation;
        }

        private static void Show()
        {
            _overlay = new GameObject("OdysseyEarlyLoading");
            UnityEngine.Object.DontDestroyOnLoad(_overlay);
            var canvas = _overlay.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;
            var scaler = _overlay.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var blocker = new GameObject("Background", typeof(RectTransform)).AddComponent<Image>();
            blocker.rectTransform.SetParent(_overlay.transform, false);
            blocker.rectTransform.anchorMin = Vector2.zero;
            blocker.rectTransform.anchorMax = Vector2.one;
            blocker.rectTransform.offsetMin = blocker.rectTransform.offsetMax = Vector2.zero;
            blocker.color = Color.black;
            LoadingProgress.Begin();
            LoadingScreenPatch.BuildContent((RectTransform)_overlay.transform, early: true);
            SceneManager.sceneLoaded += OnSceneLoaded;
            Melon<CoreMod>.Logger.Msg("Loading screen shown right after Start; the world loads in the background");
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.buildIndex == 0)
            {
                Hide();
            }
        }

        internal static void Hide()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (_overlay != null)
            {
                UnityEngine.Object.Destroy(_overlay);
            }
            _overlay = null;
        }
    }
}
