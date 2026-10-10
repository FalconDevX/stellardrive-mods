using Flora;
using MelonLoader;

[assembly: MelonInfo(typeof(FloraMod), "Flora", "0.0.1", "mateu")]
[assembly: MelonGame("CuriousOwlGames", "StellarDrive")]

namespace Flora
{
    public class FloraMod : MelonMod
    {
        internal static MelonPreferences_Entry<bool> DumpEnvironment;

        public override void OnInitializeMelon()
        {
            var category = MelonPreferences.CreateCategory("Flora");
            DumpEnvironment = category.CreateEntry("DumpEnvironment", true,
                description: "Wypisuje do UserData\\Flora\\ spis roślinności planet (do tworzenia nowych roślin)");
            category.SaveToFile(false);
        }
    }
}
