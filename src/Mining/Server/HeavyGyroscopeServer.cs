using System;
using HarmonyLib;
using MelonLoader;
using Mining.Content;
using Ships.Cables.Power;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Model.Parts.State;
using Ships.Interface.Model.State;
using Ships.Parts.Gyroscope;
using Ships.Physics.Forces;
using UnityEngine;

namespace Mining.Server
{
    [HarmonyPatch(typeof(GyroscopesUpdater))]
    internal static class HeavyGyroscopeServer
    {
        private const byte PowerSocket = 3;
        private const float PowerPerStrength = 800f;
        private static float _deltaTime;
        private static uint _shipId;
        private static bool _loggedError;

        [HarmonyPrefix]
        [HarmonyPatch(nameof(GyroscopesUpdater.PrepareGyroscopesActionsBeforePowerLimits))]
        private static void BeforePrepare(float deltaTime) => _deltaTime = deltaTime;

        [HarmonyPrefix]
        [HarmonyPatch("CalculateShipGyros")]
        private static void BeforeShip(IShipStateWrite ship) => _shipId = ship.Id;

        [HarmonyPrefix]
        [HarmonyPatch("CalculateGyroStrength")]
        private static void BeforeStrength(StatefulPart part, ref float angularVelGainImpactOnShip)
        {
            if (HeavyGyroscope.Is(part?.Settings))
            {
                angularVelGainImpactOnShip *= HeavyGyroscope.Strength;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch("CalculateGyroStrength")]
        private static void AfterStrength(StatefulPart part, Vector3 __result)
        {
            if (!HeavyGyroscope.Is(part?.Settings))
            {
                return;
            }
            try
            {
                float strength = Mathf.Max(Mathf.Abs(__result.x), Mathf.Abs(__result.y), Mathf.Abs(__result.z));
                var power = ServiceCache.Get<PowerServerTracker>();
                if (strength > 0f && power != null && power.TryGetGroupFromSocket(new EntityPartSocket(_shipId, part.Id, PowerSocket), out PowerGroupServerState group))
                {
                    group.TotalPowerNeeded += (HeavyGyroscope.Strength - 1f) * strength * PowerPerStrength * _deltaTime;
                }
            }
            catch (Exception e)
            {
                if (!_loggedError)
                {
                    _loggedError = true;
                    Melon<MiningMod>.Logger.Error($"Heavy gyroscope power: {e}");
                }
            }
        }
    }

    [HarmonyPatch(typeof(ShipRotation), "CalculateShipRotationForce")]
    internal static class HeavyGyroscopeTorquePatch
    {
        private const float GyroStrength = 8000f;

        private static void Postfix(IShipStateRead stateRead, ref Vector3 __result)
        {
            if (stateRead == null)
            {
                return;
            }
            foreach (StatefulPart part in stateRead.GetAllStatefulParts<GyroscopeState>())
            {
                if (HeavyGyroscope.Is(part.Settings))
                {
                    __result += part.LocalRotation * ((GyroscopeState)part.State).RotationStrength * (GyroStrength * (HeavyGyroscope.Strength - 1f));
                }
            }
        }
    }
}
