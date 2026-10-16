using MelonLoader;
using PlanetUnlock;

[assembly: MelonInfo(typeof(PlanetUnlockMod), "Planet Unlock", "0.1.2", "mateu")]
[assembly: MelonGame("CuriousOwlGames", "StellarDrive")]

namespace PlanetUnlock
{
    public class PlanetUnlockMod : MelonMod
    {
        internal static MelonPreferences_Entry<bool> Enabled;
        internal static MelonPreferences_Entry<bool> ActivateInactivePlanets;

        public override void OnInitializeMelon()
        {
            var category = MelonPreferences.CreateCategory("PlanetUnlock");
            Enabled = category.CreateEntry("Enabled", true,
                description: "Loads all planets of the system (the demo loads only Otrera b). BOTH players must use the same setting");
            ActivateInactivePlanets = category.CreateEntry("ActivateInactivePlanets", true,
                description: "Enables planet objects disabled in the demo scene");
            category.SaveToFile(false);

            LoggerInstance.Msg(Enabled.Value
                ? "Loading all planets of the system (experimental)."
                : "Disabled - only the demo planet.");
        }
    }
}
