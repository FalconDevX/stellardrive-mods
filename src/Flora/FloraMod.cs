using Flora;
using MelonLoader;

[assembly: MelonInfo(typeof(FloraMod), "Flora", "0.4.0", "mateu")]
[assembly: MelonGame("CuriousOwlGames", "StellarDrive")]

namespace Flora
{
    public class FloraMod : MelonMod
    {
        internal static MelonPreferences_Entry<bool> DumpEnvironment;
        internal static MelonPreferences_Entry<bool> ScanPlanetResources;
        internal static MelonPreferences_Entry<bool> FlowersEnabled;
        internal static MelonPreferences_Entry<float> FlowerNoiseOffset;
        internal static MelonPreferences_Entry<float> FlowerViewRange;
        internal static MelonPreferences_Entry<float> FlowerNoiseFrequency;

        public override void OnInitializeMelon()
        {
            var category = MelonPreferences.CreateCategory("Flora");
            FlowersEnabled = category.CreateEntry("FlowersEnabled", true, description: "Pola kwiatów");
            FlowerNoiseOffset = category.CreateEntry("FlowerNoiseOffset", -0.45f,
                description: "Wielkość pól kwiatów: więcej = większe pola (np. 0), mniej = mniejsze i rzadsze (np. -0.7)");
            FlowerViewRange = category.CreateEntry("FlowerViewDistance", 50f,
                description: "Z jakiej odległości widać kwiaty (m). Dalej znikają jak trawa (trawa w grze: 100 m)");
            // Stare wpisy: z wersji 0.1.0 (z innymi domyślnymi wartościami) i krzaki usunięte w 0.4.0.
            foreach (string old in new[] { "BushViewRange", "FlowerViewRange", "BushesEnabled", "BushNoiseOffset", "BushViewDistance", "BushNoiseFrequency" })
            {
                category.DeleteEntry(old);
            }
            // Dla porównania w grze: las ma częstotliwość 100 (duże plamy), trawa 3200 (drobne plamki).
            FlowerNoiseFrequency = category.CreateEntry("FlowerNoiseFrequency", 600f,
                description: "Wielkość pól kwiatów: mniej = większe pola (trawa w grze ma 3200)");
            DumpEnvironment = category.CreateEntry("DumpEnvironment", false,
                description: "Wypisuje do UserData\\Flora\\ spis roślinności planet (diagnostyka)");
            ScanPlanetResources = category.CreateEntry("ScanPlanetResources", true,
                description: "Po wczytaniu świata sprawdza, gdzie na planetach mogą pojawić się złoża (plik UserData/Flora/planet_resources.txt)");
            category.SaveToFile(false);

            LoggerInstance.Msg("Zmiany gęstości działają po ponownym uruchomieniu gry.");
        }

        public override void OnUpdate()
        {
            Diagnostics.FloraDebugTool.Update();
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            if (ScanPlanetResources.Value && sceneName == "MainGame")
            {
                MelonCoroutines.Start(Diagnostics.PlanetResourceScan.ScanAfterDelay(20f));
            }
            if (DumpEnvironment.Value && sceneName == "MainGame")
            {
                MelonCoroutines.Start(Diagnostics.WorldEnvironmentDump.DumpAfterDelay(15f));
            }
        }
    }
}
