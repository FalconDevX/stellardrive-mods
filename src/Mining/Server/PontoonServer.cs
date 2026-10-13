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
        private static readonly Dictionary<(uint ship, ushort part), float> Filled = new Dictionary<(uint, ushort), float>();
        private static readonly Dictionary<(uint ship, ushort part), string> Status = new Dictionary<(uint, ushort), string>();
        private static bool _loggedError;

        private static void Postfix(TrackedShipServer __instance, float deltaTime)
        {
            try
            {
                ConnectionsServerTracker connections = null;
                foreach (StatefulPart part in __instance.StatefulParts.Where(p => p?.Settings != null && p.Settings.id == Pontoon.PartId && p.State is DoorState).ToList())
                {
                    var key = (__instance.Id, part.Id);
                    var state = (DoorState)part.State;
                    if (state.IsOpen)
                    {
                        Filled.Remove(key);
                        Report(key, "inflated");
                        continue;
                    }
                    connections = connections ?? ServiceLocator.GetService<ConnectionsServerTracker>();
                    if (connections == null ||
                        !connections.TryGetFluidGroupCacheFromSocket(new EntityPartSocket(__instance.Id, part.Id, Pontoon.OxygenSocket), out FluidGroupServerState group))
                    {
                        Report(key, "deflated, no pipe on the oxygen inlet");
                        continue;
                    }
                    if (group.FluidItemId != ItemsConstants.OxygenItemId)
                    {
                        Report(key, group.FluidItemId == 0 ? "deflated, the pipe is empty" : $"deflated, the pipe carries item {group.FluidItemId}, not oxygen");
                        continue;
                    }
                    float filled = Filled.TryGetValue(key, out float f) ? f : 0f;
                    float take = Mathf.Min(group.Available, MaxIntakePerSecond * deltaTime, Pontoon.OxygenToInflate - filled);
                    if (take > 0f)
                    {
                        group.RemoveFluid(take);
                        filled += take;
                    }
                    Filled[key] = filled;
                    Report(key, "taking oxygen");
                    if (filled >= Pontoon.OxygenToInflate - 0.0001f)
                    {
                        Filled.Remove(key);
                        __instance.UpdatePartState(part.Id, state with { IsOpen = true });
                        Report(key, "inflating");
                    }
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
