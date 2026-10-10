using MelonLoader;
using ResourceTweaks;

[assembly: MelonInfo(typeof(ResourceTweaksMod), "Resource Tweaks", "0.3.1", "mateu")]
[assembly: MelonGame("CuriousOwlGames", "StellarDrive")]

namespace ResourceTweaks
{
    public class ResourceTweaksMod : MelonMod
    {
        internal static MelonPreferences_Entry<float> DropMultiplier;
        internal static MelonPreferences_Entry<float> IronDropMultiplier;
        internal static MelonPreferences_Entry<int> MaxStackSize;
        internal static MelonPreferences_Entry<bool> LogDrops;
        internal static MelonPreferences_Entry<float> TreeMiningTime;
        internal static MelonPreferences_Entry<float> OtherMiningTime;

        public override void OnInitializeMelon()
        {
            var category = MelonPreferences.CreateCategory("ResourceTweaks");
            DropMultiplier = category.CreateEntry("DropMultiplier", 3f,
                description: "Mnożnik wszystkich surowców wydobywanych na planetach (1 = bez zmian)");
            // Nowa nazwa wpisu, żeby nie wczytała się stara wartość 3 z wersji 0.1.0.
            IronDropMultiplier = category.CreateEntry("IronMultiplier", 10f,
                description: "Osobny mnożnik dla żelaza (zastępuje DropMultiplier dla żelaza, 10 = 10x więcej)");
            MaxStackSize = category.CreateEntry("MaxStackSize", 100,
                description: "Maksymalny stack przedmiotów, które da się stackować (1-255). Narzędzia (stack 1) bez zmian");
            TreeMiningTime = category.CreateEntry("TreeMiningTime", 0.5f,
                description: "Czas kopania drzew względem normalnego (0.5 = o 50% krócej, 1 = bez zmian)");
            OtherMiningTime = category.CreateEntry("OtherMiningTime", 0.75f,
                description: "Czas kopania pozostałych surowców względem normalnego (0.75 = o 25% krócej)");
            LogDrops = category.CreateEntry("LogDrops", true,
                description: "Wypisuje każdy drop do MelonLoader\\Latest.log (do sprawdzania, wyłącz po testach)");
            // Zapis od razu, żeby wpisy pojawiły się w UserData\MelonPreferences.cfg bez wychodzenia z gry.
            category.SaveToFile(false);

            LoggerInstance.Msg($"Surowce x{DropMultiplier.Value}, żelazo x{IronDropMultiplier.Value}, stack do {MaxStackSize.Value}");
        }
    }
}
