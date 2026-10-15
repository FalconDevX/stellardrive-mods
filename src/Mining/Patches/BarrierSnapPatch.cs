using System;
using Core.Services;
using HarmonyLib;
using MelonLoader;
using Mining.Content;
using Ships;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Placement;
using Ships.Services;
using Tools.Build;
using Tools.Build.Model;
using UnityEngine;

namespace Mining.Patches
{
    [HarmonyPatch(typeof(PartPreviewDetection), nameof(PartPreviewDetection.CalculateSnapDetectionUpdate))]
    internal static class BarrierSnapPatch
    {
        private const float Reach = 0.4f;

        private static void Postfix(SnapDetectionState state, ref SnapDetectionResults __result)
        {
            try
            {
                if (state.partSettings == null || state.partSettings.id != Barrier.PartId || !__result.IsSnapped || __result.shipPartsProxy == null
                    || !(__result.placement is FloatingPartPlacement placement))
                {
                    return;
                }
                var ships = ServiceLocator.GetService<ShipsClientTracker>();
                if (ships == null || !ships.TryGetTrackedShip(__result.shipPartsProxy.ShipId, out TrackedShipClient ship))
                {
                    return;
                }
                Vector3 axis = placement.Rotation * Vector3.right * Barrier.HalfLength;
                Vector3[] mine = { placement.Coord - axis, placement.Coord + axis };
                Vector3 best = Vector3.zero;
                float bestDistance = Reach * Reach;
                foreach (StatefulPart part in ship.StatefulParts)
                {
                    if (part.Settings != null && part.Settings.id == Stairs.PartId && part.Placement is FloatingPartPlacement stairs)
                    {
                        foreach (Vector3 anchor in Stairs.Anchors)
                        {
                            Vector3 end = stairs.Coord + stairs.Rotation * anchor;
                            foreach (Vector3 own in mine)
                            {
                                Vector3 delta = end - own;
                                if (Mathf.Abs(delta.y) > 0.3f || delta.sqrMagnitude >= bestDistance) continue;
                                bestDistance = delta.sqrMagnitude;
                                best = delta;
                            }
                        }
                        continue;
                    }
                    if (part.Settings == null || part.Settings.id != Barrier.PartId || !(part.Placement is FloatingPartPlacement other)) continue;
                    Vector3 otherAxis = other.Rotation * Vector3.right * Barrier.HalfLength;
                    foreach (Vector3 end in new[] { other.Coord - otherAxis, other.Coord + otherAxis })
                    {
                        foreach (Vector3 own in mine)
                        {
                            Vector3 delta = end - own;
                            if (Mathf.Abs(delta.y) > 0.3f || (placement.Coord + delta - other.Coord).sqrMagnitude < 0.36f) continue;
                            if (delta.sqrMagnitude < bestDistance)
                            {
                                bestDistance = delta.sqrMagnitude;
                                best = delta;
                            }
                        }
                    }
                }
                if (bestDistance >= Reach * Reach || best == Vector3.zero)
                {
                    return;
                }
                var moved = new FloatingPartPlacement(placement.Coord + best, placement.ForwardOrientation, placement.UpOrientation);
                __result.placement = moved;
                __result.pos = __result.shipPartsProxy.ToWorldPos(moved);
            }
            catch (Exception e)
            {
                Melon<MiningMod>.Logger.Error($"Barrier snap: {e}");
            }
        }
    }
}
