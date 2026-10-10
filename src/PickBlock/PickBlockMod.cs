using MelonLoader;
using PickBlock;

[assembly: MelonInfo(typeof(PickBlockMod), "Pick Block", "0.1.0", "mateu")]
[assembly: MelonGame("CuriousOwlGames", "StellarDrive")]

namespace PickBlock
{
    public class PickBlockMod : MelonMod
    {
        internal static MelonPreferences_Entry<float> Range;
        internal static MelonPreferences_Entry<bool> RequireUnlocked;

        public override void OnInitializeMelon()
        {
            var category = MelonPreferences.CreateCategory("PickBlock");
            Range = category.CreateEntry("Range", 25f, description: "Z jakiej odległości można pobrać część środkowym przyciskiem myszy (m)");
            RequireUnlocked = category.CreateEntry("RequireUnlocked", true,
                description: "Pozwól pobrać tylko części odblokowane w badaniach (false = każdą, jak w trybie kreatywnym)");
            category.SaveToFile(false);

            LoggerInstance.Msg("Z narzędziem budowania: środkowy przycisk myszy na części = buduj tą samą częścią.");
        }
    }
}
