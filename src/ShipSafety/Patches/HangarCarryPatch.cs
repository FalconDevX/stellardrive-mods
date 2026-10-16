using System;
using System.Collections.Generic;
using System.Linq;
using Core.Services;
using HarmonyLib;
using Managers.Server;
using MelonLoader;
using Ships;
using Ships.Grid.Model;
using Ships.Physics;
using Ships.Physics.Forces;
using Ships.Physics.Model;
using Ships.Interface.Model.State;
using Ships.Services;
using UnityEngine;
using WorldProvision.Interface.Services;

namespace ShipSafety.Patches
{
    [HarmonyPatch(typeof(WorldPhysicsServerUpdater), "UpdateShipsPhysics")]
    internal static class HangarCarryPatch
    {
        private struct Carry
        {
            public Vector3 SpinVelocity, HostAngular;
            public int Tick;
        }

        private const float BoundsMargin = 0.5f;
        private const int BoundsRefreshTicks = 50;

        private static readonly AccessTools.FieldRef<TrackedShipServer, ShipServerPhysics> Physics =
            AccessTools.FieldRefAccess<TrackedShipServer, ShipServerPhysics>("_shipServerPhysics");
        private static readonly AccessTools.FieldRef<ShipServerPhysics, ShipNewtonianPhysics> Newtonian =
            AccessTools.FieldRefAccess<ShipServerPhysics, ShipNewtonianPhysics>("_newtonianPhysics");
        private static readonly AccessTools.FieldRef<ShipNewtonianPhysics, ShipDrag> Drag =
            AccessTools.FieldRefAccess<ShipNewtonianPhysics, ShipDrag>("_drag");

        private static readonly Dictionary<(uint host, uint guest), Carry> Carries = new Dictionary<(uint, uint), Carry>();
        private static readonly Dictionary<uint, (Bounds bounds, int tick)> HullBounds = new Dictionary<uint, (Bounds, int)>();
        private static readonly List<(uint, uint)> Stale = new List<(uint, uint)>();
        private static readonly HashSet<uint> Logged = new HashSet<uint>();
        internal static readonly HashSet<ShipDrag> Sheltered = new HashSet<ShipDrag>();
        internal static readonly Dictionary<ShipNewtonianPhysics, (Vector3 force, float mass, ShipPhysicsState state)> Pushes = new Dictionary<ShipNewtonianPhysics, (Vector3, float, ShipPhysicsState)>();
        private static int _tick;
        private static bool _loggedError;

        private static void Postfix(ShipsServerTracker ____ships, float deltaTime)
        {
            if (!ShipSafetyMod.CarryShipsInHangars.Value || ____ships == null)
            {
                Sheltered.Clear();
                return;
            }
            try
            {
                Run(____ships, deltaTime);
            }
            catch (Exception e)
            {
                if (!_loggedError)
                {
                    _loggedError = true;
                    Melon<ShipSafetyMod>.Logger.Error($"Ships carried in hangars: {e}");
                }
            }
            Pushes.Clear();
        }

        private static void Run(ShipsServerTracker ships, float deltaTime)
        {
            _tick++;
            Sheltered.Clear();
            List<TrackedShipServer> roots = ships.LoadedRootShips.Where(s => !s.IsShipDestroyed).ToList();
            if (roots.Count < 2)
            {
                Carries.Clear();
                return;
            }
            IWorldEnvironmentServerProvider environment = null;
            foreach (TrackedShipServer guest in roots)
            {
                Rigidbody guestBody = guest.RigidbodyObject != null ? guest.RigidbodyObject.GetComponent<Rigidbody>() : null;
                if (guestBody == null || guestBody.isKinematic)
                {
                    continue;
                }
                TrackedShipServer host = FindHost(guest, guestBody.worldCenterOfMass, roots);
                if (host == null)
                {
                    continue;
                }
                Rigidbody hostBody = host.RigidbodyObject.GetComponent<Rigidbody>();
                Vector3 arm = guestBody.worldCenterOfMass - hostBody.worldCenterOfMass;
                var carry = new Carry { SpinVelocity = Vector3.Cross(hostBody.angularVelocity, arm), HostAngular = hostBody.angularVelocity, Tick = _tick };
                var key = (host.Id, guest.Id);
                ShipServerPhysics hostPhysics = Physics(host);
                ShipNewtonianPhysics hostNewtonian = hostPhysics != null ? Newtonian(hostPhysics) : null;
                if (hostNewtonian != null && Pushes.TryGetValue(hostNewtonian, out var push) && push.mass > 0f)
                {
                    environment = environment ?? ServiceLocator.GetService<IWorldEnvironmentServerProvider>();
                    Vector3 gravity = environment != null ? environment.GetGravityAtPosition(push.state.SpaceId, push.state.Position) : Vector3.zero;
                    guestBody.linearVelocity += (push.state.Rotation * push.force / push.mass - gravity) * deltaTime;
                }
                if (Carries.TryGetValue(key, out Carry previous) && previous.Tick == _tick - 1)
                {
                    guestBody.linearVelocity += carry.SpinVelocity - previous.SpinVelocity;
                    guestBody.angularVelocity += carry.HostAngular - previous.HostAngular;
                }
                else if (Logged.Add(guest.Id))
                {
                    Melon<ShipSafetyMod>.Logger.Msg($"Ship {guest.Id} is inside ship {host.Id} - it moves with it");
                }
                Carries[key] = carry;
                ShipServerPhysics physics = Physics(guest);
                ShipNewtonianPhysics newtonian = physics != null ? Newtonian(physics) : null;
                ShipDrag drag = newtonian != null ? Drag(newtonian) : null;
                if (drag != null)
                {
                    Sheltered.Add(drag);
                }
            }
            foreach (var pair in Carries)
            {
                if (pair.Value.Tick != _tick) Stale.Add(pair.Key);
            }
            foreach (var key in Stale)
            {
                Carries.Remove(key);
                Logged.Remove(key.Item2);
            }
            Stale.Clear();
        }

        private static TrackedShipServer FindHost(TrackedShipServer guest, Vector3 point, List<TrackedShipServer> roots)
        {
            TrackedShipServer best = null;
            foreach (TrackedShipServer host in roots)
            {
                if (host == guest || host.SpaceId != guest.SpaceId || host.PartCount <= guest.PartCount || host.RigidbodyObject == null)
                {
                    continue;
                }
                Vector3 local = Quaternion.Inverse(host.Rotation) * (point - host.Position);
                if (Bounds(host).Contains(local) && (best == null || host.PartCount < best.PartCount))
                {
                    best = host;
                }
            }
            return best;
        }

        private static Bounds Bounds(TrackedShipServer ship)
        {
            if (HullBounds.TryGetValue(ship.Id, out (Bounds bounds, int tick) cached) && _tick - cached.tick < BoundsRefreshTicks)
            {
                return cached.bounds;
            }
            CoordBounds coords = ship.GetHullCoordBounds();
            var bounds = new Bounds();
            bounds.SetMinMax((Vector3)coords.LowerCoord - Vector3.one * (0.5f + BoundsMargin), (Vector3)coords.UpperCoord + Vector3.one * (0.5f + BoundsMargin));
            HullBounds[ship.Id] = (bounds, _tick);
            return bounds;
        }
    }

    [HarmonyPatch(typeof(ShipNewtonianPhysics), nameof(ShipNewtonianPhysics.ComputePhysicsForces))]
    internal static class HangarPushPatch
    {
        private static void Postfix(ShipNewtonianPhysics __instance, ShipPhysicsCalculationProperties calculationProperties, ShipPhysicsForcesResult __result)
        {
            if (!ShipSafetyMod.CarryShipsInHangars.Value || calculationProperties.MassIncludingConnectedShips <= 0f)
            {
                return;
            }
            HangarCarryPatch.Pushes[__instance] = (__result.ForceToAdd, calculationProperties.MassIncludingConnectedShips, calculationProperties.PhysicsState);
        }
    }

    [HarmonyPatch(typeof(ShipDrag), nameof(ShipDrag.ComputeForce))]
    internal static class HangarDragPatch
    {
        private static bool Prefix(ShipDrag __instance, ref Vector3 __result)
        {
            if (HangarCarryPatch.Sheltered.Count == 0 || !HangarCarryPatch.Sheltered.Contains(__instance))
            {
                return true;
            }
            __result = Vector3.zero;
            return false;
        }
    }
}
