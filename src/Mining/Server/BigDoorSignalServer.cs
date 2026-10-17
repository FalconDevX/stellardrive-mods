using System;
using System.Collections.Generic;
using HarmonyLib;
using MelonLoader;
using Mining.Content;
using Ships;
using Ships.Cables;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Model.Parts.StateTypes;

namespace Mining.Server
{
    [HarmonyPatch(typeof(TrackedShipServer), nameof(TrackedShipServer.PrepareConsumers))]
    internal static class BigDoorSignalServer
    {
        private static readonly Dictionary<(uint ship, ushort part), bool> Last = new Dictionary<(uint, ushort), bool>();
        private static bool _loggedError;

        internal static void Reset() => Last.Clear();

        private static void Postfix(TrackedShipServer __instance)
        {
            try
            {
                ConnectionsServerTracker connections = null;
                foreach (var variant in BigDoors.Variants)
                {
                    List<ushort> ids = ShipPartIndex.Get(__instance, variant.id);
                    foreach (ushort id in ids)
                    {
                        connections = connections ?? ServiceCache.Get<ConnectionsServerTracker>();
                        if (connections == null)
                        {
                            return;
                        }
                        var key = (__instance.Id, id);
                        var front = new EntityPartSocket(__instance.Id, id, BigDoors.SignalSocket);
                        var back = new EntityPartSocket(__instance.Id, id, BigDoors.BackSignalSocket);
                        bool frontWired = connections.TryGetSignalConnectionGroupWithSocket(front, out _);
                        bool backWired = connections.TryGetSignalConnectionGroupWithSocket(back, out _);
                        if (!frontWired && !backWired)
                        {
                            Last.Remove(key);
                            continue;
                        }
                        bool open = (frontWired && connections.GetSignalValue(front) >= 0.5) || (backWired && connections.GetSignalValue(back) >= 0.5);
                        if (Last.TryGetValue(key, out bool previous) && previous == open)
                        {
                            continue;
                        }
                        Last[key] = open;
                        if (!__instance.State.TryGetPart(id, out StatefulPart part) || !(part.State is IDoorState door) || door.IsOpen == open)
                        {
                            continue;
                        }
                        if (open) __instance.OpenDoor(id);
                        else __instance.CloseDoor(id);
                    }
                }
            }
            catch (Exception e)
            {
                if (!_loggedError)
                {
                    _loggedError = true;
                    Melon<MiningMod>.Logger.Error($"Door signal: {e}");
                }
            }
        }
    }
}
