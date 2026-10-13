using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Managers.Client;
using Managers.Server;
using MelonLoader;
using Ships;
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
                uint space = host.RelativeSpaceId;
                foreach (TrackedShip guest in ships)
                {
                    if (guest == null || guest == host || guest.SpaceId != space || guest.IsShipDestroyed)
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
                        Melon<ShipSafetyMod>.Logger.Msg($"Ship {guest.Id} rests on ship {host.Id} - added its colliders for walking ({(server ? "server" : "client")})");
                    }
                    entry.Frame = frame;
                    if (entry.Colliders.scene != scene)
                    {
                        SceneManager.MoveGameObjectToScene(entry.Colliders, scene);
                    }
                    entry.Colliders.transform.SetPositionAndRotation(guest.Position, guest.Rotation);
                    if (entry.Hull != null && entry.Hull.ShouldUpdate)
                    {
                        entry.Hull.UpdateColliders();
                    }
                }
            }
            foreach (var key in Guests.Where(p => p.Key.server == server && p.Key.host == host.Id && p.Value.Frame != frame).Select(p => p.Key).ToList())
            {
                Remove(key);
            }
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
}
