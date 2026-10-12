using MelonLoader;
using ShipSafety;

[assembly: MelonInfo(typeof(ShipSafetyMod), "Ship Safety", "0.4.0", "mateu")]
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
        internal static MelonPreferences_Entry<float> MaxSpeedJump;
        internal static MelonPreferences_Entry<float> ShipCrashSpeed;
        internal static MelonPreferences_Entry<int> ShipSolverIterations;
        internal static MelonPreferences_Entry<float> ShipDepenetrationSpeed;

        public override void OnInitializeMelon()
        {
            var category = MelonPreferences.CreateCategory("ShipSafety");
            Enabled = category.CreateEntry("Enabled", true,
                description: "Ograniczanie prędkości i przyspieszenia statków (false = tylko zapis do logu)");
            MaxSpeed = category.CreateEntry("MaxSpeed", 5000f,
                description: "Maksymalna prędkość statku w m/s (prędkość ucieczki z planet to ok. 700-2500 m/s)");
            MaxAcceleration = category.CreateEntry("MaxAcceleration", 300f,
                description: "Maksymalne przyspieszenie od sił (silniki, opór, siła nośna...) w m/s^2 (300 = ok. 30 g)");
            MaxSpeedJump = category.CreateEntry("MaxSpeedJump", 1000f,
                description: "Maksymalna zmiana prędkości statku w jednym kroku fizyki (m/s). Większy skok to błąd fizyki - prędkość wraca do tej sprzed kroku");
            ShipCrashSpeed = category.CreateEntry("ShipCrashSpeed", 12f,
                description: "Zderzenie dwóch statków niszczy części dopiero od tej prędkości względnej (m/s). Wolniejszy kontakt (lądowanie w hangarze) - bez zniszczeń");
            ShipSolverIterations = category.CreateEntry("ShipSolverIterations", 12,
                description: "Iteracje solvera fizyki statków (gra: 6). Więcej = mniej drgań statku stojącego na innym statku");
            ShipDepenetrationSpeed = category.CreateEntry("ShipDepenetrationSpeed", 3f,
                description: "Z jaką prędkością (m/s) fizyka rozsuwa statki, które weszły jeden w drugi (0 = jak w grze). Mniej = łagodniej, bez wystrzeliwania");
            category.SaveToFile(false);

            LoggerInstance.Msg($"Limit prędkości statków {MaxSpeed.Value} m/s, przyspieszenia {MaxAcceleration.Value} m/s^2" +
                               (Enabled.Value ? "" : " (tylko log)"));
        }
    }
}
