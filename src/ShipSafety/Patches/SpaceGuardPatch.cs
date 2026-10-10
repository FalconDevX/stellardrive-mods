using Core.Values;
using HarmonyLib;
using MelonLoader;
using Planets.Interface.Model;
using WorldTracking.Interface.Model.Spaces;
using WorldTracking.Interface.Model.Spaces.Common;
using WorldTracking.Spaces;

namespace ShipSafety.Patches
{
    // Gdy statek opuszcza okolice planety, gra przenosi go na orbitę albo na lot "po prostej" (trajectory)
    // z wyliczoną prędkością. Tu łapiemy absurdalne prędkości w chwili tworzenia takiej przestrzeni.
    internal static class SpaceGuard
    {
        internal static Vector3Double Clamp(Vector3Double velocity, string what)
        {
            double speed = velocity.Magnitude;
            double max = ShipSafetyMod.MaxSpeed.Value;
            bool broken = double.IsNaN(speed) || double.IsInfinity(speed);
            if (!broken && speed <= max)
            {
                return velocity;
            }

            Melon<ShipSafetyMod>.Logger.Warning($"{what}: prędkość {speed:0.#} m/s ({speed / 299792458.0:0.##}c)" +
                                                (ShipSafetyMod.Enabled.Value ? $" -> obcięta do {max:0} m/s" : ""));
            if (!ShipSafetyMod.Enabled.Value)
            {
                return velocity;
            }
            return broken ? Vector3Double.Zero : velocity * (max / speed);
        }
    }

    [HarmonyPatch(typeof(SpacesServer), nameof(SpacesServer.CreateSpace))]
    internal static class TrajectorySpaceGuardPatch
    {
        private static void Prefix(ref ISpace space)
        {
            if (space is TrajectorySpace trajectory)
            {
                Vector3Double velocity = SpaceGuard.Clamp(trajectory.Velocity, "Nowy lot poza planetą (trajectory)");
                if (!velocity.Equals(trajectory.Velocity))
                {
                    space = trajectory with { Velocity = velocity };
                }
            }
        }
    }

    [HarmonyPatch(typeof(SpacesServer), nameof(SpacesServer.CreateOrbitSpace))]
    internal static class OrbitSpaceGuardPatch
    {
        private static void Prefix(IPlanetProperties planet, ref Vector3Double velocity)
        {
            velocity = SpaceGuard.Clamp(velocity, $"Nowa orbita wokół {planet?.Name}");
        }
    }
}
