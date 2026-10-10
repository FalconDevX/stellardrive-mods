using MelonLoader;
using SharedPiloting;

[assembly: MelonInfo(typeof(SharedPilotingMod), "Shared Piloting", "0.1.0", "mateu")]
[assembly: MelonGame("CuriousOwlGames", "StellarDrive")]

namespace SharedPiloting
{
    public class SharedPilotingMod : MelonMod
    {
        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("Kilka foteli pilota: obraca statkiem ten, kto rusza myszą (bezczynne fotele nie blokują).");
        }
    }
}
