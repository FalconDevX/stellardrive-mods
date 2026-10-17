using System;
using System.Collections.Generic;
using System.Linq;
using Core.Services;
using HarmonyLib;
using MelonLoader;
using Odyssey.Content;
using Ships;
using Ships.Cables;
using Ships.Cables.Power;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Model.Parts.State;
using UnityEngine;

namespace Odyssey.Server
{
    [HarmonyPatch(typeof(TrackedShipServer))]
    internal static class LaserCannonServer
    {
        private static readonly Dictionary<(uint ship, ushort part), string> Status = new Dictionary<(uint, ushort), string>();
        private static readonly Dictionary<(uint ship, ushort part), (float heat, bool overheated)> Heat = new Dictionary<(uint, ushort), (float, bool)>();

        private static bool Overheated(uint shipId, ushort partId) => Heat.TryGetValue((shipId, partId), out var h) && h.overheated;
        private static bool _loggedError;


        private static bool Triggered(ConnectionsServerTracker connections, uint shipId, ushort partId) =>
            connections != null && connections.GetSignalValue(new EntityPartSocket(shipId, partId, LaserCannon.SignalSocket)) > 0.0;

        [HarmonyPostfix]
        [HarmonyPatch(nameof(TrackedShipServer.PrepareConsumers))]
        private static void PrepareConsumers(TrackedShipServer __instance, float deltaTime)
        {
            List<ushort> ids = ShipPartIndex.Get(__instance, LaserCannon.PartId);
            if (ids.Count == 0)
            {
                return;
            }
            try
            {
                var power = ServiceCache.Get<PowerServerTracker>();
                var connections = ServiceCache.Get<ConnectionsServerTracker>();
                foreach (ushort id in ids)
                {
                    if (!__instance.TryGetStatefulPart(id, out StatefulPart part) || !(part.State is DoorState))
                    {
                        continue;
                    }
                    if (Triggered(connections, __instance.Id, part.Id) && !Overheated(__instance.Id, part.Id) &&
                        power.TryGetGroupFromSocket(new EntityPartSocket(__instance.Id, part.Id, LaserCannon.PowerSocket), out PowerGroupServerState group))
                    {
                        group.TotalPowerNeeded += deltaTime * LaserCannon.PowerWatts;
                    }
                }
            }
            catch (Exception e)
            {
                LogError(e);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(TrackedShipServer.UpdateParts))]
        private static void UpdateParts(TrackedShipServer __instance, float deltaTime)
        {
            List<ushort> ids = ShipPartIndex.Get(__instance, LaserCannon.PartId);
            if (ids.Count == 0)
            {
                return;
            }
            try
            {
                var power = ServiceCache.Get<PowerServerTracker>();
                var connections = ServiceCache.Get<ConnectionsServerTracker>();
                foreach (ushort id in ids)
                {
                    if (!__instance.TryGetStatefulPart(id, out StatefulPart part) || !(part.State is DoorState))
                    {
                        continue;
                    }
                    var key = (__instance.Id, part.Id);
                    Heat.TryGetValue(key, out var heat);
                    string status;
                    bool firing = false;
                    if (!Triggered(connections, __instance.Id, part.Id))
                    {
                        status = heat.overheated ? "cooling down" : "idle";
                    }
                    else if (heat.overheated)
                    {
                        status = "overheated";
                    }
                    else if (!power.TryGetGroupFromSocket(new EntityPartSocket(__instance.Id, part.Id, LaserCannon.PowerSocket), out PowerGroupServerState group))
                    {
                        status = "no power cable";
                    }
                    else if (group.NormalizedConsumerRate < 0.5f)
                    {
                        status = "not enough power";
                    }
                    else
                    {
                        status = "firing";
                        firing = true;
                    }
                    LaserHeat.Step(ref heat.heat, ref heat.overheated, firing, deltaTime);
                    if (heat.overheated)
                    {
                        firing = false;
                    }
                    if (heat.heat > 0f || heat.overheated)
                    {
                        Heat[key] = heat;
                    }
                    else
                    {
                        Heat.Remove(key);
                    }
                    var state = (DoorState)part.State;
                    if (state.IsOpen != firing)
                    {
                        __instance.UpdatePartState(part.Id, state with { IsOpen = firing });
                    }
                    if (!Status.TryGetValue(key, out string previous) || previous != status)
                    {
                        Status[key] = status;
                        Melon<GearMod>.Logger.Msg($"Laser Cannon (ship {key.Item1}, part {key.Item2}): {status}");
                    }
                }
            }
            catch (Exception e)
            {
                LogError(e);
            }
        }

        private static void LogError(Exception e)
        {
            if (!_loggedError)
            {
                _loggedError = true;
                Melon<GearMod>.Logger.Error($"Laser cannon error: {e}");
            }
        }
    }
}
