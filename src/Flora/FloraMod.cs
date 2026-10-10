using Flora;
using MelonLoader;

[assembly: MelonInfo(typeof(FloraMod), "Flora", "0.1.0", "mateu")]
[assembly: MelonGame("CuriousOwlGames", "StellarDrive")]

namespace Flora
{
    public class FloraMod : MelonMod
    {
        internal static MelonPreferences_Entry<bool> DumpEnvironment;
        internal static MelonPreferences_Entry<bool> BushesEnabled;
        internal static MelonPreferences_Entry<float> BushNoiseOffset;
        internal static MelonPreferences_Entry<float> BushViewRange;
        internal static MelonPreferences_Entry<bool> FlowersEnabled;
        internal static MelonPreferences_Entry<float> FlowerNoiseOffset;
        internal static MelonPreferences_Entry<float> FlowerViewRange;

        public override void OnInitializeMelon()
        {
            var category = MelonPreferences.CreateCategory("Flora");
            BushesEnabled = category.CreateEntry("BushesEnabled", true, description: "Nowe krzaki na planetach");
            BushNoiseOffset = category.CreateEntry("BushNoiseOffset", -0.35f,
                description: "Gęstość krzaków: więcej = gęściej (np. 0), mniej = rzadziej i w mniejszych kępach (np. -0.6)");
            BushViewRange = category.CreateEntry("BushViewRange", 400f, description: "Z jakiej odległości widać krzaki (m)");
            FlowersEnabled = category.CreateEntry("FlowersEnabled", true, description: "Pola kwiatów");
            FlowerNoiseOffset = category.CreateEntry("FlowerNoiseOffset", -0.45f,
                description: "Wielkość pól kwiatów: więcej = większe pola (np. 0), mniej = mniejsze i rzadsze (np. -0.7)");
            FlowerViewRange = category.CreateEntry("FlowerViewRange", 120f, description: "Z jakiej odległości widać kwiaty (m)");
            DumpEnvironment = category.CreateEntry("DumpEnvironment", false,
                description: "Wypisuje do UserData\\Flora\\ spis roślinności planet (diagnostyka)");
            category.SaveToFile(false);

            LoggerInstance.Msg("Zmiany gęstości działają po ponownym uruchomieniu gry.");
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            if (DumpEnvironment.Value && sceneName == "MainGame")
            {
                MelonCoroutines.Start(Diagnostics.WorldEnvironmentDump.DumpAfterDelay(15f));
            }
        }
    }
}
