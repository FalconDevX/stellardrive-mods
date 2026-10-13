using System;
using System.Linq;
using Core.Services;
using HarmonyLib;
using Items.Model;
using MelonLoader;
using Mining.Content;
using Ships;
using Ships.Cables;
using Ships.Cables.Fluids;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Model.Parts.State;

namespace Mining.Server
{
    [HarmonyPatch(typeof(TrackedShipServer), nameof(TrackedShipServer.UpdateParts))]
    internal static class PontoonServer
    {
        private static bool _loggedError;

        private static void Postfix(TrackedShipServer __instance)
        {
            try
            {
                ConnectionsServerTracker connections = null;
                foreach (StatefulPart part in __instance.StatefulParts.Where(p => p?.Settings != null && p.Settings.id == Pontoon.PartId && p.State is DoorState).ToList())
                {
                    var state = (DoorState)part.State;
                    if (state.IsOpen)
                    {
                        continue;
                    }
                    connections = connections ?? ServiceLocator.GetService<ConnectionsServerTracker>();
                    if (connections == null ||
                        !connections.TryGetFluidGroupCacheFromSocket(new EntityPartSocket(__instance.Id, part.Id, Pontoon.OxygenSocket), out FluidGroupServerState group) ||
                        group.FluidItemId != ItemsConstants.OxygenItemId || group.Available < Pontoon.OxygenToInflate)
                    {
                        continue;
                    }
                    group.RemoveFluid(Pontoon.OxygenToInflate);
                    __instance.UpdatePartState(part.Id, state with { IsOpen = true });
                    Melon<MiningMod>.Logger.Msg($"Pontoon (ship {__instance.Id}, part {part.Id}): inflating with oxygen");
                }
            }
            catch (Exception e)
            {
                if (!_loggedError)
                {
                    _loggedError = true;
                    Melon<MiningMod>.Logger.Error($"Pontoon error: {e}");
                }
            }
        }
    }
}
