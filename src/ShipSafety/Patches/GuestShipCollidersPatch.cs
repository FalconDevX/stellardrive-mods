using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Managers.Client;
using Managers.Server;
using MelonLoader;
using Ships;
using Ships.Grid.Model;
using Ships.Physics.Collision;
using Ships.Values;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ShipSafety.Patches
{
    internal static class GuestShipColliders
    {
        private sealed class Guest
        {
            public GameObject Colliders;
            public ShipHullColliders Hull;
            public TrackedShip Ship;
            public int Frame;
        }

        private static readonly AccessTools.FieldRef<TrackedShip, ShipPrefabs> PrefabsRef = AccessTools.FieldRefAccess<TrackedShip, ShipPrefabs>("Prefabs");
        private static readonly Dictionary<(bool server, uint host, uint guest), Guest> Guests = new Dictionary<(bool, uint, uint), Guest>();
        private static readonly List<(bool server, uint host, uint guest)> Gone = new List<(bool, uint, uint)>();
        private static readonly Dictionary<uint, (float radius, int frame)> Radii = new Dictionary<uint, (float, int)>();
        private const int RadiusRefreshFrames = 120;
        private const float NearMargin = 2f;
        private const int MaxGuestParts = 1500;
        private const float MaxGuestScale = 1.25f;
        private static readonly HashSet<(bool, uint, uint)> Logged = new HashSet<(bool, uint, uint)>();
        private static bool _failed;

        internal static void Update(bool server, TrackedShip host, IEnumerable<TrackedShip> ships)
        {
            if (_failed || !ShipSafetyMod.Enabled.Value || !ShipSafetyMod.GuestShipCollision.Value)
            {
                return;
            }
            try
            {
                Run(server, host, ships);
            }
            catch (Exception e)
            {
                _failed = true;
                Melon<ShipSafetyMod>.Logger.Error($"Ship-on-ship collision disabled: {e}");
            }
        }

        private static void Run(bool server, TrackedShip host, IEnumerable<TrackedShip> ships)
        {
            int frame = Time.frameCount;
            if (host.IsInteriorColliderObjectCreated && host.InteriorColliders != null)
            {
                Scene scene = host.InteriorColliders.scene;
                uint inside = host.RelativeSpaceId;
                Quaternion toHost = Quaternion.Inverse(host.Rotation);
                foreach (TrackedShip guest in ships)
                {
                    if (guest == null || guest == host || guest.IsShipDestroyed || guest.PartCount > MaxGuestParts)
                    {
                        continue;
                    }
                    Vector3 position;
                    Quaternion rotation;
                    if (guest.SpaceId == inside)
                    {
                        position = guest.Position;
                        rotation = guest.Rotation;
                    }
                    else if (guest.SpaceId == host.SpaceId && Radius(guest, frame) <= Radius(host, frame) * MaxGuestScale && Near(host, guest, frame))
                    {
                        position = toHost * (guest.Position - host.Position);
                        rotation = toHost * guest.Rotation;
                    }
                    else
                    {
                        continue;
                    }
                    var key = (server, host.Id, guest.Id);
                    if (!Guests.TryGetValue(key, out Guest entry) || entry.Colliders == null)
                    {
                        entry = Create(guest);
                        if (entry == null)
                        {
                            continue;
                        }
                        Guests[key] = entry;
                        if (Logged.Add(key))
                        {
                            Melon<ShipSafetyMod>.Logger.Msg($"Ship {guest.Id} is next to or on ship {host.Id} - added its colliders for walking ({(server ? "server" : "client")})");
                        }
                    }
                    entry.Frame = frame;
                    if (entry.Colliders.scene != scene)
                    {
                        SceneManager.MoveGameObjectToScene(entry.Colliders, scene);
                    }
                    entry.Colliders.transform.SetPositionAndRotation(position, rotation);
                    if (entry.Hull != null && entry.Hull.ShouldUpdate)
                    {
                        entry.Hull.UpdateColliders();
                    }
                }
            }
            if (Guests.Count == 0)
            {
                return;
            }
            foreach (var pair in Guests)
            {
                if (pair.Key.server == server && pair.Key.host == host.Id && pair.Value.Frame != frame) Gone.Add(pair.Key);
            }
            foreach (var key in Gone)
            {
                Remove(key);
            }
            Gone.Clear();
        }

        private static float Radius(TrackedShip ship, int frame)
        {
            if (Radii.TryGetValue(ship.Id, out (float radius, int frame) cached) && frame - cached.frame < RadiusRefreshFrames)
            {
                return cached.radius;
            }
            CoordBounds bounds = ship.GetHullCoordBounds();
            float radius = Mathf.Max(((Vector3)bounds.UpperCoord).magnitude, ((Vector3)bounds.LowerCoord).magnitude) + 1f;
            Radii[ship.Id] = (radius, frame);
            return radius;
        }

        private static bool Near(TrackedShip host, TrackedShip guest, int frame)
        {
            float reach = Radius(host, frame) + Radius(guest, frame) + NearMargin;
            return (guest.Position - host.Position).sqrMagnitude <= reach * reach;
        }

        private static Guest Create(TrackedShip guest)
        {
            ShipPrefabs prefabs = PrefabsRef(guest);
            if (prefabs?.shipColliders == null)
            {
                return null;
            }
            GameObject go = Object.Instantiate(prefabs.shipColliders);
            go.name = $"ship {guest.Id} colliders (resting on another ship)";
            guest.State.HookPartListeners(go);
            var hull = go.GetComponent<ShipHullColliders>();
            hull?.UpdateColliders();
            return new Guest { Colliders = go, Hull = hull, Ship = guest };
        }

        internal static void SetActive(bool active)
        {
            foreach (Guest entry in Guests.Values)
            {
                if (entry.Colliders != null && entry.Colliders.activeSelf != active)
                {
                    entry.Colliders.SetActive(active);
                }
            }
        }

        private static void Remove((bool, uint, uint) key)
        {
            Guest entry = Guests[key];
            Guests.Remove(key);
            if (entry.Colliders != null)
            {
                try
                {
                    entry.Ship.State.UnhookPartListeners(entry.Colliders);
                }
                catch (Exception)
                {
                }
                Object.Destroy(entry.Colliders);
            }
        }
    }

    [HarmonyPatch(typeof(WorldPhysicsServerUpdater), "UpdateShipInteriorLoading")]
    internal static class GuestCollidersServerPatch
    {
        private static void Postfix(TrackedShipServer ship, Ships.Services.ShipsServerTracker ____ships)
        {
            if (ship != null && ____ships != null)
            {
                GuestShipColliders.Update(true, ship, ____ships.Ships);
            }
        }
    }

    [HarmonyPatch(typeof(WorldPhysicsClientUpdater), "UpdateShipInteriorLoading")]
    internal static class GuestCollidersClientPatch
    {
        private static void Postfix(TrackedShipClient ship, Ships.Services.ShipsClientTracker ____ships)
        {
            if (ship != null && ____ships != null)
            {
                GuestShipColliders.Update(false, ship, ____ships.Ships);
            }
        }
    }

    [HarmonyPatch(typeof(Ships.Services.ShipRepositioningUpdater), "CheckPhysicalPlacement")]
    internal static class GuestCollidersRepositionPatch
    {
        private static void Prefix() => GuestShipColliders.SetActive(false);

        private static void Finalizer() => GuestShipColliders.SetActive(true);
    }
}
