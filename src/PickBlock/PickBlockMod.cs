using MelonLoader;
using UnityEngine;
using PickBlock;

[assembly: MelonInfo(typeof(PickBlockMod), "Pick Block", "0.3.1", "mateu")]
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
        internal static MelonPreferences_Entry<bool> AreaBuild;
        internal static MelonPreferences_Entry<int> AreaMaxSize;

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
            AreaBuild = category.CreateEntry("AreaBuild", true,
                description: "Ctrl + scroll with the build tool on a floor or wall part changes the size of the square that one click builds");
            AreaMaxSize = category.CreateEntry("AreaMaxSize", 9, description: "Largest square side for area building (odd number)");
            category.SaveToFile(false);

            LoggerInstance.Msg("Build tool: middle mouse on a part = build the same part; hold left mouse = keep building; Ctrl + scroll on floors and walls = area size.");
        }

        public override void OnGUI()
        {
            if (!Patches.AreaBuildPatch.Showing)
            {
                return;
            }
            string text = $"{Patches.AreaBuildPatch.Size} x {Patches.AreaBuildPatch.Size}";
            var style = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            var rect = new Rect(Screen.width * 0.5f - 60f, Screen.height * 0.5f + 28f, 120f, 26f);
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, style);
            GUI.color = new Color(0.55f, 0.9f, 1f, 1f);
            GUI.Label(rect, text, style);
            GUI.color = Color.white;
        }
    }
}
