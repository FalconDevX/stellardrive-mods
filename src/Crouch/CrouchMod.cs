using Crouch;
using MelonLoader;

[assembly: MelonInfo(typeof(CrouchMod), "Crouch", "0.1.1", "mateu")]
[assembly: MelonGame("CuriousOwlGames", "StellarDrive")]

namespace Crouch
{
    public class CrouchMod : MelonMod
    {
        internal static MelonPreferences_Entry<float> HeightFactor;
        internal static MelonPreferences_Entry<float> SpeedFactor;

        public override void OnInitializeMelon()
        {
            var category = MelonPreferences.CreateCategory("Crouch");
            HeightFactor = category.CreateEntry("HeightFactor", 0.6f,
                description: "Crouched character height relative to normal (0.3 - 0.95)");
            SpeedFactor = category.CreateEntry("SpeedFactor", 0.5f,
                description: "Crouched walking speed relative to normal (sprinting while crouched is disabled)");
            category.SaveToFile(false);

            LoggerInstance.Msg("Crouch: hold the 'crouch' key from the control settings (the same as descending in space).");
        }
    }
}
