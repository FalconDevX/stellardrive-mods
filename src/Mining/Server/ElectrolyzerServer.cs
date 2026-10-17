using System;
using Core.Services;
using HarmonyLib;
using Items.Model;
using MelonLoader;
using Mining.Content;
using Ships.Cables;
using Ships.Cables.Fluids;
using Ships.Cables.Power;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Model.Parts.State;
using Ships.Interface.Model.State;
using Ships.Parts.Electrolyzer;
using UnityEngine;

namespace Mining.Server
{
    [HarmonyPatch(typeof(ElectrolyzersUpdater))]
    internal static class ElectrolyzerServer
    {
        private const float PowerWatts = 2000f;
        private const float IcePerSecond = 0.25f;
        private const float WaterPerSecond = 0.5f;
        private const float HydrogenPerSecond = 0.5f;
        private const float OxygenPerSecond = 0.25f;
        private const float WaterFromIcePerSecond = 0.25f;
        private static bool _loggedError;

        [HarmonyPrefix]
        [HarmonyPatch(nameof(ElectrolyzersUpdater.WritePowerRequest))]
        private static bool WritePowerRequest(IShipStateWrite ____ship, float deltaTime)
        {
            try
            {
                var power = ServiceCache.Get<PowerServerTracker>();
                var connections = ServiceCache.Get<ConnectionsServerTracker>();
                foreach (StatefulPart part in ____ship.GetAllStatefulParts<ElectrolyzerState>())
                {
                    if (CanRun(____ship, connections, part, out _) &&
                        power.TryGetGroupFromSocket(new EntityPartSocket(____ship.Id, part.Id, ElectrolyzerPart.PowerSocket), out PowerGroupServerState group))
                    {
                        group.TotalPowerNeeded += deltaTime * PowerWatts * ScaledVariants.Rate(part.Settings);
                    }
                }
            }
            catch (Exception e)
            {
                LogError(e);
            }
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(ElectrolyzersUpdater.Update))]
        private static bool Update(IShipStateWrite ____ship, float deltaTime)
        {
            try
            {
                var power = ServiceCache.Get<PowerServerTracker>();
                var connections = ServiceCache.Get<ConnectionsServerTracker>();
                foreach (StatefulPart part in ____ship.GetAllStatefulParts<ElectrolyzerState>())
                {
                    var state = (ElectrolyzerState)part.State;
                    float rate = 0f;
                    if (CanRun(____ship, connections, part, out bool fromIce) &&
                        power.TryGetGroupFromSocket(new EntityPartSocket(____ship.Id, part.Id, ElectrolyzerPart.PowerSocket), out PowerGroupServerState group))
                    {
                        rate = group.NormalizedConsumerRate;
                    }
                    if (rate > 0f)
                    {
                        state = Produce(____ship, connections, part, state, rate * deltaTime * ScaledVariants.Rate(part.Settings), fromIce);
                    }
                    ____ship.UpdatePartState(part.Id, state with { NormalizedProductionRate = rate });
                }
            }
            catch (Exception e)
            {
                LogError(e);
            }
            return false;
        }

        private static bool CanRun(IShipStateWrite ship, ConnectionsServerTracker connections, StatefulPart part, out bool fromIce)
        {
            var state = (ElectrolyzerState)part.State;
            fromIce = state.GetIceAvailable() > 0.001f;
            var signal = new EntityPartSocket(ship.Id, part.Id, ElectrolyzerPart.SignalSocket);
            if (connections.TryGetSignalConnectionGroupWithSocket(signal, out _) && connections.GetSignalValue(signal) <= 0.0)
            {
                return false;
            }
            if (!TryGetOutput(ship, connections, part.Id, ElectrolyzerPart.HydrogenSocket, HydrogenThruster.HydrogenItemId, out _))
            {
                return false;
            }
            return fromIce || TryGetWaterInput(ship, connections, part.Id, out _);
        }

        private static ElectrolyzerState Produce(IShipStateWrite ship, ConnectionsServerTracker connections, StatefulPart part, ElectrolyzerState state, float amount, bool fromIce)
        {
            if (fromIce)
            {
                float ice = Mathf.Min(IcePerSecond * amount, state.GetIceAvailable());
                float scale = ice / IcePerSecond;
                float left = state.GetIceAvailable() - ice;
                int whole = Mathf.FloorToInt(left);
                state = state with
                {
                    IceItemSlot = whole > 0 ? new InventorySlot { HasItem = true, ItemId = ItemsConstants.IceItemId, Quantity = (byte)whole } : default(InventorySlot),
                    PartialItemConsumptionLevel = left - whole,
                };
                AddTo(ship, connections, part.Id, ElectrolyzerPart.HydrogenSocket, HydrogenThruster.HydrogenItemId, HydrogenPerSecond * scale);
                AddTo(ship, connections, part.Id, ElectrolyzerPart.OxygenSocket, ItemsConstants.OxygenItemId, OxygenPerSecond * scale);
                AddTo(ship, connections, part.Id, ElectrolyzerPart.WaterSocket, HydrogenThruster.WaterItemId, WaterFromIcePerSecond * scale);
            }
            else if (TryGetWaterInput(ship, connections, part.Id, out FluidGroupServerState water))
            {
                float used = Mathf.Min(WaterPerSecond * amount, water.Available);
                float scale = used / WaterPerSecond;
                water.RemoveFluid(used);
                AddTo(ship, connections, part.Id, ElectrolyzerPart.HydrogenSocket, HydrogenThruster.HydrogenItemId, HydrogenPerSecond * scale);
                AddTo(ship, connections, part.Id, ElectrolyzerPart.OxygenSocket, ItemsConstants.OxygenItemId, OxygenPerSecond * scale);
            }
            return state;
        }

        private static bool TryGetOutput(IShipStateWrite ship, ConnectionsServerTracker connections, ushort partId, byte socket, uint itemId, out FluidGroupServerState group)
        {
            return connections.TryGetFluidGroupCacheFromSocket(new EntityPartSocket(ship.Id, partId, socket), out group) &&
                   (group.FluidItemId == itemId || group.FluidItemId == 0) && group.ReceivingCapacity > 0f;
        }

        private static bool TryGetWaterInput(IShipStateWrite ship, ConnectionsServerTracker connections, ushort partId, out FluidGroupServerState group)
        {
            return connections.TryGetFluidGroupCacheFromSocket(new EntityPartSocket(ship.Id, partId, ElectrolyzerPart.WaterSocket), out group) &&
                   group.FluidItemId == HydrogenThruster.WaterItemId && group.Available > 0.001f;
        }

        private static void AddTo(IShipStateWrite ship, ConnectionsServerTracker connections, ushort partId, byte socket, uint itemId, float quantity)
        {
            if (quantity > 0f && TryGetOutput(ship, connections, partId, socket, itemId, out FluidGroupServerState group))
            {
                group.AddFluid(itemId, Mathf.Min(quantity, group.ReceivingCapacity));
            }
        }

        private static void LogError(Exception e)
        {
            if (!_loggedError)
            {
                _loggedError = true;
                Melon<MiningMod>.Logger.Error($"Electrolyzer error: {e}");
            }
        }
    }
}
