using System.Reflection;
using HarmonyLib;
using Items.Model;
using Odyssey.Content;
using Ships.Cables;
using Ships.Interface.Model;
using Ships.Interface.Model.Gases;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Model.Parts.State;
using Ships.Interface.Model.State;
using Ships.Parts.FluidPump;
using Ships.Parts.GasExhaust;
using Ships.Parts.GasIntake;
using UnityEngine;

namespace Odyssey.Server
{
    [HarmonyPatch(typeof(GasExhaustsUpdater), "CalculateNewState")]
    internal static class ScaledVentPatch
    {
        private static readonly MethodInfo FindRoom = AccessTools.Method(typeof(GasExhaustsUpdater), "FindRoomAtExhaust");

        private static bool Prefix(GasExhaustsUpdater __instance, StatefulPart gasExhaust, float deltaTime, IShipStateWrite ____ship, ConnectionsServerTracker ____connections, ref GasExhaustState __result)
        {
            float rate = ScaledVariants.Rate(gasExhaust.Settings);
            if (rate <= 1f || FindRoom == null)
            {
                return true;
            }
            var socket = new EntityPartSocket(____ship.Id, gasExhaust.Id, 0);
            if (!____connections.TryGetFluidGroupCacheFromSocket(socket, out var group) || group.FluidItemId != ItemsConstants.OxygenItemId || group.InitialTotalFluid <= 0f)
            {
                __result = new GasExhaustState { FluidItemId = 0u, OutputRate = 0f };
                return false;
            }
            var room = (RoomSampleResult)FindRoom.Invoke(__instance, new object[] { gasExhaust });
            RoomGasState gas = default;
            float room_space = 0f;
            if (!room.IsRoom)
            {
                room_space = 100f * rate;
            }
            else if (____ship.TryGetRoomGasState(room.RoomId, out gas))
            {
                room_space = Mathf.Max(1f - gas.GasState.GasDensity, 0f) * gas.RoomVolume;
            }
            float flow = Mathf.Min(0.5f * rate * deltaTime, Mathf.Min(group.Available, room_space));
            group.RemoveFluid(flow);
            if (room.IsRoom)
            {
                GasState before = gas.GasState;
                gas.GasState = new GasState { GasItemId = before.GasItemId, GasDensity = before.GasDensity + flow / gas.RoomVolume };
                ____ship.UpdateRoomGasState(room.RoomId, gas);
            }
            __result = new GasExhaustState { FluidItemId = group.FluidItemId, OutputRate = flow / deltaTime / rate };
            return false;
        }
    }

    [HarmonyPatch(typeof(GasIntakesUpdater), "CalculateNewState")]
    internal static class ScaledIntakePatch
    {
        private static void Postfix(StatefulPart part, float deltaTime, IShipStateWrite ____ship, ConnectionsServerTracker ____connections, ref GasIntakeState __result)
        {
            float rate = ScaledVariants.Rate(part.Settings);
            if (rate <= 1f || __result == null || __result.IntakeRate <= 0f ||
                !____connections.TryGetFluidGroupCacheFromSocket(new EntityPartSocket(____ship.Id, part.Id, 0), out var group))
            {
                return;
            }
            float extra = Mathf.Min(__result.IntakeRate * deltaTime * (rate - 1f), group.ReceivingCapacity);
            if (extra <= 0f)
            {
                return;
            }
            group.AddFluid(ItemsConstants.OxygenItemId, extra);
            __result = __result with { FluidItemId = group.FluidItemId, IntakeRate = __result.IntakeRate + extra / deltaTime };
        }
    }

    [HarmonyPatch(typeof(FluidPumpsServerUpdater), nameof(FluidPumpsServerUpdater.Update))]
    internal static class ScaledPumpPatch
    {
        private static bool Prefix(IShipStateWrite ____ship, ConnectionsServerTracker ____connections, float deltaTime)
        {
            foreach (StatefulPart part in ____ship.GetAllStatefulParts<FluidPumpState>())
            {
                float signal = (float)____connections.GetSignalValue(new EntityPartSocket(____ship.Id, part.Id, 0));
                if (signal <= 0f ||
                    !____connections.TryGetFluidGroupCacheFromSocket(new EntityPartSocket(____ship.Id, part.Id, 2), out var from) ||
                    !____connections.TryGetFluidGroupCacheFromSocket(new EntityPartSocket(____ship.Id, part.Id, 3), out var to) ||
                    from == to || from.FluidItemId == 0 || (to.FluidItemId != 0 && from.FluidItemId != to.FluidItemId))
                {
                    ____ship.UpdatePartState(part.Id, new FluidPumpState { PumpRate = 0f });
                    continue;
                }
                float full = 5f * ScaledVariants.Rate(part.Settings) * deltaTime;
                float moved = Mathf.Max(Mathf.Min(from.Available, to.ReceivingCapacity, signal * full), 0f);
                to.AddFluid(from.FluidItemId, moved);
                from.RemoveFluid(moved);
                ____ship.UpdatePartState(part.Id, new FluidPumpState { PumpRate = moved / full });
            }
            return false;
        }
    }
}
