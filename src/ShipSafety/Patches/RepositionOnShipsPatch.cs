using System;
using System.Collections.Generic;
using System.Linq;
using Core.Utils;
using HarmonyLib;
using MelonLoader;
using Players.Interface.Model;
using Players.Interface.Values;
using Ships;
using Ships.Grid.Model;
using Ships.Interface.Services;
using Ships.Services;
using UnityEngine;
using WorldProvision.Interface.Services;
using WorldTracking.Interface.Services;
using WorldTracking.Interface.Values;

namespace ShipSafety.Patches
{
    [HarmonyPatch(typeof(ShipRepositioningUpdater), "CheckPhysicsHit")]
    internal static class RepositionOnShipsPatch
    {
        private const float RayDistance = 20f;
        private const float DropHeight = 5f;
        private static readonly RaycastHit[] Hits = new RaycastHit[64];
        private static readonly HashSet<uint> Moved = new HashSet<uint>();
        private static int _mask;
        private static bool _failed;

        private static bool Prefix(CameraPhysicsData cameraPhysics, ShipsClientTracker ____ships, CameraValue ___playerCam, IWorldEnvironmentClientProvider ____envProvider,
            IWorldTransposeClient ____transpose, PlayerState ____lastReceivedState, Quaternion ____currentRotation,
            ref Vector3 ____currentPhysicsPosition, ref Vector3 ____targetRelativePosition, ref bool __result)
        {
            if (_failed || !ShipSafetyMod.Enabled.Value)
            {
                return true;
            }
            try
            {
                __result = Check(cameraPhysics, ____ships, ___playerCam, ____envProvider, ____transpose, ____lastReceivedState, ____currentRotation,
                    ref ____currentPhysicsPosition, ref ____targetRelativePosition);
                return false;
            }
            catch (Exception e)
            {
                _failed = true;
                Melon<ShipSafetyMod>.Logger.Error($"Ship moving onto other ships disabled: {e}");
                return true;
            }
        }

        private static bool Check(CameraPhysicsData cameraPhysics, ShipsClientTracker ships, CameraValue playerCam, IWorldEnvironmentClientProvider env,
            IWorldTransposeClient transpose, PlayerState state, Quaternion rotation, ref Vector3 physicsPosition, ref Vector3 relativePosition)
        {
            if (!ships.TryGetTrackedRootShip(state.EntityId, out TrackedShipClient ship) || playerCam.Get() == null)
            {
                return false;
            }
            Vector3? localUp = env.GetLocalUp(ClientOrigin.LocalPhysicsSpaceId, cameraPhysics.CameraPos);
            if (!localUp.HasValue)
            {
                return false;
            }
            if (_mask == 0)
            {
                _mask = (1 << LayerMask.NameToLayer("Default")) | (1 << LayerMask.NameToLayer("Ship"));
            }
            Moved.Clear();
            foreach (var connected in ships.GetRootConnectedShips(ship.Id)) Moved.Add(connected.Ship.Id);
            Moved.Add(ship.Id);

            Vector3 up = localUp.Value;
            int count = ClientScene.PhysicsScene.Raycast(cameraPhysics.CameraPos, cameraPhysics.Direction, Hits, RayDistance, _mask, QueryTriggerInteraction.Ignore);
            if (!Nearest(count, false, out RaycastHit hit))
            {
                return false;
            }
            CoordBounds bounds = ship.GetHullCoordBounds();
            Vector3 center = rotation * bounds.Center;
            Vector3 start = hit.point + up * DropHeight;
            count = ClientScene.PhysicsScene.BoxCast(start, bounds.Extents, -up, Hits, rotation, DropHeight * 2f, _mask, QueryTriggerInteraction.Ignore);
            float drop = Nearest(count, true, out RaycastHit floor) ? floor.distance : 0f;
            Vector3 position = start - up * drop - center + up * 0.05f;
            physicsPosition = transpose.Position(ClientOrigin.LocalPhysicsSpace, ClientOrigin.LocalVisualSpace, position);
            relativePosition = physicsPosition - cameraPhysics.CameraPos;
            return true;
        }

        private static bool Nearest(int count, bool skipOverlaps, out RaycastHit best)
        {
            best = default;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < Math.Min(count, Hits.Length); i++)
            {
                RaycastHit h = Hits[i];
                if (h.collider == null || (skipOverlaps && h.distance <= 0f) || h.distance >= bestDistance || IsMoved(h.collider)) continue;
                best = h;
                bestDistance = h.distance;
            }
            return bestDistance < float.MaxValue;
        }

        private static bool IsMoved(Collider collider)
        {
            IShipPartHelper helper = collider.GetComponentInParent<IShipPartHelper>();
            if (helper != null)
            {
                return Moved.Contains(helper.ShipId);
            }
            return GuestShipColliders.IsGuestOf(collider, Moved);
        }
    }
}
