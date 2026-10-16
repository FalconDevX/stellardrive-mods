using System;
using Core.Services;
using HarmonyLib;
using Items.Model;
using MelonLoader;
using Players.Interface.Services;
using Ships.Interface.Model.Parts;
using Ships.Interface.Services;
using Tools.Build;
using UI.Research;
using UnityEngine;

namespace Mining.Blueprints
{
    [HarmonyPatch(typeof(ItemSettingsList), "ResetDict")]
    internal static class BlueprintToolItemPatch
    {
        private static void Prefix(ref ItemSettings[] ___items)
        {
            ___items = BlueprintToolItem.EnsureIn(___items);
            Content.PaintToolModel.Apply(___items);
        }
    }

    [HarmonyPatch(typeof(GenericPartOpenableMenuActivator), nameof(GenericPartOpenableMenuActivator.ShowMenu))]
    internal static class WorkbenchOpenPatch
    {
        private static void Prefix()
        {
            try
            {
                var players = ServiceCache.Get<IPlayersProviderClient>();
                var ships = ServiceCache.Get<IShipsClientProvider>();
                WorkbenchUi.Pending = players != null && ships != null && players.TryGetLocalPlayerState(out var state)
                    && ships.TryGetStatefulPart(new PartContext(state.EntityId, state.PartId), out StatefulPart part)
                    && part.Settings != null && part.Settings.id == Workbench.PartId;
            }
            catch (Exception e)
            {
                Melon<MiningMod>.Logger.Error($"Workbench menu: {e}");
            }
        }

        private static void Postfix() => WorkbenchUi.Pending = false;
    }

    [HarmonyPatch(typeof(BuildToolLocalClient), "PrimaryActionBegin")]
    internal static class BuildHologramPatch
    {
        private static bool Prefix()
        {
            try
            {
                Camera cam = Camera.main;
                if (cam == null || BlueprintGhosts.All.Count == 0)
                {
                    return true;
                }
                BlueprintGhost ghost = BlueprintGhosts.Aimed(new Ray(cam.transform.position, cam.transform.forward), 40f);
                if (ghost == null)
                {
                    return true;
                }
                if (BlueprintServer.Build(ghost.Data, ghost.Target, out string message))
                {
                    BlueprintGhosts.Remove(ghost);
                }
                MiningMod.ShowMessage(message, 6f);
                return false;
            }
            catch (Exception e)
            {
                Melon<MiningMod>.Logger.Error($"Building a blueprint: {e}");
                MiningMod.ShowMessage("Could not build the blueprint - see the log", 5f);
                return false;
            }
        }
    }
}
