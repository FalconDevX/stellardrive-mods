using MelonLoader;
using WarpUnlock;

[assembly: MelonInfo(typeof(WarpUnlockMod), "Warp Unlock", "0.1.2", "mateu")]
[assembly: MelonGame("CuriousOwlGames", "StellarDrive")]

namespace WarpUnlock
{
    public class WarpUnlockMod : MelonMod
    {
        internal static MelonPreferences_Entry<string> PartKeywords;
        internal static MelonPreferences_Entry<bool> UnlockAllHidden;

        public override void OnInitializeMelon()
        {
            var category = MelonPreferences.CreateCategory("WarpUnlock");
            PartKeywords = category.CreateEntry("PartKeywords", "Warp",
                description: "Badania odblokowujące części, których nazwa zawiera któreś z tych słów (oddzielone przecinkami), pojawią się w drzewie badań dema");
            UnlockAllHidden = category.CreateEntry("UnlockAllHidden", false,
                description: "true = pokaż WSZYSTKIE badania ukryte w demie (mogą być niedokończone)");
            category.SaveToFile(false);

            LoggerInstance.Msg(UnlockAllHidden.Value
                ? "Drzewo badań: pokazuję wszystkie badania ukryte w demie."
                : $"Drzewo badań: odblokowuję badania z częściami: {PartKeywords.Value}");
        }
    }
}
