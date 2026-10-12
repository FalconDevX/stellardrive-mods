using HarmonyLib;
using Mining.Content;
using Players.Inventory;
using Players.Network;
using Ships.Interface.Model.Parts;
using Ships.Interface.Services;

namespace Mining.Patches
{
    // Reaktor ma stan generatora na drewno, a serwer wpuszcza do jego slotu tylko drewno - dla reaktora tylko uran.
    // (Przeciąganie i Shift idą przez ten sam sprawdzian na serwerze.)
    [HarmonyPatch]
    internal static class ReactorSlotPatch
    {
        private static System.Reflection.MethodBase TargetMethod() =>
            AccessTools.Method(AccessTools.Inner(typeof(PlayerInventoryTransactions), "ChestAccessor"), "CanReceiveItem");

        private static void Postfix(PlayerNetworkState ____state, IShipsServerProvider ____shipsServer, uint itemId, ref bool __result)
        {
            if (____state == null || ____shipsServer == null)
            {
                return;
            }
            var context = new PartContext { ShipId = ____state.ServerState.EntityId, PartId = ____state.ServerState.PartId };
            if (____shipsServer.TryGetStatefulPart(context, out StatefulPart part) && Reactor.Is(part.Settings))
            {
                __result = itemId == Uranium.ItemId;
            }
        }
    }
}
