using MelonLoader;
using ShipSafety;

[assembly: MelonInfo(typeof(ShipSafetyMod), "Ship Safety", "0.1.0", "mateu")]
[assembly: MelonGame("CuriousOwlGames", "StellarDrive")]

namespace ShipSafety
{
    // Zabezpieczenie przed błędem gry, w którym statek nagle dostaje absurdalną prędkość (np. 93c)
    // i zostaje wyrzucony miliony km od planety. Mod zapisuje do logu, która siła to zrobiła,
    // i nie pozwala statkowi przekroczyć rozsądnej prędkości. Liczy to serwer (host).
    public class ShipSafetyMod : MelonMod
    {
        internal static MelonPreferences_Entry<bool> Enabled;
        internal static MelonPreferences_Entry<float> MaxSpeed;
        internal static MelonPreferences_Entry<float> MaxAcceleration;

        public override void OnInitializeMelon()
        {
            var category = MelonPreferences.CreateCategory("ShipSafety");
            Enabled = category.CreateEntry("Enabled", true,
                description: "Ograniczanie prędkości i przyspieszenia statków (false = tylko zapis do logu)");
            MaxSpeed = category.CreateEntry("MaxSpeed", 5000f,
                description: "Maksymalna prędkość statku w m/s (prędkość ucieczki z planet to ok. 700-2500 m/s)");
            MaxAcceleration = category.CreateEntry("MaxAcceleration", 300f,
                description: "Maksymalne przyspieszenie od sił (silniki, opór, siła nośna...) w m/s^2 (300 = ok. 30 g)");
            category.SaveToFile(false);

            LoggerInstance.Msg($"Limit prędkości statków {MaxSpeed.Value} m/s, przyspieszenia {MaxAcceleration.Value} m/s^2" +
                               (Enabled.Value ? "" : " (tylko log)"));
        }
    }
}
