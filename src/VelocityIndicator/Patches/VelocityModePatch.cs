using HarmonyLib;
using MelonLoader;
using Ships.Interface.Model.Parts.StateTypes;
using Ships.Interface.Model.State;
using Ships.Parts.Common.Services;
using Ships.Services;
using WorldTracking.Interface.Model.Spaces;
using WorldTracking.Interface.Model.Spaces.Common;
using WorldTracking.Interface.Services;

namespace VelocityIndicator.Patches
{
    // W grze tryb wskaźnika (względem powierzchni / kosmosu) wybiera przestrzeń, w której jest kamera gracza,
    // a prędkość liczona jest w przestrzeni statku. Przy wejściu na orbitę wskaźnik przeskakuje na prędkość
    // względem kosmosu - dochodzi do niej obrót planety (setki m/s w bok), więc strzałka nagle ucieka w bok,
    // a gdy przestrzenie się nie zgadzają, gra pokazuje 0. Tu tryb wybieramy według przestrzeni statku.
    [HarmonyPatch(typeof(ShipVelocityProvider), "CalculateShipRelativeVelocity")]
    internal static class VelocityModePatch
    {
        private static readonly AccessTools.FieldRef<ShipVelocityProvider, IShipStateReadClient> StateRef =
            AccessTools.FieldRefAccess<ShipVelocityProvider, IShipStateReadClient>("_shipStateRead");

        private static readonly AccessTools.FieldRef<ShipVelocityProvider, ShipsClientTracker> ShipsRef =
            AccessTools.FieldRefAccess<ShipVelocityProvider, ShipsClientTracker>("_ships");

        private static readonly AccessTools.FieldRef<ShipVelocityProvider, ISpacesClientProvider> SpacesRef =
            AccessTools.FieldRefAccess<ShipVelocityProvider, ISpacesClientProvider>("_spaces");

        private static RelativeSpaceType? _lastMode;

        private static void Prefix(ShipVelocityProvider __instance, ref RelativeSpaceType relativeSpaceType)
        {
            IShipStateReadClient state = StateRef(__instance);
            ShipsClientTracker ships = ShipsRef(__instance);
            ISpacesClientProvider spaces = SpacesRef(__instance);
            if (state == null || ships == null || spaces == null || !ships.TryGetTrackedRootShip(state.Id, out var ship))
            {
                return;
            }

            ISpace space = spaces.GetSpace(ship.SpaceId);
            RelativeSpaceType? mode = space switch
            {
                PlanetSurfaceSpace _ => RelativeSpaceType.Surface,
                OrbitSpace _ => VelocityIndicatorMod.OrbitRelativeToSurface.Value ? RelativeSpaceType.Surface : RelativeSpaceType.Space,
                TrajectorySpace _ => RelativeSpaceType.Space,
                WarpSpace _ => RelativeSpaceType.Space,
                PlanetRingSpace _ => RelativeSpaceType.Rings,
                _ => null,
            };
            if (!mode.HasValue)
            {
                return;
            }

            if (VelocityIndicatorMod.LogModeChanges.Value && mode != _lastMode)
            {
                Melon<VelocityIndicatorMod>.Logger.Msg(
                    $"Wskaźnik: statek w {space.GetType().Name}, tryb {mode} (gra chciała {relativeSpaceType})");
                _lastMode = mode;
            }
            relativeSpaceType = mode.Value;
        }
    }
}
