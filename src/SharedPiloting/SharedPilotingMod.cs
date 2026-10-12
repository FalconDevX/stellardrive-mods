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
            LoggerInstance.Msg("Several pilot seats: whoever moves the mouse turns the ship (idle seats do not block).");
        }
    }
}
