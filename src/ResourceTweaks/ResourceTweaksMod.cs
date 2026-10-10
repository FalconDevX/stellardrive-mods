using MelonLoader;
using ResourceTweaks;

[assembly: MelonInfo(typeof(ResourceTweaksMod), "Resource Tweaks", "0.2.0", "mateu")]
[assembly: MelonGame("CuriousOwlGames", "StellarDrive")]

namespace ResourceTweaks
{
    public class ResourceTweaksMod : MelonMod
    {
        internal static MelonPreferences_Entry<float> DropMultiplier;
        internal static MelonPreferences_Entry<int> MaxStackSize;
        internal static MelonPreferences_Entry<bool> LogDrops;

        public override void OnInitializeMelon()
        {
            var category = MelonPreferences.CreateCategory("ResourceTweaks");
            DropMultiplier = category.CreateEntry("DropMultiplier", 3f,
                description: "Mnożnik wszystkich surowców wydobywanych na planetach (1 = bez zmian)");
            MaxStackSize = category.CreateEntry("MaxStackSize", 100,
                description: "Maksymalny stack przedmiotów, które da się stackować (1-255). Narzędzia (stack 1) bez zmian");
            LogDrops = category.CreateEntry("LogDrops", true,
                description: "Wypisuje każdy drop do MelonLoader\\Latest.log (do sprawdzania, wyłącz po testach)");
            // Stary wpis z wersji 0.1.0 (tylko żelazo) - już nieużywany.
            category.DeleteEntry("IronDropMultiplier");
            // Zapis od razu, żeby wpisy pojawiły się w UserData\MelonPreferences.cfg bez wychodzenia z gry.
            category.SaveToFile(false);

            LoggerInstance.Msg($"Surowce x{DropMultiplier.Value}, stack do {MaxStackSize.Value}");
        }
    }
}
