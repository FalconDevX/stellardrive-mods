using MelonLoader;
using PlanetUnlock;

[assembly: MelonInfo(typeof(PlanetUnlockMod), "Planet Unlock", "0.1.1", "mateu")]
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
                description: "Wczytuje wszystkie planety układu (demo wczytuje tylko Otrera b). OBAJ gracze muszą mieć to samo ustawienie");
            ActivateInactivePlanets = category.CreateEntry("ActivateInactivePlanets", true,
                description: "Włącza obiekty planet wyłączone w scenie dema");
            category.SaveToFile(false);

            LoggerInstance.Msg(Enabled.Value
                ? "Wczytuję wszystkie planety układu (eksperymentalne)."
                : "Wyłączone - tylko planeta z dema.");
        }
    }
}
