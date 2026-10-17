using Core.Services;
using Players.Interface.Services;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.State;
using Ships.Interface.Services;

namespace Odyssey.Patches
{
    internal static class StorageMenuPatch
    {
        internal static bool IsLocalPlayerUsing(System.Func<Ships.Interface.Settings.PartSettings, bool> isPart)
        {
            var players = ServiceCache.Get<IPlayersProviderClient>();
            var ships = ServiceCache.Get<IShipsClientProvider>();
            if (players == null || ships == null || !players.TryGetLocalPlayerState(out var state))
            {
                return false;
            }
            return ships.TryGetShip(state.EntityId, out IShipStateReadClient ship) &&
                   ship.TryGetStatefulPart(state.PartId, out StatefulPart part) &&
                   isPart(part.Settings);
        }
    }
}
