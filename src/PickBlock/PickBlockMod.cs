using MelonLoader;
using PickBlock;

[assembly: MelonInfo(typeof(PickBlockMod), "Pick Block", "0.2.1", "mateu")]
[assembly: MelonGame("CuriousOwlGames", "StellarDrive")]

namespace PickBlock
{
    public class PickBlockMod : MelonMod
    {
        internal static MelonPreferences_Entry<float> Range;
        internal static MelonPreferences_Entry<bool> RequireUnlocked;
        internal static MelonPreferences_Entry<bool> HoldToBuild;
        internal static MelonPreferences_Entry<float> HoldDelay;
        internal static MelonPreferences_Entry<float> HoldRepeat;

        public override void OnInitializeMelon()
        {
            var category = MelonPreferences.CreateCategory("PickBlock");
            Range = category.CreateEntry("Range", 25f, description: "Distance from which a part can be picked with the middle mouse button (m)");
            RequireUnlocked = category.CreateEntry("RequireUnlocked", true,
                description: "Only allow picking parts unlocked in research (false = any, like creative mode)");
            HoldToBuild = category.CreateEntry("HoldToBuild", true,
                description: "Holding the left button with the build tool places the part in every new free spot (like Minecraft)");
            HoldDelay = category.CreateEntry("HoldDelay", 0.25f, description: "Seconds of holding before placing further parts starts");
            HoldRepeat = category.CreateEntry("HoldRepeat", 0.12f, description: "Minimum interval between parts while holding (s)");
            category.SaveToFile(false);

            LoggerInstance.Msg("Build tool: middle mouse on a part = build the same part; hold left mouse = keep building.");
        }
    }
}
