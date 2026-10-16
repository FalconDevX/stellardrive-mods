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
using Ships.Parts.WoodPowerGenerator;
using UnityEngine;

namespace Mining.Server
{
    [HarmonyPatch(typeof(PowerGeneratorUpdaters))]
    internal static class ReactorServer
    {
        private const float WoodPerSecond = 0.1f, WoodPower = 4000f;
        private const float WaterPerSecond = 0.05f;
        private const float IdleLevel = 0.35f;
        private static bool _loggedError;
        private static readonly System.Collections.Generic.Dictionary<(uint, ushort), string> Reasons = new System.Collections.Generic.Dictionary<(uint, ushort), string>();

        [HarmonyPrefix]
        [HarmonyPatch(nameof(PowerGeneratorUpdaters.WriteProductionCapacity))]
        private static bool WriteCapacity(IShipStateWrite ____ship, PowerServerTracker ____power, float deltaTime)
        {
            try
            {
                foreach (StatefulPart part in ____ship.GetAllStatefulParts<WoodPowerGeneratorState>())
                {
                    if (____power.TryGetGroupFromSocket(new EntityPartSocket(____ship.Id, part.Id, 0), out PowerGroupServerState group))
                    {
                        group.TotalPowerProductionCapacity += Capacity(____ship, part, deltaTime);
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
        [HarmonyPatch(nameof(PowerGeneratorUpdaters.Update))]
        private static bool Update(IShipStateWrite ____ship, PowerServerTracker ____power, float deltaTime)
        {
            try
            {
                foreach (StatefulPart part in ____ship.GetAllStatefulParts<WoodPowerGeneratorState>())
                {
                    bool reactor = Reactor.Is(part.Settings);
                    bool grouped = ____power.TryGetGroupFromSocket(new EntityPartSocket(____ship.Id, part.Id, 0), out PowerGroupServerState group);
                    if (reactor)
                    {
                        Report(____ship, part, grouped);
                    }
                    if (!grouped)
                    {
                        continue;
                    }
                    var state = (WoodPowerGeneratorState)part.State;
                    float scale = Reactor.Scale(part.Settings);
                    float maxPower = deltaTime * (reactor ? MiningMod.ReactorPower.Value * scale : WoodPower);
                    float capacity = Capacity(____ship, part, deltaTime);
                    float level = capacity / maxPower * group.NormalizedGeneratorRate;
                    float shown = reactor && capacity > 0f ? IdleLevel + (1f - IdleLevel) * level : level;
                    float fuelPerSecond = reactor ? scale / Mathf.Max(1f, MiningMod.ReactorSecondsPerUranium.Value) : WoodPerSecond;
                    uint fuelItem = reactor ? Uranium.ItemId : ItemsConstants.WoodItemId;
                    float left = Mathf.Max(Fuel(state, reactor) - level * deltaTime * fuelPerSecond, 0f);
                    int whole = Mathf.FloorToInt(left);
                    InventorySlot slot = whole > 0 ? new InventorySlot { HasItem = true, Quantity = (byte)whole, ItemId = fuelItem } : new InventorySlot { HasItem = false };
                    if (reactor && level > 0f && TryGetWater(____ship, part.Id, out FluidGroupServerState water))
                    {
                        water.RemoveFluid(Mathf.Min(level * WaterPerSecond * scale * deltaTime, water.Available));
                    }
                    ____ship.UpdatePartState(part.Id, state with { ItemSlot = slot, NormalizedProductionLevel = shown, PartialItemConsumptionLevel = left - whole });
                }
            }
            catch (Exception e)
            {
                LogError(e);
            }
            return false;
        }

        private static float Capacity(IShipStateWrite ship, StatefulPart part, float deltaTime)
        {
            var state = (WoodPowerGeneratorState)part.State;
            bool reactor = Reactor.Is(part.Settings);
            float scale = Reactor.Scale(part.Settings);
            float fuelPerSecond = reactor ? scale / Mathf.Max(1f, MiningMod.ReactorSecondsPerUranium.Value) : WoodPerSecond;
            float need = deltaTime * fuelPerSecond;
            float factor = Mathf.Min(Fuel(state, reactor), need) / need;
            if (reactor && !SwitchedOn(ship, part.Id))
            {
                return 0f;
            }
            if (reactor)
            {
                float waterNeed = WaterPerSecond * scale * deltaTime;
                factor = TryGetWater(ship, part.Id, out FluidGroupServerState water) ? Mathf.Min(factor, Mathf.Min(water.Available, waterNeed) / waterNeed) : 0f;
            }
            return factor * deltaTime * (reactor ? MiningMod.ReactorPower.Value * scale : WoodPower);
        }

        private static void Report(IShipStateWrite ship, StatefulPart part, bool grouped)
        {
            var state = (WoodPowerGeneratorState)part.State;
            var connections = ServiceLocator.GetService<ConnectionsServerTracker>();
            var signal = new EntityPartSocket(ship.Id, part.Id, Reactor.SignalSocket);
            bool wired = Reactor.HasSignalSocket && connections.TryGetSignalConnectionGroupWithSocket(signal, out _);
            var problems = new System.Collections.Generic.List<string>();
            if (!grouped) problems.Add("power socket not connected");
            if (Fuel(state, true) <= 0f) problems.Add("no uranium");
            if (!TryGetWater(ship, part.Id, out _)) problems.Add("no water in the pipe");
            if (Reactor.HasSignalSocket && !wired) problems.Add("signal socket not wired");
            else if (Reactor.HasSignalSocket && connections.GetSignalValue(signal) <= 0.5f) problems.Add($"signal off ({connections.GetSignalValue(signal):0.##})");
            string reason = problems.Count == 0 ? "running" : string.Join(", ", problems);
            var key = (ship.Id, part.Id);
            if (!Reasons.TryGetValue(key, out string last) || last != reason)
            {
                Reasons[key] = reason;
                Melon<MiningMod>.Logger.Msg($"Reactor {part.Id} on ship {ship.Id}: {reason}");
            }
        }

        private static float Fuel(WoodPowerGeneratorState state, bool reactor)
        {
            if (reactor && state.ItemSlot.HasItem && state.ItemSlot.ItemId != Uranium.ItemId)
            {
                return state.PartialItemConsumptionLevel;
            }
            return state.GetWoodAvailable();
        }

        private static bool SwitchedOn(IShipStateWrite ship, ushort partId)
        {
            if (!Reactor.HasSignalSocket)
            {
                return true;
            }
            var connections = ServiceLocator.GetService<ConnectionsServerTracker>();
            var socket = new EntityPartSocket(ship.Id, partId, Reactor.SignalSocket);
            return connections.TryGetSignalConnectionGroupWithSocket(socket, out _) && connections.GetSignalValue(socket) > 0.5f;
        }

        private static bool TryGetWater(IShipStateWrite ship, ushort partId, out FluidGroupServerState water)
        {
            var connections = ServiceLocator.GetService<ConnectionsServerTracker>();
            return connections.TryGetFluidGroupCacheFromSocket(new EntityPartSocket(ship.Id, partId, Reactor.WaterSocket), out water) &&
                   water.FluidItemId == HydrogenThruster.WaterItemId && water.Available > 0.0001f;
        }

        private static void LogError(Exception e)
        {
            if (!_loggedError)
            {
                _loggedError = true;
                Melon<MiningMod>.Logger.Error($"Generator/reactor error: {e}");
            }
        }
    }
}
