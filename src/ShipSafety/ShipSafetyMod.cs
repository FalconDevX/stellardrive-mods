using MelonLoader;
using ShipSafety;

[assembly: MelonInfo(typeof(ShipSafetyMod), "Ship Safety", "0.6.0", "mateu")]
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
        internal static MelonPreferences_Entry<bool> CarryShipsInHangars;
        internal static MelonPreferences_Entry<bool> WeldParkedShipsInHangars;
        internal static MelonPreferences_Entry<bool> KeepJumpingPlayersInShip;
        internal static MelonPreferences_Entry<bool> SleepParkedShips;

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
            CarryShipsInHangars = category.CreateEntry("CarryShipsInHangars", true,
                description: "A smaller ship inside a bigger ship's hull (e.g. in a hangar) moves and turns with it, but can still fly and land on its own. Host only");
            WeldParkedShipsInHangars = category.CreateEntry("WeldParkedShipsInHangars", true,
                description: "A ship inside a hangar with nobody in its pilot seat is held rigidly in place in the bigger ship, like part of its grid. Taking the pilot seat releases it. Host only");
            KeepJumpingPlayersInShip = category.CreateEntry("KeepJumpingPlayersInShip", true,
                description: "A player who jumps or falls inside a flying ship's hull (open hangar, deck without a sealed room) keeps moving with the ship instead of being left behind and hit by it");
            SleepParkedShips = category.CreateEntry("SleepParkedShips", true,
                description: "A ship that has been standing still for a second with no change in thrust is put to sleep in the physics engine, so it costs almost nothing until something hits it, it fires thrusters or its mass changes. Host only");
            category.SaveToFile(false);

            LoggerInstance.Msg($"Ship speed limit {MaxSpeed.Value} m/s, acceleration {MaxAcceleration.Value} m/s^2" +
                               (Enabled.Value ? "" : " (log only)"));
        }
    }
}
