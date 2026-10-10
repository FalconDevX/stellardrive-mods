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
                description: "Na orbicie planety wskaźnik pokazuje prędkość względem powierzchni (jak przy ziemi), a nie względem kosmosu");
            LogModeChanges = category.CreateEntry("LogModeChanges", true,
                description: "Zapisuje do MelonLoader\\Latest.log każdą zmianę trybu wskaźnika (do sprawdzania)");
            category.SaveToFile(false);

            LoggerInstance.Msg(OrbitRelativeToSurface.Value
                ? "Wskaźnik prędkości: przy planecie i na jej orbicie zawsze względem powierzchni."
                : "Wskaźnik prędkości: tryb według przestrzeni statku.");
        }
    }
}
