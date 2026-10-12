using System;
using System.Collections.Generic;
using Core.Services;
using Core.Values;
using HarmonyLib;
using MelonLoader;
using Mining.Content;
using Ships;
using Ships.Cables;
using Ships.Cables.Fluids;
using Ships.Cables.Power;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Model.Parts.State;
using Ships.Interface.Model.State;
using Ships.Services;
using UnityEngine;
using WorldTracking.Interface.Model.Spaces;
using WorldTracking.Interface.Services;

namespace Mining.Server
{
    [HarmonyPatch(typeof(TrackedShipServer))]
    internal static class WaterPumpServer
    {
        private const float PowerWatts = 800f;
        private const float AutoRaiseSeconds = 3f;

        private sealed class PumpRuntime
        {
            public float Deployed, OutOfReach;
            public string Status;
        }

        private static readonly Dictionary<(uint ship, ushort part), PumpRuntime> Runtime = new Dictionary<(uint, ushort), PumpRuntime>();
        private static bool _loggedError;

        internal static void Reset() => Runtime.Clear();

        [HarmonyPostfix]
        [HarmonyPatch(nameof(TrackedShipServer.PrepareConsumers))]
        private static void PrepareConsumers(TrackedShipServer __instance, float deltaTime)
        {
            Run(() =>
            {
                var power = ServiceLocator.GetService<PowerServerTracker>();
                var connections = ServiceLocator.GetService<ConnectionsServerTracker>();
                foreach (StatefulPart part in Pumps(__instance))
                {
                    if (Plan(__instance, connections, part, 0f, out _, out _, out _, out _) &&
                        power.TryGetGroupFromSocket(new EntityPartSocket(__instance.Id, part.Id, WaterPump.PowerSocket), out PowerGroupServerState group))
                    {
                        group.TotalPowerNeeded += deltaTime * PowerWatts;
                    }
                }
            });
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(TrackedShipServer.UpdateParts))]
        private static void UpdateParts(TrackedShipServer __instance, float deltaTime)
        {
            Run(() =>
            {
                var power = ServiceLocator.GetService<PowerServerTracker>();
                var connections = ServiceLocator.GetService<ConnectionsServerTracker>();
                foreach (StatefulPart part in Pumps(__instance))
                {
                    var key = (__instance.Id, part.Id);
                    if (!Runtime.TryGetValue(key, out PumpRuntime rt))
                    {
                        Runtime[key] = rt = new PumpRuntime();
                    }
                    var state = (DoorState)part.State;

                    float limit = WaterPump.HoseLimit(connections, __instance.Id, part.Id, out bool signalled, out float signal);
                    if (signalled)
                    {
                        bool wanted = signal > 0.01f;
                        if (wanted != state.IsOpen)
                        {
                            state = state with { IsOpen = wanted };
                            __instance.UpdatePartState(part.Id, state);
                        }
                    }
                    rt.Deployed = state.IsOpen ? rt.Deployed + deltaTime : 0f;

                    string status;
                    bool reach = Probe(__instance, part, out float needed, out bool water);
                    if (state.IsOpen && !signalled && (!reach || needed > limit + 2f))
                    {
                        rt.OutOfReach += deltaTime;
                        if (rt.OutOfReach >= AutoRaiseSeconds)
                        {
                            __instance.UpdatePartState(part.Id, state with { IsOpen = false });
                            rt.OutOfReach = 0f;
                        }
                    }
                    else
                    {
                        rt.OutOfReach = 0f;
                    }

                    bool planned = Plan(__instance, connections, part, rt.Deployed, out bool fromLake, out FluidGroupServerState input,
                        out FluidGroupServerState outA, out FluidGroupServerState outB);
                    PowerGroupServerState group = null;
                    if (!planned)
                    {
                        status = outA == null && outB == null ? "no room in the output pipes" :
                                 state.IsOpen && water && reach && needed <= limit ? "lowering the hose" :
                                 state.IsOpen && water && reach ? "hose too short to reach the water" :
                                 state.IsOpen ? "no water under the hose" : "hose up, no water on the input";
                    }
                    else if (!power.TryGetGroupFromSocket(new EntityPartSocket(__instance.Id, part.Id, WaterPump.PowerSocket), out group))
                    {
                        status = "no power cable";
                    }
                    else if (group.NormalizedConsumerRate <= 0f)
                    {
                        status = "not enough power";
                    }
                    else
                    {
                        float amount = Mathf.Max(0f, MiningMod.PumpRate.Value) * group.NormalizedConsumerRate * deltaTime;
                        if (!fromLake)
                        {
                            amount = Mathf.Min(amount, input.Available);
                        }
                        float moved = Deliver(outA, outB, amount);
                        if (!fromLake && moved > 0f)
                        {
                            input.RemoveFluid(moved);
                        }
                        status = fromLake ? "pumping from the lake" : "pumping from the input pipe";
                    }
                    if (rt.Status != status)
                    {
                        rt.Status = status;
                        Melon<MiningMod>.Logger.Msg($"Water Pump (ship {key.Item1}, part {key.Item2}): {status}");
                    }
                }
            });
        }

        private static bool Plan(TrackedShipServer ship, ConnectionsServerTracker connections, StatefulPart part, float deployed,
            out bool fromLake, out FluidGroupServerState input, out FluidGroupServerState outA, out FluidGroupServerState outB)
        {
            fromLake = false;
            input = null;
            outA = Output(ship, connections, part.Id, WaterPump.OutSocketA);
            outB = Output(ship, connections, part.Id, WaterPump.OutSocketB);
            if (ReferenceEquals(outA, outB))
            {
                outB = null;
            }
            if (outA == null && outB == null)
            {
                return false;
            }
            var state = (DoorState)part.State;
            float limit = WaterPump.HoseLimit(connections, ship.Id, part.Id, out _, out _);
            if (state.IsOpen && Probe(ship, part, out float needed, out bool water) && water && needed <= limit)
            {
                fromLake = deployed <= 0f ? RuntimeDeployed(ship, part) >= needed / WaterPump.HoseSpeed : deployed >= needed / WaterPump.HoseSpeed;
            }
            if (fromLake)
            {
                return true;
            }
            if (connections.TryGetFluidGroupCacheFromSocket(new EntityPartSocket(ship.Id, part.Id, WaterPump.InSocket), out FluidGroupServerState group) &&
                group.FluidItemId == HydrogenThruster.WaterItemId && group.Available > 0.001f && !ReferenceEquals(group, outA) && !ReferenceEquals(group, outB))
            {
                input = group;
                return true;
            }
            return false;
        }

        private static float RuntimeDeployed(TrackedShipServer ship, StatefulPart part) =>
            Runtime.TryGetValue((ship.Id, part.Id), out PumpRuntime rt) ? rt.Deployed : 0f;

        private static FluidGroupServerState Output(TrackedShipServer ship, ConnectionsServerTracker connections, ushort partId, byte socket)
        {
            return connections.TryGetFluidGroupCacheFromSocket(new EntityPartSocket(ship.Id, partId, socket), out FluidGroupServerState group) &&
                   (group.FluidItemId == HydrogenThruster.WaterItemId || group.FluidItemId == 0) && group.ReceivingCapacity > 0.0001f
                ? group
                : null;
        }

        private static float Deliver(FluidGroupServerState a, FluidGroupServerState b, float amount)
        {
            if (amount <= 0f)
            {
                return 0f;
            }
            float moved = 0f;
            if (a != null && b != null)
            {
                float half = amount / 2f;
                float toA = Mathf.Min(half, a.ReceivingCapacity), toB = Mathf.Min(half, b.ReceivingCapacity);
                float rest = amount - toA - toB;
                toA += Mathf.Min(rest, a.ReceivingCapacity - toA);
                rest = amount - toA - toB;
                toB += Mathf.Min(rest, b.ReceivingCapacity - toB);
                if (toA > 0f) { a.AddFluid(HydrogenThruster.WaterItemId, toA); moved += toA; }
                if (toB > 0f) { b.AddFluid(HydrogenThruster.WaterItemId, toB); moved += toB; }
                return moved;
            }
            FluidGroupServerState only = a ?? b;
            float q = Mathf.Min(amount, only.ReceivingCapacity);
            if (q > 0f)
            {
                only.AddFluid(HydrogenThruster.WaterItemId, q);
            }
            return q;
        }

        private static bool Probe(TrackedShipServer ship, StatefulPart part, out float needed, out bool water)
        {
            needed = float.MaxValue;
            water = false;
            var ships = ServiceLocator.GetService<ShipsServerTracker>();
            var spaces = ServiceLocator.GetService<ISpacesServerProvider>();
            var transpose = ServiceLocator.GetService<IWorldTransposeServer>();
            if (!ships.TryGetRootShip(ship.Id, out var root) || !(root is TrackedShipServer rootShip) ||
                !(spaces.GetSpace(rootShip.SpaceId) is PlanetSurfaceSpace surface))
            {
                return false;
            }
            Vector3Double inSurface = transpose.Position(ship.RelativeSpaceId, surface, (Vector3Double)part.LocalPosition);
            Vector3 planetPos = (Vector3)(surface.Center + inSurface);
            if (!WaterPump.TryProbe(surface.PlanetId, planetPos, out float above, out water))
            {
                return false;
            }
            needed = WaterPump.HoseNeeded(above, water);
            return true;
        }

        private static IEnumerable<StatefulPart> Pumps(TrackedShipServer ship)
        {
            foreach (StatefulPart part in ship.GetAllStatefulParts<DoorState>())
            {
                if (WaterPump.Is(part.Settings))
                {
                    yield return part;
                }
            }
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
                    Melon<MiningMod>.Logger.Error($"Pump error (server): {e}");
                }
            }
        }
    }
}
