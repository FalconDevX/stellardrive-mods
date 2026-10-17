using System.Collections.Generic;
using HarmonyLib;
using Ships;
using Ships.Interface.Model.Joints;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.State;
using Ships.Interface.Model.Placement;
using Ships.Services;
using UnityEngine;

namespace Odyssey.Server
{
    internal static class RotorObstacles
    {
        private const float Margin = 0.08f;
        private const float DeeperBy = 0.002f;
        private const float CacheSeconds = 1f;
        private static readonly Collider[] Hits = new Collider[16];
        private static readonly Dictionary<uint, Group> Cache = new Dictionary<uint, Group>();
        private static readonly Dictionary<uint, Transform> Frames = new Dictionary<uint, Transform>();
        private static HashSet<Collider> Own;
        private static List<Collider> Moving;

        private sealed class Group
        {
            public readonly HashSet<Collider> Own = new HashSet<Collider>();
            public readonly List<Collider> Moving = new List<Collider>();
            public float Until;
        }

        internal static bool IsBlocked(TrackedShipServer parent, StatefulPart rotor, RotorState state, float newAngle)
        {
            if (parent == null || state.AnchoredShipId == 0)
            {
                return false;
            }
            var ships = ServiceCache.Get<ShipsServerTracker>();
            TrackedShipServer child = ships?.GetShip(state.AnchoredShipId);
            Transform parentFrame = CollidersObject(parent);
            if (child == null || parentFrame == null)
            {
                return false;
            }
            if (!Cache.TryGetValue(child.Id, out Group group) || Time.time > group.Until || group.Moving.Exists(c => c == null))
            {
                group = group ?? new Group();
                Cache[child.Id] = group;
                group.Own.Clear();
                group.Moving.Clear();
                Own = group.Own;
                Moving = group.Moving;
                CollectOwn(ships, child, 0);
                group.Until = Time.time + CacheSeconds;
            }
            Own = group.Own;
            Moving = group.Moving;
            if (Moving.Count == 0)
            {
                return false;
            }

            RotorJointConnection joint = RotorJointConnection.BuildFromRotorPart(new PartContext(parent.Id, rotor.Id), rotor.Placement, state.ChildShipAnchorPosition);
            joint.IsDirectionReversed = true;
            (Vector3 oldPos, Quaternion oldRot) = joint.CalculateTransform(state.Angle);
            (Vector3 newPos, Quaternion newRot) = joint.CalculateTransform(newAngle);
            Matrix4x4 move = Matrix4x4.TRS(newPos, newRot, Vector3.one) * Matrix4x4.TRS(oldPos, oldRot, Vector3.one).inverse;
            Matrix4x4 toWorld = parentFrame.localToWorldMatrix * move * parentFrame.worldToLocalMatrix;
            Quaternion worldRotation = toWorld.rotation;

            foreach (Collider collider in Moving)
            {
                if (!GetBox(collider, out Vector3 center, out Vector3 halfExtents, out Quaternion rotation))
                {
                    continue;
                }
                Transform t = collider.transform;
                Vector3 movedPosition = toWorld.MultiplyPoint3x4(t.position);
                Quaternion movedTransformRotation = worldRotation * t.rotation;
                Vector3 movedCenter = toWorld.MultiplyPoint3x4(center);
                Quaternion movedRotation = worldRotation * rotation;
                PhysicsScene scene = collider.gameObject.scene.GetPhysicsScene();
                int count = scene.OverlapBox(movedCenter, halfExtents, Hits, movedRotation, Physics.AllLayers, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    Collider other = Hits[i];
                    if (other == null || Own.Contains(other))
                    {
                        continue;
                    }
                    float after = Depth(collider, movedPosition, movedTransformRotation, other);
                    if (after == 0f)
                    {
                        continue;
                    }
                    if (after < 0f || after > Depth(collider, t.position, t.rotation, other) + DeeperBy)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static float Depth(Collider collider, Vector3 position, Quaternion rotation, Collider other)
        {
            Transform o = other.transform;
            try
            {
                return Physics.ComputePenetration(collider, position, rotation, other, o.position, o.rotation, out _, out float distance) ? distance : 0f;
            }
            catch (System.Exception)
            {
                return -1f;
            }
        }

        private static void CollectOwn(ShipsServerTracker ships, TrackedShipServer ship, int depth)
        {
            Transform frame = CollidersObject(ship);
            if (frame != null)
            {
                foreach (Collider c in frame.GetComponentsInChildren<Collider>())
                {
                    if (c.enabled && !c.isTrigger)
                    {
                        Own.Add(c);
                        Moving.Add(c);
                    }
                }
            }
            if (depth > 8)
            {
                return;
            }
            foreach (StatefulPart part in ship.GetAllStatefulParts<RotorState>())
            {
                TrackedShipServer sub = ships.GetShip(((RotorState)part.State).AnchoredShipId);
                if (sub != null && sub != ship)
                {
                    CollectOwn(ships, sub, depth + 1);
                }
            }
        }

        private static Transform CollidersObject(TrackedShipServer ship)
        {
            if (ship == null || !ship.IsColliderObjectCreated)
            {
                return null;
            }
            if (Frames.TryGetValue(ship.Id, out Transform cached) && cached != null)
            {
                return cached;
            }
            object physics = Traverse.Create(ship).Field("_shipServerPhysics").GetValue();
            GameObject go = physics != null ? Traverse.Create(physics).Property("CollidersObject").GetValue<GameObject>() : null;
            if (go == null)
            {
                return null;
            }
            Frames[ship.Id] = go.transform;
            return go.transform;
        }

        private static bool GetBox(Collider collider, out Vector3 center, out Vector3 halfExtents, out Quaternion rotation)
        {
            Transform t = collider.transform;
            rotation = t.rotation;
            Vector3 localCenter, size;
            switch (collider)
            {
                case BoxCollider box:
                    localCenter = box.center;
                    size = box.size;
                    break;
                case MeshCollider mesh when mesh.sharedMesh != null:
                    localCenter = mesh.sharedMesh.bounds.center;
                    size = mesh.sharedMesh.bounds.size;
                    break;
                default:
                    center = default;
                    halfExtents = default;
                    return false;
            }
            center = t.TransformPoint(localCenter);
            Vector3 scale = t.lossyScale;
            halfExtents = new Vector3(
                Mathf.Max(0.01f, Mathf.Abs(size.x * scale.x) * 0.5f - Margin),
                Mathf.Max(0.01f, Mathf.Abs(size.y * scale.y) * 0.5f - Margin),
                Mathf.Max(0.01f, Mathf.Abs(size.z * scale.z) * 0.5f - Margin));
            return true;
        }
    }
}
