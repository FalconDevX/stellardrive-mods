using HarmonyLib;
using Managers.Interface.Model;
using Managers.Server.Services;
using Players;
using Players.Interface.Model;
using Players.Services;
using Ships.Cables;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;

namespace Mining.Patches
{
    // Błąd gry: przy wyjmowaniu wtyczki gra porównuje tylko numer części i gniazda, bez statku. Kabel między dwoma
    // statkami (np. koparka stojąca na ziemi i generator na statku) z takimi samymi numerami po obu stronach
    // odpinał się wtedy z obu końców naraz. Tu porównujemy też statek - drugi koniec zostaje wpięty.
    [HarmonyPatch(typeof(ShipInteractionService), "RpcLogic___PickUpCable_ServerRPC___1176123958")]
    internal static class CablePickUpFixPatch
    {
        private static bool Prefix(SocketNetworkRequest __0, FishNet.Connection.NetworkConnection __1, PlayersServerTracker ____players, CablesServerTracker ____cables)
        {
            EntityPartSocket socket = __0.EntityPartSocket;
            if (__1 == null || ____players == null || ____cables == null)
            {
                return false;
            }
            TrackedPlayerServer player = ____players.GetTrackedPlayerFromConnectionId(__1.ClientId);
            if (player == null)
            {
                return false;
            }
            PlayerStateType stateVal = player.StateVal;
            if ((stateVal != PlayerStateType.Walking && stateVal != PlayerStateType.CheatFlying) ||
                !____cables.TryGetCableId(socket, out uint cableId) || !____cables.TryGetCableState(cableId, out CableState state))
            {
                return false;
            }
            CablePlugSocket plugA = state.PlugA;
            CablePlugSocket plugB = state.PlugB;
            if (!plugA.IsHeldByPlayer && plugA.EntityPartSocket.Equals(socket))
            {
                plugA.IsHeldByPlayer = true;
                plugA.EntityId = player.Id;
                plugA.PartSocket = default(PartSocket);
                state.PlugA = plugA;
                ____cables.UpdateCableState(cableId, state);
            }
            else if (!plugB.IsHeldByPlayer && plugB.EntityPartSocket.Equals(socket))
            {
                plugB.IsHeldByPlayer = true;
                plugB.EntityId = player.Id;
                plugB.PartSocket = default(PartSocket);
                state.PlugB = plugB;
                ____cables.UpdateCableState(cableId, state);
            }
            return false;
        }
    }
}
