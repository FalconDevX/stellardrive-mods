using MelonLoader;
using VelocityIndicator;

[assembly: MelonInfo(typeof(VelocityIndicatorMod), "Velocity Indicator", "0.1.0", "mateu")]
[assembly: MelonGame("CuriousOwlGames", "StellarDrive")]

namespace VelocityIndicator
{
    public class VelocityIndicatorMod : MelonMod
    {
        internal static MelonPreferences_Entry<bool> OrbitRelativeToSurface;
        internal static MelonPreferences_Entry<bool> LogModeChanges;

        public override void OnInitializeMelon()
        {
            var category = MelonPreferences.CreateCategory("VelocityIndicator");
            OrbitRelativeToSurface = category.CreateEntry("OrbitRelativeToSurface", true,
                description: "In planet orbit the indicator shows speed relative to the surface (like near the ground), not relative to space");
            LogModeChanges = category.CreateEntry("LogModeChanges", true,
                description: "Logs every indicator mode change to MelonLoader\\Latest.log (for checking)");
            category.SaveToFile(false);

            LoggerInstance.Msg(OrbitRelativeToSurface.Value
                ? "Velocity indicator: near a planet and in its orbit always relative to the surface."
                : "Velocity indicator: mode follows the ship's space.");
        }
    }
}
