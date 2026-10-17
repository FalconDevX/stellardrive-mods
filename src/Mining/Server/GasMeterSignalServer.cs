using System;
using System.Collections.Generic;
using Core.Services;
using HarmonyLib;
using MelonLoader;
using Mining.Content;
using Ships;
using Ships.Interface.Model.Gases;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.State;
using Ships.Interface.Values;
using UnityEngine;
using WorldProvision.Interface.Services;

namespace Mining.Server
{
    [HarmonyPatch(typeof(TrackedShipServer), nameof(TrackedShipServer.UpdateParts))]
    internal static class GasMeterSignalServer
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
                Next[__instance.Id] = Time.time + Interval;
                IWorldEnvironmentServerProvider environment = null;
                foreach (ushort id in ShipPartIndex.Get(__instance, GasMeterSignal.PartId))
                {
                    if (!__instance.TryGetStatefulPart(id, out StatefulPart part) || !(part.State is ConstantState state))
                    {
                        continue;
                    }
                    Vector3 position = part.LocalPosition + part.LocalRotation * Vector3.back / ShipRoomsConstants.RoomVoxelResolution;
                    float density;
                    if (__instance.TryGetRoomGasStateAtPosition(position, out RoomGasState gas))
                    {
                        density = gas.RoomVolume > 0f ? gas.OxygenAmount / gas.RoomVolume : 0f;
                    }
                    else
                    {
                        environment = environment ?? ServiceLocator.GetService<IWorldEnvironmentServerProvider>();
                        density = environment != null ? environment.GetAtmosphereDensityAtPosition(__instance.RelativeSpaceId, position) : 0f;
                    }
                    double value = Math.Round(density, 2);
                    if (Math.Abs(state.ConstantValue - value) >= 0.005)
                    {
                        __instance.UpdatePartState(id, state with { ConstantValue = value });
                    }
                }
            }
            catch (Exception e)
            {
                if (!_loggedError)
                {
                    _loggedError = true;
                    Melon<MiningMod>.Logger.Error($"Gas meter signal error: {e}");
                }
            }
        }
    }
}
