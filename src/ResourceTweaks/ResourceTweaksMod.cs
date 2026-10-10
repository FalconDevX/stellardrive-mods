using MelonLoader;
using ResourceTweaks;

[assembly: MelonInfo(typeof(ResourceTweaksMod), "Resource Tweaks", "0.1.0", "mateu")]
[assembly: MelonGame("CuriousOwlGames", "StellarDrive")]

namespace ResourceTweaks
{
    public class ResourceTweaksMod : MelonMod
    {
        internal static MelonPreferences_Entry<float> IronDropMultiplier;
        internal static MelonPreferences_Entry<int> MaxStackSize;

        public override void OnInitializeMelon()
        {
            var category = MelonPreferences.CreateCategory("ResourceTweaks");
            IronDropMultiplier = category.CreateEntry("IronDropMultiplier", 3f,
                description: "Mnożnik żelaza z wydobycia i recyklingu obiektów na planetach (1 = bez zmian)");
            MaxStackSize = category.CreateEntry("MaxStackSize", 100,
                description: "Maksymalny stack przedmiotów, które da się stackować (1-255). Narzędzia (stack 1) bez zmian");
            // Zapis od razu, żeby wpisy pojawiły się w UserData\MelonPreferences.cfg bez wychodzenia z gry.
            category.SaveToFile(false);

            LoggerInstance.Msg($"Żelazo x{IronDropMultiplier.Value}, stack do {MaxStackSize.Value}");
        }
    }
}
