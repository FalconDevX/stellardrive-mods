using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Managers.Interface.Model;
using Managers.Server.Services;
using MelonLoader;
using Ships;
using Ships.Interface.Model.Placement;
using Ships.Interface.Services;
using Ships.Interface.Settings;
using Ships.Services;
using Tools.Build.Model;
using Tools.Build.PlacementSnapDetection;
using UnityEngine;

namespace Odyssey.Patches
{
    [HarmonyPatch]
    internal static class HullPlacementGuardPatch
    {
        private static readonly AccessTools.FieldRef<ShipInteractionService, ShipsServerTracker> ShipsRef =
            AccessTools.FieldRefAccess<ShipInteractionService, ShipsServerTracker>("_ships");
        private static readonly AccessTools.FieldRef<ShipInteractionService, PartSettingsProvider> PartsRef =
            AccessTools.FieldRefAccess<ShipInteractionService, PartSettingsProvider>("_partSettingsProvider");
        private static int _logged;

        private static MethodBase TargetMethod() =>
            typeof(ShipInteractionService).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                .First(m => m.Name.StartsWith("RpcLogic___PlaceHullPart_ServerRPC", StringComparison.Ordinal));

        private static bool Prefix(ShipInteractionService __instance, object[] __args)
        {
            try
            {
                if (!(__args[0] is PlaceHullPartNetworkRequest request))
                {
                    return true;
                }
                TrackedShipServer ship = ShipsRef(__instance)?.GetShip(request.ShipId);
                PartSettings settings = PartsRef(__instance)?.GetPartSettingById(request.SettingsId);
                if (ship == null || !(settings is HullPartSettings hull))
                {
                    return true;
                }
                HullPartPlacement placement = request.Placement;
                foreach (Vector3Int coord in placement.CalculateAllCoords(hull.size))
                {
                    if (ship.GetVoxel(coord)?.GetPartFromType(placement.Position.Type).HasValue == true)
                    {
                        if (_logged++ < 20)
                        {
                            Melon<TweaksMod>.Logger.Msg($"Blocked '{hull.fullLabel}' on ship {ship.Id} at {placement.Coord}: {coord} is already taken");
                        }
                        return false;
                    }
                }
            }
            catch (Exception e)
            {
                Melon<TweaksMod>.Logger.Warning($"Hull placement check: {e.Message}");
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(PlacementSnapDetector), "DetectPlacement")]
    internal static class HullPreviewGuardPatch
    {
        private static void Postfix(PlacementSnapDetectionProperties props, ref IPartPlacement __result)
        {
            if (!(__result is HullPartPlacement placement) || !(props.PartSettings is HullPartSettings hull) || props.ShipParts == null)
            {
                return;
            }
            if (hull.size.x <= 1f && hull.size.y <= 1f && hull.size.z <= 1f)
            {
                return;
            }
            foreach (Vector3Int coord in placement.CalculateAllCoords(hull.size))
            {
                if (!props.ShipParts.IsHullPartAvailable(coord, placement.Position.Type))
                {
                    __result = null;
                    return;
                }
            }
        }
    }
}
