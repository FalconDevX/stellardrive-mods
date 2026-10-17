using Flora;
using MelonLoader;

[assembly: MelonInfo(typeof(FloraMod), "Flora", "0.5.0", "mateu")]
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
        internal static MelonPreferences_Entry<bool> ForestPropsEnabled;
        internal static MelonPreferences_Entry<float> ForestPropsDensity;
        internal static MelonPreferences_Entry<float> ForestPropsViewRange;
        internal static MelonPreferences_Entry<bool> UndergrowthEnabled;
        internal static MelonPreferences_Entry<float> UndergrowthDensity;
        internal static MelonPreferences_Entry<float> UndergrowthViewRange;

        public override void OnInitializeMelon()
        {
            var category = MelonPreferences.CreateCategory("Flora");
            FlowersEnabled = category.CreateEntry("FlowersEnabled", true, description: "Flower fields");
            FlowerNoiseOffset = category.CreateEntry("FlowerNoiseOffset", -0.45f,
                description: "Flower field size: more = bigger fields (e.g. 0), less = smaller and sparser (e.g. -0.7)");
            FlowerViewRange = category.CreateEntry("FlowerViewDistance", 50f,
                description: "Distance at which flowers are visible (m). Further away they fade like grass (game grass: 100 m)");
            foreach (string old in new[] { "BushViewRange", "FlowerViewRange", "BushesEnabled", "BushNoiseOffset", "BushViewDistance", "BushNoiseFrequency" })
            {
                category.DeleteEntry(old);
            }
            FlowerNoiseFrequency = category.CreateEntry("FlowerNoiseFrequency", 600f,
                description: "Flower field size: less = bigger fields (game grass uses 3200)");
            ForestPropsEnabled = category.CreateEntry("ForestPropsEnabled", true,
                description: "Bushes, fallen logs, stumps and rocks in forests");
            ForestPropsDensity = category.CreateEntry("ForestPropsDensity", 0f,
                description: "Added to the forest noise: more = props also at forest edges (e.g. 0.5), less = only deep in forests (e.g. -1)");
            ForestPropsViewRange = category.CreateEntry("ForestPropsViewDistance", 250f,
                description: "Distance at which bushes, logs, stumps and rocks are visible (m)");
            UndergrowthEnabled = category.CreateEntry("UndergrowthEnabled", true,
                description: "Ferns, tall grass, branches, mushrooms, pebbles and bell flowers on the forest floor");
            UndergrowthDensity = category.CreateEntry("UndergrowthDensity", -0.2f,
                description: "Added to the forest noise: more = undergrowth also at forest edges (e.g. 0.5), less = only deep in forests (e.g. -1)");
            UndergrowthViewRange = category.CreateEntry("UndergrowthViewDistance", 70f,
                description: "Distance at which the forest undergrowth is visible (m)");
            DumpEnvironment = category.CreateEntry("DumpEnvironment", false,
                description: "Writes a dump of planet vegetation to UserData\\Flora\\ (diagnostics)");
            ScanPlanetResources = category.CreateEntry("ScanPlanetResources", true,
                description: "After a world loads, checks where deposits can appear on planets (file UserData/Flora/planet_resources.txt)");
            category.SaveToFile(false);

            LoggerInstance.Msg("Density changes take effect after restarting the game.");
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
