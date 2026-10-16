using System;
using System.Collections.Generic;
using Core.Services;
using Core.Utils;
using HarmonyLib;
using MelonLoader;
using Mining.Content;
using Ships;
using Ships.Interface.Model;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Placement;
using Ships.Interface.Services;
using Ships.Interface.Settings;
using Ships.Parts.Common.Model;
using Ships.Services;
using Tools.Build;
using Tools.Build.Model;
using Tools.Common;
using UnityEngine;
using WorldTracking.Interface.Values;

namespace Mining.Patches
{
    internal sealed class StairsCollider : MonoBehaviour
    {
    }

    [HarmonyPatch(typeof(PartPreviewDetection), nameof(PartPreviewDetection.CalculateSnapDetectionUpdate))]
    internal static class StairsSnapPatch
    {
        private const float Reach = 5f;
        private static readonly List<Collider> Hidden = new List<Collider>();

        private static bool IsStairs(PartSettings settings) => settings != null && settings.id == Stairs.PartId;

        private static bool IsBarrier(PartSettings settings) => settings != null && settings.id == Barrier.PartId;

        private static readonly Collider[] Nearby = new Collider[1024];

        private static void Hide(Collider collider)
        {
            collider.enabled = false;
            Hidden.Add(collider);
        }

        private static void Prefix(SnapDetectionState state, CameraPhysicsData cameraPhysics)
        {
            if (state.partSettings == null)
            {
                return;
            }
            try
            {
                if (IsBarrier(state.partSettings))
                {
                    int count = ClientScene.PhysicsScene.OverlapSphere(cameraPhysics.CameraPos, Reach + 3f, Nearby, RaycastMasks.BuildDetectionLayer, QueryTriggerInteraction.Collide);
                    for (int i = 0; i < count; i++)
                    {
                        Collider c = Nearby[i];
                        if (c != null && c.enabled && (c.GetComponent<BarrierCollider>() != null || c.GetComponent<StairsCollider>() != null)) Hide(c);
                    }
                    return;
                }
                bool stairs = IsStairs(state.partSettings);
                for (int i = 0; i < 8; i++)
                {
                    if (!ClientScene.PhysicsScene.Raycast(cameraPhysics.CameraPos, cameraPhysics.Direction, out RaycastHit hit, Reach, RaycastMasks.BuildDetectionLayer)
                        || !(hit.collider.GetComponent<BarrierCollider>() != null || (!stairs && hit.collider.GetComponent<StairsCollider>() != null)))
                    {
                        return;
                    }
                    Hide(hit.collider);
                }
            }
            catch (Exception e)
            {
                Melon<MiningMod>.Logger.Error($"Stairs snap: {e}");
            }
        }

        private static void Postfix(SnapDetectionState state, CameraPhysicsData cameraPhysics, ref SnapDetectionResults __result)
        {
            try
            {
                if (IsStairs(state.partSettings))
                {
                    Continue(state, cameraPhysics, ref __result);
                }
                else if (IsBarrier(state.partSettings))
                {
                    BarrierSnap.Apply(ref __result);
                }
            }
            catch (Exception e)
            {
                Melon<MiningMod>.Logger.Error($"Stairs snap: {e}");
            }
            finally
            {
                Restore();
            }
        }

        private static void Finalizer() => Restore();

        private static void Restore()
        {
            if (Hidden.Count == 0)
            {
                return;
            }
            foreach (Collider c in Hidden)
            {
                if (c != null) c.enabled = true;
            }
            Hidden.Clear();
        }

        private static void Continue(SnapDetectionState state, CameraPhysicsData cameraPhysics, ref SnapDetectionResults result)
        {
            if (!ClientScene.PhysicsScene.Raycast(cameraPhysics.CameraPos, cameraPhysics.Direction, out RaycastHit hit, Reach, RaycastMasks.BuildDetectionLayer)
                || hit.collider.GetComponent<StairsCollider>() == null)
            {
                return;
            }
            var id = hit.collider.GetComponent<ShipPartIdentifier>();
            IShipPartPlacements ship = hit.collider.GetComponentInParent<IShipPartPlacements>();
            var ships = ServiceCache.Get<ShipsClientTracker>();
            if (id == null || ship == null || ships == null || !ships.TryGetTrackedShip(ship.ShipId, out TrackedShipClient tracked))
            {
                return;
            }
            FloatingPartPlacement? found = null;
            foreach (StatefulPart part in tracked.StatefulParts)
            {
                if (part.Id == id.Id && part.Placement is FloatingPartPlacement p)
                {
                    found = p;
                    break;
                }
            }
            if (!found.HasValue)
            {
                return;
            }
            FloatingPartPlacement below = found.Value;
            Vector3 local = ship.ConvertHitToLocalHit(hit).Pos;
            bool up = (Quaternion.Inverse(below.Rotation) * (local - below.Coord)).z >= 0f;
            Vector3 step = below.Rotation * new Vector3(0f, Stairs.Rise, Stairs.Run);
            var next = new FloatingPartPlacement(below.Coord + (up ? step : -step), below.ForwardOrientation, below.UpOrientation);
            if (!ship.IsFloatingPlacementFree(next, state.partSettings.Bounds))
            {
                return;
            }
            result.IsSnapped = true;
            result.pivot = null;
            result.shipPartsProxy = ship;
            result.placement = next;
            result.pos = ship.ToWorldPos(next);
        }
    }
}
