using System;
using HarmonyLib;
using MelonLoader;
using Mining.Content;
using Ships.Cables;
using Ships.Cables.Fluids;
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
    // (tlen + etanol) go odrzuca i ustawia mu ciąg 0. Po zwykłej aktualizacji silników liczymy nasz: wodór jako paliwo,
    // woda do chłodzenia (zużywana wolniej). Ciąg jest mocniejszy niż chemiczny (MiningMod.HydrogenThrustMultiplier).
    [HarmonyPatch(typeof(ShipThrustersUpdater))]
    internal static class HydrogenThrusterServer
    {
        private const float HydrogenPerSecond = 0.05f; // jak etanol w silniku chemicznym
        private const float WaterPerSecond = 0.02f;    // chłodzenie
        private static bool _loggedError;

        [HarmonyPostfix]
        [HarmonyPatch(nameof(ShipThrustersUpdater.WriteFluidTargetConsumption))]
        private static void WriteTargets(IShipStateWrite ____ship, ConnectionsServerTracker ____connections, float deltaTime)
        {
            Run(() =>
            {
                foreach (StatefulPart part in ____ship.GetAllStatefulParts<ThrusterState>())
                {
                    if (HydrogenThruster.Is(part.Settings) && TryGetGroups(____ship, ____connections, part.Id, out var hydrogen, out var water))
                    {
                        hydrogen.AddConsumptionTarget(HydrogenPerSecond * deltaTime);
                        water.AddConsumptionTarget(WaterPerSecond * deltaTime);
                    }
                }
            });
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(ShipThrustersUpdater.Update))]
        private static void Update(IShipStateWrite ____ship, ConnectionsServerTracker ____connections, float deltaTime)
        {
            Run(() =>
            {
                foreach (StatefulPart part in ____ship.GetAllStatefulParts<ThrusterState>())
                {
                    if (!HydrogenThruster.Is(part.Settings))
                    {
                        continue;
                    }
                    float rate = 0f;
                    if (TryGetGroups(____ship, ____connections, part.Id, out var hydrogen, out var water))
                    {
                        rate = Mathf.Min(hydrogen.EffectiveConsumptionRate, water.EffectiveConsumptionRate);
                        if (rate > 0f)
                        {
                            hydrogen.RemoveFluid(rate * HydrogenPerSecond * deltaTime);
                            water.RemoveFluid(rate * WaterPerSecond * deltaTime);
                        }
                    }
                    ____ship.UpdatePartState(part.Id, (ThrusterState)part.State with { ThrusterStrength = rate });
                }
            });
        }

        // Gniazda 0 i 1: jedno z wodorem, drugie z wodą (w dowolnej kolejności).
        private static bool TryGetGroups(IShipStateWrite ship, ConnectionsServerTracker connections, ushort partId, out FluidGroupServerState hydrogen, out FluidGroupServerState water)
        {
            hydrogen = water = null;
            if (!connections.TryGetFluidGroupCacheFromSocket(new EntityPartSocket(ship.Id, partId, 0), out var a) ||
                !connections.TryGetFluidGroupCacheFromSocket(new EntityPartSocket(ship.Id, partId, 1), out var b) || a == b)
            {
                return false;
            }
            if (a.FluidItemId == HydrogenThruster.HydrogenItemId && b.FluidItemId == HydrogenThruster.WaterItemId)
            {
                hydrogen = a;
                water = b;
            }
            else if (b.FluidItemId == HydrogenThruster.HydrogenItemId && a.FluidItemId == HydrogenThruster.WaterItemId)
            {
                hydrogen = b;
                water = a;
            }
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
            if (stateRead == null || Mathf.Approximately(extra, 0f))
            {
                return;
            }
            foreach (StatefulPart part in stateRead.GetAllStatefulParts<ThrusterState>())
            {
                if (HydrogenThruster.Is(part.Settings))
                {
                    __result += -part.Placement.UpOrientation.GetVector3() * (((ThrusterState)part.State).ThrusterStrength * extra);
                }
            }
        }
    }
}
