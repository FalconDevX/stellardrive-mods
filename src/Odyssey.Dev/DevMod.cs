using MelonLoader;
using Odyssey;

[assembly: MelonInfo(typeof(DevMod), "Odyssey Dev", "1.0.0", "mateu")]
[assembly: MelonGame("CuriousOwlGames", "StellarDrive")]
[assembly: MelonAdditionalDependencies("Odyssey.Core", "Odyssey.Mining", "Odyssey.Power", "Odyssey.Building", "Odyssey.Logic", "Odyssey.Gear", "Odyssey.Tweaks")]

namespace Odyssey
{
    public class DevMod : MelonMod
    {
        internal static MelonPreferences_Entry<string> DevAutoLoadWorld;
        internal static MelonPreferences_Entry<bool> DevDump;
        internal static MelonPreferences_Entry<bool> DevQuitAfterDump;
        internal static MelonPreferences_Entry<bool> DevTest;
        internal static MelonPreferences_Entry<bool> DevPerf;
        internal static MelonPreferences_Entry<bool> DevJetpack;

        public override void OnInitializeMelon()
        {
            var category = Settings.Category("OdysseyDev", "Odyssey Dev");
            DevAutoLoadWorld = Settings.Entry(category, "DevAutoLoadWorld", "", "Mod developer: full path to a world.json loaded automatically from the menu (empty = off)");
            DevDump = Settings.Entry(category, "DevDump", false, "Mod developer: dump parts and materials to UserData/Mining/dump.txt");
            DevQuitAfterDump = Settings.Entry(category, "DevQuitAfterDump", false, "Mod developer: quit the game after the dump");
            DevTest = Settings.Entry(category, "DevTest", false, "Mod developer: preview the excavator and a deposit in front of the camera after the world loads");
            DevJetpack = Settings.Entry(category, "DevJetpack", false, "Mod developer: jetpack flight test");
            DevPerf = Settings.Entry(category, "DevPerf", false, "Mod developer: measure lag while quickly placing and removing parts");
            category.SaveToFile(false);
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            Diagnostics.DevRunner.OnSceneLoaded(sceneName);
        }
    }
}
