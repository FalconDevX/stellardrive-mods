using System.Reflection;
using HarmonyLib;
using Items.Model;
using Mining.Content;
using Ships.Cables;
using Ships.Interface.Model;
using Ships.Interface.Model.Gases;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Model.Parts.State;
using Ships.Interface.Model.State;
using Ships.Parts.GasExhaust;
using UnityEngine;

namespace Mining.Server
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
}
