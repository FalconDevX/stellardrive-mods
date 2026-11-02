using MelonLoader;
using SunRays;

[assembly: MelonInfo(typeof(SunRaysMod), "Sun Rays", "0.1.0", "mateu")]
[assembly: MelonGame("CuriousOwlGames", "StellarDrive")]

namespace SunRays
{
    public class SunRaysMod : MelonMod
    {
        internal static MelonPreferences_Entry<bool> Enabled;
        internal static MelonPreferences_Entry<float> Intensity;
        internal static MelonPreferences_Entry<float> Frequency;
        internal static MelonPreferences_Entry<int> Steps;
        internal static MelonPreferences_Entry<bool> Debug;
        internal static MelonPreferences_Entry<bool> Force;

        public override void OnInitializeMelon()
        {
            var category = MelonPreferences.CreateCategory("SunRays", "Sun Rays");
            Enabled = category.CreateEntry("Enabled", true, description: "Rays of sunlight through gaps in the clouds. Only visual, each player");
            Intensity = category.CreateEntry("Intensity", 1f, description: "Brightness of the rays (0.1-3)");
            Frequency = category.CreateEntry("Frequency", 0.6f, description: "How much of the time the weather allows rays when the clouds and the sun are right (0 = never, 1 = always)");
            Steps = category.CreateEntry("Steps", 32, description: "Samples along each pixel's view ray (12-64). Lower is faster, higher is smoother");
            Debug = category.CreateEntry("Debug", false, description: "Log the cloud cover and every condition that switches the rays");
            Force = category.CreateEntry("Force", false, description: "Ignore the sky and always show the rays on planets with clouds, for screenshots");
            category.SaveToFile(false);
        }

        public override void OnUpdate() => SunRaysEffect.Update();

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            SunRaysEffect.Clear();
            ServiceCache.Reset();
        }
    }
}
