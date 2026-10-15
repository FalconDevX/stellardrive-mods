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
        }
    }

    [HarmonyPatch(typeof(GenericPartOpenableMenuActivator), nameof(GenericPartOpenableMenuActivator.ShowMenu))]
    internal static class WorkbenchOpenPatch
    {
        private static bool Prefix()
        {
            try
            {
                var players = ServiceLocator.GetService<IPlayersProviderClient>();
                var ships = ServiceLocator.GetService<IShipsClientProvider>();
                if (players == null || ships == null || !players.TryGetLocalPlayerState(out var state))
                {
                    return true;
                }
                if (!ships.TryGetStatefulPart(new PartContext(state.EntityId, state.PartId), out StatefulPart part) || part.Settings == null || part.Settings.id != Workbench.PartId)
                {
                    return true;
                }
                WorkbenchMenu.Open();
                return false;
            }
            catch (Exception e)
            {
                Melon<MiningMod>.Logger.Error($"Workbench menu: {e}");
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(GenericPartOpenableMenuActivator), nameof(GenericPartOpenableMenuActivator.HideMenu))]
    internal static class WorkbenchHidePatch
    {
        private static void Postfix() => WorkbenchMenu.Hide();
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
