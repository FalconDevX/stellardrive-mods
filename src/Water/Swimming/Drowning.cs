using System.Collections.Generic;
using Core.Values;
using HarmonyLib;
using Players;
using Players.Interface.Model;
using Players.Interface.Values;
using Players.Network;
using Players.Network.Model;
using UnityEngine;
using Water.Buoyancy;
using WorldTracking.Interface.Model.Spaces;
using WorldTracking.Interface.Services;

namespace Water.Swimming
{
    internal static class Drowning
    {
        private const float HeadDepth = 1.8f;
        private static readonly Dictionary<PlayerStatusUpdater, LakeCache> Lakes = new Dictionary<PlayerStatusUpdater, LakeCache>();

        internal static bool IsHeadUnderwater(PlayerStatusUpdater updater, PlayerNetworkState state, ISpacesProvider spaces)
        {
            if (!WaterMod.Enabled.Value || WaterMod.UnderwaterAirSeconds.Value <= 0f || state == null || spaces == null || !WaterLevel.Ready())
            {
                return false;
            }
            PlayerPhysicsState physics = state.GetPhysicsState(asServer: true);
            if (!(spaces.GetSpace(physics.SpaceId) is PlanetSurfaceSpace planet) || planet.PlanetId != WaterLevel.PlanetId)
            {
                return false;
            }
            Vector3Double position = planet.Center + (Vector3Double)physics.Position;
            float depth = WaterLevel.SeaRadius - (float)position.Magnitude;
            if (depth < HeadDepth)
            {
                return false;
            }
            if (!Lakes.TryGetValue(updater, out LakeCache lake))
            {
                lake = new LakeCache();
                Lakes[updater] = lake;
            }
            return WaterLevel.IsLake(position, lake);
        }
    }

    [HarmonyPatch(typeof(PlayerStatusUpdater), "TryTakeOxygen")]
    internal static class DrowningOxygenPatch
    {
        private static void Postfix(PlayerStatusUpdater __instance, PlayerNetworkState ____state, ISpacesProvider ____spaces, ref float __result)
        {
            if (__result > 0f && Drowning.IsHeadUnderwater(__instance, ____state, ____spaces))
            {
                __result = 0f;
            }
        }
    }

    [HarmonyPatch(typeof(PlayerStatusUpdater), nameof(PlayerStatusUpdater.UpdateStatus))]
    internal static class DrowningDrainPatch
    {
        private static void Postfix(PlayerStatusUpdater __instance, PlayerNetworkState ____state, PlayerStatusSettings ____settings, ISpacesProvider ____spaces, float deltaTime)
        {
            if (____state == null || ____settings == null || ____state.ClientState.Type == PlayerStateType.Dead || ____state.OxygenLevel <= 0f)
            {
                return;
            }
            if (!Drowning.IsHeadUnderwater(__instance, ____state, ____spaces))
            {
                return;
            }
            float drain = ____settings.maxOxygenTank / WaterMod.UnderwaterAirSeconds.Value * deltaTime;
            ____state.ServerSetOxygenTankLevel(Mathf.Max(0f, ____state.OxygenLevel - drain));
        }
    }
}
