using System.Collections.Generic;
using HarmonyLib;
using Ships;
using Ships.Interface.Model.Joints;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Parts.State;
using Ships.Interface.Model.Placement;
using Ships.Services;
using UnityEngine;

namespace Mining.Server
{
    // Rotor zatrzymuje się na przeszkodzie: przed każdym krokiem obrotu sprawdzamy, czy obracana część (podstatek na
    // talerzu, razem z jego własnymi podstatkami) w nowym położeniu nie wjedzie w teren, inny statek/element albo
    // gracza (kolizje fizyki u hosta). Jeśli tak - rotor stoi i próbuje dalej w kolejnych klatkach, aż przeszkoda zniknie.
    internal static class RotorObstacles
    {
        private const float Margin = 0.08f;          // zmniejszenie brył, żeby stykające się elementy nie blokowały
        private static readonly Collider[] Hits = new Collider[16];
        private static readonly HashSet<Collider> Own = new HashSet<Collider>();
        private static readonly List<Collider> Moving = new List<Collider>();

        internal static bool IsBlocked(TrackedShipServer parent, StatefulPart rotor, RotorState state, float newAngle)
        {
            if (parent == null || state.AnchoredShipId == 0)
            {
                return false;
            }
            var ships = Core.Services.ServiceLocator.GetService<ShipsServerTracker>();
            TrackedShipServer child = ships?.GetShip(state.AnchoredShipId);
            Transform parentFrame = CollidersObject(parent);
            if (child == null || parentFrame == null)
            {
                return false;
            }
            Own.Clear();
            Moving.Clear();
            CollectOwn(ships, child, 0);
            if (Moving.Count == 0)
            {
                return false;
            }

            // Ruch talerza w układzie statku-rodzica: T(nowy kąt) * T(stary kąt)^-1 (jak gra liczy pozycję podstatku).
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
                Vector3 movedCenter = toWorld.MultiplyPoint3x4(center);
                Quaternion movedRotation = worldRotation * rotation;
                PhysicsScene scene = collider.gameObject.scene.GetPhysicsScene();
                int count = scene.OverlapBox(movedCenter, halfExtents, Hits, movedRotation, Physics.AllLayers, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    if (Hits[i] != null && !Own.Contains(Hits[i]))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        // Podstatek i wszystko, co jest na jego własnych rotorach.
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
            object physics = Traverse.Create(ship).Field("_shipServerPhysics").GetValue();
            GameObject go = physics != null ? Traverse.Create(physics).Property("CollidersObject").GetValue<GameObject>() : null;
            return go != null ? go.transform : null;
        }

        // Bryła kolizji jako prostopadłościan w świecie (pudełka dokładnie, rogi i skosy - ich obrys), zmniejszona o margines.
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
