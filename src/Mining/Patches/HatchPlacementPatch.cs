using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Managers.Interface.Model;
using Managers.Server.Services;
using MelonLoader;
using Mining.Content;
using Players;
using Players.Services;
using Ships;
using Ships.Interface.Services;
using Ships.Services;
using Ships.Interface.Model;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Placement;
using Ships.Interface.Settings;
using Tools.Build;
using Tools.Build.Model;
using Tools.Build.PlacementSnapDetection;
using Tools.Build.SnapDetectors.Model;
using Tools.Build.SnapDetectors.Wrappers;
using Items.Model;
using UnityEngine;

namespace Mining.Patches
{
    internal static class HatchPlacement
    {
        internal static PartSettings Current;

        internal static HullPartPlacement Fix(HullPartPlacement placement, PartSettings part)
        {
            int n = Mathf.RoundToInt(part.size.x);
            var shift = new Vector3Int((n - 1) / 2, 0, (n - 1) / 2);
            return new HullPartPlacement(placement.Coord - shift, VoxelPartType.Floor, PartOrientation.Up, PartOrientation.Right);
        }
    }

    [HarmonyPatch(typeof(PartPreviewDetection), nameof(PartPreviewDetection.CalculateSnapDetectionUpdate))]
    internal static class HatchPreviewPartPatch
    {
        private static void Prefix(SnapDetectionState state) => HatchPlacement.Current = state.partSettings;
    }

    [HarmonyPatch(typeof(HorizontalFloor), nameof(HorizontalFloor.DetectSnapPlacement))]
    internal static class HatchHitPlacementPatch
    {
        private static void Postfix(ref IPartPlacement __result)
        {
            PartSettings part = HatchPlacement.Current;
            if (__result is HullPartPlacement placement && placement.Position.Type == VoxelPartType.Floor && BigDoors.IsHatch(part))
            {
                __result = HatchPlacement.Fix(placement, part);
            }
        }
    }

    [HarmonyPatch(typeof(PlacementSnapDetector), "GetHullPlacementFromProps")]
    internal static class HatchPositionPlacementPatch
    {
        private static void Postfix(PlacementSnapDetectionProperties props, ref HullPartPlacement? __result)
        {
            if (__result.HasValue && __result.Value.Position.Type == VoxelPartType.Floor && props != null && BigDoors.IsHatch(props.PartSettings))
            {
                __result = HatchPlacement.Fix(__result.Value, props.PartSettings);
            }
        }
    }

    [HarmonyPatch(typeof(PartPreviewDetection), "TryFindSnapInRayHit")]
    internal static class HatchAimPatch
    {
        private static bool Prefix(SnapDetectionState state, RaycastHit hit, ref Pivot? pivot, ref IPartPlacement partPlacement, ref bool __result)
        {
            PartSettings part = state.partSettings;
            if (!BigDoors.IsHatch(part) || hit.collider == null)
            {
                return true;
            }
            IShipPartPlacements ship = hit.collider.GetComponentInParent<IShipPartPlacements>();
            if (ship == null)
            {
                return true;
            }
            PartHit local = ship.ConvertHitToLocalHit(hit);
            if (Mathf.Abs(local.Normal.y) < 0.7f)
            {
                return true;
            }
            Vector3 p = local.Pos - local.Normal * 0.05f;
            var centre = new Vector3Int(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y + 0.5f), Mathf.RoundToInt(p.z));
            pivot = null;
            partPlacement = HatchPlacement.Fix(new HullPartPlacement(centre, VoxelPartType.Floor, PartOrientation.Up, PartOrientation.Right), part);
            __result = true;
            return false;
        }
    }

    [HarmonyPatch]
    internal static class HatchReplaceFloorPatch
    {
        private static readonly AccessTools.FieldRef<ShipInteractionService, PlayersServerTracker> Players = AccessTools.FieldRefAccess<ShipInteractionService, PlayersServerTracker>("_players");
        private static readonly AccessTools.FieldRef<ShipInteractionService, ShipsServerTracker> Ships = AccessTools.FieldRefAccess<ShipInteractionService, ShipsServerTracker>("_ships");
        private static readonly AccessTools.FieldRef<ShipInteractionService, PartSettingsProvider> Settings = AccessTools.FieldRefAccess<ShipInteractionService, PartSettingsProvider>("_partSettingsProvider");

        private static readonly Func<TrackedShip, Ships.Grid.ShipGrid> GridOf =
            AccessTools.MethodDelegate<Func<TrackedShip, Ships.Grid.ShipGrid>>(AccessTools.PropertyGetter(typeof(TrackedShip), "Grid"));

        private static MethodBase Target() => AccessTools.FirstMethod(typeof(ShipInteractionService), m => m.Name.StartsWith("RpcLogic___PlaceHullPart_ServerRPC"));

        private static bool Prepare() => Target() != null;

        private static MethodBase TargetMethod() => Target();

        private static bool Prefix(ShipInteractionService __instance, PlaceHullPartNetworkRequest __0, FishNet.Connection.NetworkConnection __1)
        {
            try
            {
                PartSettings part = Settings(__instance)?.GetPartSettingById(__0.SettingsId);
                if (!BigDoors.IsHatch(part) || __1 == null)
                {
                    return true;
                }
                TrackedPlayerServer player = Players(__instance)?.GetTrackedPlayerFromConnectionId(__1.ClientId);
                TrackedShipServer ship = Ships(__instance)?.GetShip(__0.ShipId);
                if (player == null || ship == null)
                {
                    return true;
                }
                if (!player.IsUsingCheatFreeBuild && !player.HasItemsInInventory(part.BuildingCost))
                {
                    return true;
                }
                var floors = new List<HullPartPosition>();
                foreach (Vector3Int coord in __0.Placement.CalculateAllCoords(part.size))
                {
                    var position = new HullPartPosition(coord, VoxelPartType.Floor);
                    if (ship.TryGetHullPartState(position, out _))
                    {
                        floors.Add(position);
                    }
                    else if (GridOf(ship)?.IsPartOccupied(position) == true)
                    {
                        Melon<MiningMod>.Logger.Msg($"{part.fullLabel}: another part is in the way at {coord} - not placed");
                        return false;
                    }
                }
                var refund = new List<ItemDrop>();
                foreach (HullPartPosition position in floors)
                {
                    if (ship.TryGetHullPartState(position, out HullPartNetworkedState floor))
                    {
                        PartSettings removed = Settings(__instance).GetPartSettingById(floor.PartId);
                        ship.RemoveHullPart(position);
                        if (removed != null) refund.AddRange(removed.BuildingCost);
                    }
                }
                if (!player.IsUsingCheatFreeBuild && refund.Count > 0)
                {
                    player.AddItemsToInventory(refund);
                }
                if (floors.Count > 0)
                {
                    Melon<MiningMod>.Logger.Msg($"{part.fullLabel}: replaced {floors.Count} floor tiles");
                }
            }
            catch (Exception e)
            {
                Melon<MiningMod>.Logger.Warning($"Hatch placement: {e.Message}");
            }
            return true;
        }
    }
}
