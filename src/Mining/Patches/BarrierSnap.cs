using System.Collections.Generic;
using System.Linq;
using Core.Services;
using Mining.Content;
using Ships;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Placement;
using Ships.Services;
using Tools.Build.Model;
using UnityEngine;

namespace Mining.Patches
{
    internal static class BarrierSnap
    {
        private const float Reach = 0.4f;

        internal static void Apply(ref SnapDetectionResults result)
        {
            if (!result.IsSnapped || result.shipPartsProxy == null || !(result.placement is FloatingPartPlacement placement))
            {
                return;
            }
            var ships = ServiceLocator.GetService<ShipsClientTracker>();
            if (ships == null || !ships.TryGetTrackedShip(result.shipPartsProxy.ShipId, out TrackedShipClient ship))
            {
                return;
            }
            Vector3 axis = placement.Rotation * Vector3.right * Barrier.HalfLength;
            Vector3[] mine = { placement.Coord - axis, placement.Coord + axis };
            var candidates = new List<Vector3>();
            var segments = new List<(Vector3 center, Vector3 axis)>();
            var stairs = new List<FloatingPartPlacement>();
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
                    stairs.Add(other);
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
            bool Fits(Vector3 center) => !segments.Any(seg => Conflicts(center, axis, seg.center, seg.axis)) && !stairs.Any(s => InsideStairs(s, center, axis));
            foreach (Vector3 delta in candidates.OrderBy(d => d.sqrMagnitude).Append(Vector3.zero))
            {
                if (!Fits(placement.Coord + delta))
                {
                    continue;
                }
                if (delta != Vector3.zero)
                {
                    var moved = new FloatingPartPlacement(placement.Coord + delta, placement.ForwardOrientation, placement.UpOrientation);
                    result.placement = moved;
                    result.pos = result.shipPartsProxy.ToWorldPos(moved);
                }
                return;
            }
            result.IsSnapped = false;
        }

        private static bool InsideStairs(FloatingPartPlacement stairs, Vector3 center, Vector3 axis)
        {
            Quaternion inverse = Quaternion.Inverse(stairs.Rotation);
            foreach (float t in new[] { -0.8f, 0f, 0.8f })
            {
                if (Stairs.Contains(inverse * (center + axis * t + Vector3.up * 0.5f - stairs.Coord))) return true;
            }
            return false;
        }

        private static bool Conflicts(Vector3 center, Vector3 axis, Vector3 otherCenter, Vector3 otherAxis)
        {
            Vector3 offset = otherCenter - center;
            if (Mathf.Abs(offset.y) > 0.3f)
            {
                return false;
            }
            Vector3 dir = axis.normalized, otherDir = otherAxis.normalized;
            if (Mathf.Abs(Vector3.Dot(dir, otherDir)) >= 0.97f)
            {
                return Vector3.ProjectOnPlane(offset, dir).sqrMagnitude <= 0.01f
                       && Mathf.Abs(Vector3.Dot(offset, dir)) < axis.magnitude + otherAxis.magnitude - 0.05f;
            }
            var a = new Vector2(axis.x, axis.z);
            var b = new Vector2(otherAxis.x, otherAxis.z);
            var d = new Vector2(offset.x, offset.z);
            float cross = a.x * b.y - a.y * b.x;
            if (Mathf.Abs(cross) < 1e-5f)
            {
                return false;
            }
            float t = (d.x * b.y - d.y * b.x) / cross;
            float u = (d.x * a.y - d.y * a.x) / cross;
            return Mathf.Abs(t) < 0.85f && Mathf.Abs(u) < 0.85f;
        }
    }
}
