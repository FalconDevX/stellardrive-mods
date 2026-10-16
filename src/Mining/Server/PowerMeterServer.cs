using System;
using System.Collections.Generic;
using Core.Services;
using HarmonyLib;
using MelonLoader;
using Mining.Content;
using Ships;
using Ships.Cables.Power;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Model.Parts.State;
using UnityEngine;

namespace Mining.Server
{
    [HarmonyPatch(typeof(TrackedShipServer), nameof(TrackedShipServer.UpdateParts))]
    internal static class PowerMeterServer
    {
        private const float Interval = 0.25f;
        private static readonly Dictionary<uint, float> Next = new Dictionary<uint, float>();
        private static bool _loggedError;

        private static void Postfix(TrackedShipServer __instance)
        {
            try
            {
                if (Next.TryGetValue(__instance.Id, out float next) && Time.time < next)
                {
                    return;
                }
                PowerServerTracker power = null;
                foreach (ushort id in ShipPartIndex.Get(__instance, PowerMeter.PartId))
                {
                    if (!__instance.TryGetStatefulPart(id, out StatefulPart part) || !(part.State is ConstantState state))
                    {
                        continue;
                    }
                    power = power ?? ServiceCache.Get<PowerServerTracker>();
                    double load = 0.0;
                    if (power.TryGetGroupFromSocket(new EntityPartSocket(__instance.Id, id, PowerMeter.PowerSocket), out PowerGroupServerState group))
                    {
                        load = group.TotalPowerProductionCapacity > 0f ? group.TotalPowerNeeded / group.TotalPowerProductionCapacity * 100.0 : group.TotalPowerNeeded > 0f ? 100.0 : 0.0;
                    }
                    load = Math.Round(load, 1);
                    if (Math.Abs(state.ConstantValue - load) >= 0.05)
                    {
                        __instance.UpdatePartState(id, state with { ConstantValue = load });
                    }
                }
                Next[__instance.Id] = Time.time + Interval;
            }
            catch (Exception e)
            {
                if (!_loggedError)
                {
                    _loggedError = true;
                    Melon<MiningMod>.Logger.Error($"Power meter error: {e}");
                }
            }
        }
    }
}
