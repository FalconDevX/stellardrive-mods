using MelonLoader;
using Mining;
using UnityEngine;
using UnityEngine.InputSystem;

[assembly: MelonInfo(typeof(MiningMod), "Mining", "0.4.4", "mateu")]
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
        internal static MelonPreferences_Entry<float> UraniumChance;
        internal static MelonPreferences_Entry<bool> ShowSites;
        internal static MelonPreferences_Entry<float> DrillVolume;
        internal static MelonPreferences_Entry<float> HydrogenThrustMultiplier;
        internal static MelonPreferences_Entry<float> RotorSpeed;
        internal static MelonPreferences_Entry<float> JetpackSecondsPerWood;
        internal static MelonPreferences_Entry<float> JetpackAcceleration;
        internal static MelonPreferences_Entry<float> JetpackMaxSpeed;
        internal static MelonPreferences_Entry<float> JetpackVolume;
        internal static MelonPreferences_Entry<bool> FastRooms;
        internal static MelonPreferences_Entry<float> ReactorPower;
        internal static MelonPreferences_Entry<float> ReactorSecondsPerUranium;
        internal static MelonPreferences_Entry<float> PumpRate;
        internal static MelonPreferences_Entry<float> PumpHoseLength;
        internal static MelonPreferences_Entry<float> PumpVolume;
        internal static MelonPreferences_Entry<string> DevAutoLoadWorld;
        internal static MelonPreferences_Entry<bool> DevDump;
        internal static MelonPreferences_Entry<bool> DevQuitAfterDump;
        internal static MelonPreferences_Entry<bool> DevTest;
        internal static MelonPreferences_Entry<bool> DevPerf;
        internal static MelonPreferences_Entry<bool> DevJetpack;

        private static string _message;
        private static float _messageUntil;

        public override void OnInitializeMelon()
        {
            var category = MelonPreferences.CreateCategory("Mining");
            category.DeleteEntry("SecondsPerItem"); // stara wartość domyślna (4 s) - zastąpiona szybszą
            SecondsPerItem = category.CreateEntry("SecondsPerOre", 1f, description: "Co ile sekund koparka wydobywa 1 sztukę rudy (przy pełnym zasilaniu). Wystarczy u hosta");
            PowerWatts = category.CreateEntry("PowerWatts", 1500f, description: "Pobór prądu koparki (W). Generator na drewno daje 4000 W");
            SiteSpacing = category.CreateEntry("SiteSpacing", 300f,
                description: "Rozmiar komórki siatki złóż (m) - w każdej może być jedno złoże. Mniej = złoża gęściej. Wszyscy gracze muszą mieć to samo");
            SiteChance = category.CreateEntry("SiteChance", 0.5f, description: "Szansa na złoże w komórce (0-1). Wszyscy gracze muszą mieć to samo");
            SiteRadius = category.CreateEntry("SiteRadius", 5f, description: "Promień złoża (m). Wszyscy gracze muszą mieć to samo");
            category.DeleteEntry("UraniumChance"); // stara wartość (0.12) - uranu było za mało
            UraniumChance = category.CreateEntry("UraniumShare", 0.3f, description: "Jaka część złóż na Otrera b to uran (0-1). Wszyscy gracze muszą mieć to samo");
            ShowSites = category.CreateEntry("ShowSites", true, description: "Pokazuj złoża (pola kamieni z rudą)");
            DrillVolume = category.CreateEntry("DrillVolume", 0.8f, description: "Głośność wiertła (0-1)");
            ReactorPower = category.CreateEntry("ReactorPower", 20000f, description: "Moc reaktora (W). Generator na drewno daje 4000 W. Wystarczy u hosta");
            ReactorSecondsPerUranium = category.CreateEntry("ReactorSecondsPerUranium", 120f, description: "Na ile sekund pracy na pełnej mocy starcza 1 uran. Wystarczy u hosta");
            PumpRate = category.CreateEntry("PumpRate", 1f, description: "Ile wody na sekundę daje pompa przy pełnym zasilaniu. Wystarczy u hosta");
            PumpVolume = category.CreateEntry("PumpVolume", 0.7f, description: "Głośność pompy wody i wyciągarki węża (0-1)");
            PumpHoseLength = category.CreateEntry("PumpHoseLength", 20f, description: "Długość węża pompy (m). Wszyscy gracze powinni mieć to samo");
            JetpackSecondsPerWood = category.CreateEntry("JetpackSecondsPerWood", 20f, description: "Ile sekund lotu jetpackiem daje 1 drewno. Wystarczy u hosta");
            JetpackAcceleration = category.CreateEntry("JetpackAcceleration", 6f, description: "Przyspieszenie jetpacka w górę ponad grawitację (m/s^2). Wszyscy gracze muszą mieć to samo");
            JetpackMaxSpeed = category.CreateEntry("JetpackMaxSpeed", 7f, description: "Maksymalna prędkość wznoszenia jetpackiem (m/s). Wszyscy gracze muszą mieć to samo");
            category.DeleteEntry("JetpackVolume"); // stara, za głośna wartość domyślna
            JetpackVolume = category.CreateEntry("JetpackFireVolume", 0.45f, description: "Głośność ognia jetpacka (0-1)");
            FastRooms = category.CreateEntry("FastRooms", true, description: "Szybkie przeliczanie pomieszczeń/tlenu po zmianie statku (mniej lagów przy budowaniu). false = jak w grze. Liczy host");
            RotorSpeed = category.CreateEntry("RotorSpeed", 90f, description: "Prędkość obrotu rotora (stopnie na sekundę). Wystarczy u hosta");
            HydrogenThrustMultiplier = category.CreateEntry("HydrogenThrustMultiplier", 1.8f, description: "Ile razy silnik wodorowy jest mocniejszy od chemicznego. Wystarczy u hosta");
            DevAutoLoadWorld = category.CreateEntry("DevAutoLoadWorld", "",
                description: "Dla twórcy moda: pełna ścieżka do world.json wczytywanego automatycznie z menu (puste = wyłączone)");
            DevDump = category.CreateEntry("DevDump", false, description: "Dla twórcy moda: zrzut części i materiałów do UserData/Mining/dump.txt");
            DevQuitAfterDump = category.CreateEntry("DevQuitAfterDump", false, description: "Dla twórcy moda: zamknij grę po zrzucie");
            DevTest = category.CreateEntry("DevTest", false, description: "Dla twórcy moda: podgląd koparki i złoża przed kamerą po wczytaniu świata");
            DevJetpack = category.CreateEntry("DevJetpack", false, description: "Dla twórcy moda: test lotu jetpackiem");
            DevPerf = category.CreateEntry("DevPerf", false, description: "Dla twórcy moda: pomiar lagów przy szybkim stawianiu i usuwaniu części");
            category.SaveToFile(false);
            LoggerInstance.Msg("Press N: nearest ore deposit, U: nearest uranium deposit, J: jetpack on/off.");
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            Sites.MiningSites.Reset();
            Sites.MiningSiteVisuals.Clear();
            Server.ExcavatorServer.Reset();
            Server.WaterPumpServer.Reset();
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
                if (kb != null && kb.uKey.wasPressedThisFrame && !Diagnostics.DevTools.IsTyping())
                {
                    ShowMessage(Sites.MiningSiteVisuals.DescribeNearest(uraniumOnly: true));
                }
                if (kb != null && kb.jKey.wasPressedThisFrame && !Diagnostics.DevTools.IsTyping() && Content.Jetpack.IsUnlocked())
                {
                    Content.Jetpack.Enabled = !Content.Jetpack.Enabled;
                    ShowMessage(Content.Jetpack.Enabled ? "Jetpack ON" : "Jetpack OFF", 1.5f);
                }
            }
            catch (System.Exception e)
            {
                LoggerInstance.Error($"Błąd złóż: {e}");
                ShowSites.Value = false;
            }
        }

        internal static void ShowMessage(string text, float seconds = 6f)
        {
            _message = text;
            _messageUntil = Time.unscaledTime + seconds;
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
