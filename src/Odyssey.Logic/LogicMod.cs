using MelonLoader;
using Odyssey;

[assembly: MelonInfo(typeof(LogicMod), "Odyssey Logic", "1.0.0", "mateu")]
[assembly: MelonGame("CuriousOwlGames", "StellarDrive")]
[assembly: MelonAdditionalDependencies("Odyssey.Core")]

namespace Odyssey
{
    public class LogicMod : MelonMod
    {
        public override void OnInitializeMelon()
        {
            Patches.Registration.Register();
        }
    }
}
