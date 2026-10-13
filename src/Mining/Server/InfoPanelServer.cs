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
            Run(() =>
            {
                var power = ServiceLocator.GetService<PowerServerTracker>();
                foreach (StatefulPart part in __instance.StatefulParts.Where(p => InfoPanels.Is(p?.Settings)))
                {
                    if (power.TryGetGroupFromSocket(new EntityPartSocket(__instance.Id, part.Id, InfoPanels.PowerSocket), out PowerGroupServerState group))
                    {
                        group.TotalPowerNeeded += deltaTime * InfoPanels.Find(part.Settings.id).Watts;
                    }
                }
            });
        }

        [HarmonyPostfix]
        [HarmonyPatch(nameof(TrackedShipServer.UpdateParts))]
        private static void UpdateParts(TrackedShipServer __instance)
        {
            Run(() =>
            {
                var power = ServiceLocator.GetService<PowerServerTracker>();
                foreach (StatefulPart part in __instance.StatefulParts.Where(p => InfoPanels.Is(p?.Settings) && p.State is DoorState).ToList())
                {
                    bool powered = power.TryGetGroupFromSocket(new EntityPartSocket(__instance.Id, part.Id, InfoPanels.PowerSocket), out PowerGroupServerState group) &&
                                   group.NormalizedConsumerRate >= 0.5f;
                    var state = (DoorState)part.State;
                    if (state.IsOpen != powered)
                    {
                        __instance.UpdatePartState(part.Id, state with { IsOpen = powered });
                    }
                }
            });
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
                    Melon<MiningMod>.Logger.Error($"Info panel error: {e}");
                }
            }
        }
    }
}
