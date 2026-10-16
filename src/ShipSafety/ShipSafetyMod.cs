using MelonLoader;
using ShipSafety;

[assembly: MelonInfo(typeof(ShipSafetyMod), "Ship Safety", "0.5.8", "mateu")]
[assembly: MelonGame("CuriousOwlGames", "StellarDrive")]

namespace ShipSafety
{
    public class ShipSafetyMod : MelonMod
    {
        internal static MelonPreferences_Entry<bool> Enabled;
        internal static MelonPreferences_Entry<float> MaxSpeed;
        internal static MelonPreferences_Entry<float> MaxAcceleration;
        internal static MelonPreferences_Entry<float> MaxSpeedJump;
        internal static MelonPreferences_Entry<float> ShipCrashSpeed;
        internal static MelonPreferences_Entry<int> ShipSolverIterations;
        internal static MelonPreferences_Entry<float> ShipDepenetrationSpeed;
        internal static MelonPreferences_Entry<bool> GuestShipCollision;

        public override void OnInitializeMelon()
        {
            var category = MelonPreferences.CreateCategory("ShipSafety");
            Enabled = category.CreateEntry("Enabled", true,
                description: "Limit ship speed and acceleration (false = log only)");
            MaxSpeed = category.CreateEntry("MaxSpeed", 5000f,
                description: "Maximum ship speed in m/s (planet escape speed is about 700-2500 m/s)");
            MaxAcceleration = category.CreateEntry("MaxAcceleration", 300f,
                description: "Maximum acceleration from forces (thrusters, drag, lift...) in m/s^2 (300 = about 30 g)");
            MaxSpeedJump = category.CreateEntry("MaxSpeedJump", 1000f,
                description: "Maximum ship speed change in one physics step (m/s). A bigger jump is a physics bug - the speed goes back to the one before the step");
            ShipCrashSpeed = category.CreateEntry("ShipCrashSpeed", 12f,
                description: "A collision of two ships destroys parts only from this relative speed (m/s). Slower contact (landing in a hangar) - no damage");
            ShipSolverIterations = category.CreateEntry("ShipSolverIterations", 12,
                description: "Ship physics solver iterations (game: 6). More = less jitter of a ship resting on another ship");
            ShipDepenetrationSpeed = category.CreateEntry("ShipDepenetrationSpeed", 3f,
                description: "Speed (m/s) at which physics pushes apart overlapping ships (0 = like the game). Less = gentler, no launching");
            GuestShipCollision = category.CreateEntry("GuestShipCollision", true,
                description: "A ship resting on or standing next to another ship stays solid - players on either ship cannot walk through the other one");
            category.SaveToFile(false);

            LoggerInstance.Msg($"Ship speed limit {MaxSpeed.Value} m/s, acceleration {MaxAcceleration.Value} m/s^2" +
                               (Enabled.Value ? "" : " (log only)"));
        }
    }
}
