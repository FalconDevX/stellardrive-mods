using System;
using System.Collections.Generic;
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
using Ships.Interface.Model.Placement;
using Ships.Interface.Model.State;
using Ships.Parts.Thruster;
using Ships.Physics.Forces;
using UnityEngine;

namespace Mining.Server
{
    [HarmonyPatch(typeof(ShipThrustersUpdater))]
    internal static class HydrogenThrusterServer
    {
        private const float HydrogenPerSecond = 0.05f;
        private const float OxygenPerSecond = 0.05f;
        private static bool _loggedError;

        private static readonly Dictionary<(uint ship, ushort part), string> Status = new Dictionary<(uint, ushort), string>();

        [HarmonyPostfix]
        [HarmonyPatch(nameof(ShipThrustersUpdater.WriteFluidTargetConsumption))]
        private static void WriteTargets(IShipStateWrite ____ship, ConnectionsServerTracker ____connections, float deltaTime)
        {
            try
            {
                var power = ServiceLocator.GetService<PowerServerTracker>();
                foreach (StatefulPart part in ____ship.GetAllStatefulParts<ThrusterState>())
                {
                    if (!HydrogenThruster.Is(part.Settings) || !TryGetFluids(____ship, ____connections, part.Id, out var hydrogen, out var oxygen))
                    {
                        continue;
                    }
                    float scale = HydrogenThruster.Scale(part.Settings);
                    hydrogen.AddConsumptionTarget(HydrogenPerSecond * scale * deltaTime);
                    oxygen?.AddConsumptionTarget(OxygenPerSecond * scale * deltaTime);
                    if (power.TryGetGroupFromSocket(new EntityPartSocket(____ship.Id, part.Id, HydrogenThruster.PowerSocket), out PowerGroupServerState group))
                    {
                        group.TotalPowerNeeded += deltaTime * HydrogenThruster.PowerWatts * scale;
                    }
                }
            }
            catch (Exception e)
            {
                LogError(e);
            }
        }

        private delegate bool TryGroups(ShipThrustersUpdater updater, ushort partId, out FluidGroupServerState group0, out FluidGroupServerState group1);

        private static readonly TryGroups GameGroups =
            AccessTools.MethodDelegate<TryGroups>(AccessTools.Method(typeof(ShipThrustersUpdater), "TryGetValidFluidGroups"));

        [HarmonyPrefix]
        [HarmonyPatch(nameof(ShipThrustersUpdater.Update))]
        private static bool Update(ShipThrustersUpdater __instance, IShipStateWrite ____ship, ConnectionsServerTracker ____connections, float deltaTime)
        {
            bool done = false;
            try
            {
                var power = ServiceLocator.GetService<PowerServerTracker>();
                foreach (StatefulPart part in ____ship.GetAllStatefulParts<ThrusterState>())
                {
                    if (!HydrogenThruster.Is(part.Settings))
                    {
                        float strength = 0f;
                        if (GameGroups(__instance, part.Id, out var group0, out var group1))
                        {
                            strength = Mathf.Min(group0.EffectiveConsumptionRate, group1.EffectiveConsumptionRate);
                            if (strength > 0f)
                            {
                                group0.RemoveFluid(strength * ThrusterState.MaxFluidConsumptionRate * deltaTime);
                                group1.RemoveFluid(strength * ThrusterState.MaxFluidConsumptionRate * deltaTime);
                            }
                        }
                        ____ship.UpdatePartState(part.Id, (ThrusterState)part.State with { ThrusterStrength = strength });
                        continue;
                    }
                    float rate = 0f;
                    string status = "no hydrogen";
                    if (TryGetFluids(____ship, ____connections, part.Id, out var hydrogen, out var oxygen))
                    {
                        bool cabled = power.TryGetGroupFromSocket(new EntityPartSocket(____ship.Id, part.Id, HydrogenThruster.PowerSocket), out PowerGroupServerState group);
                        float powerRate = cabled ? group.NormalizedConsumerRate : 0f;
                        status = oxygen == null ? "no oxygen" : !cabled ? "no power cable" : powerRate <= 0f ? "no power" : null;
                        if (oxygen != null)
                        {
                            rate = Mathf.Min(Mathf.Min(hydrogen.EffectiveConsumptionRate, oxygen.EffectiveConsumptionRate), powerRate);
                        }
                        if (rate > 0f)
                        {
                            float scale = HydrogenThruster.Scale(part.Settings);
                            hydrogen.RemoveFluid(rate * HydrogenPerSecond * scale * deltaTime);
                            oxygen.RemoveFluid(rate * OxygenPerSecond * scale * deltaTime);
                        }
                        if (status == null && rate <= 0f)
                        {
                            status = hydrogen.EffectiveConsumptionRate <= 0f ? "hydrogen pipe empty" : "oxygen pipe empty";
                        }
                    }
                    if (status == null) status = "running";
                    if (!Status.TryGetValue((____ship.Id, part.Id), out string previous) || previous != status)
                    {
                        Status[(____ship.Id, part.Id)] = status;
                        Melon<MiningMod>.Logger.Msg($"Hydrogen Thruster (ship {____ship.Id}, part {part.Id}): {status}");
                    }
                    ____ship.UpdatePartState(part.Id, (ThrusterState)part.State with { ThrusterStrength = rate });
                }
                done = true;
            }
            catch (Exception e)
            {
                LogError(e);
            }
            return !done;
        }

        private static bool TryGetFluids(IShipStateWrite ship, ConnectionsServerTracker connections, ushort partId,
            out FluidGroupServerState hydrogen, out FluidGroupServerState oxygen)
        {
            hydrogen = oxygen = null;
            for (byte socket = 0; socket < 2; socket++)
            {
                if (!connections.TryGetFluidGroupCacheFromSocket(new EntityPartSocket(ship.Id, partId, socket), out var group))
                {
                    continue;
                }
                if (group.FluidItemId == HydrogenThruster.HydrogenItemId && hydrogen == null) hydrogen = group;
                else if (group.FluidItemId == ItemsConstants.OxygenItemId && oxygen == null) oxygen = group;
            }
            if (ReferenceEquals(hydrogen, oxygen)) oxygen = null;
            return hydrogen != null;
        }

        private static void LogError(Exception e)
        {
            if (!_loggedError)
            {
                _loggedError = true;
                Melon<MiningMod>.Logger.Error($"Hydrogen thruster error: {e}");
            }
        }
    }

    [HarmonyPatch(typeof(ShipThrust), "CalculateShipThrustForce")]
    internal static class HydrogenThrustForcePatch
    {
        private const float ChemicalThrust = 7800f;

        private static void Postfix(IShipStateRead __0, ref Vector3 __result)
        {
            float multiplier = MiningMod.HydrogenThrustMultiplier.Value;
            IShipStateRead stateRead = __0;
            if (stateRead == null)
            {
                return;
            }
            foreach (StatefulPart part in stateRead.GetAllStatefulParts<ThrusterState>())
            {
                if (HydrogenThruster.Is(part.Settings))
                {
                    float extra = (multiplier * HydrogenThruster.Scale(part.Settings) - 1f) * ChemicalThrust;
                    __result += -part.Placement.UpOrientation.GetVector3() * (((ThrusterState)part.State).ThrusterStrength * extra);
                }
            }
        }
    }
}
