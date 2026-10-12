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
                description: "Research unlocking parts whose name contains any of these words (comma separated) appears in the demo research tree");
            UnlockAllHidden = category.CreateEntry("UnlockAllHidden", false,
                description: "true = show ALL research hidden in the demo (may be unfinished)");
            category.SaveToFile(false);

            LoggerInstance.Msg(UnlockAllHidden.Value
                ? "Research tree: showing all research hidden in the demo."
                : $"Research tree: unlocking research with parts: {PartKeywords.Value}");
        }
    }
}
