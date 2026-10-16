using MelonLoader;
using ResourceTweaks;

[assembly: MelonInfo(typeof(ResourceTweaksMod), "Resource Tweaks", "0.5.1", "mateu")]
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
        internal static MelonPreferences_Entry<int> InventorySlots;
        internal static MelonPreferences_Entry<int> InventoryRows;

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
            InventorySlots = category.CreateEntry("InventorySlots", 45,
                description: "Personal inventory slots (15-250, the game has 15). Scroll the inventory window with the mouse wheel. All players must use the same value");
            InventoryRows = category.CreateEntry("InventoryRows", 0,
                description: "Rows of slots visible in the inventory window opened with Tab, slots keep their size (0 = all rows that fit on the screen). Each player");
            LogDrops = category.CreateEntry("LogDrops", true,
                description: "Logs every drop to MelonLoader\\Latest.log (for checking, turn off after testing)");
            category.SaveToFile(false);
            Patches.InventorySize.Apply();

            LoggerInstance.Msg($"Resources x{DropMultiplier.Value}, iron x{IronDropMultiplier.Value}, stack up to {MaxStackSize.Value}, inventory {Patches.InventorySize.Slots} slots");
        }
    }
}
