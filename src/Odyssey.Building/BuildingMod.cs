using MelonLoader;
using Odyssey;

[assembly: MelonInfo(typeof(BuildingMod), "Odyssey Building", "1.0.1", "mateu")]
[assembly: MelonGame("CuriousOwlGames", "StellarDrive")]
[assembly: MelonAdditionalDependencies("Odyssey.Core")]

namespace Odyssey
{
    public class BuildingMod : MelonMod
    {
        internal static MelonPreferences_Entry<float> DoorVolume;
        internal static MelonPreferences_Entry<float> DoorSignalReturnSeconds;
        internal static MelonPreferences_Entry<float> PontoonLeakPerMinute;
        internal static MelonPreferences_Entry<float> RotorSpeed;
        internal static MelonPreferences_Entry<float> RotorLeverAngle;
        internal static MelonPreferences_Entry<bool> RotorCollision;

        public override void OnInitializeMelon()
        {
            var category = Settings.Category("OdysseyBuilding", "Odyssey Building");
            DoorVolume = Settings.Entry(category, "DoorVolume", 0.8f, "Large door volume (0-1)");
            DoorSignalReturnSeconds = Settings.Entry(category, "DoorSignalReturnSeconds", 30f, "A door, gate or hatch with a signal cable that you open or close by hand goes back to what its signal says after this many seconds (signal 0 closes it, 1 opens it). 0 = stays as you left it until the signal changes. Host only");
            PontoonLeakPerMinute = Settings.Entry(category, "PontoonLeakPerMinute", 4f, "Oxygen leaking out of an inflated pontoon per minute (it holds 2); without an oxygen supply it deflates when empty");
            RotorSpeed = Settings.Entry(category, "RotorSpeed", 90f, "Rotor turning speed (degrees per second). Host only");
            RotorLeverAngle = Settings.Entry(category, "RotorLeverAngle", 90f, "Rotor angle for a signal of 1, e.g. from a lever (degrees). Signals between -1 and 1 are multiplied by it, bigger signals are degrees. Host only");
            RotorCollision = Settings.Entry(category, "RotorCollision", false, "When true, a turning rotor stops before its moving part hits something. When false, it turns through everything. Host only");
            category.SaveToFile(false);
            Patches.Registration.Register();
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            Server.BigDoorSignalServer.Reset();
        }
    }
}
