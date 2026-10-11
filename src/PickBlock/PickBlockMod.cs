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
            Range = category.CreateEntry("Range", 25f, description: "Z jakiej odległości można pobrać część środkowym przyciskiem myszy (m)");
            RequireUnlocked = category.CreateEntry("RequireUnlocked", true,
                description: "Pozwól pobrać tylko części odblokowane w badaniach (false = każdą, jak w trybie kreatywnym)");
            HoldToBuild = category.CreateEntry("HoldToBuild", true,
                description: "Przytrzymany lewy przycisk z narzędziem budowania stawia część w każdym nowym wolnym miejscu (jak w Minecrafcie)");
            HoldDelay = category.CreateEntry("HoldDelay", 0.25f, description: "Po ilu sekundach przytrzymania zaczyna się stawianie kolejnych części");
            HoldRepeat = category.CreateEntry("HoldRepeat", 0.12f, description: "Minimalny odstęp między kolejnymi częściami przy przytrzymaniu (s)");
            category.SaveToFile(false);

            LoggerInstance.Msg("Build tool: middle mouse on a part = build the same part; hold left mouse = keep building.");
        }
    }
}
