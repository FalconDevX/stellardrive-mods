using MelonLoader;
using Reentry;

[assembly: MelonInfo(typeof(ReentryMod), "Reentry", "0.2.0", "mateu")]
[assembly: MelonGame("CuriousOwlGames", "StellarDrive")]

namespace Reentry
{
    public class ReentryMod : MelonMod
    {
        internal static MelonPreferences_Entry<bool> Enabled;
        internal static MelonPreferences_Entry<float> Brightness;
        internal static MelonPreferences_Entry<float> TrailLength;
        internal static MelonPreferences_Entry<bool> Lights;
        internal static MelonPreferences_Entry<float> MaxDistance;
        internal static MelonPreferences_Entry<bool> Debug;

        public override void OnInitializeMelon()
        {
            var category = MelonPreferences.CreateCategory("Reentry", "Reentry");
            Enabled = category.CreateEntry("Enabled", true, description: "Glowing plasma and a fiery trail around ships that fly fast through an atmosphere. Only visual, each player");
            Brightness = category.CreateEntry("Brightness", 1f, description: "Brightness of the plasma, flames and glow (0.2-3)");
            TrailLength = category.CreateEntry("TrailLength", 1f, description: "Length of the fiery trail behind the ship (0-3, 0 = no trail)");
            Lights = category.CreateEntry("Lights", true, description: "The plasma lights up the ship and its surroundings");
            MaxDistance = category.CreateEntry("MaxDistance", 6000f, description: "Ships farther than this from the camera (m) get no effect");
            Debug = category.CreateEntry("Debug", false, description: "Log speed, air density and heating of ships that glow");
            category.SaveToFile(false);
        }

        public override void OnUpdate() => ReentryEffects.Update();

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            ReentryEffects.Clear();
            ServiceCache.Reset();
        }
    }
}
