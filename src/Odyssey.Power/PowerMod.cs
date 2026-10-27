using MelonLoader;
using Odyssey;
using UnityEngine;

[assembly: MelonInfo(typeof(PowerMod), "Odyssey Power", "1.1.5", "mateu")]
[assembly: MelonGame("CuriousOwlGames", "StellarDrive")]
[assembly: MelonAdditionalDependencies("Odyssey.Core")]

namespace Odyssey
{
    public class PowerMod : MelonMod
    {
        internal static MelonPreferences_Entry<float> ReactorPower;
        internal static MelonPreferences_Entry<float> ReactorSecondsPerUranium;
        internal static MelonPreferences_Entry<float> PumpRate;
        internal static MelonPreferences_Entry<float> PumpHoseLength;
        internal static MelonPreferences_Entry<float> PumpVolume;
        internal static MelonPreferences_Entry<float> ReactorVolume;
        internal static MelonPreferences_Entry<float> ElectrolyzerVolume;
        internal static MelonPreferences_Entry<float> VentVolume;
        internal static MelonPreferences_Entry<float> HydrogenEngineVolume;
        internal static MelonPreferences_Entry<float> HydrogenThrustMultiplier;

        public override void OnInitializeMelon()
        {
            var category = Settings.Category("OdysseyPower", "Odyssey Power");
            ReactorPower = Settings.Entry(category, "ReactorPower", 45000f, "Reactor power (W), the large reactor gives 5x. The wood generator gives 4000 W. Host only");
            if (ReactorPower.Value <= 20000f)
            {
                ReactorPower.Value = 45000f;
            }
            ReactorSecondsPerUranium = Settings.Entry(category, "ReactorSecondsPerUranium", 120f, "Seconds of full-power operation per 1 uranium. Host only");
            PumpRate = Settings.Entry(category, "PumpRate", 1f, "Water per second pumped at full power. Host only");
            PumpHoseLength = Settings.Entry(category, "PumpHoseLength", 30f, "Pump hose length (m). All players should use the same value");
            if (Mathf.Approximately(PumpHoseLength.Value, 20f)) PumpHoseLength.Value = 30f;
            PumpVolume = Settings.Entry(category, "PumpVolume", 0.7f, "Water pump volume (0-1)");
            ReactorVolume = Settings.Entry(category, "ReactorVolume", 0.6f, "Running reactor volume (0-1)");
            ElectrolyzerVolume = Settings.Entry(category, "ElectrolyzerVolume", 0.25f, "Electrolyzer volume while working (0-1)");
            VentVolume = Settings.Entry(category, "VentVolume", 0.5f, "Air vent fan volume (0-1)");
            HydrogenEngineVolume = Settings.Entry(category, "HydrogenEngineVolume", 0.8f, "Hydrogen engine volume (0-1)");
            HydrogenThrustMultiplier = Settings.Entry(category, "HydrogenThrustMultiplier", 1.8f, "How many times stronger the hydrogen thruster is than the chemical one. Host only");
            category.SaveToFile(false);
            Patches.Registration.Register();
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            Server.WaterPumpServer.Reset();
            Server.HydrogenThrusterServer.Reset();
        }
    }
}
