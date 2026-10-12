using MelonLoader;
using ResourceTweaks;

[assembly: MelonInfo(typeof(ResourceTweaksMod), "Resource Tweaks", "0.3.2", "mateu")]
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
                description: "Multiplier for all resources mined on planets (1 = unchanged)");
            IronDropMultiplier = category.CreateEntry("IronMultiplier", 10f,
                description: "Separate multiplier for iron (replaces DropMultiplier for iron, 10 = 10x more)");
            MaxStackSize = category.CreateEntry("MaxStackSize", 100,
                description: "Maximum stack size of stackable items (1-255). Tools (stack 1) unchanged");
            TreeMiningTime = category.CreateEntry("TreeMiningTime", 0.5f,
                description: "Tree mining time relative to normal (0.5 = 50% shorter, 1 = unchanged)");
            OtherMiningTime = category.CreateEntry("OtherMiningTime", 0.75f,
                description: "Mining time of other resources relative to normal (0.75 = 25% shorter)");
            LogDrops = category.CreateEntry("LogDrops", true,
                description: "Logs every drop to MelonLoader\\Latest.log (for checking, turn off after testing)");
            category.SaveToFile(false);

            LoggerInstance.Msg($"Resources x{DropMultiplier.Value}, iron x{IronDropMultiplier.Value}, stack up to {MaxStackSize.Value}");
        }
    }
}
