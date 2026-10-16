using System;
using System.Collections.Generic;
using Core.Services;
using Core.Values;
using HarmonyLib;
using MelonLoader;
using Ships;
using Ships.Interface.Model;
using Ships.Interface.Model.State;
using Ships.Physics;
using Ships.Physics.Collision;
using Ships.Services;
using UnityEngine;
using WorldProvision.Interface.Services;
using WorldTracking.Interface.Model.Spaces;
using WorldTracking.Interface.Services;

namespace Water.Buoyancy
{
    [HarmonyPatch(typeof(ShipServerPhysics), "UpdateRootRigidbody")]
    internal static class ShipWaterPhysics
    {
        private sealed class ShipEntry
        {
            public readonly LakeCache Lake = new LakeCache();
            public readonly PontoonSet Pontoons = new PontoonSet();
            public bool Wet;
            public float WetTime = -100f;
        }

        private const float ImpactSpeed = 5f;
        private const float SafeTime = 1.5f;

        private static readonly AccessTools.FieldRef<ShipServerPhysics, ShipRigidbody> ShipRigidbodyRef =
            AccessTools.FieldRefAccess<ShipServerPhysics, ShipRigidbody>("_shipRigidbody");

        private static readonly AccessTools.FieldRef<ShipServerPhysics, ShipState> StateRef =
            AccessTools.FieldRefAccess<ShipServerPhysics, ShipState>("_networkState");

        private static readonly AccessTools.FieldRef<ShipRigidbody, Rigidbody> RigidbodyRef =
            AccessTools.FieldRefAccess<ShipRigidbody, Rigidbody>("_rigidbody");

        private static readonly Dictionary<uint, ShipEntry> Entries = new Dictionary<uint, ShipEntry>();
        private static ISpacesServerProvider _spaces;
        private static ShipsServerTracker _tracker;
        private static IWorldEnvironmentServerProvider _environment;
        private static bool _failed;

        internal static void Clear()
        {
            Entries.Clear();
            _spaces = null;
            _tracker = null;
            _environment = null;
        }

        internal static bool RecentlyWet(uint shipId) =>
            Entries.TryGetValue(shipId, out ShipEntry entry) && Time.time - entry.WetTime < SafeTime;

        private static void Postfix(ShipServerPhysics __instance, float deltaTime)
        {
            if (_failed || !WaterMod.Enabled.Value || !WaterMod.ShipPhysics.Value)
            {
                return;
            }
            try
            {
                Step(__instance, deltaTime);
            }
            catch (Exception e)
            {
                _failed = true;
                Melon<WaterMod>.Logger.Error($"Ship water physics disabled: {e}");
            }
        }

        private static void Step(ShipServerPhysics physics, float dt)
        {
            ShipRigidbody shipRigidbody = ShipRigidbodyRef(physics);
            Rigidbody rb = shipRigidbody != null ? RigidbodyRef(shipRigidbody) : null;
            ShipState state = StateRef(physics);
            if (rb == null || rb.isKinematic || state == null || dt <= 0f)
            {
                return;
            }
            _spaces = _spaces ?? ServiceCache.Get<ISpacesServerProvider>();
            if (!(_spaces?.GetSpace(state.PhysicsState.SpaceId) is PlanetSurfaceSpace space) || space.PlanetId != WaterLevel.PlanetId || !WaterLevel.Ready())
            {
                return;
            }
            _tracker = _tracker ?? ServiceCache.Get<ShipsServerTracker>();
            TrackedShipServer ship = _tracker?.GetShip(state.Id);
            if (ship == null || ship.PartCount <= 0)
            {
                return;
            }
            if (!Entries.TryGetValue(state.Id, out ShipEntry entry))
            {
                Entries[state.Id] = entry = new ShipEntry();
            }

            Vector3Double origin = space.Center + (Vector3Double)rb.position;
            if (!WaterLevel.IsLake(origin, entry.Lake))
            {
                entry.Wet = false;
                entry.Pontoons.Floating = false;
                return;
            }

            _environment = _environment ?? ServiceCache.Get<IWorldEnvironmentServerProvider>();
            float gravity = _environment != null ? _environment.GetGravityAtPosition(state.PhysicsState.SpaceId, rb.position).magnitude : 9.8f;
            Buoyancy.Pontoons.Refresh(ship, entry.Pontoons);
            Buoyancy.Pontoons.UpdateInflation(ship, entry.Pontoons, dt);
            if (Buoyancy.Pontoons.Apply(rb, space.Center, entry.Pontoons, gravity, state.Id))
            {
                entry.WetTime = Time.time;
            }

            var bounds = ship.GetHullCoordBounds();
            Immersion water = WaterLevel.Measure(origin, rb.rotation, bounds.Center, bounds.Extents);
            float f = water.Fraction;
            if (f <= 0f)
            {
                entry.Wet = false;
                return;
            }
            Vector3 up = water.Up;
            Vector3 v = rb.linearVelocity;
            float vUp = Vector3.Dot(v, up);
            Vector3 vh = v - up * vUp;

            if (!entry.Wet && -vUp > ImpactSpeed)
            {
                Impact(rb, ref vh, ref vUp, up, state.Id);
            }
            entry.Wet = true;
            entry.WetTime = Time.time;

            float drag = Mathf.Max(0f, WaterMod.WaterDrag.Value);
            float kh = 0.9f * drag * f * (1f + 0.04f * vh.magnitude);
            float kv = 1.6f * drag * f * (1f + 0.08f * Mathf.Abs(vUp));
            Vector3 target = vh * Mathf.Exp(-kh * dt) + up * (vUp * Mathf.Exp(-kv * dt));
            rb.AddForce(target - v, ForceMode.VelocityChange);
            rb.angularVelocity *= Mathf.Exp(-1.5f * drag * f * dt);

            float lift = gravity * Mathf.Clamp(WaterMod.ShipBuoyancy.Value, 0f, 2f) * f * rb.mass;
            rb.AddForceAtPosition(up * lift, rb.transform.TransformPoint(water.LocalCenter));
        }

        private static void Impact(Rigidbody rb, ref Vector3 vh, ref float vUp, Vector3 up, uint shipId)
        {
            float down = -vUp;
            float horizontal = vh.magnitude;
            float severity = Mathf.Clamp01(down / 40f);
            bool skim = horizontal > 12f && Mathf.Atan2(down, horizontal) * Mathf.Rad2Deg < 25f;
            vh *= skim ? Mathf.Lerp(0.85f, 0.6f, severity) : Mathf.Lerp(0.8f, 0.4f, severity);
            vUp = down > 8f ? down * (skim ? 0.45f : 0.2f) : vUp * 0.3f;
            Vector3 pitchAxis = horizontal > 0.5f ? Vector3.Cross(up, vh / horizontal) : Vector3.zero;
            rb.angularVelocity = rb.angularVelocity * 0.5f + pitchAxis * (severity * (skim ? 0.4f : 0.8f));
            Melon<WaterMod>.Logger.Msg($"Ship {shipId} hit the water at {down:0.0} m/s down, {horizontal:0.0} m/s along" + (skim ? " (skipped off)" : ""));
        }
    }

    [HarmonyPatch(typeof(ShipCollisionDestruction), "OnCollisionEnter")]
    internal static class UnderwaterCollisionPatch
    {
        private static readonly AccessTools.FieldRef<ShipCollisionDestruction, IShipStateWrite> ShipRef =
            AccessTools.FieldRefAccess<ShipCollisionDestruction, IShipStateWrite>("_ship");

        private static bool Prefix(ShipCollisionDestruction __instance, Collision collision)
        {
            if (!WaterMod.Enabled.Value || !WaterMod.ShipPhysics.Value)
            {
                return true;
            }
            IShipStateWrite ship = ShipRef(__instance);
            if (ship == null || !ShipWaterPhysics.RecentlyWet(((IShipStateRead)ship).Id))
            {
                return true;
            }
            return collision.relativeVelocity.magnitude >= WaterMod.UnderwaterCrashSpeed.Value;
        }
    }
}
