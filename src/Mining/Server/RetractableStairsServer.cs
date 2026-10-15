using System;
using Core.Services;
using HarmonyLib;
using MelonLoader;
using Mining.Content;
using Ships;
using Ships.Cables;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Model.Parts.State;

namespace Mining.Server
{
    [HarmonyPatch(typeof(TrackedShipServer), nameof(TrackedShipServer.UpdateParts))]
    internal static class RetractableStairsServer
    {
        private static bool _loggedError;

        private static void Postfix(TrackedShipServer __instance)
        {
            try
            {
                ConnectionsServerTracker connections = null;
                foreach (ushort id in ShipPartIndex.Get(__instance, RetractableStairs.PartId))
                {
                    if (!__instance.TryGetStatefulPart(id, out StatefulPart part) || !(part.State is DoorState state))
                    {
                        continue;
                    }
                    connections = connections ?? ServiceLocator.GetService<ConnectionsServerTracker>();
                    var socket = new EntityPartSocket(__instance.Id, id, RetractableStairs.SignalSocket);
                    bool open = connections != null && connections.TryGetSignalConnectionGroupWithSocket(socket, out _) && connections.GetSignalValue(socket) >= 0.5;
                    if (state.IsOpen != open)
                    {
                        __instance.UpdatePartState(id, state with { IsOpen = open });
                    }
                }
            }
            catch (Exception e)
            {
                if (!_loggedError)
                {
                    _loggedError = true;
                    Melon<MiningMod>.Logger.Error($"Retractable stairs error: {e}");
                }
            }
        }
    }
}
