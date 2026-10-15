using System;
using System.Collections.Generic;
using System.Linq;
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
                var candidates = new List<Vector3>();
                var segments = new List<(Vector3 center, Vector3 axis)>();
                void Consider(Vector3 end)
                {
                    foreach (Vector3 own in mine)
                    {
                        Vector3 delta = end - own;
                        if (Mathf.Abs(delta.y) <= 0.3f && delta.sqrMagnitude < Reach * Reach) candidates.Add(delta);
                    }
                }
                foreach (StatefulPart part in ship.StatefulParts)
                {
                    if (part.Settings == null || !(part.Placement is FloatingPartPlacement other)) continue;
                    if (part.Settings.id == Stairs.PartId)
                    {
                        foreach (Vector3 anchor in Stairs.Anchors) Consider(other.Coord + other.Rotation * anchor);
                    }
                    else if (part.Settings.id == Barrier.PartId)
                    {
                        Vector3 otherAxis = other.Rotation * Vector3.right * Barrier.HalfLength;
                        segments.Add((other.Coord, otherAxis));
                        Consider(other.Coord - otherAxis);
                        Consider(other.Coord + otherAxis);
                    }
                }
                Vector3 best = Vector3.zero;
                bool found = false;
                foreach (Vector3 delta in candidates.OrderBy(d => d.sqrMagnitude))
                {
                    if (!segments.Any(seg => Overlaps(placement.Coord + delta, axis, seg.center, seg.axis)))
                    {
                        best = delta;
                        found = true;
                        break;
                    }
                }
                if (!found || best == Vector3.zero)
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

        private static bool Overlaps(Vector3 center, Vector3 axis, Vector3 otherCenter, Vector3 otherAxis)
        {
            Vector3 dir = axis.normalized, otherDir = otherAxis.normalized;
            if (Mathf.Abs(Vector3.Dot(dir, otherDir)) < 0.97f)
            {
                return false;
            }
            Vector3 offset = otherCenter - center;
            if (Mathf.Abs(offset.y) > 0.3f || Vector3.ProjectOnPlane(offset, dir).sqrMagnitude > 0.01f)
            {
                return false;
            }
            return Mathf.Abs(Vector3.Dot(offset, dir)) < axis.magnitude + otherAxis.magnitude - 0.05f;
        }
    }
}
