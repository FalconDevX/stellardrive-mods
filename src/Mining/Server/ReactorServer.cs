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
    // Reaktor ma stan generatora na drewno, więc obsługujemy tu oba (generator 1:1 jak w grze):
    //  - generator: drewno ze slotu, 4000 W,
    //  - reaktor: uran ze slotu (1 sztuka na MiningMod.ReactorSecondsPerUranium s przy pełnej mocy) + woda z rury do chłodzenia
    //    + sygnał włączenia (gniazdo 3, wartość > 0.5), MiningMod.ReactorPower W. Bez wody, uranu albo sygnału nie produkuje.
    [HarmonyPatch(typeof(PowerGeneratorUpdaters))]
    internal static class ReactorServer
    {
        private const float WoodPerSecond = 0.1f, WoodPower = 4000f;      // jak w grze (WoodPowerGeneratorState)
        private const float WaterPerSecond = 0.05f;                       // chłodzenie reaktora przy pełnej mocy
        private static bool _loggedError;

        [HarmonyPrefix]
        [HarmonyPatch(nameof(PowerGeneratorUpdaters.WriteProductionCapacity))]
        private static bool WriteCapacity(IShipStateWrite ____ship, PowerServerTracker ____power, float deltaTime)
        {
            Run(() =>
            {
                foreach (StatefulPart part in ____ship.GetAllStatefulParts<WoodPowerGeneratorState>())
                {
                    if (____power.TryGetGroupFromSocket(new EntityPartSocket(____ship.Id, part.Id, 0), out PowerGroupServerState group))
                    {
                        group.TotalPowerProductionCapacity += Capacity(____ship, part, deltaTime);
                    }
                }
            });
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(nameof(PowerGeneratorUpdaters.Update))]
        private static bool Update(IShipStateWrite ____ship, PowerServerTracker ____power, float deltaTime)
        {
            Run(() =>
            {
                foreach (StatefulPart part in ____ship.GetAllStatefulParts<WoodPowerGeneratorState>())
                {
                    if (!____power.TryGetGroupFromSocket(new EntityPartSocket(____ship.Id, part.Id, 0), out PowerGroupServerState group))
                    {
                        continue;
                    }
                    var state = (WoodPowerGeneratorState)part.State;
                    bool reactor = Reactor.Is(part.Settings);
                    float maxPower = deltaTime * (reactor ? MiningMod.ReactorPower.Value : WoodPower);
                    float level = Capacity(____ship, part, deltaTime) / maxPower * group.NormalizedGeneratorRate; // 0..1
                    float fuelPerSecond = reactor ? 1f / Mathf.Max(1f, MiningMod.ReactorSecondsPerUranium.Value) : WoodPerSecond;
                    uint fuelItem = reactor ? Uranium.ItemId : ItemsConstants.WoodItemId;
                    float left = Mathf.Max(Fuel(state, reactor) - level * deltaTime * fuelPerSecond, 0f);
                    int whole = Mathf.FloorToInt(left);
                    InventorySlot slot = whole > 0 ? new InventorySlot { HasItem = true, Quantity = (byte)whole, ItemId = fuelItem } : new InventorySlot { HasItem = false };
                    if (reactor && level > 0f && TryGetWater(____ship, part.Id, out FluidGroupServerState water))
                    {
                        water.RemoveFluid(Mathf.Min(level * WaterPerSecond * deltaTime, water.Available));
                    }
                    ____ship.UpdatePartState(part.Id, state with { ItemSlot = slot, NormalizedProductionLevel = level, PartialItemConsumptionLevel = left - whole });
                }
            });
            return false;
        }

        // Ile energii część może dać w tym kroku (jak CalculateProductionCapacity w grze + woda dla reaktora).
        private static float Capacity(IShipStateWrite ship, StatefulPart part, float deltaTime)
        {
            var state = (WoodPowerGeneratorState)part.State;
            bool reactor = Reactor.Is(part.Settings);
            float fuelPerSecond = reactor ? 1f / Mathf.Max(1f, MiningMod.ReactorSecondsPerUranium.Value) : WoodPerSecond;
            float need = deltaTime * fuelPerSecond;
            float factor = Mathf.Min(Fuel(state, reactor), need) / need;
            if (reactor && !SwitchedOn(ship, part.Id))
            {
                return 0f;
            }
            if (reactor)
            {
                float waterNeed = WaterPerSecond * deltaTime;
                factor = TryGetWater(ship, part.Id, out FluidGroupServerState water) ? Mathf.Min(factor, Mathf.Min(water.Available, waterNeed) / waterNeed) : 0f;
            }
            return factor * deltaTime * (reactor ? MiningMod.ReactorPower.Value : WoodPower);
        }

        // Paliwo w slocie: generator liczy każdy przedmiot jak drewno (jak w grze), reaktor tylko uran.
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

        private static void Run(Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                if (!_loggedError)
                {
                    _loggedError = true;
                    Melon<MiningMod>.Logger.Error($"Błąd generatora/reaktora: {e}");
                }
            }
        }
    }
}
