using System;
using System.Collections.Generic;
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
using UnityEngine;

namespace Mining.Server
{
    [HarmonyPatch(typeof(TrackedShipServer), nameof(TrackedShipServer.UpdateParts))]
    internal static class PontoonServer
    {
        private const float MaxIntakePerSecond = 2f;
        private static readonly Dictionary<(uint ship, ushort part), float> Air = new Dictionary<(uint, ushort), float>();
        private static readonly Dictionary<(uint ship, ushort part), string> Status = new Dictionary<(uint, ushort), string>();
        private static bool _loggedError;

        private static void Postfix(TrackedShipServer __instance, float deltaTime)
        {
            try
            {
                ConnectionsServerTracker connections = null;
                foreach (ushort id in ShipPartIndex.Get(__instance, Pontoon.PartId))
                {
                    if (!__instance.TryGetStatefulPart(id, out StatefulPart part) || !(part.State is DoorState))
                    {
                        continue;
                    }
                    var key = (__instance.Id, part.Id);
                    var state = (DoorState)part.State;
                    float air = Air.TryGetValue(key, out float a) ? a : state.IsOpen ? Pontoon.OxygenToInflate : 0f;
                    if (state.IsOpen)
                    {
                        air -= Mathf.Max(0f, MiningMod.PontoonLeakPerMinute.Value) / 60f * deltaTime;
                    }
                    connections = connections ?? ServiceCache.Get<ConnectionsServerTracker>();
                    string supply = Supply(__instance.Id, part.Id, connections, out FluidGroupServerState group);
                    if (group != null)
                    {
                        float take = Mathf.Min(group.Available, MaxIntakePerSecond * deltaTime, Pontoon.OxygenToInflate - air);
                        if (take > 0f)
                        {
                            group.RemoveFluid(take);
                            air += take;
                        }
                    }
                    if (state.IsOpen && air <= 0f)
                    {
                        Air[key] = 0f;
                        __instance.UpdatePartState(part.Id, state with { IsOpen = false });
                        Report(key, "out of air, deflating");
                        continue;
                    }
                    Air[key] = air;
                    if (!state.IsOpen && air >= Pontoon.OxygenToInflate - 0.0001f)
                    {
                        __instance.UpdatePartState(part.Id, state with { IsOpen = true });
                        Report(key, "inflating");
                        continue;
                    }
                    Report(key, state.IsOpen ? (group != null ? "inflated, topped up with oxygen" : $"inflated, leaking ({supply})") : group != null ? "taking oxygen" : $"deflated, {supply}");
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

        private static string Supply(uint ship, ushort part, ConnectionsServerTracker connections, out FluidGroupServerState group)
        {
            group = null;
            if (connections == null ||
                !connections.TryGetFluidGroupCacheFromSocket(new EntityPartSocket(ship, part, Pontoon.OxygenSocket), out FluidGroupServerState found))
            {
                return "no pipe on the oxygen inlet";
            }
            if (found.FluidItemId != ItemsConstants.OxygenItemId)
            {
                return found.FluidItemId == 0 ? "the pipe is empty" : $"the pipe carries item {found.FluidItemId}, not oxygen";
            }
            group = found;
            return "oxygen supplied";
        }

        private static void Report((uint ship, ushort part) key, string status)
        {
            if (Status.TryGetValue(key, out string previous) && previous == status)
            {
                return;
            }
            Status[key] = status;
            Melon<MiningMod>.Logger.Msg($"Pontoon (ship {key.ship}, part {key.part}): {status}");
        }
    }
}
