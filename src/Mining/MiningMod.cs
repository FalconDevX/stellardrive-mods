using MelonLoader;
using Mining;
using UnityEngine;
using UnityEngine.InputSystem;

[assembly: MelonInfo(typeof(MiningMod), "Mining", "0.1.0", "mateu")]
[assembly: MelonGame("CuriousOwlGames", "StellarDrive")]

namespace Mining
{
    public class MiningMod : MelonMod
    {
        internal static MelonPreferences_Entry<float> SecondsPerItem;
        internal static MelonPreferences_Entry<float> PowerWatts;
        internal static MelonPreferences_Entry<float> SiteSpacing;
        internal static MelonPreferences_Entry<float> SiteChance;
        internal static MelonPreferences_Entry<float> SiteRadius;
        internal static MelonPreferences_Entry<bool> ShowSites;
        internal static MelonPreferences_Entry<float> DrillVolume;
        internal static MelonPreferences_Entry<string> DevAutoLoadWorld;
        internal static MelonPreferences_Entry<bool> DevDump;
        internal static MelonPreferences_Entry<bool> DevQuitAfterDump;
        internal static MelonPreferences_Entry<bool> DevTest;

        private static string _message;
        private static float _messageUntil;

        public override void OnInitializeMelon()
        {
            var category = MelonPreferences.CreateCategory("Mining");
            SecondsPerItem = category.CreateEntry("SecondsPerItem", 4f, description: "Co ile sekund koparka wydobywa 1 sztukę rudy (przy pełnym zasilaniu)");
            PowerWatts = category.CreateEntry("PowerWatts", 1500f, description: "Pobór prądu koparki (W). Generator na drewno daje 4000 W");
            SiteSpacing = category.CreateEntry("SiteSpacing", 300f,
                description: "Rozmiar komórki siatki złóż (m) - w każdej może być jedno złoże. Mniej = złoża gęściej. Wszyscy gracze muszą mieć to samo");
            SiteChance = category.CreateEntry("SiteChance", 0.5f, description: "Szansa na złoże w komórce (0-1). Wszyscy gracze muszą mieć to samo");
            SiteRadius = category.CreateEntry("SiteRadius", 5f, description: "Promień złoża (m). Wszyscy gracze muszą mieć to samo");
            ShowSites = category.CreateEntry("ShowSites", true, description: "Pokazuj złoża (pola kamieni z rudą)");
            DrillVolume = category.CreateEntry("DrillVolume", 0.8f, description: "Głośność wiertła (0-1)");
            DevAutoLoadWorld = category.CreateEntry("DevAutoLoadWorld", "",
                description: "Dla twórcy moda: pełna ścieżka do world.json wczytywanego automatycznie z menu (puste = wyłączone)");
            DevDump = category.CreateEntry("DevDump", false, description: "Dla twórcy moda: zrzut części i materiałów do UserData/Mining/dump.txt");
            DevQuitAfterDump = category.CreateEntry("DevQuitAfterDump", false, description: "Dla twórcy moda: zamknij grę po zrzucie");
            DevTest = category.CreateEntry("DevTest", false, description: "Dla twórcy moda: podgląd koparki i złoża przed kamerą po wczytaniu świata");
            category.SaveToFile(false);
            LoggerInstance.Msg("Press N: direction to the nearest ore deposit.");
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            Sites.MiningSites.Reset();
            Sites.MiningSiteVisuals.Clear();
            Server.ExcavatorServer.Reset();
            Diagnostics.DevTools.OnSceneLoaded(sceneName);
        }

        public override void OnUpdate()
        {
            try
            {
                Sites.MiningSiteVisuals.Update();
                Keyboard kb = Keyboard.current;
                if (kb != null && kb.nKey.wasPressedThisFrame && !Diagnostics.DevTools.IsTyping())
                {
                    ShowMessage(Sites.MiningSiteVisuals.DescribeNearest());
                }
            }
            catch (System.Exception e)
            {
                LoggerInstance.Error($"Błąd złóż: {e}");
                ShowSites.Value = false;
            }
        }

        internal static void ShowMessage(string text)
        {
            _message = text;
            _messageUntil = Time.unscaledTime + 6f;
            Melon<MiningMod>.Logger.Msg(text);
        }

        public override void OnGUI()
        {
            // Powód, dla którego koparka stoi - nad jej otwartym oknem (tylko u hosta - to serwer liczy wydobycie).
            if (Patches.StorageMenuPatch.TryGetOpenExcavator(out uint shipId, out ushort partId) && Server.ExcavatorServer.TryGetStatus(shipId, partId, out string status) &&
                !status.StartsWith("working"))
            {
                DrawLabel("Excavator: " + status, Screen.height * 0.1f);
            }
            if (_message == null || Time.unscaledTime > _messageUntil)
            {
                return;
            }
            DrawLabel(_message, Screen.height * 0.18f);
        }

        private static void DrawLabel(string text, float y)
        {
            string _message = text;
            var style = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter };
            style.normal.textColor = Color.white;
            var rect = new Rect(0, y, Screen.width, 40);
            var shadow = new GUIStyle(style);
            shadow.normal.textColor = new Color(0, 0, 0, 0.8f);
            GUI.Label(new Rect(rect.x + 2, rect.y + 2, rect.width, rect.height), _message, shadow);
            GUI.Label(rect, _message, style);
        }
    }
}
