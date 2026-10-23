using System;
using System.Collections.Generic;
using MelonLoader;
using Odyssey;
using UnityEngine;

[assembly: MelonInfo(typeof(CoreMod), "Odyssey Core", "1.2.5", "mateu")]
[assembly: MelonGame("CuriousOwlGames", "StellarDrive")]
[assembly: MelonPriority(-1000)]

namespace Odyssey
{
    public class CoreMod : MelonMod
    {
        internal static MelonPreferences_Entry<float> RoomUpdateDelay;
        internal static MelonPreferences_Entry<float> PartSyncPerSecond;
        internal static MelonPreferences_Entry<float> LastLoadSeconds;

        private static string _message;
        private static float _messageUntil;
        private static readonly HashSet<string> Failed = new HashSet<string>();

        public override void OnInitializeMelon()
        {
            Diagnostics.FreezeWatchdog.Start();
            var category = Settings.Category("OdysseyCore", "Odyssey Core");
            RoomUpdateDelay = Settings.Entry(category, "RoomUpdateDelay", 1.5f, "While you keep building, rooms and oxygen are recalculated only after this many seconds without a hull change (at most 8x this long; the visual copy on each player waits 2.5x longer), instead of after every part. Big bases otherwise freeze for a moment on every placed part. 0 = after every part like the game");
            PartSyncPerSecond = Settings.Entry(category, "PartSyncPerSecond", 10f, "Host only: how many times per second a part that keeps changing (tanks, reactors, pumps...) is sent to players. The first change is sent at once, faster ones are merged. Less = more FPS on big bases, 0 = like the game (every physics step)");
            LastLoadSeconds = category.CreateEntry("LastLoadSeconds", 20f, description: "How long the last world took to load (s); the loading bar is paced by it. Updated automatically");
            category.SaveToFile(false);
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            Server.ShipPartIndex.Reset();
            ServiceCache.Reset();
        }

        public override void OnUpdate()
        {
            Diagnostics.FreezeWatchdog.Beat();
        }

        internal static void Run(MelonLogger.Instance log, string name, Action action)
        {
            if (Failed.Contains(name))
            {
                return;
            }
            Diagnostics.FreezeWatchdog.Enter("Odyssey " + name);
            try
            {
                action();
            }
            catch (Exception e)
            {
                Failed.Add(name);
                log.Error($"{name} disabled for this session: {e}");
            }
            finally
            {
                Diagnostics.FreezeWatchdog.Exit();
            }
        }

        internal static bool KeyPressed(UnityEngine.InputSystem.Controls.KeyControl key) =>
            key != null && key.wasPressedThisFrame && !Diagnostics.DevTools.IsTyping();

        internal static void ShowMessage(string text, float seconds = 6f)
        {
            _message = text;
            _messageUntil = Time.unscaledTime + seconds;
            Melon<CoreMod>.Logger.Msg(text);
        }

        public override void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || _message == null || Time.unscaledTime > _messageUntil)
            {
                return;
            }
            DrawLabel(_message, Screen.height * 0.18f);
        }

        private static GUIStyle _labelStyle, _shadowStyle;

        private static void DrawLabel(string text, float y)
        {
            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter };
                _labelStyle.normal.textColor = Color.white;
                _shadowStyle = new GUIStyle(_labelStyle);
                _shadowStyle.normal.textColor = new Color(0, 0, 0, 0.8f);
            }
            var rect = new Rect(0, y, Screen.width, 40);
            GUI.Label(new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height), text, _shadowStyle);
            GUI.Label(rect, text, _labelStyle);
        }
    }

    internal static class Settings
    {
        private const string LegacyCategory = "Mining";
        private static MelonPreferences_Category _legacy;

        internal static MelonPreferences_Category Category(string id, string name) => MelonPreferences.CreateCategory(id, name);

        internal static MelonPreferences_Entry<T> Entry<T>(MelonPreferences_Category category, string id, T value, string description, string legacyId = null)
        {
            MelonPreferences_Entry<T> entry = category.CreateEntry(id, value, description: description);
            _legacy = _legacy ?? MelonPreferences.CreateCategory(LegacyCategory);
            string oldId = legacyId ?? id;
            MelonPreferences_Entry<T> old = _legacy.CreateEntry(oldId, value);
            if (!EqualityComparer<T>.Default.Equals(old.Value, value))
            {
                entry.Value = old.Value;
            }
            _legacy.DeleteEntry(oldId);
            return entry;
        }
    }
}
