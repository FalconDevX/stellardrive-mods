using System;
using System.Collections.Generic;
using System.Linq;
using Core.Services;
using HarmonyLib;
using Managers.Server;
using MelonLoader;
using Ships;
using Ships.Grid.Model;
using Ships.Parts.Gyroscope;
using Ships.Physics;
using Ships.Physics.Forces;
using Ships.Physics.Model;
using Ships.Interface.Model;
using Ships.Interface.Model.Parts;
using Ships.Interface.Model.Placement;
using Ships.Interface.Model.Parts.State;
using Ships.Interface.Model.State;
using Ships.Services;
using UnityEngine;
using WorldProvision.Interface.Services;

namespace ShipSafety.Patches
{
    [HarmonyPatch(typeof(WorldPhysicsServerUpdater), "UpdateShipsPhysics")]
    internal static class HangarCarryPatch
    {
        private const float BoundsMargin = 0.5f;
        private const int BoundsRefreshTicks = 50;
        private const float WeldMaxSpeed = 1f;
        private const int MinEnclosedSides = 4;
        private const int EnclosureRefreshTicks = 25;

        private static readonly AccessTools.FieldRef<TrackedShipServer, ShipServerPhysics> Physics =
            AccessTools.FieldRefAccess<TrackedShipServer, ShipServerPhysics>("_shipServerPhysics");
        private static readonly AccessTools.FieldRef<ShipServerPhysics, ShipNewtonianPhysics> Newtonian =
            AccessTools.FieldRefAccess<ShipServerPhysics, ShipNewtonianPhysics>("_newtonianPhysics");
        private static readonly AccessTools.FieldRef<ShipNewtonianPhysics, ShipDrag> Drag =
            AccessTools.FieldRefAccess<ShipNewtonianPhysics, ShipDrag>("_drag");

        private static readonly Dictionary<uint, uint> Carries = new Dictionary<uint, uint>();
        private static readonly Dictionary<uint, (Bounds bounds, int tick)> HullBounds = new Dictionary<uint, (Bounds, int)>();
        private static readonly List<uint> Stale = new List<uint>();
        private static readonly Dictionary<(uint guest, uint host), (Vector3Int voxel, bool inside, int tick)> Enclosure = new Dictionary<(uint, uint), (Vector3Int, bool, int)>();
        internal static readonly HashSet<ShipDrag> Sheltered = new HashSet<ShipDrag>();
        internal static readonly Dictionary<ShipNewtonianPhysics, (Vector3 force, Vector3 turn, float mass, ShipPhysicsState state)> Pushes = new Dictionary<ShipNewtonianPhysics, (Vector3, Vector3, float, ShipPhysicsState)>();
        internal static readonly Dictionary<uint, Vector3> HostSpin = new Dictionary<uint, Vector3>();
        private static readonly Dictionary<uint, Welded> Welds = new Dictionary<uint, Welded>();
        internal static readonly HashSet<ShipNewtonianPhysics> Frozen = new HashSet<ShipNewtonianPhysics>();
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
            HostSpin.Clear();
            if (roots.Count < 2)
            {
                Carries.Clear();
                Welds.Clear();
                Frozen.Clear();
                Enclosure.Clear();
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
                Vector3 spin = hostBody.angularVelocity, turn = Vector3.zero, hostAcceleration = Vector3.zero, gravity = Vector3.zero;
                ShipServerPhysics hostPhysics = Physics(host);
                ShipNewtonianPhysics hostNewtonian = hostPhysics != null ? Newtonian(hostPhysics) : null;
                if (hostNewtonian != null && Pushes.TryGetValue(hostNewtonian, out var push) && push.mass > 0f)
                {
                    environment = environment ?? ServiceLocator.GetService<IWorldEnvironmentServerProvider>();
                    gravity = environment != null ? environment.GetGravityAtPosition(push.state.SpaceId, push.state.Position) : Vector3.zero;
                    hostAcceleration = push.state.Rotation * push.force / push.mass;
                    turn = push.state.Rotation * push.turn * deltaTime;
                }
                Vector3 spinAfter = spin + turn;
                ShipServerPhysics physics = Physics(guest);
                ShipNewtonianPhysics newtonian = physics != null ? Newtonian(physics) : null;
                if (Weld(guest, host, guestBody, hostBody, newtonian, hostAcceleration, spinAfter, deltaTime))
                {
                    HostSpin[guest.Id] = spinAfter;
                }
                else
                {
                    Vector3 kick = (hostAcceleration - gravity) * deltaTime + Vector3.Cross(spinAfter, arm) - Vector3.Cross(spin, arm) + Vector3.Cross(spin, Vector3.Cross(spin, arm)) * deltaTime;
                    if (kick.sqrMagnitude > 1e-8f) guestBody.linearVelocity += kick;
                    if (turn.sqrMagnitude > 1e-10f) guestBody.angularVelocity += turn;
                    HostSpin[guest.Id] = spinAfter;
                }
                if (!Carries.TryGetValue(guest.Id, out uint previous) || previous != host.Id)
                {
                    Melon<ShipSafetyMod>.Logger.Msg($"Ship {guest.Id} is inside ship {host.Id} - it moves with it");
                }
                Carries[guest.Id] = host.Id;
                ShipDrag drag = newtonian != null ? Drag(newtonian) : null;
                if (drag != null)
                {
                    Sheltered.Add(drag);
                }
            }
            foreach (uint id in Carries.Keys)
            {
                if (!HostSpin.ContainsKey(id)) Stale.Add(id);
            }
            foreach (uint id in Stale)
            {
                Carries.Remove(id);
            }
            Stale.Clear();
            foreach (uint id in Welds.Keys)
            {
                if (!HostSpin.ContainsKey(id)) Stale.Add(id);
            }
            foreach (uint id in Stale)
            {
                ReleaseWeld(id);
            }
            Stale.Clear();
        }

        private static bool Weld(TrackedShipServer guest, TrackedShipServer host, Rigidbody guestBody, Rigidbody hostBody, ShipNewtonianPhysics newtonian, Vector3 hostAcceleration, Vector3 spinAfter, float deltaTime)
        {
            if (!ShipSafetyMod.WeldParkedShipsInHangars.Value || IsPiloted(guest))
            {
                ReleaseWeld(guest.Id);
                return false;
            }
            Quaternion hostRotation = hostBody.rotation;
            if (!Welds.TryGetValue(guest.Id, out Welded weld) || weld.Host != host.Id)
            {
                Vector3 relative = guestBody.linearVelocity - hostBody.GetPointVelocity(guestBody.worldCenterOfMass);
                if (relative.sqrMagnitude > WeldMaxSpeed * WeldMaxSpeed)
                {
                    ReleaseWeld(guest.Id);
                    return false;
                }
                Quaternion inverse = Quaternion.Inverse(hostRotation);
                weld = new Welded(host.Id, inverse * (guestBody.position - hostBody.position), inverse * guestBody.rotation, newtonian);
                Welds[guest.Id] = weld;
                if (newtonian != null) Frozen.Add(newtonian);
                Melon<ShipSafetyMod>.Logger.Msg($"Ship {guest.Id} is parked in ship {host.Id} - welded until someone takes the pilot seat");
            }
            Vector3 position = hostBody.position + hostRotation * weld.Position;
            Quaternion rotation = hostRotation * weld.Rotation;
            if (hostBody.IsSleeping() && (guestBody.position - position).sqrMagnitude < 1e-6f && Quaternion.Angle(guestBody.rotation, rotation) < 0.01f)
            {
                if (!guestBody.IsSleeping()) guestBody.Sleep();
                return true;
            }
            guestBody.position = position;
            guestBody.rotation = rotation;
            guestBody.transform.SetPositionAndRotation(position, rotation);
            Vector3 arm = position + rotation * guestBody.centerOfMass - hostBody.worldCenterOfMass;
            guestBody.linearVelocity = hostBody.linearVelocity + hostAcceleration * deltaTime + Vector3.Cross(spinAfter, arm);
            guestBody.angularVelocity = spinAfter;
            return true;
        }

        private static void ReleaseWeld(uint id)
        {
            if (!Welds.TryGetValue(id, out Welded weld))
            {
                return;
            }
            Welds.Remove(id);
            if (weld.Newtonian != null) Frozen.Remove(weld.Newtonian);
            Melon<ShipSafetyMod>.Logger.Msg($"Ship {id} is no longer welded in ship {weld.Host}");
        }

        private static bool IsPiloted(TrackedShipServer ship)
        {
            foreach (StatefulPart part in ship.GetAllStatefulParts<PilotSeatState>())
            {
                if (((PilotSeatState)part.State).IsInUse) return true;
            }
            foreach (StatefulPart part in ship.GetAllStatefulParts<PilotSeatAdvState>())
            {
                if (((PilotSeatAdvState)part.State).IsInUse) return true;
            }
            return false;
        }

        private readonly struct Welded
        {
            public readonly uint Host;
            public readonly Vector3 Position;
            public readonly Quaternion Rotation;
            public readonly ShipNewtonianPhysics Newtonian;

            public Welded(uint host, Vector3 position, Quaternion rotation, ShipNewtonianPhysics newtonian)
            {
                Host = host;
                Position = position;
                Rotation = rotation;
                Newtonian = newtonian;
            }
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
                if (Bounds(host).Contains(local) && (best == null || host.PartCount < best.PartCount) && IsEnclosed(guest, host, local))
                {
                    best = host;
                }
            }
            return best;
        }

        private static bool IsEnclosed(TrackedShipServer guest, TrackedShipServer host, Vector3 local)
        {
            var voxel = Vector3Int.RoundToInt(local);
            var key = (guest.Id, host.Id);
            if (Enclosure.TryGetValue(key, out var cached) && cached.voxel == voxel && _tick - cached.tick < EnclosureRefreshTicks)
            {
                return cached.inside;
            }
            CoordBounds coords = host.GetHullCoordBounds();
            Vector3Int lower = coords.LowerCoord, upper = coords.UpperCoord;
            ShipState state = host.State;
            int sides = 0;
            for (int axis = 0; axis < 3; axis++)
            {
                if (Blocked(state, voxel, axis, 1, lower, upper)) sides++;
                if (Blocked(state, voxel, axis, -1, lower, upper)) sides++;
            }
            bool inside = sides >= MinEnclosedSides;
            if (!inside && Carries.TryGetValue(guest.Id, out uint carried) && carried == host.Id)
            {
                Melon<ShipSafetyMod>.Logger.Msg($"Ship {guest.Id} touches ship {host.Id} from outside ({sides} closed sides) - not carried");
            }
            Enclosure[key] = (voxel, inside, _tick);
            return inside;
        }

        private static bool Blocked(ShipState state, Vector3Int start, int axis, int step, Vector3Int lower, Vector3Int upper)
        {
            VoxelPartType face = axis == 0 ? VoxelPartType.RightWall : axis == 1 ? VoxelPartType.Floor : VoxelPartType.ForwardWall;
            bool faceOnNext = axis == 1 ? step > 0 : step < 0;
            Vector3Int c = start;
            while (c[axis] >= lower[axis] - 1 && c[axis] <= upper[axis] + 1)
            {
                Vector3Int next = c;
                next[axis] += step;
                Vector3Int faceCoord = faceOnNext ? next : c;
                if (state.ContainsHullPart(new HullPartPosition(faceCoord, face)) || state.ContainsHullPart(new HullPartPosition(next, VoxelPartType.Interior)))
                {
                    return true;
                }
                c = next;
            }
            return false;
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
        private static void Postfix(ShipNewtonianPhysics __instance, ShipPhysicsCalculationProperties calculationProperties, ref ShipPhysicsForcesResult __result)
        {
            if (!ShipSafetyMod.CarryShipsInHangars.Value || calculationProperties.MassIncludingConnectedShips <= 0f)
            {
                return;
            }
            if (HangarCarryPatch.Frozen.Count > 0 && HangarCarryPatch.Frozen.Contains(__instance))
            {
                __result = default;
            }
            HangarCarryPatch.Pushes[__instance] = (__result.ForceToAdd, __result.TorqueToAddInDegreesSecond, calculationProperties.MassIncludingConnectedShips, calculationProperties.PhysicsState);
        }
    }

    [HarmonyPatch(typeof(GyroscopesUpdater), nameof(GyroscopesUpdater.PrepareGyroscopesActionsBeforePowerLimits))]
    internal static class HangarGyroPatch
    {
        private static readonly AccessTools.FieldRef<GyroscopesUpdater, IShipStateWrite> Ship =
            AccessTools.FieldRefAccess<GyroscopesUpdater, IShipStateWrite>("_ship");

        private static void Prefix(GyroscopesUpdater __instance, ref Vector3 localAngularVelocity)
        {
            if (HangarCarryPatch.HostSpin.Count == 0)
            {
                return;
            }
            IShipStateWrite ship = Ship(__instance);
            if (ship != null && HangarCarryPatch.HostSpin.TryGetValue(ship.Id, out Vector3 spin))
            {
                localAngularVelocity -= Quaternion.Inverse(ship.PhysicsState.Rotation) * spin;
            }
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
