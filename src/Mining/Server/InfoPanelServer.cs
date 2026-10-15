using System;
using System.Linq;
using Core.Services;
using HarmonyLib;
using MelonLoader;
using Mining.Content;
using Ships;
using Ships.Cables.Power;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.Common;
using Ships.Interface.Model.Parts.State;

namespace Mining.Server
{
    [HarmonyPatch(typeof(TrackedShipServer))]
    internal static class InfoPanelServer
    {
        private static bool _loggedError;

        [HarmonyPostfix]
        [HarmonyPatch(nameof(TrackedShipServer.PrepareConsumers))]
        private static void PrepareConsumers(TrackedShipServer __instance, float deltaTime)
        {
            try
            {
                PowerServerTracker power = null;
                foreach (InfoPanels.Variant variant in InfoPanels.Variants)
                {
                    foreach (ushort id in ShipPartIndex.Get(__instance, variant.Id))
                    {
                        power = power ?? ServiceLocator.GetService<PowerServerTracker>();
                        if (power.TryGetGroupFromSocket(new EntityPartSocket(__instance.Id, id, variant.PowerSocket), out PowerGroupServerState group))
                        {
                            group.TotalPowerNeeded += deltaTime * variant.Watts;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                LogError(e);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(TrackedShipServer.UpdateParts))]
        private static void UpdateParts(TrackedShipServer __instance)
        {
            try
            {
                PowerServerTracker power = null;
                foreach (InfoPanels.Variant variant in InfoPanels.Variants)
                {
                    foreach (ushort id in ShipPartIndex.Get(__instance, variant.Id))
                    {
                        if (!__instance.TryGetStatefulPart(id, out StatefulPart part) || !(part.State is DoorState || part.State is BeaconState))
                        {
                            continue;
                        }
                        power = power ?? ServiceLocator.GetService<PowerServerTracker>();
                        Update(__instance, power, part, variant);
                    }
                }
            }
            catch (Exception e)
            {
                LogError(e);
            }
        }

        private static void Update(TrackedShipServer ship, PowerServerTracker power, StatefulPart part, InfoPanels.Variant variant)
        {
            bool powered = power.TryGetGroupFromSocket(new EntityPartSocket(ship.Id, part.Id, variant.PowerSocket), out PowerGroupServerState group) &&
                           group.NormalizedConsumerRate >= 0.5f;
            if (part.State is BeaconState beacon)
            {
                string name = InfoPanels.Encode(powered, InfoPanels.Clean(InfoPanels.Decode(beacon.Name).text));
                if (beacon.Name != name)
                {
                    ship.UpdatePartState(part.Id, beacon with { Name = name });
                }
            }
            else
            {
                ship.UpdatePartState(part.Id, new BeaconState { IsInUse = false, Name = InfoPanels.Encode(powered, "") });
            }
        }

        private static void LogError(Exception e)
        {
            if (!_loggedError)
            {
                _loggedError = true;
                Melon<MiningMod>.Logger.Error($"Info panel error: {e}");
            }
        }
    }
}
