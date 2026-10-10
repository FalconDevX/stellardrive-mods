using Crouch;
using MelonLoader;

[assembly: MelonInfo(typeof(CrouchMod), "Crouch", "0.1.0", "mateu")]
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
                description: "Wysokość postaci w kucaniu względem normalnej (0.3 - 0.95)");
            SpeedFactor = category.CreateEntry("SpeedFactor", 0.5f,
                description: "Prędkość chodu w kucaniu względem normalnej (sprint w kucaniu jest wyłączony)");
            category.SaveToFile(false);

            LoggerInstance.Msg("Kucanie: przytrzymaj klawisz 'crouch' z ustawień sterowania (ten sam co opadanie w kosmosie).");
        }
    }
}
