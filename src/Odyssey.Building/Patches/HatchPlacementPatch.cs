using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Managers.Interface.Model;
using Managers.Server.Services;
using MelonLoader;
using Odyssey.Content;
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

namespace Odyssey.Patches
{
    internal static class HatchPlacement
    {
        internal static PartSettings Current;

        internal static HullPartPlacement Fix(HullPartPlacement placement, PartSettings part, PartOrientation up = PartOrientation.Right)
        {
            int n = Mathf.RoundToInt(part.size.x);
            var shift = new Vector3Int((n - 1) / 2, 0, (n - 1) / 2);
            return new HullPartPlacement(placement.Coord - shift, VoxelPartType.Floor, PartOrientation.Up, up);
        }

        internal static PartOrientation UpFrom(Quaternion localRotation)
        {
            Vector3 up = localRotation * Vector3.up;
            up.y = 0f;
            if (up.sqrMagnitude < 0.01f) return PartOrientation.Right;
            if (Mathf.Abs(up.x) >= Mathf.Abs(up.z)) return up.x >= 0f ? PartOrientation.Right : PartOrientation.Left;
            return up.z >= 0f ? PartOrientation.Forward : PartOrientation.Back;
        }

        internal static Vector3 Shift(PartSettings part, Quaternion localRotation)
        {
            Bounds b = part.Bounds;
            float minX = float.MaxValue, minZ = float.MaxValue;
            foreach (float x in new[] { b.min.x, b.max.x })
            {
                foreach (float y in new[] { b.min.y, b.max.y })
                {
                    Vector3 v = localRotation * new Vector3(x, y, b.center.z);
                    minX = Mathf.Min(minX, v.x);
                    minZ = Mathf.Min(minZ, v.z);
                }
            }
            return new Vector3(-0.5f - minX, 0f, -0.5f - minZ);
        }
    }

    [HarmonyPatch(typeof(PartPreviewDetection), nameof(PartPreviewDetection.CalculateSnapDetectionUpdate))]
    internal static class HatchPreviewPartPatch
    {
        private static void Prefix(SnapDetectionState state) => HatchPlacement.Current = state.partSettings;

        private static void Postfix(SnapDetectionState state, ref SnapDetectionResults __result)
        {
            if (__result.IsSnapped && __result.shipPartsProxy != null && __result.placement is HullPartPlacement placement && BigDoors.IsHatch(state.partSettings))
            {
                __result.pos += __result.shipPartsProxy.ToWorldRotation(Quaternion.identity) * HatchPlacement.Shift(state.partSettings, placement.Rotation);
            }
        }
    }

    [HarmonyPatch(typeof(Ships.Visuals.ShipPartVisuals), nameof(Ships.Visuals.ShipPartVisuals.AddStatefulPart))]
    internal static class HatchVisualShiftPatch
    {
        private static readonly AccessTools.FieldRef<Ships.Visuals.ShipPartVisuals, Dictionary<ushort, Ships.Parts.Common.Model.Reactions.IShipPartVisual>> Parts =
            AccessTools.FieldRefAccess<Ships.Visuals.ShipPartVisuals, Dictionary<ushort, Ships.Parts.Common.Model.Reactions.IShipPartVisual>>("_floatingParts");

        private static void Postfix(Ships.Visuals.ShipPartVisuals __instance, StatefulPart part)
        {
            if (BigDoors.IsHatch(part.Settings) && Parts(__instance).TryGetValue(part.Id, out var visual) && visual?.GameObject != null)
            {
                visual.GameObject.transform.localPosition += HatchPlacement.Shift(part.Settings, part.LocalRotation);
            }
        }
    }

    [HarmonyPatch(typeof(Ships.Physics.ShipInteractionColliders), nameof(Ships.Physics.ShipInteractionColliders.AddStatefulPart))]
    internal static class HatchInteractionShiftPatch
    {
        private static readonly AccessTools.FieldRef<Ships.Physics.ShipInteractionColliders, Dictionary<ushort, Ships.Parts.Common.Model.Reactions.IShipPartInteractions>> Parts =
            AccessTools.FieldRefAccess<Ships.Physics.ShipInteractionColliders, Dictionary<ushort, Ships.Parts.Common.Model.Reactions.IShipPartInteractions>>("_statefulParts");

        private static void Postfix(Ships.Physics.ShipInteractionColliders __instance, StatefulPart part)
        {
            if (BigDoors.IsHatch(part.Settings) && Parts(__instance).TryGetValue(part.Id, out var interactions) && interactions?.GameObject != null)
            {
                interactions.GameObject.transform.localPosition += HatchPlacement.Shift(part.Settings, part.LocalRotation);
            }
        }
    }

    [HarmonyPatch(typeof(Ships.Physics.Collision.ShipStatefulColliders), nameof(Ships.Physics.Collision.ShipStatefulColliders.AddStatefulPart))]
    internal static class HatchColliderShiftPatch
    {
        private static readonly AccessTools.FieldRef<Ships.Physics.Collision.ShipStatefulColliders, Dictionary<ushort, Ships.Parts.Common.Model.Reactions.IShipPartDynamicCollider>> Dynamic =
            AccessTools.FieldRefAccess<Ships.Physics.Collision.ShipStatefulColliders, Dictionary<ushort, Ships.Parts.Common.Model.Reactions.IShipPartDynamicCollider>>("_dynamicColliders");
        private static readonly AccessTools.FieldRef<Ships.Physics.Collision.ShipStatefulColliders, Dictionary<ushort, GameObject>> Plain =
            AccessTools.FieldRefAccess<Ships.Physics.Collision.ShipStatefulColliders, Dictionary<ushort, GameObject>>("_statefulParts");

        private static void Postfix(Ships.Physics.Collision.ShipStatefulColliders __instance, StatefulPart part)
        {
            if (!BigDoors.IsHatch(part.Settings)) return;
            Transform t = Dynamic(__instance).TryGetValue(part.Id, out var dynamic) && dynamic?.GameObject != null ? dynamic.GameObject.transform
                : Plain(__instance).TryGetValue(part.Id, out GameObject go) && go != null ? go.transform : null;
            if (t != null) t.localPosition += HatchPlacement.Shift(part.Settings, part.Placement.Rotation);
        }
    }

    [HarmonyPatch(typeof(PartPreview), nameof(PartPreview.Rotate))]
    internal static class HatchRotateStepPatch
    {
        private static readonly AccessTools.FieldRef<PartPreview, PartSettings> Selected = AccessTools.FieldRefAccess<PartPreview, PartSettings>("_actualPreviewSettings");

        private static void Prefix(PartPreview __instance, ref float direction)
        {
            if (BigDoors.IsHatch(Selected(__instance))) direction *= 2f;
        }
    }

    [HarmonyPatch(typeof(HorizontalFloor), nameof(HorizontalFloor.DetectSnapPlacement))]
    internal static class HatchHitPlacementPatch
    {
        private static void Postfix(ref IPartPlacement __result)
        {
            PartSettings part = HatchPlacement.Current;
            if (__result is HullPartPlacement placement && placement.Position.Type == VoxelPartType.Floor && BigDoors.IsHatch(part))
            {
                __result = HatchPlacement.Fix(placement, part, placement.UpOrientation);
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
                __result = HatchPlacement.Fix(__result.Value, props.PartSettings, __result.Value.UpOrientation);
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
            PartOrientation up = HatchPlacement.UpFrom(ship.ToLocalRotation(state.partRotation));
            partPlacement = HatchPlacement.Fix(new HullPartPlacement(centre, VoxelPartType.Floor, PartOrientation.Up, up), part, up);
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
                        Melon<BuildingMod>.Logger.Msg($"{part.fullLabel}: another part is in the way at {coord} - not placed");
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
                    Melon<BuildingMod>.Logger.Msg($"{part.fullLabel}: replaced {floors.Count} floor tiles");
                }
            }
            catch (Exception e)
            {
                Melon<BuildingMod>.Logger.Warning($"Hatch placement: {e.Message}");
            }
            return true;
        }
    }
}
