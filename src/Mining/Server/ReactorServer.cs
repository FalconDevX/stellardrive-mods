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
        private static readonly System.Collections.Generic.Dictionary<(uint, ushort), int> Masks = new System.Collections.Generic.Dictionary<(uint, ushort), int>();

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
            var connections = ServiceCache.Get<ConnectionsServerTracker>();
            var signal = new EntityPartSocket(ship.Id, part.Id, Reactor.SignalSocket);
            bool wired = Reactor.HasSignalSocket && connections.TryGetSignalConnectionGroupWithSocket(signal, out _);
            double value = wired ? connections.GetSignalValue(signal) : 0.0;
            int mask = (grouped ? 0 : 1) | (Fuel(state, true) <= 0f ? 2 : 0) | (TryGetWater(ship, part.Id, out _) ? 0 : 4)
                       | (Reactor.HasSignalSocket && !wired ? 8 : 0) | (wired && value <= 0.5 ? 16 + (int)(Math.Round(value * 100.0) * 32) : 0);
            var key = (ship.Id, part.Id);
            if (Masks.TryGetValue(key, out int last) && last == mask)
            {
                return;
            }
            Masks[key] = mask;
            var problems = new System.Collections.Generic.List<string>();
            if ((mask & 1) != 0) problems.Add("power socket not connected");
            if ((mask & 2) != 0) problems.Add("no uranium");
            if ((mask & 4) != 0) problems.Add("no water in the pipe");
            if ((mask & 8) != 0) problems.Add("signal socket not wired");
            if ((mask & 16) != 0) problems.Add($"signal off ({value:0.##})");
            string reason = problems.Count == 0 ? "running" : string.Join(", ", problems);
            Melon<MiningMod>.Logger.Msg($"Reactor {part.Id} on ship {ship.Id}: {reason}");
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
            var connections = ServiceCache.Get<ConnectionsServerTracker>();
            var socket = new EntityPartSocket(ship.Id, partId, Reactor.SignalSocket);
            return connections.TryGetSignalConnectionGroupWithSocket(socket, out _) && connections.GetSignalValue(socket) > 0.5f;
        }

        private static bool TryGetWater(IShipStateWrite ship, ushort partId, out FluidGroupServerState water)
        {
            var connections = ServiceCache.Get<ConnectionsServerTracker>();
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
