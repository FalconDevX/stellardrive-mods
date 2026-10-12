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
    // Silnik wodorowy ma stan silnika chemicznego, więc gra traktuje go jak zwykły silnik - tylko jej sprawdzenie paliwa
    // (tlen + etanol) go odrzuca i ustawia mu ciąg 0. Po zwykłej aktualizacji silników liczymy nasz. Gniazda 0 i 1: wodór
    // i (opcjonalnie) tlen w dowolnej kolejności, gniazdo 2: prąd.
    //  - wodór + prąd: jądrowo-termiczny (prąd rozgrzewa wodór),
    //  - wodór + tlen: spalanie (bez prądu),
    //  - wodór + tlen + prąd: dopalanie tlenem, ciąg x OxygenBoost.
    // Ciąg mocniejszy niż chemiczny (MiningMod.HydrogenThrustMultiplier).
    [HarmonyPatch(typeof(ShipThrustersUpdater))]
    internal static class HydrogenThrusterServer
    {
        private const float HydrogenPerSecond = 0.03f;
        private const float OxygenPerSecond = 0.03f;
        internal const float OxygenBoost = 1.4f;
        private static bool _loggedError;

        // Silniki z dopalaniem w tym kroku (do liczenia siły ciągu).
        internal static readonly HashSet<(uint ship, ushort part)> Boosted = new HashSet<(uint, ushort)>();

        [HarmonyPostfix]
        [HarmonyPatch(nameof(ShipThrustersUpdater.WriteFluidTargetConsumption))]
        private static void WriteTargets(IShipStateWrite ____ship, ConnectionsServerTracker ____connections, float deltaTime)
        {
            Run(() =>
            {
                var power = ServiceLocator.GetService<PowerServerTracker>();
                foreach (StatefulPart part in ____ship.GetAllStatefulParts<ThrusterState>())
                {
                    if (!HydrogenThruster.Is(part.Settings) || !TryGetFluids(____ship, ____connections, part.Id, out var hydrogen, out var oxygen))
                    {
                        continue;
                    }
                    hydrogen.AddConsumptionTarget(HydrogenPerSecond * deltaTime);
                    oxygen?.AddConsumptionTarget(OxygenPerSecond * deltaTime);
                    if (power.TryGetGroupFromSocket(new EntityPartSocket(____ship.Id, part.Id, HydrogenThruster.PowerSocket), out PowerGroupServerState group))
                    {
                        group.TotalPowerNeeded += deltaTime * HydrogenThruster.PowerWatts;
                    }
                }
            });
        }

        private delegate bool TryGroups(ShipThrustersUpdater updater, ushort partId, out FluidGroupServerState group0, out FluidGroupServerState group1);

        private static readonly TryGroups GameGroups =
            AccessTools.MethodDelegate<TryGroups>(AccessTools.Method(typeof(ShipThrustersUpdater), "TryGetValidFluidGroups"));

        // Zamiast aktualizacji gry (która najpierw dawała naszym silnikom ciąg 0, a my potem właściwy - dwie zmiany stanu
        // w każdym kroku restartowały dźwięk silnika): zwykłe silniki dokładnie jak w grze, wodorowe po naszemu, jeden zapis.
        [HarmonyPrefix]
        [HarmonyPatch(nameof(ShipThrustersUpdater.Update))]
        private static bool Update(ShipThrustersUpdater __instance, IShipStateWrite ____ship, ConnectionsServerTracker ____connections, float deltaTime)
        {
            bool done = false;
            Run(() =>
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
                    bool boosted = false;
                    if (TryGetFluids(____ship, ____connections, part.Id, out var hydrogen, out var oxygen))
                    {
                        float powerRate = power.TryGetGroupFromSocket(new EntityPartSocket(____ship.Id, part.Id, HydrogenThruster.PowerSocket), out PowerGroupServerState group)
                            ? group.NormalizedConsumerRate : 0f;
                        float oxygenRate = oxygen?.EffectiveConsumptionRate ?? 0f;
                        rate = Mathf.Min(hydrogen.EffectiveConsumptionRate, Mathf.Max(powerRate, oxygenRate));
                        if (rate > 0f)
                        {
                            hydrogen.RemoveFluid(rate * HydrogenPerSecond * deltaTime);
                            float oxygenUsed = Mathf.Min(rate, oxygenRate);
                            if (oxygenUsed > 0f)
                            {
                                oxygen.RemoveFluid(oxygenUsed * OxygenPerSecond * deltaTime);
                            }
                            boosted = powerRate > 0f && oxygenRate > 0f;
                        }
                    }
                    if (boosted) Boosted.Add((____ship.Id, part.Id));
                    else Boosted.Remove((____ship.Id, part.Id));
                    ____ship.UpdatePartState(part.Id, (ThrusterState)part.State with { ThrusterStrength = rate });
                }
                done = true;
            });
            return !done; // błąd w naszym kodzie - niech zadziała aktualizacja gry
        }

        // Gniazda 0 i 1: wodór (wymagany) i tlen (opcjonalny), w dowolnej kolejności.
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
                    Melon<MiningMod>.Logger.Error($"Błąd silnika wodorowego: {e}");
                }
            }
        }
    }

    // Ciąg: gra liczy 7800 x przepływ dla każdego silnika - silnikom wodorowym dokładamy różnicę do mnożnika.
    [HarmonyPatch(typeof(ShipThrust), "CalculateShipThrustForce")]
    internal static class HydrogenThrustForcePatch
    {
        private const float ChemicalThrust = 7800f;

        private static void Postfix(IShipStateRead __0, ref Vector3 __result)
        {
            float extra = (MiningMod.HydrogenThrustMultiplier.Value - 1f) * ChemicalThrust;
            IShipStateRead stateRead = __0;
            if (stateRead == null)
            {
                return;
            }
            foreach (StatefulPart part in stateRead.GetAllStatefulParts<ThrusterState>())
            {
                if (HydrogenThruster.Is(part.Settings))
                {
                    float total = extra;
                    if (HydrogenThrusterServer.Boosted.Contains((stateRead.Id, part.Id)))
                    {
                        total += (HydrogenThrusterServer.OxygenBoost - 1f) * MiningMod.HydrogenThrustMultiplier.Value * ChemicalThrust;
                    }
                    __result += -part.Placement.UpOrientation.GetVector3() * (((ThrusterState)part.State).ThrusterStrength * total);
                }
            }
        }
    }
}
